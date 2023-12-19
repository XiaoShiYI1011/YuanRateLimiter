using YuanRateLimiter.Enum;

/*
 * 类名：RateLimiterConfig
 * 描述：限流配置类
 * 创 建 者：十一 
 * 创建时间：2023/11/21 22:15:52 
 */
namespace YuanRateLimiter.Config
{
    public class RateLimiterConfig
    {
        public bool EnableRateLimiter { get; set; }
        public int HttpStatusCode { get; set; } = 429;
        public string LimitingMessage { get; set; } = "Request restricted, you are accessing too frequently!";
        public string CacheKey { get; set; } = "RateLimiterKey";
        public RateLimiterModel RateLimiterModel { get; set; } = RateLimiterModel.TokenBucket;
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
    }

    public class MethodFlowLimiterRules
    {
        public string Method { get; set; }
        public int Capacity { get; set; }
        public int RateLimit { get; set; }
    }

    public class ActionFlowLimiterRules
    {
        public string Path { get; set; }
        public int Capacity { get; set; }
        public int RateLimit { get; set; }
    }
}
