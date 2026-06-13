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

namespace YuanRateLimiter.Core.SlidingWindow
{
    /// <summary>
    /// IP滑动窗口算法
    /// 创 建 者：十一 
    /// 创建时间：2023/12/30 18:35:26 
    /// </summary>
    internal class IPSlidingWindow : IRateLimiter
    {
        private readonly ICacheService cacheService;
        private readonly RateLimiterConfig config;
        private readonly ConcurrentDictionary<string, SemaphoreSlim> ipSemaphores = new ConcurrentDictionary<string, SemaphoreSlim>();
        private readonly ConcurrentDictionary<string, byte> trackedCacheKeys = new ConcurrentDictionary<string, byte>();
        private bool disposed = false;

        public IPSlidingWindow(ICacheService cacheService, RateLimiterConfig config)
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
            if (!RateLimiterRuleMatcher.TryMatch(config, context, out var rule, out var ruleKey)) return true;
            string ipAddress = IPUtil.GetClientIPv4(context);
            if (string.IsNullOrEmpty(ipAddress)) return false; // 无效IP，拒绝
            var cacheKey = RateLimiterRuleMatcher.GetIpCacheKey(config, ipAddress, ruleKey);
            return await RequestWindow(TimeSpan.FromSeconds(rule.WindowSize), rule.MaxRequests, cacheKey);
        }

        /// <summary>
        /// 请求窗口
        /// </summary>
        /// <param name="windowSize">窗口大小（单位：秒）</param>
        /// <param name="maxRequests">最大请求数</param>
        /// <param name="cacheKey">当前IP和规则组合的缓存Key</param>
        /// <returns></returns>
        private async Task<bool> RequestWindow(TimeSpan windowSize, int maxRequests, string cacheKey)
        {
            var semaphore = ipSemaphores.GetOrAdd(cacheKey, _ => new SemaphoreSlim(1, 1));
            await semaphore.WaitAsync();
            try
            {
                trackedCacheKeys.TryAdd(cacheKey, 0);
                bool result = true;
                var currentTime = DateTimeOffset.Now.ToUnixTimeSeconds();
                var requestList = this.cacheService.ListGetAll<RequestQueue>(cacheKey);
                while (requestList.Count > 0 && requestList[0].RequestTime < currentTime - windowSize.TotalSeconds)
                {
                    this.cacheService.ListLeftPop<RequestQueue>(cacheKey);
                    requestList = this.cacheService.ListGetAll<RequestQueue>(cacheKey);
                }
                if (requestList.Count < maxRequests) this.cacheService.ListAdd(cacheKey, new RequestQueue { RequestTime = currentTime });
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
                foreach (var cacheKey in trackedCacheKeys.Keys)
                {
                    this.cacheService.DelKey(cacheKey);
                }
                foreach (var semaphore in ipSemaphores)
                {
                    semaphore.Value.Dispose();
                }
                ipSemaphores.Clear();
                disposed = true;
            }
        }
    }
}
