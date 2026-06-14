using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Builder;
using YuanRateLimiter.Cache;
using YuanRateLimiter.Config;
using YuanRateLimiter.Core.Interface;
using YuanRateLimiter.Core.LeakBucket;
using YuanRateLimiter.Core.SlidingWindow;
using YuanRateLimiter.Core.TokenBucket;
using YuanRateLimiter.Enum;

namespace YuanRateLimiter.Tests;

/// <summary>
/// 验证限流服务注册入口、缓存注册、异常配置兜底和中间件扩展方法的集成行为
/// 创 建 者：十一 
/// 创建时间：2026/6/13 21:02:44 
/// </summary>
public class RateLimiterSetUpTests
{
    /// <summary>
    /// 验证旧版注册重载能读取已经注册的 RateLimiterConfig 并保留用户配置
    /// </summary>
    [Fact]
    public void AddRateLimiterSetUp_LegacyOverload_UsesPreRegisteredConfig()
    {
        var services = new ServiceCollection();
        services.AddSingleton(TestHelpers.CreateAllConfig(RateLimiterModel.LeakBucket, enableIpLimiter: true));
        services.AddRateLimiterSetUp();
        using var provider = services.BuildServiceProvider();
        var config = provider.GetRequiredService<RateLimiterConfig>();
        var limiter = provider.GetRequiredService<IRateLimiter>();
        Assert.Equal(RateLimiterModel.LeakBucket, config.RateLimiterModel);
        Assert.True(config.EnableIpLimiter);
        Assert.IsType<IPLeakBucket>(limiter);
    }

    /// <summary>
    /// 验证旧版注册重载在没有预注册配置时会创建安全默认配置
    /// </summary>
    [Fact]
    public void AddRateLimiterSetUp_LegacyOverloadWithoutConfig_RegistersSafeDefaultConfiguration()
    {
        var services = new ServiceCollection();
        services.AddRateLimiterSetUp();
        using var provider = services.BuildServiceProvider();
        var config = provider.GetRequiredService<RateLimiterConfig>();
        var limiter = provider.GetRequiredService<IRateLimiter>();
        Assert.False(config.EnableRateLimiter);
        Assert.Equal(RateLimiterModel.TokenBucket, config.RateLimiterModel);
        Assert.IsType<TokenBucket>(limiter);
    }

    /// <summary>
    /// 验证配置委托抛出异常时注册入口会回退到安全默认配置
    /// </summary>
    [Fact]
    public void AddRateLimiterSetUp_WhenConfigureThrows_RegistersSafeDefaultConfiguration()
    {
        var services = new ServiceCollection();
        services.AddRateLimiterSetUp(_ => throw new InvalidOperationException("bad config"));
        using var provider = services.BuildServiceProvider();
        var config = provider.GetRequiredService<RateLimiterConfig>();
        var limiter = provider.GetRequiredService<IRateLimiter>();
        Assert.False(config.EnableRateLimiter);
        Assert.Equal(RateLimiterModel.TokenBucket, config.RateLimiterModel);
        Assert.IsType<TokenBucket>(limiter);
    }

    /// <summary>
    /// 验证 Redis 连接失败时注册入口会按配置重试后回退到内存缓存
    /// </summary>
    [Fact]
    public void AddRateLimiterSetUp_WhenRedisConnectionFails_FallsBackToMemoryCache()
    {
        var services = new ServiceCollection();
        services.AddRateLimiterSetUp(_ =>
        {
            var config = TestHelpers.CreateAllConfig(RateLimiterModel.TokenBucket);
            config.RedisRetryCount = 1;
            config.RedisRetryDelayMs = 1;
            return config;
        }, "127.0.0.1:6380,password=ydmkj.com.Redis,DefaultDatabase=0,connectTimeout=100,connectRetry=0,syncTimeout=100");
        using var provider = services.BuildServiceProvider();
        var cache = provider.GetRequiredService<ICacheService>();
        Assert.IsType<MemoryCacheRepository>(cache);
    }

    /// <summary>
    /// 验证 UseRateLimitMiddleware 扩展方法会把限流中间件挂入 ASP.NET Core 管道
    /// </summary>
    [Fact]
    public void UseRateLimitMiddleware_ReturnsApplicationBuilderForChaining()
    {
        using var provider = new ServiceCollection().BuildServiceProvider();
        var builder = new ApplicationBuilder(provider);
        var result = builder.UseRateLimitMiddleware();
        Assert.Same(builder, result);
    }

    /// <summary>
    /// 验证服务注册会根据算法模型和 IP 开关选择正确的限流器实现
    /// </summary>
    [Theory]
    [InlineData(RateLimiterModel.TokenBucket, false, typeof(TokenBucket))]
    [InlineData(RateLimiterModel.TokenBucket, true, typeof(IPTokenBucket))]
    [InlineData(RateLimiterModel.LeakBucket, false, typeof(LeakBucket))]
    [InlineData(RateLimiterModel.LeakBucket, true, typeof(IPLeakBucket))]
    [InlineData(RateLimiterModel.SlidingWindow, false, typeof(SlidingWindow))]
    [InlineData(RateLimiterModel.SlidingWindow, true, typeof(IPSlidingWindow))]
    public void AddRateLimiterSetUp_RegistersAlgorithmSelectedByModelAndIpFlag(RateLimiterModel model, bool enableIpLimiter, Type expectedLimiterType)
    {
        var services = new ServiceCollection();
        // 注册入口是使用者的主要集成面，算法选择错误会直接改变运行行为
        services.AddRateLimiterSetUp(_ => TestHelpers.CreateAllConfig(model, enableIpLimiter));
        using var provider = services.BuildServiceProvider();
        var limiter = provider.GetRequiredService<IRateLimiter>();
        var cache = provider.GetRequiredService<ICacheService>();
        var config = provider.GetRequiredService<RateLimiterConfig>();
        Assert.IsType(expectedLimiterType, limiter);
        Assert.IsType<MemoryCacheRepository>(cache);
        Assert.Equal(model, config.RateLimiterModel);
        Assert.Equal(enableIpLimiter, config.EnableIpLimiter);
    }

    /// <summary>
    /// 验证配置委托为空时会注册安全默认配置和默认令牌桶实现
    /// </summary>
    [Fact]
    public void AddRateLimiterSetUp_NullDelegate_RegistersSafeDefaultConfiguration()
    {
        var services = new ServiceCollection();
        services.AddRateLimiterSetUp((Func<RateLimiterConfig, RateLimiterConfig>)null!);
        using var provider = services.BuildServiceProvider();
        var config = provider.GetRequiredService<RateLimiterConfig>();
        var limiter = provider.GetRequiredService<IRateLimiter>();
        Assert.False(config.EnableRateLimiter);
        Assert.Equal(RateLimiterModel.TokenBucket, config.RateLimiterModel);
        Assert.IsType<TokenBucket>(limiter);
    }
}
