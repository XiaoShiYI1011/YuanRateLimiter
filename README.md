<div align="center"><img  src="https://gitee.com/XiaoShiYi-1011/yuan-rate-limiter/raw/master/Doc/images/logo.jpg" width="120" height="120" style="margin-bottom: 10px;"/></div>
<div align="center"><strong><span style="font-size: x-large;">YuanRateLimiter</span></strong></div>
<div align="center"><h4 align="center">不断更新迭代中...</h4></div>
<div align="center"><p stylt="text-align: center;">我觉得此项目开源的初衷在于，支持.Net开源生态的发展。让我康康谁还说.Net没生态的😎</p></div>

### ✨如果您觉得有帮助，请点右上角 "Star" 支持一下谢谢

## 🎇框架介绍

YuanRateLimiter是一个Asp.Net Core的限流中间件。如果你项目不想采用国外的限流组件那就可以参考此项目或者直接使用，配置灵活：通过appsettings.json文件配置，支持全接口限流、Method限流、API接口限流。默认采用基于Redis的令牌桶算法，正在集成其他限流算法。采用基于[NewLife.Redis](https://github.com/NewLifeX/NewLife.Redis)二次封装的[SimpleRedis](https://gitee.com/zxzyjs/SimpleRedis.git)。简化了Redis的操作，更方便使用。核心代码注释覆盖率>90%。值得注意的是NET 8自带了完善的限流中间件(很烦，开源开得有点晚了...慢了一步)，友情链接：[ASP.NET Core 中的速率限制中间件 | Microsoft Learn](https://learn.microsoft.com/zh-cn/aspnet/core/performance/rate-limit?view=aspnetcore-8.0)。如果你是NET 8开发的项目，请使用NET 8自带的限流中间件。温馨提示：该项目暂未开发成熟，请勿直接用于生产项目。

## 📑开发日志

- MemoryCache的支持🛠
- 集成漏桶限流算法🛠
- 发布NuGet包✔
- 基础版本开源✔

## 👨‍🏫使用教程

1. NuGet安装

    ```
    NuGet\Install-Package YuanRateLimiter -Version 1.0.1
    ```

2. 使用

    ```csharp
    // NET 6:
    // 注册限流中间件
    builder.Services.AddRateLimiterSetUp(
        builder.Configuration["Redis连接字符串"], 
        config => builder.Configuration.GetSection("RateLimiting配置节点").Get<RateLimitingConfig>());
    
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
      // Redis配置
      "RedisConfig": {
        "Defaulr": {
          "ConnectionString": "你的Redis连接字符串"
        }
      },
      // 限流配置
      "RateLimiting": {
        "EnableRateLimiting": true, // 是否开启限流
        "HttpStatusCode": 429, // 限流状态码
        "CacheKey": "TokenBucketState", // 令牌桶缓存Key，可以不配置，默认TokenBucketState
        "IsAllApiRateLimiting": false, // 是否开启全接口限流【开启为true，开启后ApiFlowLimitingRules可以不用配置】
        "IsAllApiFlowLimitingRule": {
          "Capacity": 10, // 令牌桶容量
          "TokensPerSecond": 1 // 每秒产生的令牌数量
        },
        "RateLimitingLogLevel": "Api", // 限流级别：Api、Method【如果限流级别为Api，MethodFlowLimitingRules不用配置】
        "MethodFlowLimitingRules": [
          {
            "Method": "GET",
            "Capacity": 10,
            "TokensPerSecond": 1
          },
          {
            "Method": "PUT",
            "Capacity": 230,
            "TokensPerSecond": 10
          }
        ],
        "ApiFlowLimitingRules": [
          {
            "Path": "/api/Test/Test01",
            "Capacity": 10,
            "TokensPerSecond": 1
          },
          {
            "Path": "/api/Test/Test03",
            "Capacity": 1000,
            "TokensPerSecond": 10
          }
        ]
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
