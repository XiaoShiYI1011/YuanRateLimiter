/*
 * 接 口 名：IRateLimiter
 * 描述：限流算法接口
 * 创 建 者：十一
 * 创建时间：2023/12/17 20:19:31 
 */
using Microsoft.AspNetCore.Http;

namespace YuanRateLimiter.Core.Interface
{
    public interface IRateLimiter
    {
        /// <summary>
        /// 检查限流
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        Task<bool> CheckRateLimit(HttpContext context);
    }
}
