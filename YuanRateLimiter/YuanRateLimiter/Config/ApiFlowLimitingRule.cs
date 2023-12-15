/*
 * 类名：ApiFlowLimitingRule
 * 描述：
 * 创 建 者：十一 
 * 创建时间：2023/11/20 9:38:59  
 */
namespace YuanRateLimiter.Config
{
    public class ApiFlowLimitingRule
    {
        public string Path { get; set; }
        public int Capacity { get; set; }
        public int TokensPerSecond { get; set; }
    }
}
