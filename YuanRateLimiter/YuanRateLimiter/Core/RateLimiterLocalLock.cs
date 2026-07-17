using System;

namespace YuanRateLimiter.Core
{
    /// <summary>
    /// 限流算法本机条带锁
    /// 创 建 者：十一
    /// 创建时间：2026/7/18 01:03:45 
    /// </summary>
    internal static class RateLimiterLocalLock
    {
        private const int StripeCount = 256;
        private static readonly object[] locks = CreateLocks();

        /// <summary>
        /// 根据缓存 Key 获取进程内共享锁
        /// </summary>
        /// <param name="cacheKey">限流状态基础 Key</param>
        /// <returns>缓存 Key 对应的条带锁</returns>
        internal static object Get(string cacheKey)
        {
            if (cacheKey == null) throw new ArgumentNullException(nameof(cacheKey));
            unchecked
            {
                uint hash = 2166136261;
                for (int i = 0; i < cacheKey.Length; i++) hash = (hash ^ cacheKey[i]) * 16777619;
                return locks[(int)(hash % StripeCount)];
            }
        }

        /// <summary>
        /// 创建固定数量的条带锁
        /// </summary>
        /// <returns>条带锁数组</returns>
        private static object[] CreateLocks()
        {
            var result = new object[StripeCount];
            for (int i = 0; i < result.Length; i++) result[i] = new object();
            return result;
        }
    }
}
