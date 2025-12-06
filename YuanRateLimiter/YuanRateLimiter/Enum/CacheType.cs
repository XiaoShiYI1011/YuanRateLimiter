using System.ComponentModel;

namespace YuanRateLimiter.Enum
{
    /// <summary>
    /// 缓存类型枚举
    /// 创 建 者：十一 
    /// 创建时间：2025/12/5 15:00:23 
    /// </summary>
    public enum CacheType
    {
        /// <summary>
        /// 0-Redis
        /// </summary>
        [Description("Redis")]
        Redis = 0,

        /// <summary>
        /// 1-Memory
        /// </summary>
        [Description("Memory")]
        Memory = 1,

        /// <summary>
        /// 2-Hybrid
        /// </summary>
        [Description("Hybrid")]
        Hybrid = 2
    }
}
