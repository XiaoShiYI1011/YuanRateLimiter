using System.Net;
using Microsoft.AspNetCore.Http;
using NewLife.Caching;
using YuanRateLimiter.Cache;
using YuanRateLimiter.Config;
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

        public static string UniqueCacheKey() => "test-" + Guid.NewGuid().ToString("N");

        public static async Task<string> ReadResponseBodyAsync(HttpContext context)
        {
            context.Response.Body.Position = 0;
            using var reader = new StreamReader(context.Response.Body, leaveOpen: true);
            return await reader.ReadToEndAsync();
        }
    }
}
