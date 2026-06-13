using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http;
using YuanRateLimiter.Config;
using YuanRateLimiter.Const;
using YuanRateLimiter.Enum;

namespace YuanRateLimiter.Core
{
    /// <summary>
    /// 匹配当前请求命中的限流规则，并生成稳定的规则Key
    /// 创 建 者：十一 
    /// 创建时间：2026/6/13 15:03:16 
    /// </summary>
    internal class RateLimiterRuleMatcher
    {
        private static readonly RegexOptions PathRegexOptions = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;
        private static readonly TimeSpan PathRegexTimeout = TimeSpan.FromMilliseconds(100);
        private static readonly ConcurrentDictionary<string, Regex> PathRegexCache = new ConcurrentDictionary<string, Regex>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// 根据当前限流级别匹配请求规则；Action 级别支持精确路径和通配符路径，精确路径优先
        /// </summary>
        /// <param name="config">限流配置</param>
        /// <param name="context">当前请求上下文</param>
        /// <param name="rule">命中的限流规则</param>
        /// <param name="ruleKey">用于生成缓存 Key 的稳定规则标识</param>
        /// <returns>命中可用规则时返回 true，否则返回 false</returns>
        public static bool TryMatch(RateLimiterConfig config, HttpContext context, out BaseFlowLimiterRule rule, out string ruleKey)
        {
            rule = null;
            ruleKey = null;
            if (config?.RateLimiterRule == null || context == null) return false;
            switch (config.RateLimiterRule.RateLimiterLogLevel)
            {
                case RateLimitingLevel.All:
                    rule = config.RateLimiterRule.AllFlowLimiterRule;
                    ruleKey = "all";
                    return rule != null;
                case RateLimitingLevel.Method:
                    var method = context.Request.Method;
                    var methodRule = config.RateLimiterRule.MethodFlowLimiterRules?.FirstOrDefault(t => !string.IsNullOrWhiteSpace(t.Method) && string.Equals(t.Method.Trim(), method, StringComparison.OrdinalIgnoreCase));
                    if (methodRule == null) return false;
                    rule = methodRule;
                    ruleKey = "method:" + methodRule.Method.Trim().ToUpperInvariant();
                    return true;
                case RateLimitingLevel.Action:
                    var path = NormalizePath(context.Request.Path.Value);
                    var actionRule = MatchActionRule(config.RateLimiterRule.ActionFlowLimiterRules, path);
                    if (actionRule == null) return false;
                    rule = actionRule;
                    ruleKey = "action:" + actionRule.Path.Trim();
                    return true;
                default:
                    rule = config.RateLimiterRule.AllFlowLimiterRule;
                    ruleKey = "all";
                    return rule != null;
            }
        }

        /// <summary>
        /// 匹配 Action 级路径规则。先查精确路径，再按配置顺序查通配符，避免宽泛通配符覆盖明确配置
        /// </summary>
        /// <param name="rules">Action 级限流规则数组</param>
        /// <param name="requestPath">当前请求路径</param>
        /// <returns>命中的 Action 规则，未命中时返回 null</returns>
        private static ActionFlowLimiterRules MatchActionRule(ActionFlowLimiterRules[] rules, string requestPath)
        {
            if (rules == null || rules.Length == 0) return null;
            foreach (var rule in rules)
            {
                if (string.IsNullOrWhiteSpace(rule?.Path)) continue;
                var rulePath = NormalizePath(rule.Path);
                if (string.Equals(rulePath, requestPath, StringComparison.OrdinalIgnoreCase)) return rule;
            }
            foreach (var rule in rules)
            {
                if (string.IsNullOrWhiteSpace(rule?.Path)) continue;
                var rulePath = NormalizePath(rule.Path);
                if (IsWildcardPath(rulePath) && IsPathMatch(rulePath, requestPath)) return rule;
            }
            return null;
        }

        /// <summary>
        /// 标准化路径文本，避免空路径或首尾空格导致匹配失败
        /// </summary>
        /// <param name="path">请求路径或规则路径</param>
        /// <returns>标准化后的路径</returns>
        private static string NormalizePath(string path)
        {
            path = string.IsNullOrWhiteSpace(path) ? "/" : path.Trim();
            return path;
        }

