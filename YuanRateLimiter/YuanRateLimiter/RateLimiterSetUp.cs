using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using NewLife.Caching;
using System;
using YuanRateLimiter.Cache;
using YuanRateLimiter.Config;
using YuanRateLimiter.Core.Interface;
using YuanRateLimiter.Core.LeakBucket;
using YuanRateLimiter.Core.SlidingWindow;
using YuanRateLimiter.Core.TokenBucket;
using YuanRateLimiter.Enum;
using YuanRateLimiter.Middleware;

/*
 * 类名：RateLimiterSetUp
 * 描述：限流中间件扩展
 * 创 建 者：十一 
 * 创建时间：2023/11/18 22:55:41
 */
namespace YuanRateLimiter
{
    public static class RateLimiterSetUp
    {
        /// <summary>
        /// 注册限流服务
        /// </summary>
        /// <param name="services"></param>
        /// <param name="redisConnSrt"></param>
        public static void AddRateLimiterSetUp(this IServiceCollection services, string redisConnSrt = null)
        {
            var serviceProvider = services.BuildServiceProvider();
            RateLimiterConfig rateLimitingConfig = serviceProvider.GetRequiredService<RateLimiterConfig>();
            RegisterRateLimiterServices(services, redisConnSrt, rateLimitingConfig);
        }

        /// <summary>
        /// 注册限流服务
        /// </summary>
        /// <param name="services"></param>
        /// <param name="redisConnSrt"></param>
        /// <param name="config"></param>
        public static void AddRateLimiterSetUp(
            this IServiceCollection services,
            Func<RateLimiterConfig, RateLimiterConfig> config,
            string redisConnSrt = null)
        {
            services.AddSingleton(config(new RateLimiterConfig()));
            var serviceProvider = services.BuildServiceProvider();
            RateLimiterConfig rateLimitingConfig = serviceProvider.GetRequiredService<RateLimiterConfig>();
            RegisterRateLimiterServices(services, redisConnSrt, rateLimitingConfig);
        }

        /// <summary>
        /// 选择注入的服务
        /// </summary>
        /// <param name="services"></param>
        /// <param name="redisConnSrt"></param>
        /// <param name="rateLimitingConfig"></param>
        private static void RegisterRateLimiterServices(
            IServiceCollection services,
            string redisConnSrt,
            RateLimiterConfig rateLimitingConfig)
        {
            switch (rateLimitingConfig.RateLimiterModel)
            {
                case RateLimiterModel.TokenBucket:  // 令牌桶限流
                    if (rateLimitingConfig.EnableIpLimiter) services.AddSingleton<IRateLimiter, IPTokenBucket>();
                    else services.AddSingleton<IRateLimiter, TokenBucket>();
                    break;
                case RateLimiterModel.LeakBucket: // 漏桶限流
                    if (rateLimitingConfig.EnableIpLimiter) services.AddSingleton<IRateLimiter, IPLeakBucket>();
                    else services.AddSingleton<IRateLimiter, LeakBucket>();
                    break;
                case RateLimiterModel.SlidingWindow:  // 滑动窗口限流
                    if (rateLimitingConfig.EnableIpLimiter) services.AddSingleton<IRateLimiter, IPSlidingWindow>();
                    else services.AddSingleton<IRateLimiter, SlidingWindow>();
                    break;
                default:  // 默认令牌桶限流
                    services.AddSingleton<IRateLimiter, TokenBucket>();
                    break;
            }
            if (redisConnSrt == null)
            {
                services.AddSingleton<MemoryCache>();
                services.AddSingleton<ICacheService, MemoryCacheRepository>();
            }
            else
            {
                services.AddSingleton(c =>
                {
                    FullRedis rds = new FullRedis();
                    rds.Init(redisConnSrt);
                    return rds;
                });
                services.AddSingleton<ICacheService, RedisCacheRepository>();
            }
        }

        /// <summary>
        /// 使用限流中间件
        /// </summary>
        /// <param name="builder"></param>
        /// <returns></returns>
        public static IApplicationBuilder UseRateLimitMiddleware(this IApplicationBuilder builder) => builder.UseMiddleware<RateLimiterMiddleware>();
    }
}
