using YuanRateLimiter.Enum;

namespace YuanRateLimiter.Config
{
    /// <summary>
    /// 限流配置类
    /// 创 建 者：十一 
    /// 创建时间：2023/11/21 22:15:52 
    /// </summary>
    public class RateLimiterConfig
    {
        /// <summary>
        /// 是否开启限流
        /// 必选配置项
        /// </summary>
        /// <example>true</example>
        public bool EnableRateLimiter { get; set; }

        /// <summary>
        /// 触发限流时返回的HTTP状态码
        /// 可选配置，默认值：429 (Too Many Requests)
        /// </summary>
        /// <example>429</example>
        public int HttpStatusCode { get; set; } = 429;

        /// <summary>
        /// 触发限流时返回的提示消息
        /// 可选配置，默认英文提示消息
        /// </summary>
        /// <example>"请求过于频繁，请稍后再试。"</example>
        public string LimitingMessage { get; set; } = "The request is too frequent, please try again later.";

        /// <summary>
        /// 是否启用降级缓存
        /// 当Redis不可用时自动降级到内存缓存，确保限流功能不中断
        /// 默认值：true
        /// </summary>
        /// <example>true</example>
        public bool EnableFallbackCache { get; set; } = true;

        /// <summary>
        /// 是否启用双写策略
        /// Redis写数据的同时内存也写数据，确保缓存高可用
        /// 注意：默认关闭以避免内存占用过高，Redis宕机恢复后不需要回种内存中的限流数据
        /// 默认值：false
        /// </summary>
        /// <example>true</example>
        public bool EnableDoubleWrite { get; set; } = false;

        /// <summary>
        /// Redis操作失败时的重试次数
        /// 可选配置，默认值：3
        /// </summary>
        /// <example>3</example>
        public int RedisRetryCount { get; set; } = 3;

        /// <summary>
        /// Redis重试延迟时间（毫秒）
        /// 可选配置，默认值：1000
        /// </summary>
        /// <example>1000</example>
        public int RedisRetryDelayMs { get; set; } = 1000;

        /// <summary>
        /// 缓存Key的前缀
        /// 可选配置，默认值："RateLimiterKey"
        /// 实际存储时会在此基础上追加IP、路径等信息
        /// </summary>
        /// <example>"RateLimiterKey"</example>
        public string CacheKey { get; set; } = "RateLimiterKey";

        /// <summary>
        /// 使用的限流算法模型
        /// 可选值：TokenBucket（令牌桶）、LeakBucket（漏桶）、SlidingWindow（滑动窗口）
        /// 默认值：TokenBucket
        /// </summary>
        /// <example>RateLimiterModel.TokenBucket</example>
        public RateLimiterModel RateLimiterModel { get; set; } = RateLimiterModel.TokenBucket;

        /// <summary>
        /// 是否启用基于IP的限流
        /// 启用后将对不同IP分别进行限流统计
        /// 默认值：false
        /// </summary>
        /// <example>true</example>
        public bool EnableIpLimiter { get; set; } = false;

        /// <summary>
        /// IP白名单列表
        /// 在此列表中的IP地址不受限流规则限制
        /// 可选配置，默认为空数组
        /// </summary>
        /// <example>["127.0.0.1", "0.0.0.1"]</example>
        public string[] IpWhiteList { get; set; }

        /// <summary>
        /// IP黑名单列表
        /// 在此列表中的IP地址将直接被拒绝访问
        /// 可选配置，默认为空数组
        /// </summary>
        /// <example>["122.189.37.201"]</example>
        public string[] IpBlackList { get; set; }

        /// <summary>
        /// 限流规则配置
        /// </summary>
        public RateLimiterRule RateLimiterRule { get; set; }
    }

    /// <summary>
    /// 限流规则配置类
    /// </summary>
    public class RateLimiterRule
    {
        /// <summary>
        /// 全接口限流规则
        /// 对所有接口统一应用的限流规则
        /// </summary>
        public AllFlowLimiterRule AllFlowLimiterRule { get; set; }

        /// <summary>
        /// 限流级别
        /// 可选值：Action（路径级别）、Method（HTTP方法级别）、All（全接口）
        /// 默认值：All
        /// </summary>
        /// <example>RateLimitingLevel.Method</example>
        public RateLimitingLevel RateLimiterLogLevel { get; set; } = RateLimitingLevel.All;

        /// <summary>
        /// HTTP方法级别限流规则数组
        /// 根据HTTP方法（GET、POST、PUT等）分别配置限流规则
        /// 当RateLimiterLogLevel设置为Method时生效
        /// </summary>
        public MethodFlowLimiterRules[] MethodFlowLimiterRules { get; set; }

        /// <summary>
        /// 接口路径级别限流规则数组
        /// 根据具体的API路径分别配置限流规则
        /// 当RateLimiterLogLevel设置为Action时生效
        /// </summary>
        public ActionFlowLimiterRules[] ActionFlowLimiterRules { get; set; }
    }

    /// <summary>
    /// 全接口限流规则类
    /// </summary>
    public class AllFlowLimiterRule : BaseFlowLimiterRule
    {
    }

    /// <summary>
    /// HTTP方法级别限流规则类
    /// 继承自基础限流规则，增加Method属性
    /// </summary>
    public class MethodFlowLimiterRules : BaseFlowLimiterRule
    {
        /// <summary>
        /// 目标HTTP请求方法
        /// 可选值：GET、POST、PUT、DELETE、PATCH等
        /// 必填配置项
        /// </summary>
        /// <example>"GET"</example>
        public string Method { get; set; }
    }

    /// <summary>
    /// 接口路径级别限流规则类
    /// </summary>
    public class ActionFlowLimiterRules : BaseFlowLimiterRule
    {
        /// <summary>
        /// API路径
        /// 支持精确路径和通配符路径，如"/api/Test/Test01"、"/api/Test/*"、"/api/Test/**"、"/api/User/?"、"/api/Product/{id}"
        /// 必填配置项
        /// </summary>
        /// <example>"/api/Test/**"</example>
        public string Path { get; set; }
    }
}
