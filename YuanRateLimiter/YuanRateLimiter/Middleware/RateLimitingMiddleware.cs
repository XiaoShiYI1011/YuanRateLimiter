using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using YuanRateLimiter.Config;
using YuanRateLimiter.Core;
using YuanRateLimiter.Util;

/*
 * 类名：RateLimitingMiddleware
 * 描述：限流中间件
 * 创 建 者：十一 
 * 创建时间：2023/12/15 21:45:07 
 */
namespace YuanRateLimiter.Middleware
{
    /// <summary>
    /// 限流中间件
    /// </summary>
    public class RateLimitingMiddleware
    {
        private readonly RequestDelegate next;
        private readonly TokenBucket tokenBucket;
        private readonly ILogger<RateLimitingMiddleware> logger;

        public RateLimitingMiddleware(
            RequestDelegate next,
            TokenBucket tokenBucket,
            ILogger<RateLimitingMiddleware> logger)
        {
            this.next = next;
            this.tokenBucket = tokenBucket;
            this.logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            // 是否开启限流
            if (!RateLimitingConfig.EnableRateLimiting)
            {
                await this.next(context);
                return;
            }
            int tokensPerSecond = 0;
            int capacity = 0;
            // 是否开启全接口限流
            if (RateLimitingConfig.IsAllApiRateLimiting)
            {
                tokensPerSecond = RateLimitingConfig.IsAllApiFlowLimitingRule.TokensPerSecond;
                capacity = RateLimitingConfig.IsAllApiFlowLimitingRule.Capacity;
            }
            // Method 级别限流
            if (RateLimitingConfig.RateLimitingLogLevel!.Equals("Method"))
            {
                var methodFlowLimitingRules = RateLimitingConfig.MethodFlowLimitingRules;
                var methods = methodFlowLimitingRules.Where(t => t.Method.Equals(context.Request.Method)).ToList();
                if (methods.Count <= 0)
                {
                    await this.next(context);
                    return;
                }
                tokensPerSecond = methods[0].TokensPerSecond;
                capacity = methods[0].Capacity;
            }
            // Api 级别限流
            if (RateLimitingConfig.RateLimitingLogLevel!.Equals("Api"))
            {
                var apiFlowLimitingRules = RateLimitingConfig.ApiFlowLimitingRules;
                var apis = apiFlowLimitingRules.Where(t => t.Path.Equals(context.Request.Path.Value)).ToList();
                if (apis.Count <= 0)
                {
                    await this.next(context);
                    return;
                }
                tokensPerSecond = apis[0].TokensPerSecond;
                capacity = apis[0].Capacity;
            }
            if (!await this.tokenBucket.ConsumeToken(tokensPerSecond, capacity))
            {
                context.Response.StatusCode = RateLimitingConfig.HttpStatusCode;
                // TODO:配置日志记录
                //     1.考虑到使用者可能会采用不同的日志框架，目前仅测试了微软自带的日志记录
                this.logger.LogWarning($"{DateTime.Now}：接口已限流 ==> {context.Request.Path.Value}\n请求IP ==> {IPUtil.GetClientIPv4(context)}");
                return;
            }
            await this.next(context);
        }
    }
}
