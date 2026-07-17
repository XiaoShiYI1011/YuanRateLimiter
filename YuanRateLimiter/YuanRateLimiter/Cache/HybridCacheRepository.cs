using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using YuanRateLimiter.Config;
using YuanRateLimiter.Enum;

namespace YuanRateLimiter.Cache
{
    /// <summary>
    /// 复合缓存仓储类
    /// 创 建 者：十一 
    /// 创建时间：2025/12/5 15:12:11 
    /// </summary>
    internal class HybridCacheRepository : ICacheService, ICacheFallbackExecutor, IDisposable
    {
        private readonly RedisCacheRepository redisCache; // 主缓存
        private readonly MemoryCacheRepository memoryCache; // 降级缓存
        private readonly ILogger<HybridCacheRepository> logger;  // 日志
        private readonly RateLimiterConfig config;  // 限流配置
        private volatile bool redisAvailable = true; // 标记 Redis 当前是否可用
        private readonly TimeSpan normalInterval = TimeSpan.FromSeconds(1); // 正常检查间隔
        private TimeSpan currentInterval = TimeSpan.FromSeconds(1); // 当前间隔
        private readonly TimeSpan initialBackoffInterval = TimeSpan.FromSeconds(2); // 降级后初始重试间隔
        private readonly TimeSpan maxBackoffInterval = TimeSpan.FromSeconds(30); // 降级后最大重试间隔
        private int backoffAttempts = 0; // 降级后重试次数
        private readonly object redisAvailabilityLock = new object();  // 在多线程环境中同步访问和修改 redisAvailable 状态的锁对象
        private CancellationTokenSource cts = new CancellationTokenSource();  // 服务停止时取消后台 Redis 健康检查任务
        private bool disposed = false;

        public bool IsAvailable => memoryCache.IsAvailable;

        public CacheType CacheType => CacheType.Hybrid;

        public HybridCacheRepository(
            RedisCacheRepository redisCache,
            MemoryCacheRepository memoryCache,
            ILogger<HybridCacheRepository> logger,
            RateLimiterConfig config)
        {
            this.redisCache = redisCache;
            this.memoryCache = memoryCache;
            this.logger = logger;
            this.config = config;
            _ = Task.Run(() => RedisHealthCheckLoop(this.cts.Token));
        }

        /// <summary>
        /// 将一次完整操作交给选定的缓存后端执行
        /// </summary>
        /// <typeparam name="T">操作返回类型</typeparam>
        /// <param name="operation">需要完整执行的缓存操作</param>
        /// <param name="operationName">操作名称</param>
        /// <returns>操作结果</returns>
        T ICacheFallbackExecutor.ExecuteWithFallback<T>(Func<ICacheService, T> operation, string operationName) =>
            ExecuteWithFallback(operation, null, operationName);

        /// <summary>
        /// 检查Redis健康状态
        /// </summary>
        /// <returns></returns>
        private async Task RedisHealthCheckLoop(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                TimeSpan interval;
                bool useBackoff = false;
                if (redisAvailable)
                {
                    interval = this.normalInterval;
                }
                else
                {
                    // 退避算法（interval = Min(初始间隔 * 2^尝试次数, 最大间隔)）
                    double seconds = Math.Min(this.initialBackoffInterval.TotalSeconds * Math.Pow(2, this.backoffAttempts), this.maxBackoffInterval.TotalSeconds);
                    interval = TimeSpan.FromSeconds(seconds);
                    useBackoff = true;
                }
                try
                {
                    await Task.Delay(interval, cancellationToken);
                }
                catch (TaskCanceledException)
                {
                    break;
                }
                bool newRedisAvailable;
                try
                {
                    bool ok = this.redisCache.Set("HEALTH_CHECK", DateTime.UtcNow.Ticks, TimeSpan.FromSeconds(1));
                    newRedisAvailable = ok;
                }
                catch (Exception ex)
                {
                    newRedisAvailable = false;
                    if (useBackoff) this.logger.LogWarning($"{DateTime.Now:yyyy-MM-dd HH:mm:ss:fff}：Redis健康检查异常: {ex.Message}");
                }
                // 状态变更
                if (newRedisAvailable != redisAvailable) UpdateRedisAvailability(newRedisAvailable);
                // backoff 次数
                if (newRedisAvailable) this.backoffAttempts = 0;
                else this.backoffAttempts++;
            }
        }

