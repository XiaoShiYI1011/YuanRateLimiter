using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using NewLife.Caching;
using SimpleRedis;
using YuanRateLimiter.Cache;
using YuanRateLimiter.Config;
using YuanRateLimiter.Core;
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
        /// 注册限流中间件
        /// </summary>
        /// <param name="services"></param>
        /// <param name="redisConnSrt"></param>
        public static void AddRateLimiterSetUp(this IServiceCollection services, string redisConnSrt = null)
        {
            services.AddSingleton<TokenBucket>();
            if (redisConnSrt == null) 
            {
                services.AddSingleton<MemoryCache>();
                services.AddSingleton<ICacheService, MemoryCacheRepository>(); 
            }
            else 
            {
                services.AddSimpleRedis(redisConnSrt);
                services.AddSingleton<ICacheService, RedisCacheRepository>(); 
            }
        }

        /// <summary>
        /// 注册限流中间件
        /// </summary>
        /// <param name="services"></param>
        /// <param name="redisConnSrt"></param>
        /// <param name="config"></param>
        public static void AddRateLimiterSetUp(this IServiceCollection services, Func<RateLimitingConfig, RateLimitingConfig> config, string redisConnSrt = null)
        {
            services.AddSingleton(config(new RateLimitingConfig()));
            services.AddSingleton<TokenBucket>();
            if (redisConnSrt == null)
            {
                services.AddSingleton<MemoryCache>();
                services.AddSingleton<ICacheService, MemoryCacheRepository>();
            }
            else
            {
                services.AddSimpleRedis(redisConnSrt);
                services.AddSingleton<ICacheService, RedisCacheRepository>();
            }
        }

        /// <summary>
        /// 使用限流中间件
        /// </summary>
        /// <param name="builder"></param>
        /// <returns></returns>
        public static IApplicationBuilder UseRateLimitMiddleware(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<RateLimitingMiddleware>();
        }
    }
}
