using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using YuanRateLimiter.Cache;
using YuanRateLimiter.Config;
using YuanRateLimiter.Const;
using YuanRateLimiter.Core.Interface;
using YuanRateLimiter.Utils;

namespace YuanRateLimiter.Core.TokenBucket
{
    /// <summary>
    /// IP令牌桶算法
    /// 创 建 者：十一 
    /// 创建时间：2023/12/22 23:28:32 
    /// </summary>
    internal class IPTokenBucket : IRateLimiter
    {
        private readonly ICacheService cacheService;
        private readonly RateLimiterConfig config;
        private readonly ConcurrentDictionary<string, SemaphoreSlim> ipSemaphores = new ConcurrentDictionary<string, SemaphoreSlim>();
        private readonly ConcurrentDictionary<string, byte> trackedCacheKeys = new ConcurrentDictionary<string, byte>();
        private bool disposed = false;

        public IPTokenBucket(ICacheService cacheService, RateLimiterConfig config)
        {
            this.cacheService = cacheService;
            this.config = config;
            if (string.IsNullOrEmpty(config.CacheKey)) config.CacheKey = CacheKey.RateLimiterCacheKey;
        }

        // <summary>
        /// 检查限流
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        public async Task<bool> CheckRateLimit(HttpContext context)
        {
            if (!RateLimiterRuleMatcher.TryMatch(config, context, out var rule, out var ruleKey)) return true;

            string ipAddress = IPUtil.GetClientIPv4(context);
            if (string.IsNullOrEmpty(ipAddress)) return false; // 无效IP，拒绝
            var cacheKey = RateLimiterRuleMatcher.GetIpCacheKey(config, ipAddress, ruleKey);
            return await ConsumeToken(cacheKey, rule.RateLimit, rule.Capacity);
        }

        /// <summary>
        /// 消耗令牌（请求到达，先补充再消耗）
        /// </summary>
        /// <param name="cacheKey">当前IP和规则组合的缓存Key</param>
        /// <param name="rateLimit">当前规则的速率</param>
        /// <param name="bucketSize">当前规则的容量</param>
        /// <returns></returns>
        private async Task<bool> ConsumeToken(string cacheKey, int rateLimit, int bucketSize)
        {
            var semaphore = ipSemaphores.GetOrAdd(cacheKey, _ => new SemaphoreSlim(1, 1));
            await semaphore.WaitAsync();
            try
            {
                var tokensKey = GetTokensKey(cacheKey);
                var lastKey = GetLastKey(cacheKey);
                trackedCacheKeys.TryAdd(tokensKey, 0);
                trackedCacheKeys.TryAdd(lastKey, 0);
                // 懒惰补充令牌
                var now = DateTimeOffset.Now.ToUnixTimeMilliseconds();
                var last = this.cacheService.Get<long>(lastKey);
                int currentTokens = this.cacheService.Get<int>(tokensKey);
                if (last == default)
                {
                    last = now;
                    currentTokens = bucketSize;
                }
                double timeDiff = (now - last) / 1000.0; // 转换为秒
                double toAddDouble = rateLimit * timeDiff;
                int toAdd = (int)toAddDouble;
                currentTokens = Math.Min(currentTokens + toAdd, bucketSize);
                if (currentTokens <= 0) return false; // 无令牌，拒绝
                currentTokens--; // 消耗一个
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
        /// 获取令牌数量缓存Key
        /// </summary>
        /// <param name="cacheKey"></param>
        /// <returns></returns>
        private string GetTokensKey(string cacheKey) => cacheKey + ":tokens";

        /// <summary>
        /// 获取上次补充时间缓存Key
        /// </summary>
        /// <param name="cacheKey"></param>
        /// <returns></returns>
        private string GetLastKey(string cacheKey) => cacheKey + ":last";

        /// <summary>
        /// 销毁
        /// </summary>
        public void Dispose()
        {
            if (!disposed)
            {
                foreach (var cacheKey in trackedCacheKeys.Keys)
                {
                    this.cacheService.DelKey(cacheKey);
                }
                foreach (var kvp in ipSemaphores)
                {
                    kvp.Value.Dispose();
                }
                ipSemaphores.Clear();
                disposed = true;
            }
        }
    }
}
