using Microsoft.AspNetCore.Http;
using YuanRateLimiter.Core.Interface;

/*
 * 类名：SlidingWindow
 * 描述：
 * 创 建 者：十一 
 * 创建时间：2023/12/18 18:56:39 
 */
namespace YuanRateLimiter.Core.SlidingWindow
{
    public class SlidingWindow : IRateLimiter
    {
        public async Task<bool> CheckRateLimit(HttpContext context)
        {
            throw new NotImplementedException();
        }

        public void Dispose()
        {
        }
    }
}
