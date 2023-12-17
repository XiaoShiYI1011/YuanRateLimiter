using Microsoft.AspNetCore.Http;
using YuanRateLimiter.Cache;
using YuanRateLimiter.Config;
using YuanRateLimiter.Const;
using YuanRateLimiter.Core.Interface;

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
        private readonly ICacheService chcheService;
        private readonly RateLimitingConfig config;
        private readonly SemaphoreSlim semaphore = new(1, 1);

        public TokenBucket(
            ICacheService chcheService,
            RateLimitingConfig config)
        {
            this.chcheService = chcheService;
            this.config = config;
            if (string.IsNullOrEmpty(config.CacheKey)) config.CacheKey = CacheKey.TokenBucketStateKey;
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
            // 是否开启全接口限流
            if (config.RateLimitingRule.IsAllApiRateLimiting)
            {
                tokensPerSecond = config.RateLimitingRule.IsAllApiFlowLimitingRule.TokensPerSecond;
                capacity = config.RateLimitingRule.IsAllApiFlowLimitingRule.Capacity;
            }
            // Method 级别限流
            if (config.RateLimitingRule.RateLimitingLogLevel!.Equals("Method"))
            {
                var methodFlowLimitingRules = config.RateLimitingRule.MethodFlowLimitingRules;
                var methods = methodFlowLimitingRules.Where(t => t.Method.Equals(context.Request.Method)).ToList();
                if (methods.Count <= 0)
                {
                    return true;
                }
                tokensPerSecond = methods[0].TokensPerSecond;
                capacity = methods[0].Capacity;
            }
            // Api 级别限流
            if (config.RateLimitingRule.RateLimitingLogLevel!.Equals("Api"))
            {
                var apiFlowLimitingRules = config.RateLimitingRule.ApiFlowLimitingRules;
                var apis = apiFlowLimitingRules.Where(t => t.Path.Equals(context.Request.Path.Value)).ToList();
                if (apis.Count <= 0)
                {
                    return true;
                }
                tokensPerSecond = apis[0].TokensPerSecond;
                capacity = apis[0].Capacity;
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
                var data = chcheService.Get<TokenBucketState>(config.CacheKey);
                var tokenBucketState = new TokenBucketState
                {
                    CurrentTokens = Math.Max(0, data.CurrentTokens - 1),
                    LastRefillTimestamp = data.LastRefillTimestamp,
                };
                chcheService.Set(config.CacheKey, tokenBucketState);
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
            chcheService.Set(config.CacheKey, new TokenBucketState
            {
                CurrentTokens = updatedTokens,
                LastRefillTimestamp = now,
            });
        }

        /// <summary>
        /// 获取当前令牌数量
        /// </summary>
        /// <returns></returns>
        private async Task<double> GetCurrentTokens(int capacity)
        {
            var data = chcheService.Get<TokenBucketState>(config.CacheKey);
            return await Task.FromResult(data?.CurrentTokens ?? capacity);
        }

        /// <summary>
        /// 获取最后填充时间
        /// </summary>
        /// <returns></returns>
        private async Task<long> GetLastRefillTimestamp()
        {
            var data = chcheService.Get<TokenBucketState>(config.CacheKey);
            return await Task.FromResult(data?.LastRefillTimestamp ?? 0);
        }
    }
}
