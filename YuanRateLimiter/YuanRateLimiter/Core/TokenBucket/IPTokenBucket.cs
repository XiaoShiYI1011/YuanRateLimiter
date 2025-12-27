using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using YuanRateLimiter.Cache;
using YuanRateLimiter.Config;
using YuanRateLimiter.Const;
using YuanRateLimiter.Core.Interface;
using YuanRateLimiter.Enum;
using YuanRateLimiter.Utils;

namespace YuanRateLimiter.Core.TokenBucket
{
    /// <summary>
    /// IP令牌桶算法
    /// 创 建 者：十一 
    /// 创建时间：2023/12/22 23:28:32 
    /// </summary>
    internal class IPTokenBucket : IRateLimiter
    {
        private readonly ICacheService cacheService;
        private readonly RateLimiterConfig config;
        private readonly ConcurrentDictionary<string, SemaphoreSlim> ipSemaphores = new ConcurrentDictionary<string, SemaphoreSlim>();
        private bool disposed = false;

        public IPTokenBucket(ICacheService cacheService, RateLimiterConfig config)
        {
            this.cacheService = cacheService;
            this.config = config;
            if (string.IsNullOrEmpty(config.CacheKey))
            {
                config.CacheKey = CacheKey.RateLimiterCacheKey;
            }
        }

        // <summary>
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
                    var methods = methodFlowLimitingRules
                        .Where(t => t.Method.Equals(context.Request.Method))
                        .ToList();
                    if (methods.Count <= 0) 
                    {
                        return true;
                    }
                    rateLimit = methods[0].RateLimit;
                    bucketSize = methods[0].Capacity;
                    break;
                case RateLimitingLevel.Action: // Action 级别限流
                    var actionFlowLimitingRules = config.RateLimiterRule.ActionFlowLimiterRules;
                    var apis = actionFlowLimitingRules
                        .Where(t => t.Path.Equals(context.Request.Path.Value))
                        .ToList();
                    if (apis.Count <= 0) 
                    {
                        return true;
                    }
                    rateLimit = apis[0].RateLimit;
                    bucketSize = apis[0].Capacity;
                    break;
                default: // 默认全接口限流
                    rateLimit = config.RateLimiterRule.AllFlowLimiterRule.RateLimit;
                    bucketSize = config.RateLimiterRule.AllFlowLimiterRule.Capacity;
                    break;
            }
            string ipAddress = IPUtil.GetClientIPv4(context);
            if (string.IsNullOrEmpty(ipAddress))
            {
                return false; // 无效IP，拒绝
            }
            // 为新IP初始化满桶
            var tokensKey = GetIpTokensKey(ipAddress);
            var lastKey = GetIpLastKey(ipAddress);
            if (!ipSemaphores.ContainsKey(ipAddress))
            {
                ipSemaphores[ipAddress] = new SemaphoreSlim(1, 1);
                var now = DateTimeOffset.Now.ToUnixTimeMilliseconds();
                this.cacheService.Set<int>(tokensKey, bucketSize);
                this.cacheService.Set<long>(lastKey, now);
            }
            return await ConsumeToken(ipAddress, rateLimit, bucketSize, tokensKey, lastKey);
        }

        /// <summary>
        /// 消耗令牌（请求到达，先补充再消耗）
        /// </summary>
        /// <param name="ipAddress">IP地址</param>
        /// <param name="rateLimit">当前规则的速率</param>
        /// <param name="bucketSize">当前规则的容量</param>
        /// <param name="tokensKey">令牌计数键</param>
        /// <param name="lastKey">上次补充时间键</param>
        /// <returns></returns>
        private async Task<bool> ConsumeToken(string ipAddress, int rateLimit, int bucketSize, string tokensKey, string lastKey)
        {
            var semaphore = ipSemaphores[ipAddress];
            await semaphore.WaitAsync();
            try
            {
                // 懒惰补充令牌
                var now = DateTimeOffset.Now.ToUnixTimeMilliseconds();
                var last = this.cacheService.Get<long>(lastKey);
                if (last == default)
                {
                    last = now;
                }
                double timeDiff = (now - last) / 1000.0; // 转换为秒
                double toAddDouble = rateLimit * timeDiff;
                int toAdd = (int)toAddDouble;
                int currentTokens = this.cacheService.Get<int>(tokensKey);
                currentTokens = Math.Min(currentTokens + toAdd, bucketSize);
                if (currentTokens <= 0)
                {
                    return false; // 无令牌，拒绝
                }
                currentTokens--; // 消耗一个
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
        /// 获取IP特定的令牌键
        /// </summary>
        /// <param name="ipAddress"></param>
        /// <returns></returns>
        private string GetIpTokensKey(string ipAddress)
        {
            return config.CacheKey + ":" + ipAddress + "_tokens";
        }

        /// <summary>
        /// 获取IP特定的最后时间键
        /// </summary>
        /// <param name="ipAddress"></param>
        /// <returns></returns>
        private string GetIpLastKey(string ipAddress)
        {
            return config.CacheKey + ":" + ipAddress + "_last";
        }

        /// <summary>
        /// 销毁
        /// </summary>
        public void Dispose()
        {
            if (!disposed)
            {
                foreach (var kvp in ipSemaphores)
                {
                    this.cacheService.DelKey(GetIpTokensKey(kvp.Key));
                    this.cacheService.DelKey(GetIpLastKey(kvp.Key));
                    kvp.Value.Dispose();
                }
                ipSemaphores.Clear();
                disposed = true;
            }
        }
    }
}
