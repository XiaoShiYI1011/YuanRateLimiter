using Microsoft.AspNetCore.Http;
using YuanRateLimiter.Core.Interface;

namespace YuanRateLimiter.Tests;

/// <summary>
/// 单元测试限流器实现，负责模拟限流允许或拒绝结果并记录调用次数
/// 创 建 者：十一 
/// 创建时间：2026/6/13 20:03:28 
/// </summary>
internal sealed class StubRateLimiter : IRateLimiter
{
    public bool Result { get; set; } = true;

    public int Calls { get; private set; }

    public Task<bool> CheckRateLimit(HttpContext context)
    {
        Calls++;
        return Task.FromResult(Result);
    }

    public void Dispose()
    {
    }
}
