using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using YuanRateLimiter.Cache;
using YuanRateLimiter.Config;
using YuanRateLimiter.Const;
using YuanRateLimiter.Core.Interface;
using YuanRateLimiter.Utils;

/*
 * 类名：IPSlidingWindow
 * 描述：IP滑动窗口
 * 创 建 者：十一 
 * 创建时间：2023/12/30 18:35:26 
 */
namespace YuanRateLimiter.Core.SlidingWindow
{
    /// <summary>
    /// IP滑动窗口
    /// </summary>
    internal class IPSlidingWindow : IRateLimiter
    {
        private readonly ICacheService cacheService;
        private readonly RateLimiterConfig config;
        private readonly SemaphoreSlim semaphore = new SemaphoreSlim(1, 1);
        private readonly Dictionary<string, SemaphoreSlim> ipSemaphores = new Dictionary<string, SemaphoreSlim>();
        private bool disposed = false;

        public IPSlidingWindow(ICacheService cacheService, RateLimiterConfig config)
        {
            this.cacheService = cacheService;
            this.config = config;
            if (string.IsNullOrEmpty(config.CacheKey)) config.CacheKey = CacheKey.RateLimiterCacheKey;
        }

        public async Task<bool> CheckRateLimit(HttpContext context)
        {
            string ipAddress = IPUtil.GetClientIPv4(context);
            if (!ipSemaphores.ContainsKey(ipAddress)) ipSemaphores[ipAddress] = new SemaphoreSlim(1, 1);
            return await RequestWindow(TimeSpan.FromSeconds(10), 10, ipAddress);
        }

        /// <summary>
        /// 请求窗口
        /// </summary>
        /// <param name="windowSize">窗口大小（单位：秒）</param>
        /// <param name="maxRequests">最大请求数</param>
        /// <returns></returns>
        private async Task<bool> RequestWindow(TimeSpan windowSize, int maxRequests, string ipAddress)
        {
            await ipSemaphores[ipAddress].WaitAsync();
            try
            {
                bool result = true;
                var currentTime = DateTimeOffset.Now.ToUnixTimeSeconds();
                var requestList = this.cacheService.ListGetAll<RequestQueue>(GetIpCacheKey(ipAddress));
                while (requestList.Count > 0 && requestList[0].RequestTime < currentTime - windowSize.TotalSeconds)
                {
                    this.cacheService.ListLeftPop<RequestQueue>(GetIpCacheKey(ipAddress));
                    requestList = this.cacheService.ListGetAll<RequestQueue>(GetIpCacheKey(ipAddress));
                }
                if (requestList.Count < maxRequests)
                    this.cacheService.ListAdd(GetIpCacheKey(ipAddress), new RequestQueue { RequestTime = currentTime });
                else result = false;
                return result;
            }
            finally
            {
                ipSemaphores[ipAddress].Release();
            }
        }

        /// <summary>
        /// 获取Ip特定的缓存Key
        /// </summary>
        /// <param name="ipAddress"></param>
        /// <returns></returns>
        private string GetIpCacheKey(string ipAddress) => config.CacheKey + ":" + ipAddress;

        public void Dispose()
        {
            if (!disposed)
            {
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
