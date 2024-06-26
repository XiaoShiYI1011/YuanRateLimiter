<div align="center"><img  src="https://gitee.com/XiaoShiYi-1011/yuan-rate-limiter/raw/master/Doc/images/logo.jpg" width="120" height="120" style="margin-bottom: 10px;"/></div>
<div align="center"><strong><span style="font-size: x-large;">YuanRateLimiter</span></strong></div>
<div align="center"><h4 align="center">不断更新迭代中...</h4></div>
<div align="center"><p stylt="text-align: center;">我觉得此项目开源的初衷在于，支持.Net开源生态的发展。让我康康谁还说.Net没生态的😎</p></div>

### ✨如果您觉得有帮助，请点右上角 "Star" 支持一下谢谢

本工作室开源项目：[ASP.NET Core 限流中间件](https://gitee.com/XiaoShiYi-1011/yuan-rate-limiter)、[自毁机制](https://gitee.com/XiaoShiYi-1011/self-destruct)

## 🎇框架介绍

YuanRateLimiter是一个Asp.Net Core的限流中间件。如果你项目不想采用国外的限流组件那就可以参考此项目或者直接使用，配置灵活：通过appsettings.json文件配置，支持全接口限流、Method限流、Action接口限流。支持令牌桶限流、漏桶限流、滑动窗口限流。默认采用基于Redis的令牌桶算法，支持Redis和MemoryCache的无缝切换。正在集成其他限流算法。采用[NewLife.Redis](https://github.com/NewLifeX/NewLife.Redis)高性能Redis客户端组件。核心代码注释覆盖率>90%。值得注意的是NET 7/8自带了完善的限流中间件，友情链接：[ASP.NET Core 中的速率限制中间件 | Microsoft Learn](https://learn.microsoft.com/zh-cn/aspnet/core/performance/rate-limit?view=aspnetcore-8.0)。如果你是NET 7/8及以上开发的项目，请使用NET 7/8自带的限流中间件。温馨提示：该项目暂未开发成熟，请勿直接用于生产项目。

## 🧾更新日志

- v2.3.3
    - 【BUG】[**紧急修复**：补充滑动窗口限流规则](https://gitee.com/XiaoShiYi-1011/yuan-rate-limiter/commit/ce312b9ffac601cd2048bef0347c272ec51ea3c0)

- ~~v2.3.2~~（忘记加滑动窗口的限流规则了）
    - 【BUG】[修复黑白名单为空时报错](https://gitee.com/XiaoShiYi-1011/yuan-rate-limiter/commit/8ec4b8ff97e87c1e2d33eaf17fa7d8a20fddf797)
    - 【BUG】[修复销毁时无法删除缓存的问题](https://gitee.com/XiaoShiYi-1011/yuan-rate-limiter/commit/736ff5ca919c865155c950ab3d11a0831606532f)
    - 【ADD】新增滑动窗口限流算法

- v2.2.1
    - 【UPDATE】使用纯的NewLife.Redis，不使用封装过的
    - 【BUG】[修复泛型类型错误问题](https://gitee.com/XiaoShiYi-1011/yuan-rate-limiter/commit/e55a00a302e2ea5600a8804e3a14661ebba52f8f)
    - 【ADD】新增.NET Core 5 及其以下版本的支持
- v2.1.0
    - 【UPDATE】修改触发限流的提示消息 ==> The request is too frequent, please try again later.
    - 【ADD】<u>新增IP黑/白名单</u>
    - 【ADD】<u>新增IP限流</u>
- v2.0.0
    - 【UPDATE】<u>重构令牌桶算法</u>
    - 【UPDATE】触发限流后的日志时间信息增加到毫秒
    - 【ADD】<u>新增漏桶限流算法</u>
- ~~v1.1.1【弃用】~~
    - 【ADD】封装Redis和MemoryCache的List操作，以便集成漏桶限流算法
    - 【UPDATE】令牌桶算法的缓存Key过期时间设置为5分钟
- ~~v1.1.0【弃用】~~
    - 【UPDATE】<u>重构限流配置类，使其更加易读、易配置，添加了默认值</u>
    - 【UPDATE】修改限流中间件扩展，以便支持切换限流算法
    - 【BUG】修复使用代理服务器或者负载均衡的情况下，无法获取真实IP的情况
    - 【ADD】<u>MemoryCache的支持</u>
- ~~v1.0.2【弃用】~~
    - 【UPDATE】重构限流中间件和限流中间件扩展，为集成漏桶限流算法做准备

## 📑更新计划

-  集成固定窗口算法🛠
-  完善使用文档✔
-  集成滑动窗口算法✔
-  开发.NET Standard 2.0版本，以便所有.NET版本下载使用✔
-  ~~Framework版本的支持🛠~~
-  ~~.NET Core 6.0以下版本的适配🛠~~
-  增加针对IP限流✔
-  集成漏桶限流算法✔
-  MemoryCache的支持✔
-  发布NuGet包✔
-  基础版本开源✔

## 👨‍🏫使用教程

<div align="center"><h4 align="center"><a href="https://gitee.com/XiaoShiYi-1011/yuan-rate-limiter/blob/master/Doc/%E4%BD%BF%E7%94%A8%E6%96%87%E6%A1%A3.md">点击查询详细使用文档</a></h4></div>

1. NuGet安装

    ```
    NuGet\Install-Package YuanRateLimiter -Version 2.3.3
    ```

2. 使用

    - NET Core 6

        ```c#
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

    - NET Core 5 及以下

        ```c#
        public class Startup
        {
            public IConfiguration Configuration { get; }
        
            public Startup(IConfiguration configuration)
            {
                Configuration = configuration;
            }
        
            public void ConfigureServices(IServiceCollection services)
            {
                // 使用Redis：
                services.AddRateLimiterSetUp(
                    config => Configuration.GetSection("RateLimiter").Get<RateLimiterConfig>(),
                    Configuration["RedisConfig:Default:ConnectionString"]);
        
                // 使用MemoryCache：
                //services.AddRateLimiterSetUp(
                //    config => Configuration.GetSection("RateLimiter").Get<RateLimiterConfig>());
        
                // 其他代码....
            }
            
            public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
            {
                // 其他代码....
                app.UseRouting();
                // 使用限流中间件
                app.UseRateLimitMiddleware();
                // 其他代码....
            }
        }
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
                "ConnectionString": "127.0.0.1:6379,password=ydmkj.com.Redis,DefaultDatabase=0,connectTimeout=3000,connectRetry=1,syncTimeout=10000"
            }
        },
        // 限流配置
        "RateLimiter": {
            "EnableRateLimiter": true, // 是否开启限流
            "HttpStatusCode": 429, // 限流状态码，可以不配置，默认429
            "LimitingMessage": "请求过于频繁，请稍后再试。", // 触发限流的提示消息，可以不配置，默认此文本的英文
            "CacheKey": "RateLimiterKey", // 缓存Key，可以不配置，默认RateLimiterKey
            "RateLimiterModel": "TokenBucket", // 使用的限流算法模型(TokenBucket、LeakBucket、SlidingWindow)，默认令牌桶算法(TokenBucket)
            "EnableIpLimiter": true, // 是否启用Ip限流
            //"IpWhiteList": [ "127.0.0.1", "0.0.0.1" ], // 限流Ip白名单
            "IpWhiteList": [], // 限流Ip白名单
            "IpBlackList": [ "122.189.37.201" ], // 限流Ip黑名单
            "RateLimiterRule": {
                "RateLimiterLogLevel": "All", // 限流级别：Action、Method、All，默认All
                "AllFlowLimiterRule": {
                    "Capacity": 10, // 容量
                    "RateLimit": 2, // 速率(QPS)
                    "WindowSize": 10, // 窗口大小
                    "MaxRequests": 10 // 最大请求数
                },
                // "MethodFlowLimiterRules": [
                //     {
                //         "Method": "GET",
                //         "Capacity": 10,
                //         "RateLimit": 1
                //     },
                //     {
                //         "Method": "PUT",
                //         "Capacity": 15,
                //         "RateLimit": 1
                //     }
                // ],
                // "ActionFlowLimiterRules": [
                //     {
                //         "Path": "/api/Test/Test01",
                //         "Capacity": 10,
                //         "RateLimit": 2
                //     },
                //     {
                //         "Path": "/api/Test/Test03",
                //         "Capacity": 1000,
                //         "RateLimit": 1
                //     }
                // ]
            }
        }
    }
    ```

## 👩‍💻测试截图

- API限流：

    ![](https://gitee.com/XiaoShiYi-1011/yuan-rate-limiter/raw/master/Doc/images/Test.JPG)
    
    ![](https://gitee.com/XiaoShiYi-1011/yuan-rate-limiter/raw/master/Doc/images/Test02.JPG)
    
    ![](https://gitee.com/XiaoShiYi-1011/yuan-rate-limiter/raw/master/Doc/images/Test01.JPG)
    
    ![](https://gitee.com/XiaoShiYi-1011/yuan-rate-limiter/raw/master/Doc/images/Test05.png)

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
