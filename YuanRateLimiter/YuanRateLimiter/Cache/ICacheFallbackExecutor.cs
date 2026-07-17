using System;

namespace YuanRateLimiter.Cache
{
    /// <summary>
    /// 缓存整体降级执行能力
    /// 创 建 者：十一
    /// 创建时间：2026/7/18
    /// </summary>
    internal interface ICacheFallbackExecutor
    {
        /// <summary>
        /// 将一次完整操作交给选定的缓存后端执行
        /// </summary>
        /// <typeparam name="T">操作返回类型</typeparam>
        /// <param name="operation">需要完整执行的缓存操作</param>
        /// <param name="operationName">操作名称</param>
        /// <returns>操作结果</returns>
        T ExecuteWithFallback<T>(Func<ICacheService, T> operation, string operationName);
    }
}
