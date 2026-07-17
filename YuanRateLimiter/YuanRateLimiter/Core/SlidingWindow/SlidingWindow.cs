using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using YuanRateLimiter.Cache;
using YuanRateLimiter.Config;
using YuanRateLimiter.Const;
using YuanRateLimiter.Core.Interface;
using YuanRateLimiter.Enum;

namespace YuanRateLimiter.Core.SlidingWindow
{
    /// <summary>
    /// 滑动窗口算法
    /// 创 建 者：十一 
    /// 创建时间：2023/12/18 18:56:39 
    /// </summary>
    internal class SlidingWindow : IRateLimiter
    {
        private const string RedisKeySuffix = ":sw:lua:v1";
        private const string RedisScript = @"local key = KEYS[1]
            local currentTime = math.floor(tonumber(ARGV[1]) / 1000)
            local windowSize = tonumber(ARGV[2])
            local maxRequests = tonumber(ARGV[3])
            local latest = redis.call('ZREVRANGE', key, 0, 0, 'WITHSCORES')
            if latest[2] then
                currentTime = math.max(currentTime, tonumber(latest[2]))
            end
            local cutoff = currentTime - windowSize
            redis.call('ZREMRANGEBYSCORE', key, '-inf', '(' .. cutoff)
            local count = redis.call('ZCARD', key)
            local ttl = (windowSize + 1) * 1000
            if count < maxRequests then
                redis.call('ZADD', key, currentTime, ARGV[4])
                redis.call('PEXPIRE', key, ttl)
                return 1
            end
            redis.call('PEXPIRE', key, ttl)
            return 0";
        private readonly ICacheService cacheService;
        private readonly RateLimiterConfig config;
        private readonly ConcurrentDictionary<string, byte> trackedCacheKeys = new ConcurrentDictionary<string, byte>();
        private bool disposed = false;

        public SlidingWindow(ICacheService cacheService, RateLimiterConfig config)
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
            if (this.cacheService.CacheType == CacheType.Memory) trackedCacheKeys.TryAdd(cacheKey, 0);
            return Task.FromResult(TryAcquire(this.cacheService, cacheKey, rule.WindowSize, rule.MaxRequests));
        }

        /// <summary>
        /// 尝试在滑动窗口中记录当前请求
        /// </summary>
        /// <param name="cacheService">缓存服务</param>
        /// <param name="cacheKey">限流状态基础 Key</param>
        /// <param name="windowSize">窗口大小，单位为秒</param>
        /// <param name="maxRequests">窗口内允许的最大请求数</param>
        /// <returns>是否允许当前请求</returns>
        internal static bool TryAcquire(ICacheService cacheService, string cacheKey, int windowSize, int maxRequests)
        {
            if (cacheService is ICacheFallbackExecutor fallback) return fallback.ExecuteWithFallback(cache => TryAcquireLeaf(cache, cacheKey, windowSize, maxRequests), $"TryAcquireSlidingWindow:{cacheKey}");
            return TryAcquireLeaf(cacheService, cacheKey, windowSize, maxRequests);
        }

        /// <summary>
        /// 在已选定的叶子缓存后端执行滑动窗口状态转换
        /// </summary>
        /// <param name="cacheService">叶子缓存服务</param>
        /// <param name="cacheKey">限流状态基础 Key</param>
        /// <param name="windowSize">窗口大小，单位为秒</param>
        /// <param name="maxRequests">窗口内允许的最大请求数</param>
        /// <returns>是否允许当前请求</returns>
        private static bool TryAcquireLeaf(ICacheService cacheService, string cacheKey, int windowSize, int maxRequests)
        {
            if (cacheService is ICacheFallbackExecutor) throw new InvalidOperationException("限流算法必须在叶子缓存后端执行");
            if (cacheService is IRedisLuaExecutor redisLuaExecutor) return redisLuaExecutor.Eval(cacheKey + RedisKeySuffix, RedisScript, windowSize, maxRequests, Guid.NewGuid().ToString("N")) == 1;
            if (cacheService.CacheType != CacheType.Memory) throw new InvalidOperationException("当前缓存后端不支持原子滑动窗口限流");
            lock (RateLimiterLocalLock.Get(cacheKey))
            {
                long currentTime = DateTimeOffset.Now.ToUnixTimeSeconds();
                var requestList = cacheService.Get<List<RequestQueue>>(cacheKey) ?? new List<RequestQueue>();
                while (requestList.Count > 0 && requestList[0].RequestTime < currentTime - windowSize) requestList.RemoveAt(0);
                bool allowed = requestList.Count < maxRequests;
                if (allowed) requestList.Add(new RequestQueue { RequestTime = currentTime });
                cacheService.Set(cacheKey, requestList, TimeSpan.FromSeconds(Math.Max(1.0, windowSize + 1.0)));
                return allowed;
            }
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
