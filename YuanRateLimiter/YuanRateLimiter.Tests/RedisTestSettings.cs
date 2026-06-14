using NewLife.Caching;

namespace YuanRateLimiter.Tests;

/// <summary>
/// Redis 集成测试配置工具，负责提供真实 Redis 连接串、客户端创建和测试 Key 生成
/// 创 建 者：十一 
/// 创建时间：2026/6/13 21:16:23 
/// </summary>
internal static class RedisTestSettings
{
    public const string EnvironmentVariableName = "YUAN_RATE_LIMITER_REDIS";
    private const string DefaultConnectionString = "127.0.0.1:6379,password=ydmkj.com.Redis,DefaultDatabase=0,connectTimeout=3000,connectRetry=1,syncTimeout=10000";

    public static string ConnectionString => Environment.GetEnvironmentVariable(EnvironmentVariableName) ?? DefaultConnectionString;

    public static FullRedis CreateClient()
    {
        var redis = new FullRedis();
        redis.Init(ConnectionString);
        return redis;
    }

    public static string UniqueKey(string name) => "yuan-rate-limiter:test:" + name + ":" + Guid.NewGuid().ToString("N");
}
