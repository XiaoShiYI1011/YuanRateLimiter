/*
 * 接 口 名：ICacheService
 * 描述：
 * 创 建 者：十一
 * 创建时间：2023/12/17 15:59:17 
 */
namespace YuanRateLimiter.Cache
{
    internal interface ICacheService
    {
        /// <summary>
        /// 添加一条缓存数据，可设置过期时间
        /// </summary>
        /// <typeparam name="T">序列化类型</typeparam>
        /// <param name="key">Key</param>
        /// <param name="value">Value</param>
        /// <param name="expires">过期时间（单位：秒）</param>
        /// <returns></returns>
        bool Set<T>(string key, T value, long expires = -1);

        /// <summary>
        /// 添加一条数据到有序集合
        /// </summary>
        /// <typeparam name="T">序列化类型</typeparam>
        /// <param name="key">Key</param>
        /// <param name="member">元素</param>
        /// <param name="score">分数</param>
        /// <returns>添加行数</returns>
        bool AddSortSet<T>(string key, T member, double score);

        /// <summary>
        /// 根据 Key 删除缓存数据
        /// </summary>
        /// <param name="key">Key</param>
        void DelKey(string key);

        /// <summary>
        /// 获取缓存数据
        /// </summary>
        /// <typeparam name="T">序列化类型</typeparam>
        /// <param name="key">Key</param>
        /// <returns></returns>
        T Get<T>(string key);

        /// <summary>
        /// 根据有序集合的 key 和元素，获取有序集合的分数
        /// </summary>
        /// <typeparam name="T">序列化类型</typeparam>
        /// <param name="key">Key</param>
        /// <param name="member">元素</param>
        /// <returns></returns>
        double GetSortSet<T>(string key, T member);

        /// <summary>
        /// 递减，原子操作，乘以100后按整数操作
        /// </summary>
        /// <param name="key">Key</param>
        /// <param name="value">变化量</param>
        /// <returns></returns>
        double Decrement(string key, double value);

        /// <summary>
        /// 递增，原子操作，乘以100后按整数操作
        /// </summary>
        /// <param name="key">Key</param>
        /// <param name="value">变化量</param>
        /// <returns></returns>
        double Increment(string key, double value);
    }
}