        /// <summary>
        /// 更新Redis可用状态
        /// </summary>
        /// <param name="isAvailable">是否可用</param>
        private void UpdateRedisAvailability(bool isAvailable)
        {
            lock (this.redisAvailabilityLock)
            {
                if (this.redisAvailable != isAvailable)
                {
                    this.redisAvailable = isAvailable;
                    string status = isAvailable ? "恢复，重新使用 Redis 缓存" : "降级到本机内存缓存，不保证全局限流";
                    this.logger.LogInformation($"{DateTime.Now:yyyy-MM-dd HH:mm:ss:fff}：Redis状态变化: {status}");
                    this.currentInterval = isAvailable ? this.normalInterval : this.initialBackoffInterval;
                }
            }
        }

        /// <summary>
        /// 执行缓存操作，带降级机制
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="operation"></param>
        /// <param name="doubleWriteAction"></param>
        /// <param name="operationName"></param>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException"></exception>
        private T ExecuteWithFallback<T>(Func<ICacheService, T> operation, Action<ICacheService, ICacheService> doubleWriteAction = null, string operationName = "Operation")
        {
            bool useRedis = this.redisAvailable;
            if (useRedis)
            {
                try
                {
                    T result = operation(this.redisCache);
                    // 如果启用双写，同时写入内存缓存（异步）
                    if (this.config.EnableDoubleWrite && doubleWriteAction != null)
                    {
                        Task.Run(() =>
                        {
                            try
                            {
                                doubleWriteAction(this.redisCache, this.memoryCache);
                            }
                            catch (Exception ex)
                            {
                                this.logger.LogWarning($"双写内存缓存失败: {ex.Message}");
                            }
                        });
                    }
                    UpdateRedisAvailability(true);  // 冗余设计，限流场景下高可用
                    return result;
                }
                catch (Exception ex)
                {
                    // Redis操作失败，降级到内存
                    UpdateRedisAvailability(false);
                    if (this.config.EnableFallbackCache)
                    {
                        this.logger.LogWarning($"{DateTime.Now:yyyy-MM-dd HH:mm:ss:fff}：Redis操作失败({operationName})，已改用本机内存缓存，不保证全局限流: {ex.Message}");
                        return operation(this.memoryCache);
                    }
                    this.logger.LogWarning($"{DateTime.Now:yyyy-MM-dd HH:mm:ss:fff}：Redis操作失败({operationName})，且未启用降级缓存，将抛出异常: {ex.Message}");
                    throw new InvalidOperationException($"缓存操作失败且降级缓存已禁用: {operationName}", ex);
                }
            }
            else if (this.config.EnableFallbackCache)
            {
                // Redis不可用，直接使用内存缓存
                return operation(this.memoryCache);
            }
            else
            {
                throw new InvalidOperationException("Redis不可用且降级缓存已禁用");
            }
        }

        /// <summary>
        /// 执行操作，无返回值
        /// </summary>
        /// <param name="operation"></param>
        /// <param name="doubleWriteAction"></param>
        /// <param name="operationName"></param>
        private void ExecuteWithFallbackVoid(Action<ICacheService> operation, Action<ICacheService, ICacheService> doubleWriteAction = null, string operationName = "Operation")
        {
            ExecuteWithFallback<bool>(cache =>
            {
                operation(cache);
                return true;
            }, doubleWriteAction, operationName);
        }

        /// <summary>
        /// 添加一条缓存数据
        /// </summary>
        /// <typeparam name="T">序列化类型</typeparam>
        /// <param name="key">Key</param>
        /// <param name="value">Value</param>
        /// <returns></returns>
        public bool Set<T>(string key, T value) => ExecuteWithFallback(cache => cache.Set(key, value), (redisCache, memoryCache) => memoryCache.Set(key, value), $"Set:{key}");

