/*
 * 类名：RateLimiterModel
 * 描述：限流算法模型枚举
 * 创 建 者：十一 
 * 创建时间：2023/12/17 22:01:52 
 */
namespace YuanRateLimiter.Enum
{
    /// <summary>
    /// 限流算法模型枚举
    /// </summary>
    public enum RateLimiterModel
    {
        /// <summary>
        /// 令牌桶算法
        /// </summary>
        TokenBucket = 0,
        /// <summary>
        /// 漏桶算法
        /// </summary>
        LeakBucket = 1,
        /// <summary>
        /// 滑动窗口算法
        /// </summary>
        SlidingWindow = 2
    }
}
