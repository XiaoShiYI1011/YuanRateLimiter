using SimpleRedis;

/*
 * 类名：RedisRepository
 * 描述：Redis 仓储类
 * 创 建 者：十一 
 * 创建时间：2023/11/20 22:24:58 
 */
namespace YuanRateLimiter.Repository
{
    /// <summary>
    /// Redis 仓储类
    /// </summary>
    public class RedisRepository
    {
        private readonly ISimpleRedis RedisClient;
        public ISimpleRedis Client => RedisClient;

        public RedisRepository(ISimpleRedis redisClient)
        {
            RedisClient = redisClient;
        }

        /// <summary>
        /// 添加一条缓存数据，可设置过期时间
        /// </summary>
        /// <typeparam name="T">序列化类型</typeparam>
        /// <param name="key">Key</param>
        /// <param name="value">Value</param>
        /// <param name="expires">过期时间（单位：秒）</param>
        /// <returns></returns>
        public bool Set<T>(string key, T value, long expires = -1)
        {
            if (expires == -1)
            {
                return RedisClient.Set<T>(key, value);
            }
            else
            {
                return RedisClient.Set<T>(key, value, TimeSpan.FromSeconds(expires));
            }
        }

        /// <summary>
        /// 添加一条数据到有序集合
        /// </summary>
        /// <typeparam name="T">序列化类型</typeparam>
        /// <param name="key">Key</param>
        /// <param name="member">元素</param>
        /// <param name="score">分数</param>
        /// <returns>添加行数</returns>
        public int AddSortSet<T>(string key, T member, double score)
        {
            return RedisClient.SortedSetAdd<T>(key, member, score);
        }

        /// <summary>
        /// 根据 Key 删除缓存数据
        /// </summary>
        /// <param name="key">Key</param>
        public void DelKey(string key)
        {
            RedisClient.Remove(key);
        }

        /// <summary>
        /// 获取缓存数据
        /// </summary>
        /// <typeparam name="T">序列化类型</typeparam>
        /// <param name="key">Key</param>
        /// <returns></returns>
        public T Get<T>(string key)
        {
            return RedisClient.Get<T>(key);
        }

        /// <summary>
        /// 根据有序集合的 key 和元素，获取有序集合的分数
        /// </summary>
        /// <typeparam name="T">序列化类型</typeparam>
        /// <param name="key">Key</param>
        /// <param name="member">元素</param>
        /// <returns></returns>
        public double GetSortSet<T>(string key, T member)
        {
            return RedisClient.GetFullRedis().GetSortedSet<T>(key).GetScore(member);
        }

        /// <summary>
        /// 递减，原子操作，乘以100后按整数操作
        /// </summary>
        /// <param name="key">Key</param>
        /// <param name="value">变化量</param>
        /// <returns></returns>
        public double Decrement(string key, double value)
        {
            return RedisClient.GetFullRedis().Decrement(key, value);
        }

        /// <summary>
        /// 递增，原子操作，乘以100后按整数操作
        /// </summary>
        /// <param name="key">Key</param>
        /// <param name="value">变化量</param>
        /// <returns></returns>
        public double Increment(string key, double value)
        {
            return RedisClient.GetFullRedis().Increment(key, value);
        }
    }
}