        /// <summary>
        /// 判断路径规则是否包含通配符。支持 *、**、? 和 {param} 四种写法
        /// </summary>
        /// <param name="path">规则路径</param>
        /// <returns>包含通配符时返回 true</returns>
        private static bool IsWildcardPath(string path)
        {
            if (string.IsNullOrEmpty(path)) return false;
            return path.IndexOf('*') >= 0
                   || path.IndexOf('?') >= 0
                   || (path.IndexOf('{') >= 0 && path.IndexOf('}') > path.IndexOf('{'));
        }

        /// <summary>
        /// 使用缓存后的正则表达式匹配通配符路径；异常时返回 false，避免错误配置影响宿主启动或请求处理
        /// </summary>
        /// <param name="pattern">通配符路径规则</param>
        /// <param name="requestPath">当前请求路径</param>
        /// <returns>路径命中通配符规则时返回 true</returns>
        private static bool IsPathMatch(string pattern, string requestPath)
        {
            try
            {
                var regex = PathRegexCache.GetOrAdd(pattern, p => new Regex(BuildPathRegexPattern(p), PathRegexOptions, PathRegexTimeout));
                return regex.IsMatch(requestPath);
            }
            catch (ArgumentException)
            {
                return false;
            }
            catch (RegexMatchTimeoutException)
            {
                return false;
            }
        }

        /// <summary>
        /// 把路径通配符转换为受控正则：* 匹配单段，** 匹配多段，? 匹配单字符，{param} 匹配一个路径段
        /// </summary>
        /// <param name="pattern">通配符路径规则</param>
        /// <returns>可用于匹配请求路径的正则表达式</returns>
        private static string BuildPathRegexPattern(string pattern)
        {
            var builder = new StringBuilder();
            builder.Append("^");
            if (pattern.EndsWith("/**", StringComparison.Ordinal))
            {
                AppendPathRegexBody(builder, pattern.Substring(0, pattern.Length - 3));
                builder.Append("(?:/.*)?");
            }
            else
            {
                AppendPathRegexBody(builder, pattern);
            }
            builder.Append("$");
            return builder.ToString();
        }

        /// <summary>
        /// 逐字符拼接路径正则主体，所有普通字符都转义，避免用户配置中的特殊字符变成正则语义
        /// </summary>
        /// <param name="builder">正则表达式构造器</param>
        /// <param name="pattern">通配符路径规则</param>
        private static void AppendPathRegexBody(StringBuilder builder, string pattern)
        {
            for (int i = 0; i < pattern.Length; i++)
            {
                var current = pattern[i];
                if (current == '*')
                {
                    if (i + 1 < pattern.Length && pattern[i + 1] == '*')
                    {
                        builder.Append(".*");
                        i++;
                    }
                    else
                    {
                        builder.Append("[^/]+");
                    }
                    continue;
                }
                if (current == '?')
                {
                    builder.Append("[^/]");
                    continue;
                }
                if (current == '{')
                {
                    var end = pattern.IndexOf('}', i + 1);
                    if (end > i + 1)
                    {
                        builder.Append("[^/]+");
                        i = end;
                        continue;
                    }
                }
                builder.Append(Regex.Escape(current.ToString()));
            }
        }

        /// <summary>
        /// 获取缓存Key
        /// </summary>
        /// <param name="config"></param>
        /// <param name="ruleKey"></param>
        /// <returns></returns>
        public static string GetCacheKey(RateLimiterConfig config, string ruleKey)
        {
            var prefix = string.IsNullOrEmpty(config?.CacheKey) ? CacheKey.RateLimiterCacheKey : config.CacheKey;
            return prefix + ":" + ruleKey;
        }

        /// <summary>
        /// 获取Ip缓存Key
        /// </summary>
        /// <param name="config"></param>
        /// <param name="ipAddress"></param>
        /// <param name="ruleKey"></param>
        /// <returns></returns>
        public static string GetIpCacheKey(RateLimiterConfig config, string ipAddress, string ruleKey)
        {
            return GetCacheKey(config, "ip:" + ipAddress + ":" + ruleKey);
        }
    }

}
