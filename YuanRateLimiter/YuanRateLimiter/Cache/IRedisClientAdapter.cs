using System;
using System.Collections.Generic;

namespace YuanRateLimiter.Cache
{
    /// <summary>
    /// Redis 客户端适配接口，用于隔离第三方客户端并提升缓存仓储的可测试性
    /// </summary>
    internal interface IRedisClientAdapter
    {
        bool Set<T>(string key, T value);

        bool Set<T>(string key, T value, int expireSeconds);

        bool Set<T>(string key, T value, TimeSpan expire);

        void ListAdd<T>(string key, T value);

        int ListLeftPushOne<T>(string key, T value);

        int ListRightPushOne<T>(string key, T value);

        int ListLeftPush<T>(string key, IEnumerable<T> values);

        int ListRightPush<T>(string key, IEnumerable<T> values);

        void Remove(string key);

        T ListLeftPop<T>(string key);

        T ListRightPop<T>(string key);

        T Get<T>(string key);

        IList<T> GetList<T>(string key);

        double Decrement(string key, double value);

        double Increment(string key, double value);

        bool ContainsKey(string key);

        bool SetExpire(string key, TimeSpan expire);

        /// <summary>
        /// 执行单 Key Redis Lua 脚本，并将 Redis 服务器当前毫秒时间自动注入 ARGV[1]
        /// </summary>
        /// <param name="key">用于集群路由和脚本执行的 Key</param>
        /// <param name="script">Lua 脚本</param>
        /// <param name="arguments">传入脚本的其余 ARGV 参数，从 ARGV[2] 开始</param>
        /// <returns>脚本返回的整数结果</returns>
        long Eval(string key, string script, params object[] arguments);
    }
}
