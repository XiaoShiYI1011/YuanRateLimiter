using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using YuanRateLimiter.Cache;
using YuanRateLimiter.Config;
using YuanRateLimiter.Const;
using YuanRateLimiter.Core.Interface;

namespace YuanRateLimiter.Core.TokenBucket
{
    /// <summary>
    /// 令牌桶算法
    /// 创 建 者：十一
    /// 创建时间：2023/12/15 21:50:56
    /// </summary>
    internal class TokenBucket : IRateLimiter
    {
        private readonly ICacheService cacheService;
        private readonly RateLimiterConfig config;
        private readonly SemaphoreSlim semaphore = new SemaphoreSlim(1, 1);
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
        public async Task<bool> CheckRateLimit(HttpContext context)
        {
            if (!RateLimiterRuleMatcher.TryMatch(config, context, out var rule, out var ruleKey)) return true;
            return await ConsumeToken(rule.RateLimit, rule.Capacity, RateLimiterRuleMatcher.GetCacheKey(config, ruleKey));
        }

        /// <summary>
        /// 消耗令牌（请求到达，先补充再消耗）
        /// </summary>
        /// <param name="rateLimit">当前规则的速率</param>
        /// <param name="bucketSize">当前规则的容量</param>
        /// <param name="cacheKey">当前规则的缓存Key</param>
        /// <returns></returns>
        private async Task<bool> ConsumeToken(int rateLimit, int bucketSize, string cacheKey)
        {
            await semaphore.WaitAsync();
            try
            {
                var tokensKey = cacheKey + ":tokens";
                var lastKey = cacheKey + ":last";
                trackedCacheKeys.TryAdd(tokensKey, 0);
                trackedCacheKeys.TryAdd(lastKey, 0);
                var now = DateTimeOffset.Now.ToUnixTimeMilliseconds();  // 懒惰补充令牌，使用毫秒级精度
                var last = this.cacheService.Get<long>(lastKey);
                int currentTokens = this.cacheService.Get<int>(tokensKey);
                if (last == default)
                {
                    last = now;
                    currentTokens = bucketSize;
                }
                double timeDiff = (now - last) / 1000.0;  // 转换为秒
                double toAddDouble = rateLimit * timeDiff;
                int toAdd = (int)toAddDouble;
                currentTokens = Math.Min(currentTokens + toAdd, bucketSize);
                if (currentTokens <= 0) return false;  // 无令牌，拒绝
                currentTokens--;  // 消耗一个
                this.cacheService.Set<int>(tokensKey, currentTokens);
                this.cacheService.Set<long>(lastKey, now);
                return true;
            }
            finally
            {
                semaphore.Release();
            }
        }

        /// <summary>
        /// 销毁
        /// </summary>
        public void Dispose()
        {
            if (!disposed)
            {
                semaphore.Dispose();
                foreach (var cacheKey in trackedCacheKeys.Keys)
                {
                    this.cacheService.DelKey(cacheKey);
                }
                disposed = true;
            }
        }
    }
}