        /// <summary>
        /// 添加一条缓存数据，可设置过期时间
        /// </summary>
        /// <typeparam name="T">序列化类型</typeparam>
        /// <param name="key">Key</param>
        /// <param name="value">Value</param>
        /// <param name="expires">过期时间</param>
        /// <returns></returns>
        public bool Set<T>(string key, T value, TimeSpan expire) => ExecuteWithFallback(cache => cache.Set(key, value, expire), (redisCache, memoryCache) => memoryCache.Set(key, value, expire), $"SetWithExpire:{key}");

        /// <summary>
        /// 添加一条数据到List
        /// </summary>
        /// <typeparam name="T">序列化类型</typeparam>
        /// <param name="key">Key</param>
        /// <param name="value">Value</param>
        public void ListAdd<T>(string key, T value) => ExecuteWithFallbackVoid(cache => cache.ListAdd(key, value), (redisCache, memoryCache) => memoryCache.ListAdd(key, value), $"ListAdd:{key}");

        /// <summary>
        /// List（头）左推
        /// </summary>
        /// <typeparam name="T">序列化类型</typeparam>
        /// <param name="key">Key</param>
        /// <param name="values">Value</param>
        /// <returns></returns>
        public int ListLeftPush<T>(string key, IEnumerable<T> values) =>
            ExecuteWithFallback(cache => cache.ListLeftPush(key, values), (redisCache, memoryCache) =>
            {
                memoryCache.ListLeftPush(key, values);
            }, $"ListLeftPush:{key}");

        /// <summary>
        /// List（尾）右推
        /// </summary>
        /// <typeparam name="T">序列化类型</typeparam>
        /// <param name="key">Key</param>
        /// <param name="values">Value</param>
        /// <returns></returns>
        public int ListRightPush<T>(string key, IEnumerable<T> values) =>
            ExecuteWithFallback(cache => cache.ListRightPush(key, values), (redisCache, memoryCache) =>
            {
                memoryCache.ListRightPush(key, values);
            }, $"ListRightPush:{key}");

        /// <summary>
        /// 根据 Key 删除缓存数据
        /// </summary>
        /// <param name="key">Key</param>
        public void DelKey(string key) => ExecuteWithFallbackVoid(cache => cache.DelKey(key), (redisCache, memoryCache) => memoryCache.DelKey(key), $"DelKey:{key}");

        /// <summary>
        /// List（头）左删，返回最左边一个元素
        /// </summary>
        /// <typeparam name="T">序列化类型</typeparam>
        /// <param name="key">Key</param>
        /// <returns></returns>
        public T ListLeftPop<T>(string key) => ExecuteWithFallback(cache => cache.ListLeftPop<T>(key), null, $"ListLeftPop:{key}");

        /// <summary>
        /// List（尾）右删，返回最右边一个元素
        /// </summary>
        /// <typeparam name="T">序列化类型</typeparam>
        /// <param name="key">Key</param>
        /// <returns></returns>
        public T ListRightPop<T>(string key) => ExecuteWithFallback(cache => cache.ListRightPop<T>(key), null, $"ListRightPop:{key}");

        /// <summary>
        /// 获取缓存数据
        /// </summary>
        /// <typeparam name="T">序列化类型</typeparam>
        /// <param name="key">Key</param>
        /// <returns></returns>
        public T Get<T>(string key)
        {
            // 先尝试从Redis读取，如果失败则从内存读取
            bool useRedis = this.redisAvailable;
            if (useRedis)
            {
                try
                {
                    T result = this.redisCache.Get<T>(key);
                    UpdateRedisAvailability(true);
                    return result;
                }
                catch (Exception ex)
                {
                    UpdateRedisAvailability(false);
                    if (this.config.EnableFallbackCache)
                    {
                        this.logger.LogWarning($"Redis读取失败({key})，已改用本机内存读取，不保证全局限流: {ex.Message}");
                        return this.memoryCache.Get<T>(key);
                    }
                    // 如果没有启用降级，但启用了双写，可能内存中有数据
                    if (this.config.EnableDoubleWrite)
                    {
                        this.logger.LogWarning($"Redis读取失败({key})，未启用降级缓存，但启用了双写，将尝试从本机内存读取: {ex.Message}");
                        try
                        {
                            return this.memoryCache.Get<T>(key);
                        }
                        catch
                        {
                            throw new InvalidOperationException($"无法从任何缓存中读取键: {key}");
                        }
                    }
                    this.logger.LogWarning($"Redis读取失败({key})，且未启用降级缓存，将抛出异常: {ex.Message}");
                    throw new InvalidOperationException($"Redis读取失败且降级缓存已禁用: {key}", ex);
                }
            }
            else if (this.config.EnableFallbackCache)
            {
                // Redis不可用，直接使用内存缓存
                return this.memoryCache.Get<T>(key);
            }
            else
            {
                throw new InvalidOperationException($"Redis不可用且降级缓存已禁用: {key}");
            }
        }

