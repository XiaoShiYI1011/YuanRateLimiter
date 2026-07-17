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
        public Task<bool> CheckRateLimit(HttpContext context)
        {
            if (!RateLimiterRuleMatcher.TryMatch(config, context, out var rule, out var ruleKey)) return Task.FromResult(true);
            string ipAddress = IPUtil.GetClientIPv4(context);
            if (string.IsNullOrEmpty(ipAddress)) return Task.FromResult(false); // 无效IP，拒绝
            var cacheKey = RateLimiterRuleMatcher.GetIpCacheKey(config, ipAddress, ruleKey);
            return Task.FromResult(SlidingWindow.TryAcquire(this.cacheService, cacheKey, rule.WindowSize, rule.MaxRequests));
        }

        /// <summary>
        /// IP 状态由缓存 TTL 管理
        /// </summary>
        public void Dispose()
        {
        }
    }
}
