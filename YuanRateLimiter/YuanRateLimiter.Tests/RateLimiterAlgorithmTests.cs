using YuanRateLimiter.Core;
using YuanRateLimiter.Core.LeakBucket;
using YuanRateLimiter.Core.SlidingWindow;
using YuanRateLimiter.Core.TokenBucket;
using YuanRateLimiter.Enum;

namespace YuanRateLimiter.Tests;

/// <summary>
/// 验证令牌桶、漏桶、滑动窗口及其 IP 维度限流实现的核心算法行为
/// 创 建 者：十一 
/// 创建时间：2026/6/13 20:32:27 
/// </summary>
public class RateLimiterAlgorithmTests
{
    /// <summary>
    /// 验证令牌桶在 All 级别会消耗容量，并在补充令牌前拒绝超量请求
    /// </summary>
    [Fact]
    public async Task TokenBucket_AllLevel_ConsumesCapacityAndDeniesUntilRefill()
    {
        var cache = TestHelpers.CreateMemoryCache();
        var config = TestHelpers.CreateAllConfig(RateLimiterModel.TokenBucket, capacity: 2, rateLimit: 1);
        using var limiter = new TokenBucket(cache, config);
        Assert.True(await limiter.CheckRateLimit(TestHelpers.CreateContext()));
        Assert.True(await limiter.CheckRateLimit(TestHelpers.CreateContext()));
        Assert.False(await limiter.CheckRateLimit(TestHelpers.CreateContext()));
    }

    /// <summary>
    /// 验证令牌桶会根据上次补充时间和速率懒惰补充令牌
    /// </summary>
    [Fact]
    public async Task TokenBucket_RefillsUsingElapsedTime()
    {
        var cache = TestHelpers.CreateMemoryCache();
        var config = TestHelpers.CreateAllConfig(RateLimiterModel.TokenBucket, capacity: 2, rateLimit: 1);
        var baseKey = RateLimiterRuleMatcher.GetCacheKey(config, "all");
        cache.Set(baseKey + ":tokens", 0);
        // 直接预置历史时间，避免测试依赖真实等待
        cache.Set(baseKey + ":last", DateTimeOffset.Now.AddSeconds(-2).ToUnixTimeMilliseconds());
        using var limiter = new TokenBucket(cache, config);
        Assert.True(await limiter.CheckRateLimit(TestHelpers.CreateContext()));
    }

    /// <summary>
    /// 验证令牌桶释放时会清理本实例触达过的缓存 Key
    /// </summary>
    [Fact]
    public async Task TokenBucket_DisposeClearsTouchedCacheKeys()
    {
        var cache = TestHelpers.CreateMemoryCache();
        var config = TestHelpers.CreateAllConfig(RateLimiterModel.TokenBucket, capacity: 1);
        var baseKey = RateLimiterRuleMatcher.GetCacheKey(config, "all");
        var limiter = new TokenBucket(cache, config);
        Assert.True(await limiter.CheckRateLimit(TestHelpers.CreateContext()));
        Assert.True(cache.ExistsKey(baseKey + ":tokens"));
        Assert.True(cache.ExistsKey(baseKey + ":last"));
        limiter.Dispose();
        Assert.False(cache.ExistsKey(baseKey + ":tokens"));
        Assert.False(cache.ExistsKey(baseKey + ":last"));
    }

    /// <summary>
    /// 验证并发请求进入令牌桶时仍然只能放行桶容量内的请求数
    /// </summary>
    [Fact]
    public async Task TokenBucket_ConcurrentRequestsRespectCapacity()
    {
        var cache = TestHelpers.CreateMemoryCache();
        var config = TestHelpers.CreateAllConfig(RateLimiterModel.TokenBucket, capacity: 5, rateLimit: 1);
        using var limiter = new TokenBucket(cache, config);
        // 并发压测实例内信号量，防止读写缓存状态时超发令牌
        var results = await Task.WhenAll(Enumerable.Range(0, 20).Select(_ => limiter.CheckRateLimit(TestHelpers.CreateContext())));
        Assert.Equal(5, results.Count(allowed => allowed));
        Assert.Equal(15, results.Count(allowed => !allowed));
    }

    /// <summary>
    /// 验证请求没有命中任何规则时会放行，并且不会写入无意义的限流状态
    /// </summary>
    [Fact]
    public async Task TokenBucket_WhenNoRuleMatches_AllowsRequestWithoutWritingState()
    {
        var cache = TestHelpers.CreateMemoryCache();
        var config = TestHelpers.CreateAllConfig(RateLimiterModel.TokenBucket);
        config.RateLimiterRule.RateLimiterLogLevel = RateLimitingLevel.Method;
        config.RateLimiterRule.MethodFlowLimiterRules = [
            new Config.MethodFlowLimiterRules { Method = "POST", Capacity = 1, RateLimit = 1 }
        ];
        using var limiter = new TokenBucket(cache, config);
        Assert.True(await limiter.CheckRateLimit(TestHelpers.CreateContext(method: "GET")));
        Assert.False(cache.ExistsKey(RateLimiterRuleMatcher.GetCacheKey(config, "method:GET") + ":tokens"));
    }

    /// <summary>
    /// 验证漏桶在未发生漏水前最多只允许桶容量内的请求
    /// </summary>
    [Fact]
    public async Task LeakBucket_AllLevel_AllowsOnlyCapacityBeforeLeakage()
    {
        var cache = TestHelpers.CreateMemoryCache();
        var config = TestHelpers.CreateAllConfig(RateLimiterModel.LeakBucket, capacity: 2, rateLimit: 1);
        using var limiter = new LeakBucket(cache, config);
        Assert.True(await limiter.CheckRateLimit(TestHelpers.CreateContext()));
        Assert.True(await limiter.CheckRateLimit(TestHelpers.CreateContext()));
        Assert.False(await limiter.CheckRateLimit(TestHelpers.CreateContext()));
    }

