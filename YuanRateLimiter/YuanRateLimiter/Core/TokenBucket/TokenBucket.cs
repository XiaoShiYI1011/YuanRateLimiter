using System;
using System.Linq;
using System.Threading;
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
        private readonly ICacheService cacheService;
        private readonly RateLimiterConfig config;
        private readonly SemaphoreSlim semaphore = new SemaphoreSlim(1, 1);
        private bool disposed = false;
        private readonly string tokensKey;  // 令牌计数键
        private readonly string lastKey;  // 上次补充时间键

        public TokenBucket(ICacheService cacheService, RateLimiterConfig config)
        {
            this.cacheService = cacheService;
            this.config = config;
            if (string.IsNullOrEmpty(config.CacheKey)) config.CacheKey = CacheKey.RateLimiterCacheKey;
            tokensKey = config.CacheKey + "_tokens";
            lastKey = config.CacheKey + "_last";
            var defaultBucketSize = config.RateLimiterRule.AllFlowLimiterRule.Capacity;
            var now = DateTimeOffset.Now.ToUnixTimeMilliseconds();
            this.cacheService.Set<int>(tokensKey, defaultBucketSize);
            this.cacheService.Set<long>(lastKey, now);
        }

        /// <summary>
        /// 检查限流
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        public async Task<bool> CheckRateLimit(HttpContext context)
        {
            int rateLimit;
            int bucketSize;
            switch (config.RateLimiterRule.RateLimiterLogLevel)
            {
                case RateLimitingLevel.All: // 全接口限流
                    rateLimit = config.RateLimiterRule.AllFlowLimiterRule.RateLimit;
                    bucketSize = config.RateLimiterRule.AllFlowLimiterRule.Capacity;
                    break;
                case RateLimitingLevel.Method: // Method 级别限流
                    var methodFlowLimitingRules = config.RateLimiterRule.MethodFlowLimiterRules;
                    var methods = methodFlowLimitingRules.Where(t => t.Method.Equals(context.Request.Method)).ToList();
                    if (methods.Count <= 0) return true;
                    rateLimit = methods[0].RateLimit;
                    bucketSize = methods[0].Capacity;
                    break;
                case RateLimitingLevel.Action: // Action 级别限流
                    var actionFlowLimitingRules = config.RateLimiterRule.ActionFlowLimiterRules;
                    var apis = actionFlowLimitingRules.Where(t => t.Path.Equals(context.Request.Path.Value)).ToList();
                    if (apis.Count <= 0) return true;
                    rateLimit = apis[0].RateLimit;
                    bucketSize = apis[0].Capacity;
                    break;
                default: // 默认全接口限流
                    rateLimit = config.RateLimiterRule.AllFlowLimiterRule.RateLimit;
                    bucketSize = config.RateLimiterRule.AllFlowLimiterRule.Capacity;
                    break;
            }
            return await ConsumeToken(rateLimit, bucketSize);
        }

        /// <summary>
        /// 消耗令牌（请求到达，先补充再消耗）
        /// </summary>
        /// <param name="rateLimit">当前规则的速率</param>
        /// <param name="bucketSize">当前规则的容量</param>
        /// <returns></returns>
        private async Task<bool> ConsumeToken(int rateLimit, int bucketSize)
        {
            await semaphore.WaitAsync();
            try
            {
                var now = DateTimeOffset.Now.ToUnixTimeMilliseconds();  // 懒惰补充令牌，使用毫秒级精度
                var last = this.cacheService.Get<long>(lastKey);
                if (last == default) last = now;  // 初始处理
                double timeDiff = (now - last) / 1000.0;  // 转换为秒
                double toAddDouble = rateLimit * timeDiff;
                int toAdd = (int)toAddDouble;
                int currentTokens = this.cacheService.Get<int>(tokensKey);
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
                this.cacheService.DelKey(config.CacheKey);
                this.cacheService.DelKey(config.CacheKey + "_last");
                disposed = true;
            }
        }
    }
}