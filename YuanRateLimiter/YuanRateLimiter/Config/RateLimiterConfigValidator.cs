using System;
using System.Collections.Generic;
using System.Linq;
using YuanRateLimiter.Enum;

namespace YuanRateLimiter.Config
{
    /// <summary>
    /// 限流配置校验器，确保错误配置不会导致宿主项目启动失败
    /// 创 建 者：十一 
    /// 创建时间：2026/6/13 16:35:08 
    /// </summary>
    internal class RateLimiterConfigValidator
    {
        private const int DefaultHttpStatusCode = 429;
        private const int DefaultCapacity = 100;
        private const int DefaultRateLimit = 20;
        private const int DefaultWindowSize = 10;
        private const int DefaultMaxRequests = 10;
        private const int DefaultRedisRetryCount = 3;
        private const int MaxRedisRetryCount = 10;
        private const int DefaultRedisRetryDelayMs = 1000;
        private const int MaxRedisRetryDelayMs = 30000;

        /// <summary>
        /// 修正用户配置中的非法值，并把所有修正原因写入中文诊断信息
        /// </summary>
        /// <param name="config">用户传入或配置文件绑定出的限流配置</param>
        /// <param name="messages">中文诊断信息集合</param>
        /// <returns></returns>
        public static RateLimiterConfig Normalize(RateLimiterConfig config, IList<string> messages)
        {
            if (messages == null) messages = new List<string>();
            if (config == null)
            {
                messages.Add("RateLimiter 配置为空，已使用默认配置；限流默认关闭");
                config = new RateLimiterConfig();
            }
            if (config.HttpStatusCode < 100 || config.HttpStatusCode > 599)
            {
                messages.Add($"RateLimiter.HttpStatusCode 配置值 {config.HttpStatusCode} 无效，已改为默认值 {DefaultHttpStatusCode}。");
                config.HttpStatusCode = DefaultHttpStatusCode;
            }
            else
            {
                config.CacheKey = config.CacheKey.Trim();
            }
            if (!System.Enum.IsDefined(typeof(RateLimiterModel), config.RateLimiterModel))
            {
                messages.Add($"RateLimiter.RateLimiterModel 配置值 {config.RateLimiterModel} 无效，已改为默认令牌桶算法 TokenBucket。");
                config.RateLimiterModel = RateLimiterModel.TokenBucket;
            }
            if (config.RedisRetryCount <= 0)
            {
                messages.Add($"RateLimiter.RedisRetryCount 配置值 {config.RedisRetryCount} 无效，已改为默认值 {DefaultRedisRetryCount}。");
                config.RedisRetryCount = DefaultRedisRetryCount;
            }
            else if (config.RedisRetryCount > MaxRedisRetryCount)
            {
                messages.Add($"RateLimiter.RedisRetryCount 配置值 {config.RedisRetryCount} 过大，为避免启动长时间阻塞，已限制为 {MaxRedisRetryCount}。");
                config.RedisRetryCount = MaxRedisRetryCount;
            }
            if (config.RedisRetryDelayMs <= 0)
            {
                messages.Add($"RateLimiter.RedisRetryDelayMs 配置值 {config.RedisRetryDelayMs} 无效，已改为默认值 {DefaultRedisRetryDelayMs}。");
                config.RedisRetryDelayMs = DefaultRedisRetryDelayMs;
            }
            else if (config.RedisRetryDelayMs > MaxRedisRetryDelayMs)
            {
                messages.Add($"RateLimiter.RedisRetryDelayMs 配置值 {config.RedisRetryDelayMs} 过大，为避免启动长时间阻塞，已限制为 {MaxRedisRetryDelayMs}。");
                config.RedisRetryDelayMs = MaxRedisRetryDelayMs;
            }
            config.IpWhiteList = NormalizeIpList(config.IpWhiteList, "IpWhiteList", messages);
            config.IpBlackList = NormalizeIpList(config.IpBlackList, "IpBlackList", messages);
            if (config.RateLimiterRule == null)
            {
                messages.Add("RateLimiter.RateLimiterRule 未配置，已创建默认 All 级别规则；如果开启限流，将按全接口规则生效。");
                config.RateLimiterRule = new RateLimiterRule();
            }
            if (!System.Enum.IsDefined(typeof(RateLimitingLevel), config.RateLimiterRule.RateLimiterLogLevel))
            {
                messages.Add($"RateLimiter.RateLimiterRule.RateLimiterLogLevel 配置值 {config.RateLimiterRule.RateLimiterLogLevel} 无效，已改为 All。");
                config.RateLimiterRule.RateLimiterLogLevel = RateLimitingLevel.All;
            }
            if (config.RateLimiterRule.MethodFlowLimiterRules != null)
            {
                var validRules = new List<MethodFlowLimiterRules>();
                for (int i = 0; i < config.RateLimiterRule.MethodFlowLimiterRules.Length; i++)
                {
                    var rule = config.RateLimiterRule.MethodFlowLimiterRules[i];
                    var path = $"RateLimiter.RateLimiterRule.MethodFlowLimiterRules[{i}]";
                    if (rule == null)
                    {
                        messages.Add($"{path} 为空，已忽略该规则。");
                        continue;
                    }
                    if (string.IsNullOrWhiteSpace(rule.Method))
                    {
                        messages.Add($"{path}.Method 为空，已忽略该规则。");
                        continue;
                    }
                    rule.Method = rule.Method.Trim().ToUpperInvariant();
                    NormalizeRule(rule, path, messages);
                    validRules.Add(rule);
                }
                config.RateLimiterRule.MethodFlowLimiterRules = validRules.ToArray();
            }
            if (config.RateLimiterRule.ActionFlowLimiterRules != null)
            {
                var validRules = new List<ActionFlowLimiterRules>();
                for (int i = 0; i < config.RateLimiterRule.ActionFlowLimiterRules.Length; i++)
                {
                    var rule = config.RateLimiterRule.ActionFlowLimiterRules[i];
                    var path = $"RateLimiter.RateLimiterRule.ActionFlowLimiterRules[{i}]";
                    if (rule == null)
                    {
                        messages.Add($"{path} 为空，已忽略该规则。");
                        continue;
                    }
                    if (string.IsNullOrWhiteSpace(rule.Path))
                    {
                        messages.Add($"{path}.Path 为空，已忽略该规则。");
                        continue;
                    }
                    rule.Path = rule.Path.Trim();
                    if (!rule.Path.StartsWith("/"))
                    {
                        messages.Add($"{path}.Path 配置值 {rule.Path} 未以 / 开头，已自动修正为 /{rule.Path}。");
                        rule.Path = "/" + rule.Path;
                    }
                    NormalizeRule(rule, path, messages);
                    validRules.Add(rule);
                }
                config.RateLimiterRule.ActionFlowLimiterRules = validRules.ToArray();
            }
            switch (config.RateLimiterRule.RateLimiterLogLevel)
            {
                case RateLimitingLevel.All:
                    EnsureAllFlowLimiterRule(config, messages);
                    break;
                case RateLimitingLevel.Method:
                    if (config.RateLimiterRule.MethodFlowLimiterRules == null || config.RateLimiterRule.MethodFlowLimiterRules.Length == 0)
                    {
                        messages.Add("RateLimiter.RateLimiterRule.RateLimiterLogLevel 配置为 Method，但没有可用的 MethodFlowLimiterRules；Method 级限流不会生效，也不会自动回退为 All。");
                    }
                    break;
                case RateLimitingLevel.Action:
                    if (config.RateLimiterRule.ActionFlowLimiterRules == null || config.RateLimiterRule.ActionFlowLimiterRules.Length == 0)
                    {
                        messages.Add("RateLimiter.RateLimiterRule.RateLimiterLogLevel 配置为 Action，但没有可用的 ActionFlowLimiterRules；Action 级限流不会生效，也不会自动回退为 All。");
                    }
                    break;
            }
            return config;
        }

