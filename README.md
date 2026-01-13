<div align="center"><img  src="https://gitee.com/XiaoShiYi-1011/yuan-rate-limiter/raw/master/Doc/images/logo.jpg" width="120" height="120" style="margin-bottom: 10px;"/></div>
<div align="center"><strong><span style="font-size: x-large;">YuanRateLimiter</span></strong></div>
<div align="center"><h4 align="center">不断更新迭代中...</h4></div>
<div align="center"><p stylt="text-align: center;">我觉得此项目开源的初衷在于，支持.Net开源生态的发展。让我康康谁还说.Net没生态的😎</p></div>

### ✨如果您觉得有帮助，请点右上角 "Star" 支持一下谢谢

## 🎇框架介绍

YuanRateLimiter 是一个基于ASP.NET Core 的高性能、高可用限流中间件，适用于需要对接口请求进行精细化控制的场景。配置灵活：通过 `appsettings.json` 文件配置，提供了**令牌桶、漏桶、滑动窗口**三种主流限流算法，支持**全接口、HTTP 方法级、接口路径级**三种限流策略。内置 Redis + MemoryCache 的**混合缓存**与**容错缓存**机制，确保在 Redis 宕机时限流仍正常限流。值得注意的是NET 7/8自带了完善的限流中间件，友情链接：[ASP.NET Core 中的速率限制中间件 | Microsoft Learn](https://learn.microsoft.com/zh-cn/aspnet/core/performance/rate-limit?view=aspnetcore-8.0)。如果你是NET 7/8及以上开发的项目，请使用NET 7/8自带的限流中间件（或者你嫌官方的配置太麻烦也可以用这个嘻嘻😚）。温馨提示：请根据您的测试环境进行严格的压力测试后再投入到生产环境使用。

> 🎯 如果你不想引入国外复杂的限流组件，或需要更灵活的配置方式，YuanRateLimiter 是一个轻量且强大的选择。

------

## 🚀 核心特性

- ✅ **多算法支持**：令牌桶、漏桶、滑动窗口三种限流算法
- ✅ **三级限流策略**：支持全接口、Method 级别、Action 级别的灵活配置
- ✅ **IP 黑白名单**：可配置 IP 白名单（跳过限流）与黑名单（直接拒绝）
- ✅ **智能缓存降级**：Redis 不可用时自动降级到内存缓存，限流不中断
- ✅ **双写策略（可选）**：Redis 与内存缓存双写，提升高可用性
- ✅ **健康检查与退避重试**：自动检测 Redis 状态，支持指数退避重连
- ✅ **多版本支持**：支持 .NET 6、.NET 5 及以下版本
- ✅ **配置驱动**：完全通过 `appsettings.json` 配置，无需硬编码
- ✅ **注释丰富**：核心代码注释覆盖率 > 90%，便于二次开发

## 👨‍🏫使用教程

<div align="center"><h4 align="center"><a href="https://gitee.com/XiaoShiYi-1011/yuan-rate-limiter/blob/master/Doc/%E4%BD%BF%E7%94%A8%E6%96%87%E6%A1%A3.md">点击查询详细使用文档</a></h4></div>

1. NuGet安装

    ```
    NuGet\Install-Package YuanRateLimiter -Version 2.4.3
    ```

2. 使用

    - NET Core 6

        ```c#
        // 注册限流中间件
        // 使用Redis：
        builder.Services.AddRateLimiterSetUp(
            builder.Configuration["Redis连接字符串"], 
            config => builder.Configuration.GetSection("RateLimiter配置节点").Get<RateLimiterConfig>());
        // 使用MemoryCache：
        builder.Services.AddRateLimiterSetUp(
            config => builder.Configuration.GetSection("RateLimiter配置节点").Get<RateLimiterConfig>());
        
        // 使用限流中间件（添加在跨域中间件的下面，否则前端无法捕获错误状态码）
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
                // 使用限流中间件（添加在跨域中间件的下面，否则前端无法捕获错误状态码）
                app.UseRateLimitMiddleware();
                // 其他代码....
            }
        }
        ```
    
3. 完整使用的 `appsettings.json` 文件示例

    ```json
    {
      "RedisConfig": {
        "Default": {
          //"ConnectionString": "127.0.0.1:6379,password=你的密码,DefaultDatabase=0,connectTimeout=3000,connectRetry=1,syncTimeout=10000"
          "ConnectionString": "127.0.0.1:6380,password=你的密码,DefaultDatabase=0,connectTimeout=3000,connectRetry=1,syncTimeout=10000"
        }
      },
      // 限流配置
      "RateLimiter": {
        "EnableRateLimiter": true, // 是否开启限流
        "HttpStatusCode": 429, // 限流状态码，可以不配置，默认429
        "LimitingMessage": "请求过于频繁，请稍后再试。", // 触发限流的提示消息，可以不配置，默认此文本的英文
        "EnableFallbackCache": true, // 启用降级缓存
        "EnableDoubleWrite": true, // 关闭双写（Redis写数据的同时内存也写数据，确保缓存高可用，默认关闭，避免内存占用过高，Redis宕机恢复后不需要回种内存中的限流数据，因为因为限流数据几乎是瞬时的，回种内存数据意义不大）
        "RedisRetryCount": 3, // Redis重试次数
        "RedisRetryDelayMs": 1000, // Redis重试延迟(毫秒)
        "CacheKey": "RateLimiterKey", // 缓存Key，可以不配置，默认RateLimiterKey
        "RateLimiterModel": "TokenBucket", // 使用的限流算法模型(TokenBucket、LeakBucket、SlidingWindow)，默认令牌桶算法(TokenBucket)
        "EnableIpLimiter": true, // 是否启用Ip限流，默认关闭
        //"IpWhiteList": [ "127.0.0.1", "0.0.0.1" ], // 限流Ip白名单
        "IpWhiteList": [], // 限流Ip白名单
        "IpBlackList": [ "122.189.37.201" ], // 限流Ip黑名单
        "RateLimiterRule": {
          "RateLimiterLogLevel": "Method", // 限流级别：Action、Method、All，默认All（每个限流级别对应的规则不同）
          "AllFlowLimiterRule": {
            "Capacity": 100, // 容量
            "RateLimit": 20, // 速率(QPS)
            "WindowSize": 10, // 窗口大小
            "MaxRequests": 10 // 最大请求数
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
              "RateLimit": 2
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
    
3. 极简使用的 `appsettings.json` 文件示例（不用Redis、容错、双写，就单单用限流【懒人专属】）

    ```json
    {
      // 限流配置
      "RateLimiter": {
        "EnableRateLimiter": true, // 是否开启限流
      }
    }
    ```

## 🧾更新日志

- v2.4.3
  - 【FIX】 修复 HybridCacheRepository.Dispose() 中的 ObjectDisposedException，确保 Dispose 方法幂等，支持多平台（如 Linux）下的关闭处理，提升整体稳定性
- ~~v2.4.2~~（弃用）
- v2.4.1
  - 【PERF】重构令牌桶算法核心架构：将定时器主动补充令牌改为请求时懒惰计算，大幅提升性能
  - 【OPT】移除`System.Threading.Timer`依赖，减少线程调度开销和潜在计时器问题
  - 【OPT】优化存储结构，令牌计数从List存储改为Key-Value存储，显著降低内存占用
  - 【OPT】简化IPTokenBucket逻辑，移除"清理不活跃IP"的定时任务，依赖缓存过期机制
  - 【FIX】确保令牌桶算法在高并发场景下的一致性，避免令牌超发问题
- v2.4.0
  - 【ADD】增加混合缓存：以 Redis作为主缓存，内存缓存作为降级缓存的双重缓存机制，Redis 不可用时自动切换，保障限流功能不中断
  - 【ADD】增加双写策略：Redis可用时，缓存数据同时写入Redis和内存，提升本地读取速度，增强缓存高可用性
- v2.3.6
  - 【BUG】[修复触发限流时的响应体类型`text/plain;charset=utf-8  ==>  application/json;charset=utf-8`](https://gitee.com/XiaoShiYi-1011/yuan-rate-limiter/commit/e41a3e70d100a2d5e0c12daa55045c2b19eb6a91)
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

-  增加Api路径通配符🛠
-  增加容错缓存机制，Redis宕机时自动降级到内存缓存，增加双写策略✔
-  集成固定窗口算法（暂缓该算法开发）🛠
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

## ❓ 常见问题（FAQ）

### Q1: 三种限流算法有什么区别？该如何选择？

**A:**
- **令牌桶算法（TokenBucket）**：允许突发流量，适合秒杀、抢购等场景
- **漏桶算法（LeakBucket）**：平滑流出，限流更严格，适合接口稳定性要求高的场景
- **滑动窗口算法（SlidingWindow）**：统计精确，适合需要对单位时间内请求数精确控制的场景

**推荐选择**：一般场景使用令牌桶算法即可，如需精确控制使用滑动窗口，如需严格控制流出速率使用漏桶。

### Q2: Redis 宕机后，限流功能还能正常工作吗？

**A:** 可以！如果配置了 `EnableFallbackCache: true`（默认开启），当 Redis 不可用时，系统会自动降级到内存缓存，确保限流功能不中断。同时后台会持续尝试重连 Redis，恢复后自动切换回去。

### Q3: 双写策略（EnableDoubleWrite）有什么作用？建议开启吗？

**A:** 双写策略会同时将数据写入 Redis 和内存缓存，进一步提高可用性。但会略微增加内存使用和写入延迟。**建议**：对于关键业务系统可以开启，一般场景保持关闭即可。

### Q4: 如何在多服务器/分布式环境下使用？

**A:** 使用 Redis 作为缓存存储时，所有服务器实例共享同一个 Redis 实例，限流计数是全局的，天然支持分布式部署。

### Q5: IP 限流（EnableIpLimiter）开启后有什么影响？

**A:** 开启后，系统会为每个 IP 单独维护限流计数。适合防止单个 IP 恶意请求的场景。**注意**：IP 数过多时，内存/Redis 使用会相应增加。

### Q6: 如何监控限流触发情况？

**A:** 可以通过以下方式：
1. 查看应用程序日志（触发限流时会记录警告日志）
2. 监控 Redis 中限流相关的 Key
3. 观察 API 返回的 429 状态码数量

### Q7: 为什么前端有时捕获不到 429 状态码？

**A:** 请确保 `app.UseRateLimitMiddleware()` 中间件添加在跨域中间件（如 `app.UseCors()`）之后，否则浏览器可能会因 CORS 策略而无法正确读取状态码。

### Q8: 支持.NET Framework 项目吗？

**A:** 目前不支持 .NET Framework，主要面向 .NET Core/5/6/7/8/9。如有 .NET Framework 需求可联系定制开发。

### Q9: 如何调整限流参数？有哪些建议值？

**A:** 参数调整建议：

- **容量（Capacity）**：一般设置为 QPS 的 2-5 倍，允许一定突发
- **速率限制（RateLimit）**：根据业务承载能力设定，可从保守值开始逐步调整
- **窗口大小（WindowSize）**：滑动窗口算法专用，一般设为 5-30 秒

**建议**：先设置较宽松的限制，通过压力测试和监控逐步调整到最优值。

### Q10: 为什么NuGet上的不是最新版本？

**A:** 因为我 NuGet 账号的密码又又又忘记了🥲，着急使用最新版本的，拉取源码自行打包使用。我会尽快找回密码把版本同步上去（如果我记得住的话 🤣）

## 👩‍💻测试截图

- API限流：

    ![](https://gitee.com/XiaoShiYi-1011/yuan-rate-limiter/raw/master/Doc/images/Test.JPG)
    
    ![](https://gitee.com/XiaoShiYi-1011/yuan-rate-limiter/raw/master/Doc/images/Test02.JPG)
    
    ![](https://gitee.com/XiaoShiYi-1011/yuan-rate-limiter/raw/master/Doc/images/Test01.JPG)
    
    ![](https://gitee.com/XiaoShiYi-1011/yuan-rate-limiter/raw/master/Doc/images/Test05.png)

## 📌 注意事项

- 建议在生产环境前进行压测，根据实际流量调整参数
- 如果使用 Redis，请确保连接字符串正确且网络通畅
- 启用双写策略会增加内存使用，请根据服务器资源酌情开启
- 若使用 .NET 7/8/9，也可考虑官方内置的限流中间件（但配置更复杂）

## 🤝商业合作

1. 我们团队(元代码科技工作室)专业从事软件开发、网站开发等多个领域。如果您有以下需求，欢迎与我们联系：
    - 移动端应用 / 电脑桌面应用 / 网站开发 / 鸿蒙应用 / 微信、支付宝、字节等第三方小程序或网站开发
    - 定制解决方案
    - ...

2. 我们团队(元代码科技工作室)的主要技术栈：.Net 、Vue、Java、鸿蒙等
    - 包括：移动端应用 / 电脑桌面应用 / 网站开发 / 鸿蒙应用 / 微信、支付宝、字节等第三方小程序或网站开发
3. 我们提供高质量的开发服务，所有项目单子，均为源码交付。大金额单子需要签订合同
4. 联系方式：[xiaoshiyi1011@163.com](mailto:xiaoshiyi1011@163.com)

##  🔐版权声明

- 该项目签署了MIT授权许可，详情请参阅 [LICENSE](https://gitee.com/XiaoShiYi-1011/yuan-rate-limiter/raw/master/LICENSE)，源码完全免费开源商用。
- 不能以任何形式将该项目用于非法为目的的行为。
- 任何基于本软件而产生的一切法律纠纷和责任，均于作者无关。

