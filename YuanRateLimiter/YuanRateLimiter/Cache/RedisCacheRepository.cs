using SimpleRedis;

/*
 * 类名：RedisCacheRepository
 * 描述：Redis 仓储类
 * 创 建 者：十一 
 * 创建时间：2023/11/20 22:24:58 
 */
namespace YuanRateLimiter.Cache
{
    /// <summary>
    /// Redis 仓储类
    /// </summary>
    internal class RedisCacheRepository : ICacheService
    {
        private readonly ISimpleRedis redisClient;
        public ISimpleRedis Client => redisClient;

        public RedisCacheRepository(ISimpleRedis redisClient)
        {
            this.redisClient = redisClient;
        }

        /// <summary>
        /// 添加一条缓存数据
        /// </summary>
        /// <typeparam name="T">序列化类型</typeparam>
        /// <param name="key">Key</param>
        /// <param name="value">Value</param>
        /// <returns></returns>
        public bool Set<T>(string key, T value)
        {
            return this.redisClient.Set<T>(key, value);
        }

        /// <summary>
        /// 添加一条缓存数据，可设置过期时间
        /// </summary>
        /// <typeparam name="T">序列化类型</typeparam>
        /// <param name="key">Key</param>
        /// <param name="value">Value</param>
        /// <param name="expires">过期时间</param>
        /// <returns></returns>
        public bool Set<T>(string key, T value, TimeSpan expire)
        {
            return this.redisClient.Set<T>(key, value, expire);
        }

        /// <summary>
        /// 添加一条数据到List
        /// </summary>
        /// <typeparam name="T">序列化类型</typeparam>
        /// <param name="key">Key</param>
        /// <param name="value">Value</param>
        public void ListAdd<T>(string key, T value)
        {
            this.redisClient.ListAdd<T>(key, value);
        }

        /// <summary>
        /// List（头）左推
        /// </summary>
        /// <typeparam name="T">序列化类型</typeparam>
        /// <param name="key">Key</param>
        /// <param name="values">Value</param>
        /// <returns></returns>
        public int ListLeftPush<T>(string key, IEnumerable<T> values)
        {
            return this.redisClient.ListLeftPush<T>(key, values);
        }

        /// <summary>
        /// List（尾）右推
        /// </summary>
        /// <typeparam name="T">序列化类型</typeparam>
        /// <param name="key">Key</param>
        /// <param name="values">Value</param>
        /// <returns></returns>
        public int ListRightPush<T>(string key, IEnumerable<T> values)
        {
            return this.redisClient.ListRightPush<T>(key, values);
        }

        /// <summary>
        /// 根据 Key 删除缓存数据
        /// </summary>
        /// <param name="key">Key</param>
        public void DelKey(string key)
        {
            this.redisClient.Remove(key);
        }

        /// <summary>
        /// List（头）左删，返回最左边一个元素
        /// </summary>
        /// <typeparam name="T">序列化类型</typeparam>
        /// <param name="key">Key</param>
        /// <returns></returns>
        public T ListLeftPop<T>(string key)
        {
            return this.redisClient.ListLeftPop<T>(key);
        }

        /// <summary>
        /// List（尾）右删，返回最右边一个元素
        /// </summary>
        /// <typeparam name="T">序列化类型</typeparam>
        /// <param name="key">Key</param>
        /// <returns></returns>
        public T ListRightPop<T>(string key)
        {
            return this.redisClient.ListRightPop<T>(key);
        }

        /// <summary>
        /// 获取缓存数据
        /// </summary>
        /// <typeparam name="T">序列化类型</typeparam>
        /// <param name="key">Key</param>
        /// <returns></returns>
        public T Get<T>(string key)
        {
            return this.redisClient.Get<T>(key);
        }

        /// <summary>
        /// 获取List
        /// </summary>
        /// <typeparam name="T">序列化类型</typeparam>
        /// <param name="key">Key</param>
        /// <returns></returns>
        public List<T> ListGetAll<T>(string key)
        {
            var existsKey = ExistsKey(key);
            if (!existsKey) return new List<T>();
            return this.redisClient.ListGetAll<T>(key);
        }

        /// <summary>
        /// 递减，原子操作，乘以100后按整数操作
        /// </summary>
        /// <param name="key">Key</param>
        /// <param name="value">变化量</param>
        /// <returns></returns>
        public double Decrement(string key, double value)
        {
            return this.redisClient.GetFullRedis().Decrement(key, value);
        }

        /// <summary>
        /// 递增，原子操作，乘以100后按整数操作
        /// </summary>
        /// <param name="key">Key</param>
        /// <param name="value">变化量</param>
        /// <returns></returns>
        public double Increment(string key, double value)
        {
            return this.redisClient.GetFullRedis().Increment(key, value);
        }

        /// <summary>
        /// 缓存 Key 是否存在
        /// </summary>
        /// <param name="key">Key</param>
        /// <returns></returns>
        public bool ExistsKey(string key)
        {
            return this.redisClient.ContainsKey(key);
        }

        /// <summary>
        /// 设置缓存Key的过期时间
        /// </summary>
        /// <param name="key">Key</param>
        /// <param name="expire">过期时间</param>
        /// <returns></returns>
        public bool SetExpires(string key, TimeSpan expire)
        {
            return this.redisClient.GetFullRedis().SetExpire(key, expire);
        }
    }
}
