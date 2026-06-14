using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using YuanRateLimiter.Config;
using YuanRateLimiter.Middleware;

namespace YuanRateLimiter.Tests;

/// <summary>
/// 验证限流中间件在开关、黑白名单、限流拒绝和正常放行场景下的管道行为
/// 创 建 者：十一 
/// 创建时间：2026/6/13 20:51:18 
/// </summary>
public class RateLimiterMiddlewareTests
{
    /// <summary>
    /// 验证关闭限流开关时中间件会直接放行，并且不会调用限流器
    /// </summary>
    [Fact]
    public async Task InvokeAsync_WhenRateLimiterIsDisabled_CallsNextWithoutCheckingLimiter()
    {
        var nextCalls = 0;
        var limiter = new StubRateLimiter { Result = false };
        var middleware = CreateMiddleware(_ =>
        {
            nextCalls++;
            return Task.CompletedTask;
        }, limiter, new RateLimiterConfig { EnableRateLimiter = false });

        await middleware.InvokeAsync(TestHelpers.CreateContext());

        Assert.Equal(1, nextCalls);
        Assert.Equal(0, limiter.Calls);
    }

    /// <summary>
    /// 验证白名单 IP 会绕过限流检查并继续执行后续中间件
    /// </summary>
    [Fact]
    public async Task InvokeAsync_WhenIpIsWhitelisted_BypassesLimiterAndCallsNext()
    {
        var nextCalls = 0;
        var limiter = new StubRateLimiter { Result = false };
        var middleware = CreateMiddleware(_ =>
        {
            nextCalls++;
            return Task.CompletedTask;
        }, limiter, new RateLimiterConfig
        {
            EnableRateLimiter = true,
            IpWhiteList = new[] { " 203.0.113.10 " }
        });
        await middleware.InvokeAsync(TestHelpers.CreateContext(remoteIp: "203.0.113.10"));
        Assert.Equal(1, nextCalls);
        Assert.Equal(0, limiter.Calls);
    }

    /// <summary>
    /// 验证黑名单 IP 会返回 403 JSON 响应，并阻断后续中间件和限流器调用
    /// </summary>
    [Fact]
    public async Task InvokeAsync_WhenIpIsBlacklisted_ReturnsForbiddenJsonAndDoesNotCallNext()
    {
        var nextCalls = 0;
        var limiter = new StubRateLimiter { Result = true };
        var middleware = CreateMiddleware(_ =>
        {
            nextCalls++;
            return Task.CompletedTask;
        }, limiter, new RateLimiterConfig
        {
            EnableRateLimiter = true,
            IpBlackList = ["203.0.113.10"]
        });
        var context = TestHelpers.CreateContext(remoteIp: "203.0.113.10");
        // 关键点：黑名单应先于限流算法生效，避免被禁止的 IP 消耗限流状态
        await middleware.InvokeAsync(context);
        Assert.Equal(0, nextCalls);
        Assert.Equal(0, limiter.Calls);
        Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);
        Assert.Equal("application/json;charset=utf-8", context.Response.ContentType);
        using var document = JsonDocument.Parse(await TestHelpers.ReadResponseBodyAsync(context));
        Assert.Equal(403, document.RootElement.GetProperty("statusCode").GetInt32());
        Assert.Equal("当前Ip被禁止访问", document.RootElement.GetProperty("message").GetString());
    }

    /// <summary>
    /// 验证黑名单采用精确 IP 匹配，避免子串匹配误杀相似地址
    /// </summary>
    [Fact]
    public async Task InvokeAsync_BlacklistUsesExactIpMatching()
    {
        var nextCalls = 0;
        var limiter = new StubRateLimiter { Result = true };
        var middleware = CreateMiddleware(_ =>
        {
            nextCalls++;
            return Task.CompletedTask;
        }, limiter, new RateLimiterConfig
        {
            EnableRateLimiter = true,
            IpBlackList = new[] { "203.0.113.100" }
        });
        // 203.0.113.10 不应被 203.0.113.100 误命中
        await middleware.InvokeAsync(TestHelpers.CreateContext(remoteIp: "203.0.113.10"));
        Assert.Equal(1, nextCalls);
        Assert.Equal(1, limiter.Calls);
    }

    /// <summary>
    /// 验证限流器拒绝请求时会返回配置的状态码和提示信息，并阻断后续中间件
    /// </summary>
    [Fact]
    public async Task InvokeAsync_WhenLimiterRejects_ReturnsConfiguredJsonAndDoesNotCallNext()
    {
        var nextCalls = 0;
        var limiter = new StubRateLimiter { Result = false };
        var middleware = CreateMiddleware(_ =>
        {
            nextCalls++;
            return Task.CompletedTask;
        }, limiter, new RateLimiterConfig
        {
            EnableRateLimiter = true,
            HttpStatusCode = 418,
            LimitingMessage = "slow down"
        });
        var context = TestHelpers.CreateContext(path: "/api/orders");
        // 业务方配置的状态码和文案必须穿透到真实响应体
        await middleware.InvokeAsync(context);
        Assert.Equal(0, nextCalls);
        Assert.Equal(1, limiter.Calls);
        Assert.Equal(418, context.Response.StatusCode);
        Assert.Equal("application/json;charset=utf-8", context.Response.ContentType);
        using var document = JsonDocument.Parse(await TestHelpers.ReadResponseBodyAsync(context));
        Assert.Equal(418, document.RootElement.GetProperty("statusCode").GetInt32());
        Assert.Equal("slow down", document.RootElement.GetProperty("message").GetString());
    }

    /// <summary>
    /// 验证限流器允许请求时中间件会继续执行后续管道
    /// </summary>
    [Fact]
    public async Task InvokeAsync_WhenLimiterAllows_CallsNext()
    {
        var nextCalls = 0;
        var limiter = new StubRateLimiter { Result = true };
        var middleware = CreateMiddleware(_ =>
        {
            nextCalls++;
            return Task.CompletedTask;
        }, limiter, new RateLimiterConfig { EnableRateLimiter = true });
        await middleware.InvokeAsync(TestHelpers.CreateContext());
        Assert.Equal(1, nextCalls);
        Assert.Equal(1, limiter.Calls);
    }

    private static RateLimiterMiddleware CreateMiddleware(RequestDelegate next, StubRateLimiter limiter, RateLimiterConfig config)
    {
        return new RateLimiterMiddleware(next, limiter, config, NullLogger<RateLimiterMiddleware>.Instance);
    }
}
