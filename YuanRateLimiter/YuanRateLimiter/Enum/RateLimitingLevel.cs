/*
 * 类名：RateLimitingLevel
 * 描述：限流级别枚举
 * 创 建 者：十一 
 * 创建时间：2023/12/19 19:44:35 
 */
namespace YuanRateLimiter.Enum
{
    /// <summary>
    /// 限流级别枚举
    /// </summary>
    public enum RateLimitingLevel
    {
        /// <summary>
        /// Method
        /// </summary>
        Method = 0,
        /// <summary>
        /// Action
        /// </summary>
        Action = 1,
        /// <summary>
        /// All
        /// </summary>
        All = 2
    }
}
