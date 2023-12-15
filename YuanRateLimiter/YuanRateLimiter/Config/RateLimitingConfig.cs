/*
 * 类名：RateLimitingConfig
 * 描述：
 * 创 建 者：十一 
 * 创建时间：2023/11/21 22:15:52 
 */
namespace YuanRateLimiter.Config
{
    public class RateLimitingConfig
    {
        public static bool EnableRateLimiting { get; set; }

        public static string RealIpHeader { get; set; }

        public static int HttpStatusCode { get; set; }

        public static bool IsAllApiRateLimiting { get; set; }

        public static IsAllApiFlowLimitingRule IsAllApiFlowLimitingRule { get; set; }

        public static string RateLimitingLogLevel { get; set; }

        public static List<MethodFlowLimitingRule> MethodFlowLimitingRules { get; set; }

        public static List<ApiFlowLimitingRule> ApiFlowLimitingRules { get; set; }
    }
}
