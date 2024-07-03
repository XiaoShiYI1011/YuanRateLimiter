using Microsoft.AspNetCore.Http;
using System.Linq;
using System.Net;

/*
 * 类名：IPUtil
 * 描述：IP 地址工具类
 * 创 建 者：十一 
 * 创建时间：2023/12/14 10:37:27 
 */
namespace YuanRateLimiter.Utils
{
    /// <summary>
    /// IP 地址工具类
    /// </summary>
    internal class IPUtil
    {
        /// <summary>
        /// 获取客户端IP地址
        /// </summary>
        /// <param name="context">HTTP请求的上下文</param>
        /// <returns>IPv4地址</returns>
        public static string GetClientIPv4(HttpContext context)
        {
            string ip = string.Empty;
            if (context.Connection.RemoteIpAddress != null)
            {
                if (context.Request.Headers.ContainsKey("X-Real-IP"))
                    ip = context.Request.Headers["X-Real-IP"].FirstOrDefault();
                if (context.Request.Headers.ContainsKey("X-Forwarded-For"))
                {
                    var forwardedFor = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
                    var forwardedIps = forwardedFor?.Split(',').Select(s => s.Trim()).ToList();
                    if (forwardedIps?.Count > 0)
                    {
                        foreach (var forwardedIp in forwardedIps)
                        {
                            if (!IPAddress.IsLoopback(IPAddress.Parse(forwardedIp)))
                            {
                                ip = forwardedIp;
                                break;
                            }
                        }
                    }
                }
                if (string.IsNullOrEmpty(ip))
                    ip = context.Connection.RemoteIpAddress?.MapToIPv4()?.ToString();
            }
            return ip;
        }
    }
}
