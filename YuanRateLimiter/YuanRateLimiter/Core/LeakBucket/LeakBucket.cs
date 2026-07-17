using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using YuanRateLimiter.Cache;
using YuanRateLimiter.Config;
using YuanRateLimiter.Const;
using YuanRateLimiter.Core.Interface;
using YuanRateLimiter.Enum;

namespace YuanRateLimiter.Core.LeakBucket
{
    /// <summary>
    /// 漏桶算法
    /// 创 建 者：十一 
    /// 创建时间：2023/12/18 0:08:44 
    /// </summary>
    internal class LeakBucket : IRateLimiter
    {
        private const string RedisKeySuffix = ":lb:lua:v1";
        private const string RedisScript = @"local key = KEYS[1]
            local now = tonumber(ARGV[1])
            local rateLimit = tonumber(ARGV[2])
            local capacity = tonumber(ARGV[3])
            local level = tonumber(redis.call('HGET', key, 'level'))
            local last = tonumber(redis.call('HGET', key, 'last'))
            if not level or not last then
                level = 0
                last = now
            else
                now = math.max(now, last)
            end
            local elapsed = (now - last) / 1000
            level = math.max(0, level - elapsed * rateLimit)
            local allowed = 0
            if level + 1 <= capacity then
                level = level + 1
                allowed = 1
            end
            redis.call('HSET', key, 'level', level)
            redis.call('HSET', key, 'last', now)
            local ttl = math.max(1000, math.ceil(capacity / rateLimit * 1000))
            redis.call('PEXPIRE', key, ttl)
            return allowed";
        private readonly ICacheService cacheService;
        private readonly RateLimiterConfig config;
        private readonly ConcurrentDictionary<string, byte> trackedCacheKeys = new ConcurrentDictionary<string, byte>();
        private bool disposed = false;

        public LeakBucket(ICacheService cacheService, RateLimiterConfig config)
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
                trackedCacheKeys.TryAdd(cacheKey + ":lastTime", 0);
                trackedCacheKeys.TryAdd(cacheKey + ":level", 0);
            }
            return Task.FromResult(TryAcquire(this.cacheService, cacheKey, rule.RateLimit, rule.Capacity));
        }

        /// <summary>
        /// 尝试向漏桶中加入一个请求
        /// </summary>
        /// <param name="cacheService">缓存服务</param>
        /// <param name="cacheKey">限流状态基础 Key</param>
        /// <param name="rateLimit">漏水速率</param>
        /// <param name="capacity">漏桶容量</param>
        /// <returns>是否允许当前请求</returns>
        internal static bool TryAcquire(ICacheService cacheService, string cacheKey, int rateLimit, int capacity)
        {
            if (cacheService is ICacheFallbackExecutor fallback) return fallback.ExecuteWithFallback(cache => TryAcquireLeaf(cache, cacheKey, rateLimit, capacity), $"TryAcquireLeakBucket:{cacheKey}");
            return TryAcquireLeaf(cacheService, cacheKey, rateLimit, capacity);
        }

        /// <summary>
        /// 在已选定的叶子缓存后端执行漏桶状态转换
        /// </summary>
        /// <param name="cacheService">叶子缓存服务</param>
        /// <param name="cacheKey">限流状态基础 Key</param>
        /// <param name="rateLimit">漏水速率</param>
        /// <param name="capacity">漏桶容量</param>
        /// <returns>是否允许当前请求</returns>
        private static bool TryAcquireLeaf(ICacheService cacheService, string cacheKey, int rateLimit, int capacity)
        {
            if (cacheService is ICacheFallbackExecutor) throw new InvalidOperationException("限流算法必须在叶子缓存后端执行");
            if (cacheService is IRedisLuaExecutor redisLuaExecutor) return redisLuaExecutor.Eval(cacheKey + RedisKeySuffix, RedisScript, rateLimit, capacity) == 1;
            if (cacheService.CacheType != CacheType.Memory) throw new InvalidOperationException("当前缓存后端不支持原子漏桶限流");
            lock (RateLimiterLocalLock.Get(cacheKey))
            {
                var lastTimeKey = cacheKey + ":lastTime";
                var levelKey = cacheKey + ":level";
                long now = DateTimeOffset.Now.ToUnixTimeMilliseconds();
                string lastTimeStr = cacheService.Get<string>(lastTimeKey);
                long lastTime = string.IsNullOrEmpty(lastTimeStr) ? now : long.Parse(lastTimeStr);
                string levelStr = cacheService.Get<string>(levelKey);
                double level = string.IsNullOrEmpty(levelStr) ? 0 : double.Parse(levelStr);
                double elapsedSeconds = Math.Max(0, now - lastTime) / 1000.0;
                level = Math.Max(0, level - elapsedSeconds * rateLimit);
                bool allowed = level + 1 <= capacity;
                if (allowed) level++;
                var expire = GetStateExpiration(rateLimit, capacity);
                cacheService.Set(lastTimeKey, now.ToString(), expire);
                cacheService.Set(levelKey, level.ToString(), expire);
                return allowed;
            }
        }

        /// <summary>
        /// 获取漏桶状态的空闲过期时间
        /// </summary>
        /// <param name="rateLimit">每秒漏水量</param>
        /// <param name="capacity">漏桶容量</param>
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