        /// <summary>
        /// 创建安全默认配置。默认配置不会主动开启限流，避免错误配置影响宿主业务启动
        /// </summary>
        /// <param name="messages">诊断信息集合</param>
        /// <param name="reason">触发默认配置的原因</param>
        /// <returns></returns>
        public static RateLimiterConfig CreateDefault(IList<string> messages, string reason)
        {
            if (messages == null) messages = new List<string>();
            messages.Add(reason + " 已使用默认配置；限流默认关闭");
            return Normalize(new RateLimiterConfig(), messages);
        }

        /// <summary>
        /// 修正单条限流规则的数值参数，确保容量、速率、窗口和请求上限都大于 0
        /// </summary>
        /// <param name="rule">需要修正的限流规则</param>
        /// <param name="path">配置路径</param>
        /// <param name="messages">中文诊断信息集合</param>
        private static void NormalizeRule(BaseFlowLimiterRule rule, string path, IList<string> messages)
        {
            if (rule.Capacity <= 0)
            {
                messages.Add($"{path}.Capacity 配置值 {rule.Capacity} 无效，已改为默认值 {DefaultCapacity}。");
                rule.Capacity = DefaultCapacity;
            }
            if (rule.RateLimit <= 0)
            {
                messages.Add($"{path}.RateLimit 配置值 {rule.RateLimit} 无效，已改为默认值 {DefaultRateLimit}。");
                rule.RateLimit = DefaultRateLimit;
            }
            if (rule.WindowSize <= 0)
            {
                messages.Add($"{path}.WindowSize 配置值 {rule.WindowSize} 无效，已改为默认值 {DefaultWindowSize}。");
                rule.WindowSize = DefaultWindowSize;
            }
            if (rule.MaxRequests <= 0)
            {
                messages.Add($"{path}.MaxRequests 配置值 {rule.MaxRequests} 无效，已改为默认值 {DefaultMaxRequests}。");
                rule.MaxRequests = DefaultMaxRequests;
            }
        }

