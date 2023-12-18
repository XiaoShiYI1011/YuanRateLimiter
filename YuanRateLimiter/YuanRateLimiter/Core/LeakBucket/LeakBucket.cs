using Microsoft.AspNetCore.Http;
using YuanRateLimiter.Core.Interface;

/*
 * 类名：LeakBucket
 * 描述：漏桶算法
 * 创 建 者：十一 
 * 创建时间：2023/12/18 0:08:44 
 */
namespace YuanRateLimiter.Core.LeakBucket
{
    internal class LeakBucket : IRateLimiter
    {
        public async Task<bool> CheckRateLimit(HttpContext context)
        {
            throw new NotImplementedException();
        }
    }
}
