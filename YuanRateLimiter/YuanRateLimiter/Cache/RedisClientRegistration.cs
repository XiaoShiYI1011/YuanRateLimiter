using NewLife.Caching;

namespace YuanRateLimiter.Cache
{
    /// <summary>
    /// Redis 启动注册结果，同时保留 FullRedis 原对象和可测试适配器。
    /// </summary>
    internal sealed class RedisClientRegistration
    {
        public RedisClientRegistration(FullRedis redisClient, IRedisClientAdapter adapter)
        {
            RedisClient = redisClient;
            Adapter = adapter;
        }

        public FullRedis RedisClient { get; }

        public IRedisClientAdapter Adapter { get; }
    }
}
