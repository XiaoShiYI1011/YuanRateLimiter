using Microsoft.AspNetCore.Http;
using System;
using System.Threading.Tasks;

namespace YuanRateLimiter.Core.Interface
{
    /// <summary>
    /// 限流算法接口
    /// 创 建 者：十一 
    /// 创建时间：2023/12/17 20:19:31 
    /// </summary>
    public interface IRateLimiter : IDisposable
    {
        /// <summary>
        /// 检查限流
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        Task<bool> CheckRateLimit(HttpContext context);
    }
}
