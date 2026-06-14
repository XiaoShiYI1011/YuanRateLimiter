using System;
using System.Collections.Generic;
using NewLife.Caching;

namespace YuanRateLimiter.Cache
{
    /// <summary>
    /// FullRedis 客户端适配器，生产环境仍然委托给 NewLife.Redis。
    /// </summary>
    internal sealed class FullRedisClientAdapter : IRedisClientAdapter
    {
        private readonly FullRedis redisClient;

        public FullRedisClientAdapter(FullRedis redisClient)
        {
            this.redisClient = redisClient;
        }

        public bool Set<T>(string key, T value) => this.redisClient.Set(key, value);

        public bool Set<T>(string key, T value, int expireSeconds) => this.redisClient.Set(key, value, expireSeconds);

        public bool Set<T>(string key, T value, TimeSpan expire) => this.redisClient.Set(key, value, expire);

        public void ListAdd<T>(string key, T value) => this.redisClient.RPUSH(key, value);

        public int ListLeftPushOne<T>(string key, T value) => this.redisClient.LPUSH(key, value);

        public int ListRightPushOne<T>(string key, T value) => this.redisClient.RPUSH(key, value);

        public int ListLeftPush<T>(string key, IEnumerable<T> values) => this.redisClient.LPUSH(key, values);

        public int ListRightPush<T>(string key, IEnumerable<T> values) => this.redisClient.RPUSH(key, values);

        public void Remove(string key) => this.redisClient.Remove(key);

        public T ListLeftPop<T>(string key) => this.redisClient.LPOP<T>(key);

        public T ListRightPop<T>(string key) => this.redisClient.RPOP<T>(key);

        public T Get<T>(string key) => this.redisClient.Get<T>(key);

        public IList<T> GetList<T>(string key) => this.redisClient.GetList<T>(key);

        public double Decrement(string key, double value) => this.redisClient.Decrement(key, value);

        public double Increment(string key, double value) => this.redisClient.Increment(key, value);

        public bool ContainsKey(string key) => this.redisClient.ContainsKey(key);

        public bool SetExpire(string key, TimeSpan expire) => this.redisClient.SetExpire(key, expire);
    }
}