    /// <summary>
    /// 验证漏桶会按经过时间释放水位，从而允许后续请求进入
    /// </summary>
    [Fact]
    public async Task LeakBucket_LeakedCapacityAllowsLaterRequest()
    {
        var cache = TestHelpers.CreateMemoryCache();
        var config = TestHelpers.CreateAllConfig(RateLimiterModel.LeakBucket, capacity: 2, rateLimit: 1);
        var baseKey = RateLimiterRuleMatcher.GetCacheKey(config, "all");
        cache.Set(baseKey + ":level", "2");
        // 预置满桶和过去时间，直接覆盖漏水计算分支
        cache.Set(baseKey + ":lastTime", DateTimeOffset.Now.AddMilliseconds(-1500).ToUnixTimeMilliseconds().ToString());
        using var limiter = new LeakBucket(cache, config);
        Assert.True(await limiter.CheckRateLimit(TestHelpers.CreateContext()));
    }

    /// <summary>
    /// 验证滑动窗口在窗口期内达到最大请求数后会拒绝后续请求
    /// </summary>
    [Fact]
    public async Task SlidingWindow_AllLevel_DeniesAfterMaxRequestsInsideWindow()
    {
        var cache = TestHelpers.CreateMemoryCache();
        var config = TestHelpers.CreateAllConfig(RateLimiterModel.SlidingWindow, windowSize: 10, maxRequests: 2);
        using var limiter = new SlidingWindow(cache, config);
        Assert.True(await limiter.CheckRateLimit(TestHelpers.CreateContext()));
        Assert.True(await limiter.CheckRateLimit(TestHelpers.CreateContext()));
        Assert.False(await limiter.CheckRateLimit(TestHelpers.CreateContext()));
    }

    /// <summary>
    /// 验证滑动窗口会先清理过期请求，再统计当前请求是否可放行
    /// </summary>
    [Fact]
    public async Task SlidingWindow_RemovesExpiredEntriesBeforeCountingCurrentRequest()
    {
        var cache = TestHelpers.CreateMemoryCache();
        var config = TestHelpers.CreateAllConfig(RateLimiterModel.SlidingWindow, windowSize: 1, maxRequests: 1);
        var baseKey = RateLimiterRuleMatcher.GetCacheKey(config, "all");
        // 如果不先清理过期项，下面这个请求会被旧数据错误拒绝
        cache.ListAdd(baseKey, new RequestQueue { RequestTime = DateTimeOffset.Now.AddSeconds(-5).ToUnixTimeSeconds() });
        cache.ListAdd(baseKey, new RequestQueue { RequestTime = DateTimeOffset.Now.AddSeconds(-4).ToUnixTimeSeconds() });
        using var limiter = new SlidingWindow(cache, config);
        Assert.True(await limiter.CheckRateLimit(TestHelpers.CreateContext()));
        var requests = cache.ListGetAll<RequestQueue>(baseKey);
        var request = Assert.Single(requests);
        Assert.True(request.RequestTime >= DateTimeOffset.Now.AddSeconds(-1).ToUnixTimeSeconds());
    }

    /// <summary>
    /// 验证三种 IP 限流算法都会按客户端 IP 独立维护配额
    /// </summary>
    [Theory]
    [InlineData(RateLimiterModel.TokenBucket)]
    [InlineData(RateLimiterModel.LeakBucket)]
    [InlineData(RateLimiterModel.SlidingWindow)]
    public async Task IpAlgorithms_KeepIndependentQuotaPerClientIp(RateLimiterModel model)
    {
        var cache = TestHelpers.CreateMemoryCache();
        var config = TestHelpers.CreateAllConfig(model, enableIpLimiter: true, capacity: 1, rateLimit: 1, windowSize: 10, maxRequests: 1);
        using var limiter = CreateIpLimiter(model, cache, config);
        // 同一 IP 第二次被拒绝，但另一个 IP 仍应拥有独立额度
        Assert.True(await limiter.CheckRateLimit(TestHelpers.CreateContext(remoteIp: "203.0.113.1")));
        Assert.False(await limiter.CheckRateLimit(TestHelpers.CreateContext(remoteIp: "203.0.113.1")));
        Assert.True(await limiter.CheckRateLimit(TestHelpers.CreateContext(remoteIp: "203.0.113.2")));
    }

    /// <summary>
    /// 验证三种 IP 限流算法在无法解析客户端 IP 时都会拒绝请求
    /// </summary>
    [Theory]
    [InlineData(RateLimiterModel.TokenBucket)]
    [InlineData(RateLimiterModel.LeakBucket)]
    [InlineData(RateLimiterModel.SlidingWindow)]
    public async Task IpAlgorithms_RejectRequestWhenClientIpCannotBeResolved(RateLimiterModel model)
    {
        var cache = TestHelpers.CreateMemoryCache();
        var config = TestHelpers.CreateAllConfig(model, enableIpLimiter: true);
        using var limiter = CreateIpLimiter(model, cache, config);
        Assert.False(await limiter.CheckRateLimit(TestHelpers.CreateContext(remoteIp: null)));
    }

    private static Core.Interface.IRateLimiter CreateIpLimiter(RateLimiterModel model, Cache.ICacheService cache, Config.RateLimiterConfig config)
    {
        return model switch
        {
            RateLimiterModel.TokenBucket => new IPTokenBucket(cache, config),
            RateLimiterModel.LeakBucket => new IPLeakBucket(cache, config),
            RateLimiterModel.SlidingWindow => new IPSlidingWindow(cache, config),
            _ => throw new ArgumentOutOfRangeException(nameof(model), model, null)
        };
    }
}
