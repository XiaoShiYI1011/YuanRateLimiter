using YuanRateLimiter.Config;
using YuanRateLimiter.Enum;

namespace YuanRateLimiter.Tests;

/// <summary>
/// 验证限流配置归一化器对非法配置、缺省配置和规则数组的修正行为
/// 创 建 者：十一 
/// 创建时间：2026/6/13 20:43:51 
/// </summary>
public class RateLimiterConfigValidatorTests
{
    /// <summary>
    /// 验证空配置会被归一化为安全默认配置，并且默认不主动开启限流
    /// </summary>
    [Fact]
    public void Normalize_NullConfig_ReturnsSafeDisabledDefaults()
    {
        var messages = new List<string>();
        var config = RateLimiterConfigValidator.Normalize(null, messages);
        Assert.False(config.EnableRateLimiter);
        Assert.Equal(429, config.HttpStatusCode);
        Assert.NotNull(config.RateLimiterRule);
        Assert.NotNull(config.RateLimiterRule.AllFlowLimiterRule);
        Assert.Equal(100, config.RateLimiterRule.AllFlowLimiterRule.Capacity);
        Assert.Equal(20, config.RateLimiterRule.AllFlowLimiterRule.RateLimit);
        Assert.NotEmpty(messages);
    }

    /// <summary>
    /// 验证非法标量值和规则数值会被修正到安全默认值或上限
    /// </summary>
    [Fact]
    public void Normalize_InvalidScalarAndRuleValues_AreClampedToSafeDefaults()
    {
        var messages = new List<string>();
        var config = new RateLimiterConfig
        {
            HttpStatusCode = 99,
            RateLimiterModel = (RateLimiterModel)99,
            RedisRetryCount = 99,
            RedisRetryDelayMs = -1,
            RateLimiterRule = new RateLimiterRule
            {
                RateLimiterLogLevel = (RateLimitingLevel)99,
                AllFlowLimiterRule = new AllFlowLimiterRule
                {
                    Capacity = 0,
                    RateLimit = -1,
                    WindowSize = 0,
                    MaxRequests = -5
                }
            }
        };
        // 配置错误不能影响宿主启动，这里锁住兜底修正行为
        var normalized = RateLimiterConfigValidator.Normalize(config, messages);
        Assert.Equal(429, normalized.HttpStatusCode);
        Assert.Equal(RateLimiterModel.TokenBucket, normalized.RateLimiterModel);
        Assert.Equal(10, normalized.RedisRetryCount);
        Assert.Equal(1000, normalized.RedisRetryDelayMs);
        Assert.Equal(RateLimitingLevel.All, normalized.RateLimiterRule.RateLimiterLogLevel);
        Assert.Equal(100, normalized.RateLimiterRule.AllFlowLimiterRule.Capacity);
        Assert.Equal(20, normalized.RateLimiterRule.AllFlowLimiterRule.RateLimit);
        Assert.Equal(10, normalized.RateLimiterRule.AllFlowLimiterRule.WindowSize);
        Assert.Equal(10, normalized.RateLimiterRule.AllFlowLimiterRule.MaxRequests);
        Assert.NotEmpty(messages);
    }

    /// <summary>
    /// 验证 IP 黑白名单会去除空值、首尾空格和重复地址
    /// </summary>
    [Fact]
    public void Normalize_IpLists_TrimsRemovesBlanksAndDeduplicates()
    {
        var config = TestHelpers.CreateAllConfig();
        config.IpWhiteList = new[] { " 203.0.113.1 ", "", "203.0.113.1", "198.51.100.5" };
        config.IpBlackList = new[] { " 192.0.2.9 ", "192.0.2.9", " " };
        var messages = new List<string>();
        var normalized = RateLimiterConfigValidator.Normalize(config, messages);
        Assert.Equal(new[] { "203.0.113.1", "198.51.100.5" }, normalized.IpWhiteList);
        Assert.Equal(new[] { "192.0.2.9" }, normalized.IpBlackList);
        Assert.Contains(messages, message => message.Contains("IpWhiteList", StringComparison.Ordinal));
        Assert.Contains(messages, message => message.Contains("IpBlackList", StringComparison.Ordinal));
    }

    /// <summary>
    /// 验证 Method 规则会丢弃无效项，并把有效 HTTP 方法规范化为大写
    /// </summary>
    [Fact]
    public void Normalize_MethodRules_DropsInvalidEntriesAndNormalizesMethod()
    {
        var config = TestHelpers.CreateAllConfig();
        config.RateLimiterRule.RateLimiterLogLevel = RateLimitingLevel.Method;
        config.RateLimiterRule.MethodFlowLimiterRules = new MethodFlowLimiterRules[]
        {
            null!,
            new MethodFlowLimiterRules { Method = " " },
            new MethodFlowLimiterRules { Method = " get ", Capacity = 0, RateLimit = 0, WindowSize = 0, MaxRequests = 0 }
        };
        var normalized = RateLimiterConfigValidator.Normalize(config, new List<string>());
        var rule = Assert.Single(normalized.RateLimiterRule.MethodFlowLimiterRules);
        Assert.Equal("GET", rule.Method);
        Assert.Equal(100, rule.Capacity);
        Assert.Equal(20, rule.RateLimit);
        Assert.Equal(RateLimitingLevel.Method, normalized.RateLimiterRule.RateLimiterLogLevel);
    }

