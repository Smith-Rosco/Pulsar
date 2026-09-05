# In-App Auto Update（内置检查更新）

## Why

Pulsar 无任何更新机制（全仓无 HttpClient 使用），是 09-03 市场评估「分发/触达 1.5 分」的核心构成，也是 M1 发布后用户留存的硬前提——没有更新通道，每个 bug 修复都要求用户手动重装。参考项目 StarPie v1.6.8 恰好完成了一次检查更新深度重构（多级容灾 + 多镜像故障转移），其方案（MIT 同源，可移植）直接针对国内网络环境（`api.github.com` 阻断/限流）验证过，移植成本远低于自行设计。

## What Changes

- **三级容灾版本检测**（移植 StarPie 架构）：Tier 1 GitHub REST API（5s 超时）→ Tier 2 Releases Atom Feed（`github.com` 主域名、免鉴权、无限流，6s 超时）→ Tier 3 `releases/latest` 302 Location 探测；任一层成功即止。
- **语义化版本比较**：`{Major}.{Minor}.{Patch}` 与 csproj `<Version>`（当前 1.10.0）比较，产出「已是最新 / 有新版本 / 解析失败」三态，杜绝倒挂误报。
- **多镜像下载故障转移**：官方直连 + 国内加速镜像（ghfast.top 等）按序切换，单一镜像 404/502/超时自动切下一源；按请求类型隔离 Accept 头（避免全局头污染导致 406——StarPie 踩过的坑）。
- **下载完整性校验**：校验 release 资产 SHA256（GitHub API 提供 digest；Atom 层无 digest 时降级为大小校验并明示）。
- **更新 UI**：设置 → 关于页新增「检查更新」区块（当前版本 / 最新版本 / 状态徽标 / 一键直达 Release 页）；检测到新版本经 `ITrayService.ShowNotification` 提示；下载进度可见。
- **更新策略**：v1 采用「检测 → 提示 → 下载 → 启动安装器」而非静默自替换（无需处理运行中 exe 锁与提权，复杂度留在 Change 4 的安装器设计里消化）。
- **隐私红线**：仅访问 GitHub 相关域名，不上传任何本机信息；检查行为可在设置中关闭/手动触发。

## Capabilities

### New Capabilities

- `app-update-service`: 应用内更新服务——三级容灾版本检测、版本比较三态、多镜像下载故障转移、完整性校验、关于页 UI 与托盘提示、隐私边界（仅 GitHub 域名、可关闭）。

### Modified Capabilities

（无——现有能力不因本变更改变需求。）

## Impact

- **Affected code**:
  - 新增 `Services/Updates/`（`IUpdateService` + 实现、`UpdateChannelConfig`、版本比较器、镜像表）+ `Services/Interfaces/IUpdateService.cs`。
  - `App.xaml.cs` DI 注册（singleton + 启动协调器延迟触发，对齐 ADR-013 时序纪律：不阻塞启动关键路径）。
  - `ViewModels/AboutViewModel.cs` + `Views/Pages/SettingsAboutPage.xaml`（更新区块 UI）。
  - `Resources/Strings.resx` / `Strings.zh-CN.resx`（更新相关本地化键）。
- **Dependencies**: 新增 `System.Net.Http` 使用（框架内置，无新包）；无需第三方 HTTP 库。
- **License note**: 三级容灾/镜像故障转移设计移植自 StarPie（MIT），在 `Docs/decisions/` 新 ADR 中注明出处。
- **Out of scope**: 安装器与 standalone 打包（Change 4）、静默自替换（热更新）、增量下载、多通道（beta/nightly）。
