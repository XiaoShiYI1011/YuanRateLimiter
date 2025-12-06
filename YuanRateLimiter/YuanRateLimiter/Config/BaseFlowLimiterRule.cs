namespace YuanRateLimiter.Config
{
    /// <summary>
    /// 限流规则基类
    /// 创 建 者：十一 
    /// 创建时间：2025/12/5 22:27:57 
    /// </summary>
    public abstract class BaseFlowLimiterRule
    {
        /// <summary>
        /// 容量（桶大小）
        /// 令牌桶算法：桶的最大容量
        /// 漏桶算法：桶的最大容量
        /// 滑动窗口：窗口内的最大请求数
        /// 必填配置项
        /// </summary>
        /// <example>100</example>
        public int Capacity { get; set; }

        /// <summary>
        /// 速率限制（QPS）
        /// 令牌桶算法：每秒生成的令牌数
        /// 漏桶算法：每秒流出的请求数
        /// 滑动窗口：每秒允许的最大请求数
        /// 必填配置项
        /// </summary>
        /// <example>20</example>
        public int RateLimit { get; set; }

        /// <summary>
        /// 窗口大小（秒）
        /// 仅滑动窗口算法使用，其他算法忽略此参数
        /// 可选配置，默认值：10
        /// </summary>
        /// <example>10</example>
        public int WindowSize { get; set; } = 10;

        /// <summary>
        /// 最大请求数
        /// 仅滑动窗口算法使用，其他算法忽略此参数
        /// 可选配置，默认值：10
        /// </summary>
        /// <example>10</example>
        public int MaxRequests { get; set; } = 10;
    }
}
