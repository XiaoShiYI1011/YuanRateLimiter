using YuanRateLimiter.Config;
using YuanRateLimiter.Core;
using YuanRateLimiter.Enum;

namespace YuanRateLimiter.Tests;

/// <summary>
/// 验证限流规则匹配器对 All、Method、Action 级别规则和路径通配符的匹配行为
/// 创 建 者：十一 
/// 创建时间：2026/6/13 20:21:09 
/// </summary>
public class RateLimiterRuleMatcherTests
{
    /// <summary>
    /// 验证 All 级别会命中全局规则，并生成稳定的全局缓存 Key
    /// </summary>
    [Fact]
    public void TryMatch_AllLevel_ReturnsAllRuleAndStableCacheKey()
    {
        var config = TestHelpers.CreateAllConfig(cacheKey: "limiter");
        var context = TestHelpers.CreateContext();
        var matched = RateLimiterRuleMatcher.TryMatch(config, context, out var rule, out var ruleKey);
        Assert.True(matched);
        Assert.Same(config.RateLimiterRule.AllFlowLimiterRule, rule);
        Assert.Equal("all", ruleKey);
        Assert.Equal("limiter:all", RateLimiterRuleMatcher.GetCacheKey(config, ruleKey));
    }

    /// <summary>
    /// 验证 Method 级别按 HTTP 方法大小写不敏感匹配，并规范化规则 Key
    /// </summary>
    [Fact]
    public void TryMatch_MethodLevel_MatchesMethodCaseInsensitivelyAndNormalizesKey()
    {
        var methodRule = new MethodFlowLimiterRules { Method = " get ", Capacity = 3, RateLimit = 1 };
        var config = new RateLimiterConfig
        {
            CacheKey = "limiter",
            RateLimiterRule = new RateLimiterRule
            {
                RateLimiterLogLevel = RateLimitingLevel.Method,
                MethodFlowLimiterRules = new[] { methodRule }
            }
        };
        var context = TestHelpers.CreateContext(method: "GET");
        var matched = RateLimiterRuleMatcher.TryMatch(config, context, out var rule, out var ruleKey);
        Assert.True(matched);
        Assert.Same(methodRule, rule);
        Assert.Equal("method:GET", ruleKey);
    }

    /// <summary>
    /// 验证 Method 级别没有对应方法规则时不会错误套用其他规则
    /// </summary>
    [Fact]
    public void TryMatch_MethodLevel_ReturnsFalseWhenMethodIsNotConfigured()
    {
        var config = new RateLimiterConfig
        {
            RateLimiterRule = new RateLimiterRule
            {
                RateLimiterLogLevel = RateLimitingLevel.Method,
                MethodFlowLimiterRules = [
                    new MethodFlowLimiterRules { Method = "POST", Capacity = 1, RateLimit = 1 }
                ]
            }
        };
        var matched = RateLimiterRuleMatcher.TryMatch(config, TestHelpers.CreateContext(method: "GET"), out var rule, out var ruleKey);
        Assert.False(matched);
        Assert.Null(rule);
        Assert.Null(ruleKey);
    }

    /// <summary>
    /// 验证 Action 级别下精确路径优先于更早配置的通配路径
    /// </summary>
    [Fact]
    public void TryMatch_ActionLevel_ExactPathWinsOverEarlierWildcardRule()
    {
        var wildcardRule = new ActionFlowLimiterRules { Path = "/api/users/*", Capacity = 1, RateLimit = 1 };
        var exactRule = new ActionFlowLimiterRules { Path = "/api/users/42", Capacity = 9, RateLimit = 1 };
        var config = new RateLimiterConfig
        {
            RateLimiterRule = new RateLimiterRule
            {
                RateLimiterLogLevel = RateLimitingLevel.Action,
                ActionFlowLimiterRules = new[] { wildcardRule, exactRule }
            }
        };
        // 精确规则优先可以避免宽泛通配配置覆盖具体接口的限流策略
        var matched = RateLimiterRuleMatcher.TryMatch(config, TestHelpers.CreateContext(path: "/api/users/42"), out var rule, out var ruleKey);

        Assert.True(matched);
        Assert.Same(exactRule, rule);
        Assert.Equal("action:/api/users/42", ruleKey);
    }

    /// <summary>
    /// 验证 Action 级别支持文档声明的多种路径通配符，并且普通字符会被正确转义
    /// </summary>
    [Theory]
    [InlineData("/api/users/*", "/api/users/42", true)]
    [InlineData("/api/users/*", "/api/users/42/detail", false)]
    [InlineData("/api/files/**", "/api/files", true)]
    [InlineData("/api/files/**", "/api/files/a/b", true)]
    [InlineData("/api/users/?", "/api/users/7", true)]
    [InlineData("/api/users/?", "/api/users/77", false)]
    [InlineData("/api/products/{id}", "/api/products/123", true)]
    [InlineData("/api/products/{id}", "/api/products/123/reviews", false)]
    [InlineData("/API/PRODUCTS/{id}", "/api/products/123", true)]
    [InlineData("/api/files/*.json", "/api/files/report.json", true)]
    [InlineData("/api/files/*.json", "/api/files/reportXjson", false)]
    public void TryMatch_ActionLevel_SupportsDocumentedPathPatterns(string pattern, string requestPath, bool expected)
    {
        var actionRule = new ActionFlowLimiterRules { Path = pattern, Capacity = 7, RateLimit = 1 };
        var config = new RateLimiterConfig
        {
            RateLimiterRule = new RateLimiterRule
            {
                RateLimiterLogLevel = RateLimitingLevel.Action,
                ActionFlowLimiterRules = new[] { actionRule }
            }
        };
        // 这里覆盖 *, **, ?, {id} 和 .json，防止正则转换把普通字符当成正则元字符
        var matched = RateLimiterRuleMatcher.TryMatch(config, TestHelpers.CreateContext(path: requestPath), out var rule, out var ruleKey);
        Assert.Equal(expected, matched);
        if (expected)
        {
            Assert.Same(actionRule, rule);
            Assert.Equal("action:" + pattern, ruleKey);
        }
        else
        {
            Assert.Null(rule);
            Assert.Null(ruleKey);
        }
    }

    /// <summary>
    /// 验证配置对象或 HttpContext 缺失时匹配器会安全返回未命中
    /// </summary>
    [Fact]
    public void TryMatch_ReturnsFalseWhenConfigOrContextIsMissing()
    {
        var config = TestHelpers.CreateAllConfig();
        Assert.False(RateLimiterRuleMatcher.TryMatch(null, TestHelpers.CreateContext(), out _, out _));
        Assert.False(RateLimiterRuleMatcher.TryMatch(config, null, out _, out _));
    }

    /// <summary>
    /// 验证 IP 维度缓存 Key 同时包含前缀、客户端 IP 和规则 Key
    /// </summary>
    [Fact]
    public void GetIpCacheKey_IncludesPrefixIpAndRuleKey()
    {
        var config = TestHelpers.CreateAllConfig(cacheKey: "limiter");
        var cacheKey = RateLimiterRuleMatcher.GetIpCacheKey(config, "203.0.113.9", "method:GET");
        Assert.Equal("limiter:ip:203.0.113.9:method:GET", cacheKey);
    }
}
