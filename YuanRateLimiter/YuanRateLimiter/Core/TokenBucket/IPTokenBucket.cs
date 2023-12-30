using Microsoft.AspNetCore.Http;
using System;
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
 * 类名：IPTokenBucket
 * 描述：IP令牌桶
 * 创 建 者：十一 
 * 创建时间：2023/12/22 23:28:32 
 */
namespace YuanRateLimiter.Core.TokenBucket
{
    /// <summary>
    /// IP令牌桶
    /// </summary>
    internal class IPTokenBucket : IRateLimiter
    {
        private readonly ICacheService cacheService;
        private readonly RateLimiterConfig config;
        private readonly SemaphoreSlim semaphore = new SemaphoreSlim(1, 1);
        private readonly Dictionary<string, SemaphoreSlim> ipSemaphores = new Dictionary<string, SemaphoreSlim>();
        private readonly System.Timers.Timer timer;
        private long generateTokenBucketDate;
        private bool disposed = false;
        private int bucketSize;
        private int rateLimit;

        public IPTokenBucket(ICacheService cacheService, RateLimiterConfig config)
        {
            this.cacheService = cacheService;
            this.config = config;
            if (string.IsNullOrEmpty(config.CacheKey)) config.CacheKey = CacheKey.RateLimiterCacheKey;
            timer = new System.Timers.Timer(1 * 1000);
            timer.Elapsed += async (sender, e) => await GenerateToken();
            timer.AutoReset = true;
            InitializeAsync();
        }

        /// <summary>
        /// 初始化令牌桶
        /// </summary>
        private async void InitializeAsync()
        {
            await GenerateToken();
            this.timer.Start();
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
            string ipAddress = IPUtil.GetClientIPv4(context);
            if (!ipSemaphores.ContainsKey(ipAddress))
            {
                generateTokenBucketDate = DateTimeOffset.Now.ToUnixTimeSeconds();
                for (int i = 0; i < rateLimit; i++)
                    this.cacheService.ListAdd<long>(GetIpCacheKey(ipAddress), generateTokenBucketDate);
                ipSemaphores[ipAddress] = new SemaphoreSlim(1, 1);
            }
            return await ConsumeToken(ipAddress);
        }

        /// <summary>
        /// 消耗令牌（请求到达，拿走一个令牌）
        /// </summary>
        /// <returns></returns>
        private async Task<bool> ConsumeToken(string ipAddress)
        {
            await ipSemaphores[ipAddress].WaitAsync();
            try
            {
                var currentBucket = this.cacheService.ListGetAll<long>(GetIpCacheKey(ipAddress));
                if (currentBucket.Count == 0) return false; // 桶中无令牌，拒绝请求
                string getToken = this.cacheService.ListLeftPop<long>(GetIpCacheKey(ipAddress)).ToString();
                if (string.IsNullOrEmpty(getToken)) return false;
                return true;
            }
            finally
            {
                ipSemaphores[ipAddress].Release();
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
                RemoveInactiveIPs();
                foreach (var ipAddress in ipSemaphores.Keys.ToList())
                {
                    for (int i = 0; i < rateLimit; i++)
                    {
                        var currentBucket = this.cacheService.ListGetAll<long>(GetIpCacheKey(ipAddress));
                        // 桶满不加
                        if (currentBucket.Count != bucketSize)
                            this.cacheService.ListAdd<long>(GetIpCacheKey(ipAddress), generateTokenBucketDate);
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
        /// 定期删除不活跃的IP并移除相应ipSemaphores
        /// </summary>
        private void RemoveInactiveIPs()
        {
            var currentTimestamp = DateTimeOffset.Now.ToUnixTimeSeconds();
            var inactiveIPs = ipSemaphores.Keys.Where(ip =>
            {
                var lastActivity = cacheService.ListGetAll<long>(GetIpCacheKey(ip)).LastOrDefault();
                return lastActivity > 0 && (currentTimestamp - lastActivity) > (bucketSize / (double)rateLimit * 4);  // 桶满时间的4倍
            }).ToList();
            foreach (var inactiveIP in inactiveIPs)
            {
                ipSemaphores[inactiveIP].Dispose();
                ipSemaphores.Remove(inactiveIP);
                this.cacheService.DelKey(GetIpCacheKey(inactiveIP));
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
