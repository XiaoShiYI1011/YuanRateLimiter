using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using YuanRateLimiter.Cache;
using YuanRateLimiter.Core.Interface;
using YuanRateLimiter.Core.SlidingWindow;
using YuanRateLimiter.Core.TokenBucket;
using YuanRateLimiter.Enum;

namespace YuanRateLimiter.Tests;

/// <summary>
/// 验证 Redis 与混合缓存相关功能在真实 Redis 环境下的集成行为
/// 创 建 者：十一 
/// 创建时间：2026/6/13 21:24:56 
/// </summary>
public class RedisIntegrationTests
{
    /// <summary>
    /// 验证 RedisCacheRepository 使用真实 Redis 完成键值写入、读取、存在性判断和删除
    /// </summary>
    [Fact]
    [Trait("Category", "RedisIntegration")]
    public void RedisCacheRepository_SetGetExistsAndDelete_RoundTripsAgainstRealRedis()
    {
        using var redis = RedisTestSettings.CreateClient();
        var cache = new RedisCacheRepository(redis);
        var key = RedisTestSettings.UniqueKey("value");
        try
        {
            Assert.Equal(CacheType.Redis, cache.CacheType);
            Assert.True(cache.IsAvailable);
            Assert.True(cache.Set(key, "redis-value", TimeSpan.FromSeconds(30)));
            Assert.True(cache.ExistsKey(key));
            Assert.Equal("redis-value", cache.Get<string>(key));
        }
        finally
        {
            cache.DelKey(key);
        }
    }

    /// <summary>
    /// 验证 RedisCacheRepository 使用真实 Redis 完成列表左右推入、读取和弹出
    /// </summary>
    [Fact]
    [Trait("Category", "RedisIntegration")]
    public void RedisCacheRepository_ListOperations_RoundTripAgainstRealRedis()
    {
        using var redis = RedisTestSettings.CreateClient();
        var cache = new RedisCacheRepository(redis);
        var key = RedisTestSettings.UniqueKey("list");
        try
        {
            cache.ListAdd(key, 2);
            Assert.Equal(1, cache.ListLeftPush(key, new[] { 1 }));
            Assert.Equal(2, cache.ListRightPush(key, new[] { 3, 4 }));
            Assert.Equal(new[] { 1, 2, 3, 4 }, cache.ListGetAll<int>(key));
            Assert.Equal(1, cache.ListLeftPop<int>(key));
            Assert.Equal(4, cache.ListRightPop<int>(key));
            Assert.Equal(new[] { 2, 3 }, cache.ListGetAll<int>(key));
        }
        finally
        {
            cache.DelKey(key);
        }
    }

    /// <summary>
    /// 验证 RedisCacheRepository 使用真实 Redis 完成数值递增、递减和过期时间设置
    /// </summary>
    [Fact]
    [Trait("Category", "RedisIntegration")]
    public void RedisCacheRepository_NumericAndExpireOperations_WorkAgainstRealRedis()
    {
        using var redis = RedisTestSettings.CreateClient();
        var cache = new RedisCacheRepository(redis);
        var key = RedisTestSettings.UniqueKey("number");
        try
        {
            Assert.Equal(2.5, cache.Increment(key, 2.5), precision: 6);
            Assert.Equal(1.25, cache.Decrement(key, 1.25), precision: 6);
            Assert.True(cache.SetExpires(key, TimeSpan.FromSeconds(30)));
        }
        finally
        {
            cache.DelKey(key);
        }
    }

