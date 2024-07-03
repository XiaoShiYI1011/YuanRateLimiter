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
 * 类名：LeakBucket
 * 描述：漏桶算法
 * 创 建 者：十一 
 * 创建时间：2023/12/18 0:08:44 
 */
namespace YuanRateLimiter.Core.LeakBucket
{
    /// <summary>
    /// 漏桶算法
    /// </summary>
    internal class LeakBucket : IRateLimiter
    {
        private readonly ICacheService cacheService;
        private readonly RateLimiterConfig config;
        private readonly SemaphoreSlim semaphore = new SemaphoreSlim(1, 1);
        private readonly Timer timer;
        private bool disposed = false;
        private int bucketSize;
        private int rateLimit;

        public LeakBucket(ICacheService cacheService, RateLimiterConfig config)
        {
            this.cacheService = cacheService;
            this.config = config;
            if (string.IsNullOrEmpty(config.CacheKey)) config.CacheKey = CacheKey.RateLimiterCacheKey;
            timer = new Timer(async _ => await ConsumeToken(), null, TimeSpan.Zero, TimeSpan.FromSeconds(1));
        }

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
            return await GenerateToken();
        }

        /// <summary>
        /// 生成令牌（加水）
        /// </summary>
        /// <returns></returns>
        private async Task<bool> GenerateToken()
        {
            await semaphore.WaitAsync();
            try
            {
                var currentBucket = this.cacheService.ListGetAll<string>(config.CacheKey);
                if (currentBucket.Count >= bucketSize) return false;  // 水多，溢出
                this.cacheService.ListAdd(config.CacheKey, DateTimeOffset.Now.ToUnixTimeSeconds().ToString());
                return true;
            }
            finally
            {
                semaphore.Release();
            }
        }

        /// <summary>
        /// 消费令牌（漏水）
        /// </summary>
        /// <returns></returns>
        private async Task ConsumeToken()
        {
            await semaphore.WaitAsync();
            try
            {
                for (int i = 0; i < rateLimit; i++)  // 一秒漏多少水
                {
                    this.cacheService.ListLeftPop<string>(config.CacheKey);
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
