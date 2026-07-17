namespace YuanRateLimiter.Cache
{
    /// <summary>
    /// Redis Lua 脚本执行能力
    /// 创 建 者：十一
    /// 创建时间：2026/7/18
    /// </summary>
    internal interface IRedisLuaExecutor
    {
        /// <summary>
        /// 执行单 Key Redis Lua 脚本，并将 Redis 服务端毫秒时间注入 ARGV[1]
        /// </summary>
        /// <param name="key">用于路由和脚本执行的 Key</param>
        /// <param name="script">Lua 脚本</param>
        /// <param name="arguments">脚本其余参数，从 ARGV[2] 开始</param>
        /// <returns>脚本返回的整数结果</returns>
        long Eval(string key, string script, params object[] arguments);
    }
}
