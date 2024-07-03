using Microsoft.AspNetCore.Http;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
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
    /// <summary>
    /// 令牌桶算法
    /// </summary>
    internal class TokenBucket : IRateLimiter
    {
        private readonly ICacheService cacheService;
        private readonly RateLimiterConfig config;
        private readonly SemaphoreSlim semaphore = new SemaphoreSlim(1, 1);
        private readonly Timer timer;
        private bool disposed = false;
        private int bucketSize;
        private int rateLimit;

        public TokenBucket(ICacheService cacheService, RateLimiterConfig config)
        {
            this.cacheService = cacheService;
            this.config = config;
            if (string.IsNullOrEmpty(config.CacheKey)) config.CacheKey = CacheKey.RateLimiterCacheKey;
            timer = new Timer(async _ => await GenerateToken(), null, TimeSpan.Zero, TimeSpan.FromSeconds(1));
            this.cacheService.ListAdd<string>(config.CacheKey, DateTimeOffset.Now.ToUnixTimeSeconds().ToString());
        }

        /// <summary>
        /// 检查限流
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        public async Task<bool> CheckRateLimit(HttpContext context)
        {
            switch (config.RateLimiterRule.RateLimiterLogLevel)
            {
                case RateLimitingLevel.All:  // 全接口限流
                    rateLimit = config.RateLimiterRule.AllFlowLimiterRule.RateLimit;
                    bucketSize = config.RateLimiterRule.AllFlowLimiterRule.Capacity;
                    break;
                case RateLimitingLevel.Method:  // Method 级别限流
                    var methodFlowLimitingRules = config.RateLimiterRule.MethodFlowLimiterRules;
                    var methods = methodFlowLimitingRules.Where(t => t.Method.Equals(context.Request.Method)).ToList();
                    if (methods.Count <= 0) return true;
                    rateLimit = methods[0].RateLimit;
                    bucketSize = methods[0].Capacity;
                    break;
                case RateLimitingLevel.Action:  // Action 级别限流
                    var actionFlowLimitingRules = config.RateLimiterRule.ActionFlowLimiterRules;
                    var apis = actionFlowLimitingRules.Where(t => t.Path.Equals(context.Request.Path.Value)).ToList();
                    if (apis.Count <= 0) return true;
                    rateLimit = apis[0].RateLimit;
                    bucketSize = apis[0].Capacity;
                    break;
                default:  // 默认全接口限流
                    rateLimit = config.RateLimiterRule.AllFlowLimiterRule.RateLimit;
                    bucketSize = config.RateLimiterRule.AllFlowLimiterRule.Capacity;
                    break;
            }
            return await ConsumeToken();
        }

        /// <summary>
        /// 消耗令牌（请求到达，拿走一个令牌）
        /// </summary>
        /// <returns></returns>
        private async Task<bool> ConsumeToken()
        {
            await semaphore.WaitAsync();
            try
            {
                var currentBucket = this.cacheService.ListGetAll<string>(config.CacheKey);
                if (currentBucket.Count == 0) return false;  // 桶中无令牌，拒绝请求
                var getToken = this.cacheService.ListLeftPop<string>(config.CacheKey);
                if (string.IsNullOrEmpty(getToken)) return false;
                return true;
            }
            finally
            {
                semaphore.Release();
            }
        }

        /// <summary>
        /// 生成令牌（固定速率生成一定量的令牌）
        /// </summary>
        /// <returns></returns>
        private async Task GenerateToken()
        {
            await semaphore.WaitAsync();
            try
            {
                for (int i = 0; i < rateLimit; i++)  // 一秒加几个
                {
                    var currentBucket = this.cacheService.ListGetAll<string>(config.CacheKey);
                    if (currentBucket.Count != bucketSize)  // 桶没装满才加
                    {
                        this.cacheService.ListAdd<string>(config.CacheKey, DateTimeOffset.Now.ToUnixTimeSeconds().ToString());
                    }
                }
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
                this.timer?.Dispose();
                semaphore.Dispose();
                this.cacheService.DelKey(config.CacheKey);
                disposed = true;
            }
        }
    }
}
