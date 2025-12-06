using System;
using System.Threading;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NewLife.Caching;
using YuanRateLimiter.Cache;
using YuanRateLimiter.Config;
using YuanRateLimiter.Core.Interface;
using YuanRateLimiter.Core.LeakBucket;
using YuanRateLimiter.Core.SlidingWindow;
using YuanRateLimiter.Core.TokenBucket;
using YuanRateLimiter.Enum;
using YuanRateLimiter.Middleware;

namespace YuanRateLimiter
{
    /// <summary>
    /// 限流中间件启动服务
    /// 创 建 者：十一 
    /// 创建时间：2023/11/18 22:55:41
    /// </summary>
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
        public static void AddRateLimiterSetUp(this IServiceCollection services, Func<RateLimiterConfig, RateLimiterConfig> config, string redisConnSrt = null)
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
        private static void RegisterRateLimiterServices(IServiceCollection services, string redisConnSrt, RateLimiterConfig rateLimitingConfig)
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
            RegisterCache(services, redisConnSrt, rateLimitingConfig);
        }

        /// <summary>
        /// 注册缓存服务
        /// </summary>
        /// <param name="services"></param>
        /// <param name="redisConnSrt"></param>
        private static void RegisterCache(IServiceCollection services, string redisConnSrt = null, RateLimiterConfig rateLimitingConfig = null)
        {
            var logger = services.BuildServiceProvider().GetRequiredService<ILoggerFactory>().CreateLogger("YuanRateLimiter");
            // 始终注册内存缓存服务
            services.AddSingleton<MemoryCache>();
            services.AddSingleton<MemoryCacheRepository>();
            services.AddSingleton<ICacheService>(provider => provider.GetRequiredService<MemoryCacheRepository>());
            if (string.IsNullOrEmpty(redisConnSrt))
            {
                logger.LogInformation($"未配置 Redis，默认使用内存缓存");
            }
            else
            {
                int maxRetries = rateLimitingConfig.RedisRetryCount;
                int retryCount = 0;
                bool isConnected = false;
                FullRedis redis = null;
                Exception lastException = null;
                while (retryCount < maxRetries && !isConnected)
                {
                    try
                    {
                        redis = new FullRedis();
                        redis.Init(redisConnSrt);
                        redis.Set("TEST", 1, 1);
                        redis.Remove("TEST");
                        isConnected = true;
                    }
                    catch (Exception ex)
                    {
                        retryCount++;
                        lastException = ex;
                        logger.LogWarning($"Redis连接异常，第{retryCount}次连接尝试失败，错误信息：{ex.Message}");
                        if (retryCount < maxRetries) Thread.Sleep(1000);
                    }
                }
                if (isConnected)
                {
                    // 注册 Redis 客户端
                    services.AddSingleton(redis);
                    // 注册 Redis 缓存服务
                    services.AddSingleton<RedisCacheRepository>();
                    logger.LogInformation("Redis连接成功");
                    // 如果启用降级缓存，使用混合缓存服务
                    if (rateLimitingConfig.EnableFallbackCache)
                    {
                        services.AddSingleton<HybridCacheRepository>();
                        services.AddSingleton<ICacheService>(provider => provider.GetRequiredService<HybridCacheRepository>());
                        logger.LogInformation("启用混合缓存服务（Redis + 内存降级）");
                    }
                    else
                    {
                        // 禁用降级，直接使用 Redis 缓存
                        services.AddSingleton<ICacheService>(provider => provider.GetRequiredService<RedisCacheRepository>());
                        logger.LogInformation("启用 Redis 缓存服务");
                    }
                }
                else
                {
                    logger.LogWarning($"限流中间件缓存服务警告，Redis 连接失败，经过{maxRetries}次重试后使用内存缓存。最后错误：{lastException?.Message}");
                }
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