    /// <summary>
    /// 验证 AddRateLimiterSetUp 在真实 Redis 可用且启用降级时会注册混合缓存
    /// </summary>
    [Fact]
    [Trait("Category", "RedisIntegration")]
    public void AddRateLimiterSetUp_WithRealRedisAndFallbackEnabled_RegistersHybridCache()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddRateLimiterSetUp(_ => TestHelpers.CreateAllConfig(
            model: RateLimiterModel.TokenBucket,
            enableIpLimiter: false,
            cacheKey: RedisTestSettings.UniqueKey("setup-hybrid")), RedisTestSettings.ConnectionString);
        using var provider = services.BuildServiceProvider();
        var cache = provider.GetRequiredService<ICacheService>();
        var limiter = provider.GetRequiredService<IRateLimiter>();
        Assert.IsType<HybridCacheRepository>(cache);
        Assert.IsType<TokenBucket>(limiter);
    }

    /// <summary>
    /// 验证 AddRateLimiterSetUp 在真实 Redis 可用且禁用降级时会注册纯 Redis 缓存
    /// </summary>
    [Fact]
    [Trait("Category", "RedisIntegration")]
    public void AddRateLimiterSetUp_WithRealRedisAndFallbackDisabled_RegistersRedisCache()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddRateLimiterSetUp(_ =>
        {
            var config = TestHelpers.CreateAllConfig(
                model: RateLimiterModel.SlidingWindow,
                enableIpLimiter: false,
                cacheKey: RedisTestSettings.UniqueKey("setup-redis"));
            config.EnableFallbackCache = false;
            return config;
        }, RedisTestSettings.ConnectionString);
        using var provider = services.BuildServiceProvider();
        var cache = provider.GetRequiredService<ICacheService>();
        var limiter = provider.GetRequiredService<IRateLimiter>();
        Assert.IsType<RedisCacheRepository>(cache);
        Assert.IsType<SlidingWindow>(limiter);
    }

    /// <summary>
    /// 验证令牌桶使用真实 Redis 时，不同限流器实例会共享同一份 Redis 状态
    /// </summary>
    [Fact]
    [Trait("Category", "RedisIntegration")]
    public async Task TokenBucket_WithRealRedis_SharesStateAcrossLimiterInstances()
    {
        using var redis = RedisTestSettings.CreateClient();
        var cache = new RedisCacheRepository(redis);
        var config = TestHelpers.CreateAllConfig(
            model: RateLimiterModel.TokenBucket,
            capacity: 1,
            rateLimit: 1,
            cacheKey: RedisTestSettings.UniqueKey("token-bucket"));
        var limiter1 = new TokenBucket(cache, config);
        var limiter2 = new TokenBucket(cache, config);
        try
        {
            // 第二个实例复用同一个 Redis Key，应看到第一个实例已经消耗掉的令牌。
            Assert.True(await limiter1.CheckRateLimit(TestHelpers.CreateContext()));
            Assert.False(await limiter2.CheckRateLimit(TestHelpers.CreateContext()));
        }
        finally
        {
            limiter1.Dispose();
            limiter2.Dispose();
        }
    }

    /// <summary>
    /// 验证混合缓存使用真实 Redis 时会以 Redis 为主存储，并按配置双写到内存缓存
    /// </summary>
    [Fact]
    [Trait("Category", "RedisIntegration")]
    public async Task HybridCacheRepository_WithRealRedis_DoubleWritesToMemoryCache()
    {
        using var redis = RedisTestSettings.CreateClient();
        var redisCache = new RedisCacheRepository(redis);
        var memoryCache = TestHelpers.CreateMemoryCache();
        var config = TestHelpers.CreateAllConfig(cacheKey: RedisTestSettings.UniqueKey("hybrid"));
        config.EnableDoubleWrite = true;
        using var hybrid = new HybridCacheRepository(redisCache, memoryCache, NullLogger<HybridCacheRepository>.Instance, config);
        var key = RedisTestSettings.UniqueKey("hybrid-value");
        try
        {
            Assert.True(hybrid.Set(key, "hybrid-value"));
            Assert.Equal("hybrid-value", redisCache.Get<string>(key));
            await WaitUntilAsync(() => memoryCache.ExistsKey(key));
            Assert.Equal("hybrid-value", memoryCache.Get<string>(key));
        }
        finally
        {
            hybrid.DelKey(key);
            memoryCache.DelKey(key);
        }
    }

    /// <summary>
    /// 验证混合缓存使用真实 Redis 时支持主缓存读取、过期设置、存在性判断和删除
    /// </summary>
    [Fact]
    [Trait("Category", "RedisIntegration")]
    public void HybridCacheRepository_WithRealRedis_SetGetExpireExistsAndDelete()
    {
        using var redis = RedisTestSettings.CreateClient();
        var redisCache = new RedisCacheRepository(redis);
        var memoryCache = TestHelpers.CreateMemoryCache();
        var config = TestHelpers.CreateAllConfig(cacheKey: RedisTestSettings.UniqueKey("hybrid-read"));
        using var hybrid = new HybridCacheRepository(redisCache, memoryCache, NullLogger<HybridCacheRepository>.Instance, config);
        var key = RedisTestSettings.UniqueKey("hybrid-read-value");
        try
        {
            Assert.True(hybrid.IsAvailable);
            Assert.Equal(CacheType.Hybrid, hybrid.CacheType);
            Assert.True(hybrid.Set(key, "read-value", TimeSpan.FromSeconds(30)));
            Assert.Equal("read-value", hybrid.Get<string>(key));
            Assert.True(hybrid.ExistsKey(key));
            Assert.True(hybrid.SetExpires(key, TimeSpan.FromSeconds(30)));
            hybrid.DelKey(key);
            Assert.False(hybrid.ExistsKey(key));
        }
        finally
        {
            hybrid.DelKey(key);
        }
    }

    /// <summary>
    /// 验证混合缓存使用真实 Redis 时可以覆盖列表、数值、存在性和过期时间等主要缓存操作
    /// </summary>
    [Fact]
    [Trait("Category", "RedisIntegration")]
    public void HybridCacheRepository_WithRealRedis_CoversPrimaryCacheOperations()
    {
        using var redis = RedisTestSettings.CreateClient();
        var redisCache = new RedisCacheRepository(redis);
        var memoryCache = TestHelpers.CreateMemoryCache();
        var config = TestHelpers.CreateAllConfig(cacheKey: RedisTestSettings.UniqueKey("hybrid-ops"));
        using var hybrid = new HybridCacheRepository(redisCache, memoryCache, NullLogger<HybridCacheRepository>.Instance, config);
        var listKey = RedisTestSettings.UniqueKey("hybrid-list");
        var numberKey = RedisTestSettings.UniqueKey("hybrid-number");
        try
        {
            hybrid.ListAdd(listKey, 2);
            Assert.Equal(1, hybrid.ListLeftPush(listKey, new[] { 1 }));
            Assert.Equal(1, hybrid.ListRightPush(listKey, new[] { 3 }));
            Assert.Equal(new[] { 1, 2, 3 }, hybrid.ListGetAll<int>(listKey));
            Assert.Equal(1, hybrid.ListLeftPop<int>(listKey));
            Assert.Equal(3, hybrid.ListRightPop<int>(listKey));
            Assert.Equal(3.0, hybrid.Increment(numberKey, 3), precision: 6);
            Assert.Equal(2.0, hybrid.Decrement(numberKey, 1), precision: 6);
            Assert.True(hybrid.ExistsKey(numberKey));
            Assert.True(hybrid.SetExpires(numberKey, TimeSpan.FromSeconds(30)));
        }
        finally
        {
            hybrid.DelKey(listKey);
            hybrid.DelKey(numberKey);
        }
    }

    /// <summary>
    /// 验证混合缓存释放逻辑对真实 Redis 场景也是幂等安全的
    /// </summary>
    [Fact]
    [Trait("Category", "RedisIntegration")]
    public void HybridCacheRepository_Dispose_IsIdempotent()
    {
        using var redis = RedisTestSettings.CreateClient();
        var hybrid = new HybridCacheRepository(
            new RedisCacheRepository(redis),
            TestHelpers.CreateMemoryCache(),
            NullLogger<HybridCacheRepository>.Instance,
            TestHelpers.CreateAllConfig(cacheKey: RedisTestSettings.UniqueKey("hybrid-dispose")));
        hybrid.Dispose();
        hybrid.Dispose();
    }

    private static async Task WaitUntilAsync(Func<bool> predicate)
    {
        for (var i = 0; i < 20; i++)
        {
            if (predicate()) return;
            await Task.Delay(50);
        }
        Assert.True(predicate());
    }
}
