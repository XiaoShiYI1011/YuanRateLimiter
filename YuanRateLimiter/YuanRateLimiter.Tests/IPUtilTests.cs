using YuanRateLimiter.Utils;

namespace YuanRateLimiter.Tests;

/// <summary>
/// 验证客户端 IPv4 获取逻辑在代理头、转发链和远程地址场景下的解析行为
/// 创 建 者：十一 
/// 创建时间：2026/6/13 20:08:42 
/// </summary>
public class IPUtilTests
{
    /// <summary>
    /// 验证没有转发链时优先使用 X-Real-IP 作为客户端 IPv4
    /// </summary>
    [Fact]
    public void GetClientIPv4_UsesXRealIpWhenForwardedForIsMissing()
    {
        var context = TestHelpers.CreateContext(remoteIp: "10.0.0.8");
        context.Request.Headers["X-Real-IP"] = "198.51.100.2";

        var ip = IPUtil.GetClientIPv4(context);

        Assert.Equal("198.51.100.2", ip);
    }

    /// <summary>
    /// 验证 X-Forwarded-For 会覆盖 X-Real-IP，并选择第一个非回环地址
    /// </summary>
    [Fact]
    public void GetClientIPv4_UsesFirstNonLoopbackForwardedIp()
    {
        var context = TestHelpers.CreateContext(remoteIp: "10.0.0.8");
        context.Request.Headers["X-Real-IP"] = "198.51.100.2";
        context.Request.Headers["X-Forwarded-For"] = "127.0.0.1, 203.0.113.9, 198.51.100.3";
        // 代理场景下跳过回环地址，避免把本机代理误判成真实客户端
        var ip = IPUtil.GetClientIPv4(context);
        Assert.Equal("203.0.113.9", ip);
    }

    /// <summary>
    /// 验证没有代理头时会回退到 RemoteIpAddress，并把 IPv6 映射地址转换为 IPv4
    /// </summary>
    [Fact]
    public void GetClientIPv4_FallsBackToRemoteIpAndMapsIpv6ToIpv4()
    {
        var context = TestHelpers.CreateContext(remoteIp: "::ffff:192.0.2.9");
        var ip = IPUtil.GetClientIPv4(context);
        Assert.Equal("192.0.2.9", ip);
    }

    /// <summary>
    /// 验证缺少 RemoteIpAddress 时不会信任请求头，直接返回空字符串
    /// </summary>
    [Fact]
    public void GetClientIPv4_ReturnsEmptyWhenRemoteIpIsMissing()
    {
        var context = TestHelpers.CreateContext(remoteIp: null);
        context.Request.Headers["X-Real-IP"] = "198.51.100.2";
        var ip = IPUtil.GetClientIPv4(context);
        Assert.Equal(string.Empty, ip);
    }
}
