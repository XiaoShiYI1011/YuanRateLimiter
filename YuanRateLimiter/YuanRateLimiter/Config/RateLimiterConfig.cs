using YuanRateLimiter.Enum;

/*
 * 类名：RateLimiterConfig
 * 描述：限流配置类
 * 创 建 者：十一 
 * 创建时间：2023/11/21 22:15:52 
 */
namespace YuanRateLimiter.Config
{
    /// <summary>
    /// 限流配置类
    /// </summary>
    public class RateLimiterConfig
    {
        public bool EnableRateLimiter { get; set; }
        public int HttpStatusCode { get; set; } = 429;
        public string LimitingMessage { get; set; } = "The request is too frequent, please try again later.";
        public string CacheKey { get; set; } = "RateLimiterKey";
        public RateLimiterModel RateLimiterModel { get; set; } = RateLimiterModel.TokenBucket;
        public bool EnableIpLimiter { get; set; } = false;
        public string[] IpWhiteList { get; set; }
        public string[] IpBlackList { get; set; }
        public RateLimiterRule RateLimiterRule { get; set; }
    }

    public class RateLimiterRule
    {
        public AllFlowLimiterRule AllFlowLimiterRule { get; set; }
        public RateLimitingLevel RateLimiterLogLevel { get; set; } = RateLimitingLevel.All;
        public MethodFlowLimiterRules[] MethodFlowLimiterRules { get; set; }
        public ActionFlowLimiterRules[] ActionFlowLimiterRules { get; set; }
    }

    public class AllFlowLimiterRule
    {
        public int Capacity { get; set; }
        public int RateLimit { get; set; }
        public int WindowSize { get; set; }
        public int MaxRequests { get; set; }
    }

    public class MethodFlowLimiterRules
    {
        public string Method { get; set; }
        public int Capacity { get; set; }
        public int RateLimit { get; set; }
        public int WindowSize { get; set; }
        public int MaxRequests { get; set; }
    }

    public class ActionFlowLimiterRules
    {
        public string Path { get; set; }
        public int Capacity { get; set; }
        public int RateLimit { get; set; }
        public int WindowSize { get; set; }
        public int MaxRequests { get; set; }
    }
}
