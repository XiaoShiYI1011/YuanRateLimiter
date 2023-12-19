using Microsoft.AspNetCore.Http;
using YuanRateLimiter.Cache;
using YuanRateLimiter.Config;
using YuanRateLimiter.Const;
using YuanRateLimiter.Core.Interface;
using YuanRateLimiter.Enum;

/*
 * 类名：TokenBucket
 * 描述：令牌桶算法
 * 创 建 者：十一 
 * 创建时间：2023/12/15 21:50:56 
 */
namespace YuanRateLimiter.Core.TokenBucket
{
    internal class TokenBucket : IRateLimiter
    {
        private readonly ICacheService cacheService;
        private readonly RateLimiterConfig config;
        private readonly SemaphoreSlim semaphore = new(1, 1);

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
            int tokensPerSecond = 0;
            int capacity = 0;
            switch (config.RateLimiterRule.RateLimiterLogLevel)
            {
                case RateLimitingLevel.All:  // 全接口限流
                    tokensPerSecond = config.RateLimiterRule.AllFlowLimiterRule.RateLimit;
                    capacity = config.RateLimiterRule.AllFlowLimiterRule.Capacity;
                    break;
                case RateLimitingLevel.Method:  // Method 级别限流
                    var methodFlowLimitingRules = config.RateLimiterRule.MethodFlowLimiterRules;
                    var methods = methodFlowLimitingRules.Where(t => t.Method.Equals(context.Request.Method)).ToList();
                    if (methods.Count <= 0) return true;
                    tokensPerSecond = methods[0].RateLimit;
                    capacity = methods[0].Capacity;
                    break;
                case RateLimitingLevel.Action:  // Action 级别限流
                    var actionFlowLimitingRules = config.RateLimiterRule.ActionFlowLimiterRules;
                    var apis = actionFlowLimitingRules.Where(t => t.Path.Equals(context.Request.Path.Value)).ToList();
                    if (apis.Count <= 0) return true;
                    tokensPerSecond = apis[0].RateLimit;
                    capacity = apis[0].Capacity;
                    break;
                default:  // 默认全接口限流
                    tokensPerSecond = config.RateLimiterRule.AllFlowLimiterRule.RateLimit;
                    capacity = config.RateLimiterRule.AllFlowLimiterRule.Capacity;
                    break;
            }
            return await ConsumeToken(tokensPerSecond, capacity);
        }

        /// <summary>
        /// 消耗令牌
        /// </summary>
        /// <param name="tokensPerSecond">每秒产生的令牌数量</param>
        /// <param name="capacity">令牌桶容量</param>
        /// <returns></returns>
        private async Task<bool> ConsumeToken(int tokensPerSecond, int capacity)
        {
            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            long lastRefillTimestamp = await GetLastRefillTimestamp();
            long elapsedTime = now - lastRefillTimestamp;
            long newTokens = elapsedTime * tokensPerSecond;
            await RefillTokens(now, newTokens, capacity);
            double currentTokens = await GetCurrentTokens(capacity);
            if (currentTokens >= 1)
            {
                await DecrementToken();
                return true;
            }
            return false;
        }

        /// <summary>
        /// 减少令牌
        /// </summary>
        /// <returns></returns>
        private async Task DecrementToken()
        {
            await semaphore.WaitAsync();
            try
            {
                var data = this.cacheService.Get<TokenBucketState>(config.CacheKey);
                var tokenBucketState = new TokenBucketState
                {
                    CurrentTokens = Math.Max(0, data.CurrentTokens - 1),
                    LastRefillTimestamp = data.LastRefillTimestamp,
                };
                this.cacheService.Set(config.CacheKey, tokenBucketState, TimeSpan.FromMinutes(5));
            }
            finally
            {
                semaphore.Release();
            }
        }

        /// <summary>
        /// 填充令牌
        /// </summary>
        /// <param name="now">当前时间戳</param>
        /// <param name="newTokens">新产生的令牌数量</param>
        /// <param name="capacity">令牌桶容量</param>
        /// <returns></returns>
        private async Task RefillTokens(long now, double newTokens, int capacity)
        {
            double currentTokens = await GetCurrentTokens(capacity);
            double updatedTokens = Math.Min(capacity, currentTokens + newTokens);
            this.cacheService.Set(config.CacheKey, new TokenBucketState
            {
                CurrentTokens = updatedTokens,
                LastRefillTimestamp = now,
            }, TimeSpan.FromMinutes(5));
        }

        /// <summary>
        /// 获取当前令牌数量
        /// </summary>
        /// <returns></returns>
        private async Task<double> GetCurrentTokens(int capacity)
        {
            var data = this.cacheService.Get<TokenBucketState>(config.CacheKey);
            return await Task.FromResult(data?.CurrentTokens ?? capacity);
        }

        /// <summary>
        /// 获取最后填充时间
        /// </summary>
        /// <returns></returns>
        private async Task<long> GetLastRefillTimestamp()
        {
            var data = this.cacheService.Get<TokenBucketState>(config.CacheKey);
            return await Task.FromResult(data?.LastRefillTimestamp ?? 0);
        }
    }
}
