using System.ComponentModel;

namespace YuanRateLimiter.Enum
{
    /// <summary>
    /// 限流级别枚举
    /// 创 建 者：十一 
    /// 创建时间：2023/12/19 19:44:35 
    /// </summary>
    public enum RateLimitingLevel
    {
        /// <summary>
        /// 0-HTTP方法级
        /// </summary>
        [Description("HTTP方法级")]
        Method = 0,

        /// <summary>
        /// 1-接口路径级
        /// </summary>
        [Description("接口路径级")]
        Action = 1,

        /// <summary>
        /// 2-全接口
        /// </summary>
        [Description("全接口")]
        All = 2
    }
}
