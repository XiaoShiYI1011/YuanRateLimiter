/*
 * 类名：IsAllApiFlowLimitingRule
 * 描述：
 * 创 建 者：十一 
 * 创建时间：2023/11/20 10:16:13 
 */
namespace YuanRateLimiter.Config
{
    public class IsAllApiFlowLimitingRule
    {
        public int Capacity { get; set; }

        public int TokensPerSecond { get; set; }
    }
}
