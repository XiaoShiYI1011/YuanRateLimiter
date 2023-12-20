using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using YuanRateLimiter.Config;
using YuanRateLimiter.Core.Interface;
using YuanRateLimiter.Util;

/*
 * 类名：RateLimiterMiddleware
 * 描述：限流中间件
 * 创 建 者：十一 
 * 创建时间：2023/12/15 21:45:07 
 */
namespace YuanRateLimiter.Middleware
{
    /// <summary>
    /// 限流中间件
    /// </summary>
    internal class RateLimiterMiddleware
    {
        private readonly RequestDelegate next;
        private readonly IRateLimiter rateLimiter;
        private readonly RateLimiterConfig config;
        private readonly ILogger<RateLimiterMiddleware> logger;

        public RateLimiterMiddleware(
            RequestDelegate next,
            IRateLimiter rateLimiter,
            RateLimiterConfig config,
            ILogger<RateLimiterMiddleware> logger)
        {
            this.next = next;
            this.rateLimiter = rateLimiter;
            this.config = config;
            this.logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            // 是否开启限流
            if (!config.EnableRateLimiter)
            {
                await this.next(context);
                return;
            }
            if (!await rateLimiter.CheckRateLimit(context))
            {
                context.Response.StatusCode = config.HttpStatusCode;
                context.Response.ContentType = "text/plain";
                await context.Response.WriteAsync(config.LimitingMessage);
                logger.LogWarning($"{DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss:fff")}：接口已限流 ==> {context.Request.Path.Value}\n请求IP ==> {IPUtil.GetClientIPv4(context)}");
                return;
            }
            await this.next(context);
        }
    }
}