        /// <summary>
        /// 获取List
        /// </summary>
        /// <typeparam name="T">序列化类型</typeparam>
        /// <param name="key">Key</param>
        /// <returns></returns>
        public List<T> ListGetAll<T>(string key) => ExecuteWithFallback(cache => cache.ListGetAll<T>(key), null, $"ListGetAll:{key}");

        /// <summary>
        /// 递减，原子操作，乘以100后按整数操作
        /// </summary>
        /// <param name="key">Key</param>
        /// <param name="value">变化量</param>
        /// <returns></returns>
        public double Decrement(string key, double value) => ExecuteWithFallback(cache => cache.Decrement(key, value), (redisCache, memoryCache) => memoryCache.Decrement(key, value), $"Decrement:{key}");

        /// <summary>
        /// 递增，原子操作，乘以100后按整数操作
        /// </summary>
        /// <param name="key">Key</param>
        /// <param name="value">变化量</param>
        /// <returns></returns>
        public double Increment(string key, double value) => ExecuteWithFallback(cache => cache.Increment(key, value), (redisCache, memoryCache) => memoryCache.Increment(key, value), $"Increment:{key}");

        /// <summary>
        /// 缓存 Key 是否存在
        /// </summary>
        /// <param name="key">Key</param>
        /// <returns></returns>
        public bool ExistsKey(string key)
        {
            // 先尝试从Redis读取，如果失败则从内存读取
            bool useRedis = this.redisAvailable;
            if (useRedis)
            {
                try
                {
                    bool exists = this.redisCache.ExistsKey(key);
                    UpdateRedisAvailability(true);
                    // 如果启用双写，同时检查内存中是否存在
                    if (this.config.EnableDoubleWrite && !exists) exists = this.memoryCache.ExistsKey(key);
                    return exists;
                }
                catch (Exception ex)
                {
                    UpdateRedisAvailability(false);
                    if (this.config.EnableFallbackCache)
                    {
                        this.logger.LogWarning($"{DateTime.Now:yyyy-MM-dd HH:mm:ss:fff}：Redis检查存在失败({key})，已改用本机内存检查，不保证全局限流: {ex.Message}");
                        return this.memoryCache.ExistsKey(key);
                    }
                    this.logger.LogWarning($"{DateTime.Now:yyyy-MM-dd HH:mm:ss:fff}：Redis检查存在失败({key})，且未启用降级缓存，将抛出异常: {ex.Message}");
                    throw new InvalidOperationException($"Redis检查存在失败且降级缓存已禁用: {key}", ex);
                }
            }
            else if (this.config.EnableFallbackCache)
            {
                // Redis不可用，直接使用内存缓存
                return this.memoryCache.ExistsKey(key);
            }
            else
            {
                throw new InvalidOperationException($"Redis不可用且降级缓存已禁用: {key}");
            }
        }

        /// <summary>
        /// 设置缓存Key的过期时间
        /// </summary>
        /// <param name="key">Key</param>
        /// <param name="expire">过期时间</param>
        /// <returns></returns>
        public bool SetExpires(string key, TimeSpan expire) => ExecuteWithFallback(cache => cache.SetExpires(key, expire), (redisCache, memoryCache) => memoryCache.SetExpires(key, expire), $"SetExpires:{key}");

        /// <summary>
        /// 销毁
        /// </summary>
        public void Dispose()
        {
            if (disposed) return;
            if (this.cts != null)
            {
                try
                {
                    this.cts.Cancel();
                }
                catch (ObjectDisposedException)
                {
                    // 忽略已处置异常（防御性编程）
                }
                this.cts.Dispose();
                this.cts = null;
            }
            disposed = true;
        }
    }
}
