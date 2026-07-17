# YuanRateLimiter 文档目录

## 目录结构

| 路径 | 用途 |
| --- | --- |
| [使用文档.md](使用文档.md) | YuanRateLimiter 使用说明 |
| [load-test/限流中间件高并发可靠性压测方案.md](load-test/限流中间件高并发可靠性压测方案.md) | 高负载高并发压测方案 |
| [load-test/限流中间件高负载高并发正式测试报告.md](load-test/限流中间件高负载高并发正式测试报告.md) | 正式压测报告 |
| [load-test/限流中间件35分钟长稳态高并发测试报告.md](load-test/限流中间件35分钟长稳态高并发测试报告.md) | 35 分钟长稳态高并发测试报告 |
| [load-test/限流中间件Redis故障高可用性测试报告.md](load-test/限流中间件Redis故障高可用性测试报告.md) | Redis 故障高可用性测试报告 |
| [load-test/测试服务器.md](load-test/测试服务器.md) | 脱敏后的测试服务器角色清单 |
| [scripts/](scripts/) | 8 台测试服务器的初始化脚本模板 |
| [images/](images/) | 文档图片资源 |

## 敏感信息

1. 文档中的服务器公网 IP 使用 `203.0.113.0/24` 示例网段替代。
2. 文档中的服务器内网 IP 使用 `10.0.2.0/24` 示例网段替代。
3. 登录密码、Redis 密码、实例 ID 均已脱敏。
4. 安装脚本不再内置 Redis 密码或真实服务器地址，运行时通过环境变量传入。

## 脚本运行提示

应用节点脚本需要显式传入 Redis 地址和密码：

```bash
export REDIS_HOST="<redis-private-ip>"
export REDIS_PASSWORD="<redis-password>"
bash scripts/install-03-app-1.sh
```

Redis 节点脚本需要显式传入绑定内网地址和 Redis 密码：

```bash
export PRIVATE_IP="<redis-private-ip>"
export REDIS_PASSWORD="<redis-password>"
bash scripts/install-06-redis-1.sh
```

负载均衡节点脚本需要显式传入三个应用 upstream：

```bash
export APP_1="<app-1-private-ip>:5001"
export APP_2="<app-2-private-ip>:5001"
export APP_3="<app-3-private-ip>:5001"
bash scripts/install-07-lb-1.sh
```

监控节点脚本需要显式传入 Node Exporter 目标列表：

```bash
export NODE_EXPORTER_TARGETS="<node-1-private-ip>:9100,<node-2-private-ip>:9100"
bash scripts/install-08-monitor-1.sh
```
