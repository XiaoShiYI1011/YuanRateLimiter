using YuanRateLimiter.Enum;

/*
 * 类名：RateLimitingConfig
 * 描述：限流配置类
 * 创 建 者：十一 
 * 创建时间：2023/11/21 22:15:52 
 */
namespace YuanRateLimiter.Config
{
    public class RateLimitingConfig
    {
        public bool EnableRateLimiting { get; set; }
        public int HttpStatusCode { get; set; } = 429;
        public string LimitingMessage { get; set; } = "Request restricted, you are accessing too frequently!";
        public string CacheKey { get; set; }
        public RateLimiterModel RateLimiterModel { get; set; }
        public Ratelimitingrule RateLimitingRule { get; set; }
    }

    public class Ratelimitingrule
    {
        public bool IsAllApiRateLimiting { get; set; }
        public IsAllapiflowlimitingrule IsAllApiFlowLimitingRule { get; set; }
        public string RateLimitingLogLevel { get; set; }
        public Methodflowlimitingrule[] MethodFlowLimitingRules { get; set; }
        public Apiflowlimitingrule[] ApiFlowLimitingRules { get; set; }
    }

    public class IsAllapiflowlimitingrule
    {
        public int Capacity { get; set; }
        public int TokensPerSecond { get; set; }
        public int MaxRequests { get; set; }
        public int RateLimit { get; set; }
    }

    public class Methodflowlimitingrule
    {
        public string Method { get; set; }
        public int Capacity { get; set; }
        public int TokensPerSecond { get; set; }
        public int MaxRequests { get; set; }
        public int RateLimit { get; set; }
    }

    public class Apiflowlimitingrule
    {
        public string Path { get; set; }
        public int Capacity { get; set; }
        public int TokensPerSecond { get; set; }
        public int MaxRequests { get; set; }
        public int RateLimit { get; set; }
    }
}
