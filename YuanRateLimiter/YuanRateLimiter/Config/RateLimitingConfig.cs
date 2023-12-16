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

        public int HttpStatusCode { get; set; }

        public string? CacheKey {  get; set; }

        public bool IsAllApiRateLimiting { get; set; }

        public IsAllApiFlowLimitingRule IsAllApiFlowLimitingRule { get; set; }

        public string RateLimitingLogLevel { get; set; }

        public List<MethodFlowLimitingRule> MethodFlowLimitingRules { get; set; }

        public List<ApiFlowLimitingRule> ApiFlowLimitingRules { get; set; }
    }
}
