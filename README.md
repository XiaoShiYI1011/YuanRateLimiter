<div align="center"><img  src="https://gitee.com/XiaoShiYi-1011/yuan-rate-limiter/raw/master/Doc/images/logo.jpg" width="120" height="120" style="margin-bottom: 10px;"/></div>
<div align="center"><strong><span style="font-size: x-large;">YuanRateLimiter</span></strong></div>
<div align="center"><h4 align="center">不断更新迭代中...</h4></div>
<div align="center"><p stylt="text-align: center;">我觉得此项目开源的初衷在于，支持.Net开源生态的发展。</p></div>

### 如果您觉得有帮助，请点右上角 "Star" 支持一下谢谢

## 框架介绍

YuanRateLimiter是一个Asp.Net Core的限流中间件。配置灵活：通过appsettings.json文件配置，支持全接口限流、Method限流、API接口限流。默认采用基于Redis的令牌桶算法，正在集成其他限流算法。采用基于[NewLife.Redis](https://github.com/NewLifeX/NewLife.Redis)二次封装的[SimpleRedis](https://gitee.com/zxzyjs/SimpleRedis.git)。简化了Redis的操作，更方便使用。核心代码注释覆盖率>90%。值得注意的是NET 8自带了完善的限流中间件(很烦，开源开得有点晚了...慢了一步)，友情链接：[ASP.NET Core 中的速率限制中间件]([ASP.NET Core 中的速率限制中间件 | Microsoft Learn](https://learn.microsoft.com/zh-cn/aspnet/core/performance/rate-limit?view=aspnetcore-8.0))。如果你是NET 8开发的项目，请使用NET 8自带的限流中间件。温馨提示：该项目暂未开发成熟，请勿直接用于生产项目。

##  版权声明

- 该项目签署了MIT授权许可，详情请参阅 [LICENSE](https://gitee.com/XiaoShiYi-1011/yuan-rate-limiter/raw/master/LICENSE)，源码完全免费开源商用。
- 不能以任何形式将该项目用于非法为目的的行为。
- 任何基于本软件而产生的一切法律纠纷和责任，均于作者无关。
