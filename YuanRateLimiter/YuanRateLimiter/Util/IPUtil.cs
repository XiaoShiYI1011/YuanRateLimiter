using Microsoft.AspNetCore.Http;
using System.Net;
using System.Net.Sockets;

/*
 * 类名：IPUtil
 * 描述：IP 地址工具类
 * 创 建 者：十一 
 * 创建时间：2023/12/14 10:37:27 
 */
namespace YuanRateLimiter.Util
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
                {
                    ip = context.Request.Headers["X-Real-IP"].FirstOrDefault();
                }
                if (context.Request.Headers.ContainsKey("X-Forwarded-For"))
                {
                    ip = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
                }
                if (string.IsNullOrEmpty(ip))
                {
                    ip = context.Connection.RemoteIpAddress?.MapToIPv4()?.ToString();
                }
            }
            return ip;
            //string ip = string.Empty;
            //if (context.Connection.RemoteIpAddress != null)
            //{
            //    if (context.Request.Headers.ContainsKey("X-Real-IP"))
            //        ip = context.Request.Headers["X-Real-IP"].FirstOrDefault();
            //    if (context.Request.Headers.ContainsKey("X-Forwarded-For"))
            //    {
            //        var forwardedFor = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
            //        var forwardedIps = forwardedFor?.Split(',').Select(s => s.Trim()).ToList();
            //        ip = forwardedIps?.FirstOrDefault(s => !IsPrivateIPAddress(s)) ?? ip;
            //    }
            //    if (string.IsNullOrEmpty(ip))
            //        ip = context.Connection.RemoteIpAddress?.MapToIPv4()?.ToString();
            //}
            //return ip;
        }

        /// <summary>
        /// 检查是否是私有地址
        /// </summary>
        /// <param name="ipAddress"></param>
        /// <returns></returns>
        private static bool IsPrivateIPAddress(string ipAddress)
        {
            var ip = IPAddress.Parse(ipAddress);
            return ip.AddressFamily == AddressFamily.InterNetwork &&
                   (ip.GetAddressBytes()[0] == 10 ||
                    (ip.GetAddressBytes()[0] == 172 && ip.GetAddressBytes()[1] >= 16 && ip.GetAddressBytes()[1] <= 31) ||
                    (ip.GetAddressBytes()[0] == 192 && ip.GetAddressBytes()[1] == 168));
        }
    }
}
