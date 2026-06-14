namespace YuanRateLimiter.Tests;

/// <summary>
/// 验证内存缓存仓储的键值、列表、数值递增递减等基础缓存行为
/// 创 建 者：十一 
/// 创建时间：2026/6/13 20:14:35 
/// </summary>
public class MemoryCacheRepositoryTests
{
    /// <summary>
    /// 验证内存缓存可以完成基础键值写入、读取、存在性判断和删除生命周期
    /// </summary>
    [Fact]
    public void SetGetExistsAndDelete_RoundTripCachedValue()
    {
        var cache = TestHelpers.CreateMemoryCache();
        var key = TestHelpers.UniqueCacheKey();
        Assert.True(cache.Set(key, "value"));
        Assert.True(cache.ExistsKey(key));
        Assert.Equal("value", cache.Get<string>(key));
        cache.DelKey(key);
        Assert.False(cache.ExistsKey(key));
        Assert.Null(cache.Get<string>(key));
    }

    /// <summary>
    /// 验证内存缓存的列表左右推入和左右弹出顺序符合限流队列使用预期
    /// </summary>
    [Fact]
    public void ListOperations_PreserveExpectedLeftAndRightOrdering()
    {
        var cache = TestHelpers.CreateMemoryCache();
        var key = TestHelpers.UniqueCacheKey();
        // 滑动窗口依赖列表顺序清理过期请求，这里锁住左右操作的顺序语义
        cache.ListAdd(key, 2);
        Assert.Equal(1, cache.ListLeftPush(key, new[] { 1 }));
        Assert.Equal(2, cache.ListRightPush(key, new[] { 3, 4 }));
        Assert.Equal(new[] { 1, 2, 3, 4 }, cache.ListGetAll<int>(key));
        Assert.Equal(1, cache.ListLeftPop<int>(key));
        Assert.Equal(4, cache.ListRightPop<int>(key));
        Assert.Equal(new[] { 2, 3 }, cache.ListGetAll<int>(key));
    }

    /// <summary>
    /// 验证内存缓存的数值递增和递减操作能正确累加变化量
    /// </summary>
    [Fact]
    public void IncrementAndDecrement_UpdateNumericValues()
    {
        var cache = TestHelpers.CreateMemoryCache();
        var key = TestHelpers.UniqueCacheKey();
        Assert.Equal(1.5, cache.Increment(key, 1.5), precision: 6);
        Assert.Equal(1.0, cache.Decrement(key, 0.5), precision: 6);
    }
}
