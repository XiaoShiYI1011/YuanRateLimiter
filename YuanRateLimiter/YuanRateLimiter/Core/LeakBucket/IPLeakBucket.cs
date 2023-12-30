using Microsoft.AspNetCore.Http;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using YuanRateLimiter.Cache;
using YuanRateLimiter.Config;
using YuanRateLimiter.Const;
using YuanRateLimiter.Core.Interface;
using YuanRateLimiter.Enum;
using YuanRateLimiter.Utils;

/*
 * 类名：IPLeakBucket
 * 描述：IP漏桶
 * 创 建 者：十一 
 * 创建时间：2023/12/23 19:08:21 
 */
namespace YuanRateLimiter.Core.LeakBucket
{
    /// <summary>
    /// IP漏桶
    /// </summary>
    internal class IPLeakBucket : IRateLimiter
    {
        private readonly ICacheService cacheService;
        private readonly RateLimiterConfig config;
        private readonly SemaphoreSlim semaphore = new SemaphoreSlim(1, 1);
        private readonly Dictionary<string, SemaphoreSlim> ipSemaphores = new Dictionary<string, SemaphoreSlim>();
        private readonly System.Timers.Timer timer;
        private bool disposed = false;
        private int bucketSize;
        private int rateLimit;

        public IPLeakBucket(ICacheService cacheService, RateLimiterConfig config)
        {
            this.cacheService = cacheService;
            this.config = config;
            if (string.IsNullOrEmpty(config.CacheKey)) config.CacheKey = CacheKey.RateLimiterCacheKey;
            timer = new System.Timers.Timer(1 * 1000);
            timer.Elapsed += async (sender, e) => await ConsumeToken();
            timer.AutoReset = true;
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
            string ipAddress = IPUtil.GetClientIPv4(context);
            if (!ipSemaphores.ContainsKey(ipAddress)) ipSemaphores[ipAddress] = new SemaphoreSlim(1, 1);
            return await GenerateToken(ipAddress);
        }

        /// <summary>
        /// 生成令牌（加水）
        /// </summary>
        /// <returns></returns>
        private async Task<bool> GenerateToken(string ipAddress)
        {
            await ipSemaphores[ipAddress].WaitAsync();
            try
            {
                var currentBucket = this.cacheService.ListGetAll<string>(GetIpCacheKey(ipAddress));
                if (currentBucket.Count >= bucketSize) return false;  // 水多，溢出
                this.cacheService.ListAdd(GetIpCacheKey(ipAddress), ipAddress);
                return true;
            }
            finally
            {
                ipSemaphores[ipAddress].Release();
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
                foreach (var ipAddress in ipSemaphores.Keys.ToList())
                {
                    for (int i = 0; i < rateLimit; i++)  // 一秒漏多少水
                    {
                        this.cacheService.ListLeftPop<long>(GetIpCacheKey(ipAddress));
                    }
                }
            }
            finally
            {
                semaphore.Release();
            }
        }

        /// <summary>
        /// 获取Ip特定的缓存Key
        /// </summary>
        /// <param name="ipAddress"></param>
        /// <returns></returns>
        private string GetIpCacheKey(string ipAddress) => config.CacheKey + ":" + ipAddress;

        /// <summary>
        /// 销毁
        /// </summary>
        public void Dispose()
        {
            if (!disposed)
            {
                this.timer?.Dispose();
                foreach (var semaphore in ipSemaphores)
                {
                    this.cacheService.DelKey(semaphore.Key);
                    semaphore.Value.Dispose();
                }
                disposed = true;
            }
        }
    }
}
