/*
 * 类名：MethodFlowLimitingRule
 * 描述：
 * 创 建 者：十一 
 * 创建时间：2023/11/20 9:55:55 
 */
namespace YuanRateLimiter.Config
{
    public class MethodFlowLimitingRule
    {
        public string Method { get; set; }
        public int Capacity { get; set; }
        public int TokensPerSecond { get; set; }
    }
}
