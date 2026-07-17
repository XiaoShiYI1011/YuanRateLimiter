using System;
using System.Collections.Generic;
using System.Globalization;
using NewLife.Caching;

namespace YuanRateLimiter.Cache
{
    /// <summary>
    /// FullRedis 客户端适配器，生产环境仍然委托给 NewLife.Redis
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

        /// <summary>
        /// 执行单 Key Redis Lua 脚本，并将 Redis 服务器当前毫秒时间自动注入 ARGV[1]
        /// </summary>
        /// <param name="key">用于集群路由和脚本执行的 Key</param>
        /// <param name="script">Lua 脚本</param>
        /// <param name="arguments">传入脚本的其余 ARGV 参数，从 ARGV[2] 开始</param>
        /// <returns>脚本返回的整数结果</returns>
        public long Eval(string key, string script, params object[] arguments)
        {
            return this.redisClient.Execute(key, (client, actualKey) =>
            {
                string[] serverTime = client.Execute<string[]>("TIME");
                if (serverTime == null || serverTime.Length < 2) throw new InvalidOperationException("Redis TIME 命令未返回有效时间。");
                long seconds = long.Parse(serverTime[0], CultureInfo.InvariantCulture);
                long microseconds = long.Parse(serverTime[1], CultureInfo.InvariantCulture);
                long serverTimeMilliseconds = seconds * 1000 + microseconds / 1000;
                int argumentCount = arguments?.Length ?? 0;
                var evalArguments = new object[argumentCount + 4];
                evalArguments[0] = script;
                evalArguments[1] = 1;
                evalArguments[2] = actualKey;
                evalArguments[3] = serverTimeMilliseconds;
                if (argumentCount > 0) Array.Copy(arguments, 0, evalArguments, 4, argumentCount);
                return client.Execute<long>("EVAL", evalArguments);
            }, true);
        }
    }
}
