using System.Net;
using Microsoft.AspNetCore.Http;
using NewLife.Caching;
using YuanRateLimiter.Cache;
using YuanRateLimiter.Config;
using YuanRateLimiter.Core.Interface;
using YuanRateLimiter.Core.LeakBucket;
using YuanRateLimiter.Core.SlidingWindow;
using YuanRateLimiter.Core.TokenBucket;
using YuanRateLimiter.Enum;

namespace YuanRateLimiter.Tests
{
    /// <summary>
    /// 单元测试通用辅助工具，负责创建 HttpContext、默认配置、内存缓存和响应体读取
    /// 创 建 者：十一 
    /// 创建时间：2026/6/13 19:58:16 
    /// </summary>
    internal static class TestHelpers
    {
        public static DefaultHttpContext CreateContext(
        string method = "GET",
        string path = "/api/test",
        string? remoteIp = "203.0.113.10")
        {
            var context = new DefaultHttpContext();
            context.Request.Method = method;
            context.Request.Path = path;
            context.Response.Body = new MemoryStream();
            if (remoteIp != null) context.Connection.RemoteIpAddress = IPAddress.Parse(remoteIp);
            return context;
        }

        public static RateLimiterConfig CreateAllConfig(RateLimiterModel model = RateLimiterModel.TokenBucket, bool enableIpLimiter = false, int capacity = 2, int rateLimit = 100, int windowSize = 10, int maxRequests = 2, string? cacheKey = null)
        {
            return new RateLimiterConfig
            {
                EnableRateLimiter = true,
                RateLimiterModel = model,
                EnableIpLimiter = enableIpLimiter,
                CacheKey = cacheKey ?? UniqueCacheKey(),
                RateLimiterRule = new RateLimiterRule
                {
                    RateLimiterLogLevel = RateLimitingLevel.All,
                    AllFlowLimiterRule = new AllFlowLimiterRule
                    {
                        Capacity = capacity,
                        RateLimit = rateLimit,
                        WindowSize = windowSize,
                        MaxRequests = maxRequests
                    }
                }
            };
        }

        public static MemoryCacheRepository CreateMemoryCache() => new MemoryCacheRepository(new MemoryCache());

        /// <summary>
        /// 根据配置创建对应的限流器实例
        /// </summary>
        /// <param name="model">限流算法</param>
        /// <param name="cache">缓存服务</param>
        /// <param name="config">限流配置</param>
        /// <returns></returns>
        public static IRateLimiter CreateLimiter(RateLimiterModel model, ICacheService cache, RateLimiterConfig config)
        {
            if (config.EnableIpLimiter)
            {
                return model switch
                {
                    RateLimiterModel.TokenBucket => new IPTokenBucket(cache, config),
                    RateLimiterModel.LeakBucket => new IPLeakBucket(cache, config),
                    RateLimiterModel.SlidingWindow => new IPSlidingWindow(cache, config),
                    _ => throw new ArgumentOutOfRangeException(nameof(model), model, null)
                };
            }
            return model switch
            {
                RateLimiterModel.TokenBucket => new TokenBucket(cache, config),
                RateLimiterModel.LeakBucket => new LeakBucket(cache, config),
                RateLimiterModel.SlidingWindow => new SlidingWindow(cache, config),
                _ => throw new ArgumentOutOfRangeException(nameof(model), model, null)
            };
        }

        /// <summary>
        /// 并发启动指定数量的异步任务
        /// </summary>
        /// <typeparam name="T">操作返回值类型</typeparam>
        /// <param name="count">并发数量</param>
        /// <param name="action">异步操作</param>
        /// <param name="beforeStart">所有任务到达起跑门后、统一放行前执行的操作</param>
        /// <returns></returns>
        public static async Task<T[]> RunConcurrentlyAsync<T>(int count, Func<int, Task<T>> action, Action? beforeStart = null)
        {
            if (count <= 0) return Array.Empty<T>();
            var start = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var allReady = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            int readyCount = 0;
            var tasks = Enumerable.Range(0, count).Select(index => Task.Run(async () =>
            {
                if (Interlocked.Increment(ref readyCount) == count) allReady.TrySetResult(true);
                await start.Task;
                return await action(index);
            })).ToArray();
            await allReady.Task;
            beforeStart?.Invoke();
            start.TrySetResult(true);
            return await Task.WhenAll(tasks);
        }

        public static string UniqueCacheKey() => "test-" + Guid.NewGuid().ToString("N");

        public static async Task<string> ReadResponseBodyAsync(HttpContext context)
        {
            context.Response.Body.Position = 0;
            using var reader = new StreamReader(context.Response.Body, leaveOpen: true);
            return await reader.ReadToEndAsync();
        }
    }
}
