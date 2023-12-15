using YuanRateLimiter.Const;
using YuanRateLimiter.Repository;

/*
 * 类名：TokenBucket
 * 描述：令牌桶算法
 * 创 建 者：十一 
 * 创建时间：2023/12/15 21:50:56 
 */
namespace YuanRateLimiter.Core
{
    public class TokenBucket
    {
        private readonly RedisRepository redisRepository;
        private readonly SemaphoreSlim semaphore = new(1, 1);

        public TokenBucket(RedisRepository redisRepository)
        {
            this.redisRepository = redisRepository;
        }

        /// <summary>
        /// 消耗令牌
        /// </summary>
        /// <param name="tokensPerSecond">每秒产生的令牌数量</param>
        /// <param name="capacity">令牌桶容量</param>
        /// <returns></returns>
        public async Task<bool> ConsumeToken(int tokensPerSecond, int capacity)
        {
            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            long lastRefillTimestamp = await GetLastRefillTimestamp();
            long elapsedTime = now - lastRefillTimestamp;
            long newTokens = elapsedTime * tokensPerSecond;
            await RefillTokens(now, newTokens, capacity);
            double currentTokens = await GetCurrentTokens(capacity);
            if (currentTokens >= 1)
            {
                await DecrementToken();
                return true;
            }
            return false;
        }

        /// <summary>
        /// 减少令牌
        /// </summary>
        /// <returns></returns>
        private async Task DecrementToken()
        {
            await semaphore.WaitAsync();
            try
            {
                var data = this.redisRepository.Get<TokenBucketState>(CacheKey.TokenBucketStateKey);
                var tokenBucketState = new TokenBucketState
                {
                    CurrentTokens = Math.Max(0, data.CurrentTokens - 1),
                    LastRefillTimestamp = data.LastRefillTimestamp,
                };
                this.redisRepository.Set<TokenBucketState>(CacheKey.TokenBucketStateKey, tokenBucketState);
            }
            finally
            {
                semaphore.Release();
            }
        }

        /// <summary>
        /// 填充令牌
        /// </summary>
        /// <param name="now">当前时间戳</param>
        /// <param name="newTokens">新产生的令牌数量</param>
        /// <param name="capacity">令牌桶容量</param>
        /// <returns></returns>
        private async Task RefillTokens(long now, double newTokens, int capacity)
        {
            double currentTokens = await GetCurrentTokens(capacity);
            double updatedTokens = Math.Min(capacity, currentTokens + newTokens);
            this.redisRepository.Set<TokenBucketState>(CacheKey.TokenBucketStateKey, new TokenBucketState
            {
                CurrentTokens = updatedTokens,
                LastRefillTimestamp = now,
            });
        }

        /// <summary>
        /// 获取当前令牌数量
        /// </summary>
        /// <returns></returns>
        private async Task<double> GetCurrentTokens(int capacity)
        {
            var data = this.redisRepository.Get<TokenBucketState>(CacheKey.TokenBucketStateKey);
            return await Task.FromResult(data?.CurrentTokens ?? capacity);
        }

        /// <summary>
        /// 获取最后填充时间
        /// </summary>
        /// <returns></returns>
        private async Task<long> GetLastRefillTimestamp()
        {
            var data = this.redisRepository.Get<TokenBucketState>(CacheKey.TokenBucketStateKey);
            return await Task.FromResult(data?.LastRefillTimestamp ?? 0);
        }
    }
}
