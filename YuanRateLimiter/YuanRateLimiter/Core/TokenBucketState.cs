/*
 * 类名：TokenBucketState
 * 描述：令牌桶实体
 * 创 建 者：十一 
 * 创建时间：2023/12/14 23:25:47 
 */
namespace YuanRateLimiter.Core
{
    public class TokenBucketState
    {
        /// <summary>
        /// 当前令牌数量
        /// </summary>
        public double CurrentTokens { get; set; }

        /// <summary>
        /// 上次填充时间
        /// </summary>
        public long LastRefillTimestamp { get; set; }
    }
}
