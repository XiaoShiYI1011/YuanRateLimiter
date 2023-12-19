<div align="center"><img  src="https://gitee.com/XiaoShiYi-1011/yuan-rate-limiter/raw/master/Doc/images/logo.jpg" width="120" height="120" style="margin-bottom: 10px;"/></div>
<div align="center"><strong><span style="font-size: x-large;">YuanRateLimiter</span></strong></div>
<div align="center"><h4 align="center">不断更新迭代中...</h4></div>
<div align="center"><p stylt="text-align: center;">我觉得此项目开源的初衷在于，支持.Net开源生态的发展。让我康康谁还说.Net没生态的😎</p></div>

### ✨如果您觉得有帮助，请点右上角 "Star" 支持一下谢谢

## 🎇框架介绍

YuanRateLimiter是一个Asp.Net Core的限流中间件。如果你项目不想采用国外的限流组件那就可以参考此项目或者直接使用，配置灵活：通过appsettings.json文件配置，支持全接口限流、Method限流、API接口限流。默认采用基于Redis的令牌桶算法，支持Redis和MemoryCache的无缝切换。正在集成其他限流算法。采用基于[NewLife.Redis](https://github.com/NewLifeX/NewLife.Redis)二次封装的[SimpleRedis](https://gitee.com/zxzyjs/SimpleRedis.git)。核心代码注释覆盖率>90%。值得注意的是NET 7/8自带了完善的限流中间件(很烦，开源开得有点晚了...慢了一步)，友情链接：[ASP.NET Core 中的速率限制中间件 | Microsoft Learn](https://learn.microsoft.com/zh-cn/aspnet/core/performance/rate-limit?view=aspnetcore-8.0)。如果你是NET 7/8及以上开发的项目，请使用NET 7/8自带的限流中间件。温馨提示：该项目暂未开发成熟，请勿直接用于生产项目。

## 🧾更新日志

- 1.1.0
    - 【UPDATE】<u>重构限流配置类，使其更加易读、易配置，添加了默认值</u>
    - 【UPDATE】修改限流中间件扩展，以便支持切换限流算法
    - 【BUG】修复使用代理服务器或者负载均衡的情况下，无法获取真实IP的情况
    - 【ADD】<u>MemoryCache的支持</u>
- 1.0.2
    - 【UPDATE】重构限流中间件和限流中间件扩展，为集成漏桶限流算法做准备

## 📑更新计划

- Framework版本的支持🛠
- 集成漏桶限流算法🛠
- MemoryCache的支持✔
- 发布NuGet包✔
- 基础版本开源✔

## 👨‍🏫使用教程

1. NuGet安装

    ```
    NuGet\Install-Package YuanRateLimiter -Version 1.1.0
    ```

2. 使用

    ```csharp
    // NET 6:
    // 注册限流中间件
    // 使用Redis：
    builder.Services.AddRateLimiterSetUp(
        builder.Configuration["Redis连接字符串"], 
        config => builder.Configuration.GetSection("RateLimiter配置节点").Get<RateLimitingConfig>());
    // 使用MemoryCache：
    builder.Services.AddRateLimiterSetUp(
        config => builder.Configuration.GetSection("RateLimiter配置节点").Get<RateLimitingConfig>());
    
    // 使用限流中间件
    app.UseRateLimitMiddleware();
    ```

3. appsettings.json文件示例

    ```json
    {
      "Logging": {
        "LogLevel": {
          "Default": "Information",
          "Microsoft.AspNetCore": "Warning"
        }
      },
      "AllowedHosts": "*",
      "RedisConfig": {
        "Default": {
          "ConnectionString": "你的Redis连接字符串"
        }
      },
      // 限流配置
      "RateLimiter": {
        "EnableRateLimiter": true, // 是否开启限流
        "HttpStatusCode": 429, // 限流状态码，可以不配置，默认429
        "LimitingMessage": "请求被限制，您访问的太频繁了！", // 触发限流的提示消息，可以不配置，默认此文本的英文
        "CacheKey": "RateLimiterKey", // 令牌桶缓存Key，可以不配置，默认RateLimiterKey
        "RateLimiterModel": "TokenBucket", // 使用的限流算法模型(TokenBucket、LeakBucket、SlidingWindow)，默认令牌桶算法(TokenBucket)
        "RateLimiterRule": {
          "RateLimiterLogLevel": "Action", // 限流级别：Action、Method、All(默认All)【如果限流级别为Action，MethodFlowLimiterRules不用配置】
          "AllFlowLimiterRule": {
            "Capacity": 10, // 令牌桶容量
            "RateLimit": 1 // 速率
          },
          "MethodFlowLimiterRules": [
            {
              "Method": "GET",
              "Capacity": 10,
              "RateLimit": 1
            },
            {
              "Method": "PUT",
              "Capacity": 15,
              "RateLimit": 1
            }
          ],
          "ActionFlowLimiterRules": [
            {
              "Path": "/api/Test/Test01",
              "Capacity": 10,
              "RateLimit": 1
            },
            {
              "Path": "/api/Test/Test03",
              "Capacity": 1000,
              "RateLimit": 1
            }
          ]
        }
      }
    }
    ```

## 👩‍💻测试截图

- API限流：

    ![](https://gitee.com/XiaoShiYi-1011/yuan-rate-limiter/raw/master/Doc/images/Test.JPG)

## 🤝商业合作

1. 我们团队(元代码科技工作室)专业从事软件开发、网站开发等多个领域。如果您有以下需求，欢迎与我们联系：
    - 移动端应用 / 电脑桌面应用 / 网站开发 / 鸿蒙应用 / 微信、支付宝、字节等第三方小程序或网站开发
    - 定制解决方案
    - ...

1. 我们团队(元代码科技工作室)的主要技术栈：.Net 、Vue、Java、鸿蒙等
    - 包括：移动端应用/ 电脑桌面应用 / 网站开发 / 鸿蒙应用 / 微信、支付宝、字节等第三方小程序或网站开发
2. 我们提供高质量的开发服务，所有项目单子，均为源码交付。大金额单子需要签订合同
3. 联系方式：[xiaoshiyi1011@163.com](mailto:xiaoshiyi1011@163.com)

##  🔐版权声明

- 该项目签署了MIT授权许可，详情请参阅 [LICENSE](https://gitee.com/XiaoShiYi-1011/yuan-rate-limiter/raw/master/LICENSE)，源码完全免费开源商用。
- 不能以任何形式将该项目用于非法为目的的行为。
- 任何基于本软件而产生的一切法律纠纷和责任，均于作者无关。
