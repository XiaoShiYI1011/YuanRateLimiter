using System;
using System.Threading;
using System.Collections.Generic;
using System.Linq;
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
            var logger = CreateSetupLogger(services);
            var messages = new List<string>();
            RateLimiterConfig rateLimitingConfig = null;
            try
            {
                // 兼容旧用法：用户可能先手动注册 RateLimiterConfig，再调用本方法
                var serviceProvider = services.BuildServiceProvider();
                rateLimitingConfig = serviceProvider.GetService<RateLimiterConfig>();
            }
            catch (Exception ex)
            {
                messages.Add($"读取 RateLimiterConfig 注册配置时发生异常，请检查 RateLimiterModel、RateLimiterRule.RateLimiterLogLevel 等配置项是否写错。原始错误：{ex.Message}。");
            }
            if (rateLimitingConfig == null) rateLimitingConfig = RateLimiterConfigValidator.CreateDefault(messages, "未找到 RateLimiterConfig 注册配置。");
            else rateLimitingConfig = RateLimiterConfigValidator.Normalize(rateLimitingConfig, messages);
            // 将修正后的配置重新注册，保证后续中间件拿到的一定是可运行配置
            services.AddSingleton(rateLimitingConfig);
            LogConfigMessages(logger, messages);
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
            var logger = CreateSetupLogger(services);
            var messages = new List<string>();
            RateLimiterConfig rateLimitingConfig = null;
            if (config == null)
            {
                rateLimitingConfig = RateLimiterConfigValidator.CreateDefault(messages, "RateLimiter 配置委托为空。");
            }
            else
            {
                try
                {
                    // 用户配置绑定失败时不能影响宿主启动，统一回退到安全默认配置
                    rateLimitingConfig = config(new RateLimiterConfig());
                }
                catch (Exception ex)
                {
                    rateLimitingConfig = RateLimiterConfigValidator.CreateDefault(messages, $"读取 RateLimiter 配置时发生异常，请检查 RateLimiterModel、RateLimiterRule.RateLimiterLogLevel 等配置项是否写错。原始错误：{ex.Message}。");
                }
            }

            rateLimitingConfig = RateLimiterConfigValidator.Normalize(rateLimitingConfig, messages);
            // 将修正后的配置注册到容器，后续算法和中间件统一读取这一份配置。
            services.AddSingleton(rateLimitingConfig);
            LogConfigMessages(logger, messages);
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
            // 公开入口已输出配置诊断；这里再兜底一次，防止未来内部调用传入空配置
            rateLimitingConfig = RateLimiterConfigValidator.Normalize(rateLimitingConfig, new List<string>());
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
            var logger = CreateSetupLogger(services);
            // 始终注册内存缓存服务
            services.AddSingleton<MemoryCache>();
            services.AddSingleton<MemoryCacheRepository>();
            services.AddSingleton<ICacheService>(provider => provider.GetRequiredService<MemoryCacheRepository>());
            if (string.IsNullOrEmpty(redisConnSrt))
            {
                LogInformation(logger, "未配置 Redis，默认使用内存缓存");
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
                        LogWarning(logger, $"Redis连接异常，第{retryCount}次连接尝试失败，错误信息：{ex.Message}");
                        if (retryCount < maxRetries) Thread.Sleep(rateLimitingConfig.RedisRetryDelayMs);
                    }
                }
                if (isConnected)
                {
                    // 注册 Redis 客户端
                    services.AddSingleton(redis);
                    // 注册 Redis 缓存服务
                    services.AddSingleton<RedisCacheRepository>();
                    LogInformation(logger, "Redis连接成功");
                    // 如果启用降级缓存，使用混合缓存服务
                    if (rateLimitingConfig.EnableFallbackCache)
                    {
                        services.AddSingleton<HybridCacheRepository>();
                        services.AddSingleton<ICacheService>(provider => provider.GetRequiredService<HybridCacheRepository>());
                        LogInformation(logger, "启用混合缓存服务（Redis + 内存降级）");
                    }
                    else
                    {
                        // 禁用降级，直接使用 Redis 缓存
                        services.AddSingleton<ICacheService>(provider => provider.GetRequiredService<RedisCacheRepository>());
                        LogInformation(logger, "启用 Redis 缓存服务");
                    }
                }
                else
                {
                    LogWarning(logger, $"限流中间件缓存服务警告，Redis 连接失败，经过{maxRetries}次重试后使用内存缓存。最后错误：{lastException?.Message}");
                }
            }
        }

        /// <summary>
        /// 创建启动阶段日志对象。
        /// 日志服务如果尚未注册或创建失败，不影响宿主启动
        /// </summary>
        /// <param name="services"></param>
        /// <returns></returns>
        private static ILogger CreateSetupLogger(IServiceCollection services)
        {
            try
            {
                return services.BuildServiceProvider().GetService<ILoggerFactory>()?.CreateLogger("YuanRateLimiter");
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 输出配置诊断信息
        /// </summary>
        /// <param name="logger"></param>
        /// <param name="messages"></param>
        private static void LogConfigMessages(ILogger logger, IEnumerable<string> messages)
        {
            foreach (var message in messages.Where(m => !string.IsNullOrWhiteSpace(m)))
            {
                LogWarning(logger, "限流中间件配置警告：" + message);
            }
        }

        /// <summary>
        /// 输出普通日志；日志服务不可用时使用控制台兜底
        /// </summary>
        /// <param name="logger"></param>
        /// <param name="message"></param>
        private static void LogInformation(ILogger logger, string message)
        {
            if (logger != null) logger.LogInformation(message);
            else Console.WriteLine("YuanRateLimiter 信息：" + message);
        }

        /// <summary>
        /// 输出警告日志；日志服务不可用时使用控制台兜底
        /// </summary>
        /// <param name="logger"></param>
        /// <param name="message"></param>
        private static void LogWarning(ILogger logger, string message)
        {
            if (logger != null) logger.LogWarning(message);
            else Console.WriteLine("YuanRateLimiter 警告：" + message);
        }

        /// <summary>
        /// 使用限流中间件
        /// </summary>
        /// <param name="builder"></param>
        /// <returns></returns>
        public static IApplicationBuilder UseRateLimitMiddleware(this IApplicationBuilder builder) => builder.UseMiddleware<RateLimiterMiddleware>();
    }
}
