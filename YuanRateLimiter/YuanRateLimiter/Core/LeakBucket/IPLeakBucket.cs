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

namespace YuanRateLimiter.Core.LeakBucket
{
    /// <summary>
    /// IP漏桶算法
    /// 创 建 者：十一 
    /// 创建时间：2023/12/23 19:08:21 
    /// </summary>
    internal class IPLeakBucket : IRateLimiter
    {
        private readonly ICacheService cacheService;
        private readonly RateLimiterConfig config;
        private readonly ConcurrentDictionary<string, SemaphoreSlim> ipSemaphores = new ConcurrentDictionary<string, SemaphoreSlim>();
        private readonly ConcurrentDictionary<string, byte> trackedCacheKeys = new ConcurrentDictionary<string, byte>();
        private bool disposed = false;

        public IPLeakBucket(ICacheService cacheService, RateLimiterConfig config)
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
            return await GenerateToken(cacheKey, rule.RateLimit, rule.Capacity);
        }

        /// <summary>
        /// 生成令牌（加水）
        /// </summary>
        /// <param name="cacheKey">当前IP和规则组合的缓存Key</param>
        /// <param name="rateLimit">漏水速率</param>
        /// <param name="bucketSize">桶容量</param>
        /// <returns></returns>
        private async Task<bool> GenerateToken(string cacheKey, int rateLimit, int bucketSize)
        {
            var semaphore = ipSemaphores.GetOrAdd(cacheKey, _ => new SemaphoreSlim(1, 1));
            await semaphore.WaitAsync();
            try
            {
                var lastTimeKey = cacheKey + ":lastTime";
                var levelKey = cacheKey + ":level";
                trackedCacheKeys.TryAdd(lastTimeKey, 0);
                trackedCacheKeys.TryAdd(levelKey, 0);
                long now = DateTimeOffset.Now.ToUnixTimeMilliseconds();
                // 获取上次时间和水位
                string lastTimeStr = cacheService.Get<string>(lastTimeKey);
                long lastTime = string.IsNullOrEmpty(lastTimeStr) ? now : long.Parse(lastTimeStr);
                string levelStr = cacheService.Get<string>(levelKey);
                double level = string.IsNullOrEmpty(levelStr) ? 0 : double.Parse(levelStr);
                // 计算漏水
                double elapsed = (now - lastTime) / 1000.0;
                level = Math.Max(0, level - elapsed * rateLimit);
                // 更新时间
                cacheService.Set(lastTimeKey, now.ToString());
                // 尝试加水
                if (level + 1 <= bucketSize)
                {
                    level += 1;
                    cacheService.Set(levelKey, level.ToString());
                    return true;
                }
                return false;
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
                    cacheService.DelKey(cacheKey);
                }
                foreach (var kvp in ipSemaphores)
                {
                    kvp.Value.Dispose();
                }
                ipSemaphores.Clear();
                disposed = true;
            }
        }
    }
}
