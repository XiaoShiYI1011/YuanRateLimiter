using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using YuanRateLimiter.Cache;
using YuanRateLimiter.Config;
using YuanRateLimiter.Const;
using YuanRateLimiter.Core.Interface;
using YuanRateLimiter.Enum;

namespace YuanRateLimiter.Core.TokenBucket
{
    /// <summary>
    /// 令牌桶算法
    /// 创 建 者：十一
    /// 创建时间：2023/12/15 21:50:56
    /// </summary>
    internal class TokenBucket : IRateLimiter
    {
        private const string RedisKeySuffix = ":tb:lua:v1";
        private const string RedisScript = @"local key = KEYS[1]
            local now = tonumber(ARGV[1])
            local rateLimit = tonumber(ARGV[2])
            local capacity = tonumber(ARGV[3])
            local tokens = tonumber(redis.call('HGET', key, 'tokens'))
            local last = tonumber(redis.call('HGET', key, 'last'))
            if not tokens or not last then
                tokens = capacity
                last = now
            else
                now = math.max(now, last)
            end
            local elapsed = (now - last) / 1000
            local toAdd = math.floor(rateLimit * elapsed)
            tokens = math.min(tokens + toAdd, capacity)
            local ttl = math.max(1000, math.ceil(capacity / rateLimit * 1000))
            if tokens <= 0 then
                redis.call('PEXPIRE', key, ttl)
                return 0
            end
            tokens = tokens - 1
            redis.call('HSET', key, 'tokens', tokens)
            redis.call('HSET', key, 'last', now)
            redis.call('PEXPIRE', key, ttl)
            return 1";
        private readonly ICacheService cacheService;
        private readonly RateLimiterConfig config;
        private readonly ConcurrentDictionary<string, byte> trackedCacheKeys = new ConcurrentDictionary<string, byte>();
        private bool disposed = false;

        public TokenBucket(ICacheService cacheService, RateLimiterConfig config)
        {
            this.cacheService = cacheService;
            this.config = config;
            if (string.IsNullOrEmpty(config.CacheKey)) config.CacheKey = CacheKey.RateLimiterCacheKey;
        }

        /// <summary>
        /// 检查限流
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        public Task<bool> CheckRateLimit(HttpContext context)
        {
            if (!RateLimiterRuleMatcher.TryMatch(config, context, out var rule, out var ruleKey)) return Task.FromResult(true);
            var cacheKey = RateLimiterRuleMatcher.GetCacheKey(config, ruleKey);
            if (this.cacheService.CacheType == CacheType.Memory)
            {
                trackedCacheKeys.TryAdd(cacheKey + ":tokens", 0);
                trackedCacheKeys.TryAdd(cacheKey + ":last", 0);
            }
            return Task.FromResult(TryAcquire(this.cacheService, cacheKey, rule.RateLimit, rule.Capacity));
        }

        /// <summary>
        /// 尝试从令牌桶中获取一个令牌
        /// </summary>
        /// <param name="cacheService">缓存服务</param>
        /// <param name="cacheKey">限流状态基础 Key</param>
        /// <param name="rateLimit">令牌补充速率</param>
        /// <param name="capacity">令牌桶容量</param>
        /// <returns>是否允许当前请求</returns>
        internal static bool TryAcquire(ICacheService cacheService, string cacheKey, int rateLimit, int capacity)
        {
            if (cacheService is ICacheFallbackExecutor fallback) return fallback.ExecuteWithFallback(cache => TryAcquireLeaf(cache, cacheKey, rateLimit, capacity), $"TryAcquireTokenBucket:{cacheKey}");
            return TryAcquireLeaf(cacheService, cacheKey, rateLimit, capacity);
        }

        /// <summary>
        /// 在已选定的叶子缓存后端执行令牌桶状态转换
        /// </summary>
        /// <param name="cacheService">叶子缓存服务</param>
        /// <param name="cacheKey">限流状态基础 Key</param>
        /// <param name="rateLimit">令牌补充速率</param>
        /// <param name="capacity">令牌桶容量</param>
        /// <returns>是否允许当前请求</returns>
        private static bool TryAcquireLeaf(ICacheService cacheService, string cacheKey, int rateLimit, int capacity)
        {
            if (cacheService is ICacheFallbackExecutor) throw new InvalidOperationException("限流算法必须在叶子缓存后端执行");
            if (cacheService is IRedisLuaExecutor redisLuaExecutor) return redisLuaExecutor.Eval(cacheKey + RedisKeySuffix, RedisScript, rateLimit, capacity) == 1;
            if (cacheService.CacheType != CacheType.Memory) throw new InvalidOperationException("当前缓存后端不支持原子令牌桶限流");
            lock (RateLimiterLocalLock.Get(cacheKey))
            {
                var tokensKey = cacheKey + ":tokens";
                var lastKey = cacheKey + ":last";
                var now = DateTimeOffset.Now.ToUnixTimeMilliseconds();
                var last = cacheService.Get<long>(lastKey);
                int currentTokens = cacheService.Get<int>(tokensKey);
                if (last == default)
                {
                    last = now;
                    currentTokens = capacity;
                }
                double elapsedSeconds = Math.Max(0, now - last) / 1000.0;
                int toAdd = (int)(rateLimit * elapsedSeconds);
                currentTokens = Math.Min(currentTokens + toAdd, capacity);
                var expire = GetStateExpiration(rateLimit, capacity);
                if (currentTokens <= 0)
                {
                    cacheService.SetExpires(tokensKey, expire);
                    cacheService.SetExpires(lastKey, expire);
                    return false;
                }
                currentTokens--;
                cacheService.Set(tokensKey, currentTokens, expire);
                cacheService.Set(lastKey, now, expire);
                return true;
            }
        }

        /// <summary>
        /// 获取令牌桶状态的空闲过期时间
        /// </summary>
        /// <param name="rateLimit">每秒补充令牌数</param>
        /// <param name="capacity">令牌桶容量</param>
        /// <returns>状态空闲过期时间</returns>
        private static TimeSpan GetStateExpiration(int rateLimit, int capacity)
        {
            double milliseconds = Math.Ceiling(Math.Max(0, capacity) * 1000.0 / Math.Max(1, rateLimit));
            return TimeSpan.FromMilliseconds(Math.Max(1000.0, milliseconds));
        }

        /// <summary>
        /// 销毁
        /// </summary>
        public void Dispose()
        {
            if (!disposed)
            {
                if (this.cacheService.CacheType == CacheType.Memory)
                {
                    foreach (var cacheKey in trackedCacheKeys.Keys)
                    {
                        this.cacheService.DelKey(cacheKey);
                    }
                }
                disposed = true;
            }
        }
    }
}
