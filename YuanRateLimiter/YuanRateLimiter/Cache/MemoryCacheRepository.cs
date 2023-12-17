using NewLife.Caching;

/*
 * 类名：MemoryCacheRepository
 * 描述：MemoryCache 仓储类
 * 创 建 者：十一 
 * 创建时间：2023/12/17 16:04:31 
 */
namespace YuanRateLimiter.Cache
{
    internal class MemoryCacheRepository : ICacheService
    {
        public readonly MemoryCache memoryCache;

        public MemoryCacheRepository(MemoryCache memoryCache)
        {
            this.memoryCache = memoryCache;
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
                return memoryCache.Set<T>(key, value);
            }
            else
            {
                return memoryCache.Set<T>(key, value, TimeSpan.FromSeconds(expires));
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
        public bool AddSortSet<T>(string key, T member, double score)
        {
            var data = memoryCache.Get<SortedList<T, double>>(key);
            if (data == null) 
            {
                var sortedList = new SortedList<T, double>
                {
                    { member, score }
                };
                memoryCache.Set(key, sortedList);
                return sortedList.Count > 0;
            }
            else
            {
                data.Add(member, score);
                memoryCache.Set(key, data);
                return data.Count > 0;
            }
        }

        /// <summary>
        /// 根据 Key 删除缓存数据
        /// </summary>
        /// <param name="key">Key</param>
        public void DelKey(string key)
        {
            memoryCache.Remove(key);
        }

        /// <summary>
        /// 获取缓存数据
        /// </summary>
        /// <typeparam name="T">序列化类型</typeparam>
        /// <param name="key">Key</param>
        /// <returns></returns>
        public T Get<T>(string key)
        {
            return memoryCache.Get<T>(key);
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
            var data = memoryCache.Get<SortedList<T, double>>(key);
            if (data != null && data.ContainsKey(member))
            {
                return data[member];
            }
            else
            {
                return 0;
            }
        }

        /// <summary>
        /// 递减，原子操作，乘以100后按整数操作
        /// </summary>
        /// <param name="key">Key</param>
        /// <param name="value">变化量</param>
        /// <returns></returns>
        public double Decrement(string key, double value)
        {
            return memoryCache.Decrement(key, value);
        }

        /// <summary>
        /// 递增，原子操作，乘以100后按整数操作
        /// </summary>
        /// <param name="key">Key</param>
        /// <param name="value">变化量</param>
        /// <returns></returns>
        public double Increment(string key, double value)
        {
            return memoryCache.Increment(key, value);
        }
    }
}
