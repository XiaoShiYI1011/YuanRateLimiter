using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using YuanRateLimiter.Cache;
using YuanRateLimiter.Config;
using YuanRateLimiter.Const;
using YuanRateLimiter.Core.Interface;

namespace YuanRateLimiter.Core.LeakBucket
{
    /// <summary>
    /// 漏桶算法
    /// 创 建 者：十一 
    /// 创建时间：2023/12/18 0:08:44 
    /// </summary>
    internal class LeakBucket : IRateLimiter
    {
        private readonly ICacheService cacheService;
        private readonly RateLimiterConfig config;
        private readonly SemaphoreSlim semaphore = new SemaphoreSlim(1, 1);
        private readonly ConcurrentDictionary<string, byte> trackedCacheKeys = new ConcurrentDictionary<string, byte>();
        private bool disposed = false;

        public LeakBucket(ICacheService cacheService, RateLimiterConfig config)
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
            return await PourIntoBucket(RateLimiterRuleMatcher.GetCacheKey(config, ruleKey), rule.RateLimit, rule.Capacity);
        }

        /// <summary>
        /// 加水
        /// </summary>
        /// <param name="cacheKey">当前规则的缓存Key</param>
        /// <param name="rateLimit">漏水速率</param>
        /// <param name="bucketSize">桶容量</param>
        /// <returns></returns>
        private async Task<bool> PourIntoBucket(string cacheKey, double rateLimit, int bucketSize)
        {
            await semaphore.WaitAsync();
            try
            {
                var lastTimeKey = cacheKey + ":lastTime";
                var levelKey = cacheKey + ":level";
                trackedCacheKeys.TryAdd(lastTimeKey, 0);
                trackedCacheKeys.TryAdd(levelKey, 0);
                long now = DateTimeOffset.Now.ToUnixTimeMilliseconds();
                // 获取上次漏水时间和当前水位
                string lastTimeStr = cacheService.Get<string>(lastTimeKey);
                long lastTime = string.IsNullOrEmpty(lastTimeStr) ? now : long.Parse(lastTimeStr);
                string levelStr = cacheService.Get<string>(levelKey);
                double level = string.IsNullOrEmpty(levelStr) ? 0 : double.Parse(levelStr);
                // 计算漏水量【漏水 = 时间间隔(秒) × 漏水速率(个/秒)】，elapsed 以毫秒差计算转为秒
                double elapsed = (now - lastTime) / 1000.0;
                level = Math.Max(0, level - elapsed * rateLimit);
                // 更新上次时间
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
                semaphore.Dispose();
                foreach (var cacheKey in trackedCacheKeys.Keys)
                {
                    cacheService.DelKey(cacheKey);
                }
                disposed = true;
            }
        }
    }
}