    /// <summary>
    /// 验证 Action 规则会丢弃无效项，并自动为缺少斜杠的路径补齐前缀
    /// </summary>
    [Fact]
    public void Normalize_ActionRules_DropsInvalidEntriesAndPrefixesMissingSlash()
    {
        var config = TestHelpers.CreateAllConfig();
        config.RateLimiterRule.RateLimiterLogLevel = RateLimitingLevel.Action;
        config.RateLimiterRule.ActionFlowLimiterRules = new ActionFlowLimiterRules[]
        {
            null!,
            new ActionFlowLimiterRules { Path = " " },
            new ActionFlowLimiterRules { Path = "api/orders/{id}", Capacity = 1, RateLimit = 1 }
        };
        var normalized = RateLimiterConfigValidator.Normalize(config, new List<string>());
        var rule = Assert.Single(normalized.RateLimiterRule.ActionFlowLimiterRules);
        Assert.Equal("/api/orders/{id}", rule.Path);
        Assert.Equal(RateLimitingLevel.Action, normalized.RateLimiterRule.RateLimiterLogLevel);
    }

    /// <summary>
    /// 验证 Method 级别没有可用规则时不会隐式回退到 All，避免用户误判实际限流级别
    /// </summary>
    [Fact]
    public void Normalize_MethodLevelWithoutUsableRules_KeepsMethodLevelAndWarns()
    {
        var config = TestHelpers.CreateAllConfig();
        config.RateLimiterRule.RateLimiterLogLevel = RateLimitingLevel.Method;
        config.RateLimiterRule.MethodFlowLimiterRules = new[]
        {
            new MethodFlowLimiterRules { Method = " " }
        };
        var messages = new List<string>();
        var normalized = RateLimiterConfigValidator.Normalize(config, messages);
        Assert.Empty(normalized.RateLimiterRule.MethodFlowLimiterRules);
        Assert.Equal(RateLimitingLevel.Method, normalized.RateLimiterRule.RateLimiterLogLevel);
        Assert.Contains(messages, message => message.Contains("Method 级限流不会生效", StringComparison.Ordinal));
        Assert.Contains(messages, message => message.Contains("不会自动回退为 All", StringComparison.Ordinal));
    }

    /// <summary>
    /// 验证 Action 级别没有可用规则时不会隐式回退到 All，避免日志和实际行为不一致
    /// </summary>
    [Fact]
    public void Normalize_ActionLevelWithoutUsableRules_KeepsActionLevelAndWarns()
    {
        var config = TestHelpers.CreateAllConfig();
        config.RateLimiterRule.RateLimiterLogLevel = RateLimitingLevel.Action;
        config.RateLimiterRule.ActionFlowLimiterRules = new[]
        {
            new ActionFlowLimiterRules { Path = " " }
        };
        var messages = new List<string>();
        var normalized = RateLimiterConfigValidator.Normalize(config, messages);
        Assert.Empty(normalized.RateLimiterRule.ActionFlowLimiterRules);
        Assert.Equal(RateLimitingLevel.Action, normalized.RateLimiterRule.RateLimiterLogLevel);
        Assert.Contains(messages, message => message.Contains("Action 级限流不会生效", StringComparison.Ordinal));
        Assert.Contains(messages, message => message.Contains("不会自动回退为 All", StringComparison.Ordinal));
    }

    /// <summary>
    /// 验证 Action 级别拥有有效接口规则时，不会因为没有 AllFlowLimiterRule 而补齐全接口规则或输出误导警告
    /// </summary>
    [Fact]
    public void Normalize_ActionLevelWithRules_DoesNotRequireAllFlowLimiterRule()
    {
        var config = new RateLimiterConfig
        {
            RateLimiterRule = new RateLimiterRule
            {
                RateLimiterLogLevel = RateLimitingLevel.Action,
                ActionFlowLimiterRules = new[]
                {
                    new ActionFlowLimiterRules { Path = "/api/test", Capacity = 2, RateLimit = 1 }
                }
            }
        };
        var messages = new List<string>();
        var normalized = RateLimiterConfigValidator.Normalize(config, messages);
        Assert.Equal(RateLimitingLevel.Action, normalized.RateLimiterRule.RateLimiterLogLevel);
        Assert.Null(normalized.RateLimiterRule.AllFlowLimiterRule);
        Assert.Single(normalized.RateLimiterRule.ActionFlowLimiterRules);
        Assert.DoesNotContain(messages, message => message.Contains("AllFlowLimiterRule 未配置", StringComparison.Ordinal));
    }

    /// <summary>
    /// 验证只有 All 级别缺少 AllFlowLimiterRule 时，才会补齐全接口规则并输出对应警告
    /// </summary>
    [Fact]
    public void Normalize_AllLevelWithoutAllRule_AddsDefaultAllRuleWithExplicitWarning()
    {
        var config = new RateLimiterConfig
        {
            RateLimiterRule = new RateLimiterRule
            {
                RateLimiterLogLevel = RateLimitingLevel.All
            }
        };
        var messages = new List<string>();
        var normalized = RateLimiterConfigValidator.Normalize(config, messages);
        Assert.NotNull(normalized.RateLimiterRule.AllFlowLimiterRule);
        Assert.Equal(100, normalized.RateLimiterRule.AllFlowLimiterRule.Capacity);
        Assert.Contains(messages, message => message.Contains("RateLimiterLogLevel 配置为 All", StringComparison.Ordinal));
        Assert.Contains(messages, message => message.Contains("AllFlowLimiterRule 未配置", StringComparison.Ordinal));
    }
}
