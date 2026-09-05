# Design — In-App Auto Update

## Context

Pulsar 当前是「网络零依赖」桌面应用（无 HttpClient 使用点），版本源 = `Pulsar.csproj` `<Version>1.10.0</Version>`。可用接缝：`ITrayService.ShowNotification`（托盘提示）、`SettingsAboutPage`（更新 UI 自然归宿）、`AppStartupCoordinator` 延迟初始化（ADR-013 时序纪律）。目标仓库 `github.com/Smith-Rosco/Pulsar`，目标用户多在国内网络环境——这正是 StarPie v1.6.8 重构所解决的同款问题。

## Goals / Non-Goals

**Goals:**
- 国内网络可达性优先：Atom Feed（`github.com` 主域名、免鉴权、无限流）作为核心破局层，与 StarPie 实测结论一致。
- 服务可单测：HTTP 层经接口抽象，三级检测与故障转移逻辑不依赖真实网络即可测试。
- 不阻塞启动：检查在启动协调器延迟阶段后台执行。

**Non-Goals:**
- 静默热更新（运行中自替换 exe）——需要处理文件锁/提权/回滚，等安装器（Change 4）落地后再评估。
- beta/nightly 通道、增量更新、企业内网代理配置。

## Decisions

1. **架构移植而非新设计**：三级容灾（API→Atom→302）+ 多镜像故障转移 + Accept 头隔离直接取材 StarPie `UpdateService`（MIT，同许可证兼容）；出处在新 ADR 注明。备选「只调 REST API + 失败即报错」被否——正是 StarPie 根因 1 的 100% 超时死循环形态，国内用户必踩。
2. **HTTP 面经 `IUpdateHttpGateway` 抽象**（`SendAsync(request) → response`），生产实现包 `HttpClient`，测试注入 fake——三级检测/比较/故障转移全部纯逻辑可测，不引入真实网络 flake。
3. **v1 = 检测/提示/下载/移交安装器**，不做自替换：文件锁与提权问题由安装器方案消化（Change 4），本变更不背包袱。
4. **镜像表硬编码 + 设置页只读展示**（v1 不做用户自定义镜像输入）：StarPie 实测有效源列表（ghfast.top 等）直接内置，避免配置面扩大；后续有真实需求再开放。
5. **状态机三态 + 错误态全落 UI**：`UpToDate / UpdateAvailable / ParseFailed / NetworkError / Downloading / ReadyToInstall / IntegrityFailed`；杜绝任何「假超时红字」型误导（StarPie 根因 3 的教训）。
6. **启动时序**：`AppStartupCoordinator` 延迟阶段 fire-and-forget 后台检查（设置未关闭时），UI 线程只接收结果投影——复用 ADR-013 的「中继在托盘初始化后解析」纪律。

## Risks / Trade-offs

- **GitHub 资产 hash 可得性**：REST 层有 digest、Atom 层没有——降级为大小校验并在 UI 明示「较弱校验」，不静默。
- **镜像源时效性**：加速镜像存活不可控——镜像表集中一处 + 启动可观测日志，失效时修一处即可。
- **MSIX/Portable 差异**：Change 4 未定案前，移交动作以「启动已下载安装包」为最小契约，不假设安装器形态。
- **代理/企业网络**：`HttpClient` 默认系统代理，企业用户大概率可用；不自建代理配置面。
