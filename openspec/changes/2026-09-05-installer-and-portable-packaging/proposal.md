# Installer & Portable Packaging（安装器 + 便携包）

## Why

Pulsar 目前只能 `dotnet run` 跑源码，无任何可分发产物——市场评估「用户装得上」验收（M1）的直接阻塞项。StarPie 的双包策略（独立单文件版内置运行时 + 轻量便携版）已验证了目标用户的真实偏好，且其与更新链路（Change 3 移交安装器）构成闭环。

## What Changes

- **两种分发形态**：
  - **安装版**：MSI（WiX）或 Inno Setup 之一（design 决策），装 `Program Files`、注册开始菜单/卸载入口、可选开机自启；
  - **Standalone 单文件版**：self-contained single-file 发布（内置 .NET 8 运行时），解压/单 exe 即用，面向免安装用户（对齐 StarPie「推荐」包）。
- **发布脚本自动化**：`scripts/` 新增 publish 流程（`dotnet publish` 双形态 + 版本号注入 + 产物命名 `Pulsar-v{version}-Setup.exe` / `Pulsar-v{version}-Standalone-win-x64.zip` + SHA256 清单），一键可重复执行。
- **升级保留用户数据**：安装/升级不改 `%AppData%\Pulsar`（配置、自定义图标、插件授权记录）；卸载默认保留并明确提示。
- **打包冒烟验证**：发布产物在干净环境可启动（无 .NET SDK/运行时依赖差异）、单实例互斥两形态均生效。

## Capabilities

### New Capabilities

- `app-packaging-distribution`: 应用打包与分发——双分发形态产物约定、发布脚本与版本注入、升级时用户数据保留、卸载行为、产物完整性与冒烟验收。

### Modified Capabilities

（无。）

## Impact

- **Affected code**:
  - 新增 `Pulsar/Pulsar.Installer/` 或 `scripts/installer/`（安装器工程/脚本，形态依 design 决策）。
  - `scripts/` 新增 `publish.ps1`（或并入 `dev.ps1` 新子命令 `publish`）。
  - `Pulsar.csproj` 可能补充 publish 相关属性（SingleFile PublishTrimmed 取舍、图标、产品名）。
  - `Docs/ops/BUILD_AND_RUN.md` 增发布章节。
- **Dependencies**: WiX Toolset 或 Inno Setup（构建机新依赖，随决策）；无运行时新依赖。
- **Verification**: 干净虚拟机/沙箱安装 → 启动 → 升级 → 卸载全链路冒烟；两形态 SHA256 清单与产物哈希一致。
- **Out of scope**: 自动更新消费端逻辑（Change 3）、代码签名证书购买（先以未签名发布，风险在 design 记录）、MSIX/Store 分发。
