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
 * 类名：SlidingWindow
 * 描述：滑动窗口算法
 * 创 建 者：十一 
 * 创建时间：2023/12/18 18:56:39 
 */
namespace YuanRateLimiter.Core.SlidingWindow
{
    /// <summary>
    /// 滑动窗口算法
    /// </summary>
    internal class SlidingWindow : IRateLimiter
    {
        private readonly ICacheService cacheService;
        private readonly RateLimiterConfig config;
        private readonly SemaphoreSlim semaphore = new SemaphoreSlim(1, 1);
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
        public async Task<bool> CheckRateLimit(HttpContext context)
        {
            int windowSize, maxRequests;
            switch (config.RateLimiterRule.RateLimiterLogLevel)
            {
                case RateLimitingLevel.All:  // 全接口限流
                    maxRequests = config.RateLimiterRule.AllFlowLimiterRule.MaxRequests;
                    windowSize = config.RateLimiterRule.AllFlowLimiterRule.WindowSize;
                    break;
                case RateLimitingLevel.Method:  // Method 级别限流
                    var methodFlowLimitingRules = config.RateLimiterRule.MethodFlowLimiterRules;
                    var methods = methodFlowLimitingRules.Where(t => t.Method.Equals(context.Request.Method)).ToList();
                    if (methods.Count <= 0) return true;
                    maxRequests = methods[0].MaxRequests;
                    windowSize = methods[0].WindowSize;
                    break;
                case RateLimitingLevel.Action:  // Action 级别限流
                    var actionFlowLimitingRules = config.RateLimiterRule.ActionFlowLimiterRules;
                    var apis = actionFlowLimitingRules.Where(t => t.Path.Equals(context.Request.Path.Value)).ToList();
                    if (apis.Count <= 0) return true;
                    maxRequests = apis[0].RateLimit;
                    windowSize = apis[0].WindowSize;
                    break;
                default:  // 默认全接口限流
                    maxRequests = config.RateLimiterRule.AllFlowLimiterRule.MaxRequests;
                    windowSize = config.RateLimiterRule.AllFlowLimiterRule.WindowSize;
                    break;
            }
            return await RequestWindow(TimeSpan.FromSeconds(windowSize), maxRequests);
        }

        /// <summary>
        /// 请求窗口
        /// </summary>
        /// <param name="windowSize">窗口大小（单位：秒）</param>
        /// <param name="maxRequests">最大请求数</param>
        /// <returns></returns>
        private async Task<bool> RequestWindow(TimeSpan windowSize, int maxRequests)
        {
            await semaphore.WaitAsync();
            try
            {
                bool result = true;
                var currentTime = DateTimeOffset.Now.ToUnixTimeSeconds();
                var requestList = this.cacheService.ListGetAll<RequestQueue>(config.CacheKey);
                while (requestList.Count > 0 && requestList[0].RequestTime < currentTime - windowSize.TotalSeconds)
                {
                    this.cacheService.ListLeftPop<RequestQueue>(config.CacheKey);
                    requestList = this.cacheService.ListGetAll<RequestQueue>(config.CacheKey);
                }
                if (requestList.Count < maxRequests)
                    this.cacheService.ListAdd(config.CacheKey, new RequestQueue { RequestTime = currentTime });
                else result = false;
                return result;
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
                disposed = true;
            }
        }
    }
}
