using System;
using System.Collections.Generic;
using System.Linq;
using NewLife.Caching;
using YuanRateLimiter.Enum;

namespace YuanRateLimiter.Cache
{
    /// <summary>
    /// Redis 仓储类
    /// 创 建 者：十一 
    /// 创建时间：2023/11/20 22:24:58 
    /// </summary>
    internal class RedisCacheRepository : ICacheService
    {
        private readonly FullRedis redisClient;
        private volatile bool isAvailable = true;

        public RedisCacheRepository(FullRedis redisClient)
        {
            this.redisClient = redisClient;
            TestConnection();
        }

        public bool IsAvailable => isAvailable;

        public CacheType CacheType => CacheType.Redis;

        /// <summary>
        /// 测试Redis连接
        /// </summary>
        private void TestConnection()
        {
            try
            {
                this.isAvailable = this.redisClient.Set("CONNECTION_TEST", DateTime.Now.Ticks, 1);
            }
            catch
            {
                this.isAvailable = false;
            }
        }

        /// <summary>
        /// 添加一条缓存数据
        /// </summary>
        /// <typeparam name="T">序列化类型</typeparam>
        /// <param name="key">Key</param>
        /// <param name="value">Value</param>
        /// <returns></returns>
        public bool Set<T>(string key, T value) => this.redisClient.Set(key, value);

        /// <summary>
        /// 添加一条缓存数据，可设置过期时间
        /// </summary>
        /// <typeparam name="T">序列化类型</typeparam>
        /// <param name="key">Key</param>
        /// <param name="value">Value</param>
        /// <param name="expires">过期时间</param>
        /// <returns></returns>
        public bool Set<T>(string key, T value, TimeSpan expire) => this.redisClient.Set(key, value, expire);

        /// <summary>
        /// 添加一条数据到List
        /// </summary>
        /// <typeparam name="T">序列化类型</typeparam>
        /// <param name="key">Key</param>
        /// <param name="value">Value</param>
        public void ListAdd<T>(string key, T value) => this.redisClient.RPUSH(key, value);

        /// <summary>
        /// List（头）左推
        /// </summary>
        /// <typeparam name="T">序列化类型</typeparam>
        /// <param name="key">Key</param>
        /// <param name="values">Value</param>
        /// <returns></returns>
        public int ListLeftPush<T>(string key, IEnumerable<T> values) => this.redisClient.LPUSH(key, values);

        /// <summary>
        /// List（尾）右推
        /// </summary>
        /// <typeparam name="T">序列化类型</typeparam>
        /// <param name="key">Key</param>
        /// <param name="values">Value</param>
        /// <returns></returns>
        public int ListRightPush<T>(string key, IEnumerable<T> values) => this.redisClient.RPUSH(key, values);

        /// <summary>
        /// 根据 Key 删除缓存数据
        /// </summary>
        /// <param name="key">Key</param>
        public void DelKey(string key) => redisClient.Remove(key);

        /// <summary>
        /// List（头）左删，返回最左边一个元素
        /// </summary>
        /// <typeparam name="T">序列化类型</typeparam>
        /// <param name="key">Key</param>
        /// <returns></returns>
        public T ListLeftPop<T>(string key) => this.redisClient.LPOP<T>(key);

        /// <summary>
        /// List（尾）右删，返回最右边一个元素
        /// </summary>
        /// <typeparam name="T">序列化类型</typeparam>
        /// <param name="key">Key</param>
        /// <returns></returns>
        public T ListRightPop<T>(string key) => this.redisClient.RPOP<T>(key);

        /// <summary>
        /// 获取缓存数据
        /// </summary>
        /// <typeparam name="T">序列化类型</typeparam>
        /// <param name="key">Key</param>
        /// <returns></returns>
        public T Get<T>(string key) => this.redisClient.Get<T>(key);

        /// <summary>
        /// 获取List
        /// </summary>
        /// <typeparam name="T">序列化类型</typeparam>
        /// <param name="key">Key</param>
        /// <returns></returns>
        public List<T> ListGetAll<T>(string key)
        {
            IList<T> data = this.redisClient.GetList<T>(key);
            return data?.ToList() ?? new List<T>();
        }

        /// <summary>
        /// 递减，原子操作，乘以100后按整数操作
        /// </summary>
        /// <param name="key">Key</param>
        /// <param name="value">变化量</param>
        /// <returns></returns>
        public double Decrement(string key, double value) => this.redisClient.Decrement(key, value);

        /// <summary>
        /// 递增，原子操作，乘以100后按整数操作
        /// </summary>
        /// <param name="key">Key</param>
        /// <param name="value">变化量</param>
        /// <returns></returns>
        public double Increment(string key, double value) => this.redisClient.Increment(key, value);

        /// <summary>
        /// 缓存 Key 是否存在
        /// </summary>
        /// <param name="key">Key</param>
        /// <returns></returns>
        public bool ExistsKey(string key) => this.redisClient.ContainsKey(key);

        /// <summary>
        /// 设置缓存Key的过期时间
        /// </summary>
        /// <param name="key">Key</param>
        /// <param name="expire">过期时间</param>
        /// <returns></returns>
        public bool SetExpires(string key, TimeSpan expire) => this.redisClient.SetExpire(key, expire);
    }
}
