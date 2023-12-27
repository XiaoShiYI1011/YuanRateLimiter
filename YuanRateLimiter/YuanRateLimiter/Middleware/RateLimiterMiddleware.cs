using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading.Tasks;
using YuanRateLimiter.Config;
using YuanRateLimiter.Core.Interface;
using YuanRateLimiter.Utils;

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
            string requestIp = IPUtil.GetClientIPv4(context);
            var isIpWhiteList = config.IpWhiteList != null && config.IpWhiteList.Where(i => i.Contains(requestIp)).Any();  // 白名单
            if (isIpWhiteList)
            {
                await this.next(context);
                return;
            }
            var isIpBlackList = config.IpWhiteList != null && config.IpBlackList.Where(i => i.Contains(requestIp)).Any();  // 黑名单
            if (isIpBlackList)
            {
                context.Response.StatusCode = 403;
                context.Response.ContentType = "text/plain;charset=utf-8";
                await context.Response.WriteAsync("当前Ip被禁止访问");
                return;
            }
            if (!await rateLimiter.CheckRateLimit(context))
            {
                context.Response.StatusCode = config.HttpStatusCode;
                context.Response.ContentType = "text/plain;charset=utf-8";
                await context.Response.WriteAsync(config.LimitingMessage);
                logger.LogWarning($"{DateTime.Now:yyyy-MM-dd HH:mm:ss:fff}：接口已限流 ==> {context.Request.Path.Value}\n请求IP ==> {IPUtil.GetClientIPv4(context)}");
                return;
            }
            await this.next(context);
        }
    }
}
