using NewLife.Caching;
using System;
using System.Collections.Generic;

/*
 * 类名：MemoryCacheRepository
 * 描述：MemoryCache 仓储类
 * 创 建 者：十一 
 * 创建时间：2023/12/17 16:04:31 
 */
namespace YuanRateLimiter.Cache
{
    /// <summary>
    /// MemoryCache 仓储类
    /// </summary>
    internal class MemoryCacheRepository : ICacheService
    {
        private readonly MemoryCache memoryCache;

        public MemoryCacheRepository(MemoryCache memoryCache) => this.memoryCache = memoryCache;

        /// <summary>
        /// 添加一条缓存数据
        /// </summary>
        /// <typeparam name="T">序列化类型</typeparam>
        /// <param name="key">Key</param>
        /// <param name="value">Value</param>
        /// <returns></returns>
        public bool Set<T>(string key, T value) => this.memoryCache.Set(key, value);

        /// <summary>
        /// 添加一条缓存数据，可设置过期时间
        /// </summary>
        /// <typeparam name="T">序列化类型</typeparam>
        /// <param name="key">Key</param>
        /// <param name="value">Value</param>
        /// <param name="expires">过期时间</param>
        /// <returns></returns>
        public bool Set<T>(string key, T value, TimeSpan expire) => this.memoryCache.Set(key, value, expire);

        /// <summary>
        /// 添加一条数据到List
        /// </summary>
        /// <typeparam name="T">序列化类型</typeparam>
        /// <param name="key">Key</param>
        /// <param name="value">Value</param>
        public void ListAdd<T>(string key, T value)
        {
            var data = Get<List<T>>(key) ?? new List<T>();
            data.Add(value);
            this.memoryCache.Set(key, data);
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
            var data = Get<List<T>>(key) ?? new List<T>();
            int count = 0;
            foreach (var value in values)
            {
                count++;
                data.Insert(0, value);
            }
            Set(key, data);
            return count;
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
            var data = Get<List<T>>(key) ?? new List<T>();
            int count = 0;
            foreach (var value in values)
            {
                count++;
                data.Add(value);
            }
            Set(key, data);
            return count;
        }

        /// <summary>
        /// 根据 Key 删除缓存数据
        /// </summary>
        /// <param name="key">Key</param>
        public void DelKey(string key) => this.memoryCache.Remove(key);

        /// <summary>
        /// List（头）左删，返回最左边一个元素
        /// </summary>
        /// <typeparam name="T">序列化类型</typeparam>
        /// <param name="key">Key</param>
        /// <returns></returns>
        public T ListLeftPop<T>(string key)
        {
            var list = Get<List<T>>(key);
            if (list != null && list.Count > 0)
            {
                var first = list[0];
                list.RemoveAt(0);
                Set(key, list);
                return first;
            }
            return default;
        }

        /// <summary>
        /// List（尾）右删，返回最右边一个元素
        /// </summary>
        /// <typeparam name="T">序列化类型</typeparam>
        /// <param name="key">Key</param>
        /// <returns></returns>
        public T ListRightPop<T>(string key)
        {
            var list = Get<List<T>>(key);
            if (list != null && list.Count > 0)
            {
                var last = list[list.Count - 1];
                list.RemoveAt(list.Count - 1);
                Set(key, list);
                return last;
            }
            return default;
        }

        /// <summary>
        /// 获取缓存数据
        /// </summary>
        /// <typeparam name="T">序列化类型</typeparam>
        /// <param name="key">Key</param>
        /// <returns></returns>
        public T Get<T>(string key) => this.memoryCache.Get<T>(key);

        /// <summary>
        /// 获取List
        /// </summary>
        /// <typeparam name="T">序列化类型</typeparam>
        /// <param name="key">Key</param>
        /// <returns></returns>
        public List<T> ListGetAll<T>(string key)
        {
            var data = this.memoryCache.Get<List<T>>(key);
            if (data == null) return new List<T>();
            return data;
        }

        /// <summary>
        /// 递减，原子操作，乘以100后按整数操作
        /// </summary>
        /// <param name="key">Key</param>
        /// <param name="value">变化量</param>
        /// <returns></returns>
        public double Decrement(string key, double value) => this.memoryCache.Decrement(key, value);

        /// <summary>
        /// 递增，原子操作，乘以100后按整数操作
        /// </summary>
        /// <param name="key">Key</param>
        /// <param name="value">变化量</param>
        /// <returns></returns>
        public double Increment(string key, double value) => this.memoryCache.Increment(key, value);

        /// <summary>
        /// 缓存 Key 是否存在
        /// </summary>
        /// <param name="key">Key</param>
        /// <returns></returns>
        public bool ExistsKey(string key) => this.memoryCache.ContainsKey(key);

        /// <summary>
        /// 设置缓存Key的过期时间
        /// </summary>
        /// <param name="key">Key</param>
        /// <param name="expire">过期时间</param>
        /// <returns></returns>
        public bool SetExpires(string key, TimeSpan expire) => this.memoryCache.SetExpire(key, expire);
    }
}
