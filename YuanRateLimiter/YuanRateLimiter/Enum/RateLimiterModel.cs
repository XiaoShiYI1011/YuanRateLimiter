using System.ComponentModel;

namespace YuanRateLimiter.Enum
{
    /// <summary>
    /// 限流算法模型枚举
    /// 创 建 者：十一 
    /// 创建时间：2023/12/17 22:01:52 
    /// </summary>
    public enum RateLimiterModel
    {
        /// <summary>
        /// 0-令牌桶算法
        /// </summary>
        [Description("令牌桶算法")]
        TokenBucket = 0,

        /// <summary>
        /// 1-漏桶算法
        /// </summary>
        [Description("漏桶算法")]
        LeakBucket = 1,

        /// <summary>
        /// 2-滑动窗口算法
        /// </summary>
        [Description("滑动窗口算法")]
        SlidingWindow = 2
    }
}