        /// <summary>
        /// 确保 All 级别限流拥有可用的全接口规则
        /// </summary>
        /// <param name="config">限流配置</param>
        /// <param name="messages">中文诊断信息集合</param>
        private static void EnsureAllFlowLimiterRule(RateLimiterConfig config, IList<string> messages)
        {
            if (config.RateLimiterRule.AllFlowLimiterRule == null)
            {
                messages.Add("RateLimiter.RateLimiterRule.RateLimiterLogLevel 配置为 All，但 AllFlowLimiterRule 未配置，已补充默认全接口限流规则。");
                config.RateLimiterRule.AllFlowLimiterRule = new AllFlowLimiterRule
                {
                    Capacity = DefaultCapacity,
                    RateLimit = DefaultRateLimit,
                    WindowSize = DefaultWindowSize,
                    MaxRequests = DefaultMaxRequests
                };
            }
            else
            {
                NormalizeRule(config.RateLimiterRule.AllFlowLimiterRule, "RateLimiter.RateLimiterRule.AllFlowLimiterRule", messages);
            }
        }

        /// <summary>
        /// 清洗 IP 列表，去掉空值、首尾空格和重复项
        /// </summary>
        /// <param name="ipList">用户配置的 IP 列表</param>
        /// <param name="name">配置项名称</param>
        /// <param name="messages">中文诊断信息集合</param>
        /// <returns></returns>
        private static string[] NormalizeIpList(string[] ipList, string name, IList<string> messages)
        {
            if (ipList == null) return null;
            var normalized = ipList
                .Where(ip => !string.IsNullOrWhiteSpace(ip))
                .Select(ip => ip.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (normalized.Length != ipList.Length) messages.Add($"RateLimiter.{name} 中存在空值或重复值，已自动清理。");
            return normalized;
        }
    }

}
