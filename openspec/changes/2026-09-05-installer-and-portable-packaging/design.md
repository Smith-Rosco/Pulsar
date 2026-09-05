# Design — Installer & Portable Packaging

## Context

单项目解决方案（`Pulsar/Pulsar/Pulsar.csproj`，.NET 8 WPF），用户数据集中在 `%AppData%\Pulsar`（`ConfigService.cs:118`），版本源 `<Version>` 属性。当前无 publish 流程、无安装器工程。StarPie 双包策略（Standalone 推荐 + Lightweight）与命名约定可直接对标；Change 3 的更新链路以「启动已下载安装包」为移交契约，本变更决定安装器形态。

## Goals / Non-Goals

**Goals:**
- 一条命令产出可发布双形态 + SHA256 清单。
- 干净机器可用：Standalone 零依赖、安装版注册完整卸载入口。
- 升级/卸载对 `%AppData%\Pulsar` 绝对安全。

**Non-Goals:**
- 代码签名（证书成本，v1 未签名发布并在 README 注明 SmartScreen 提示处理）。
- MSIX / Microsoft Store、增量升级、企业 MSI 部署定制（transform 等）。

## Decisions

1. **安装器选 Inno Setup 而非 WiX/MSI**：脚本化简单、中文安装界面成熟、卸载/自启勾选项开箱即用；WiX 优势（企业级 MSI/AD 部署）非 v1 目标用户诉求。脚本入 `scripts/installer/pulsar.iss`，可审计可版本化。备选 WiX 被否：学习/维护成本高，MSI 收益用不上。
2. **Standalone 用 `dotnet publish -c Release -r win-x64 --self-contained -p:PublishSingleFile=true`**，`IncludeNativeLibrariesForSelfExtract` 处理原生依赖；**不裁剪**（`PublishTrimmed=false`）——WPF + 反射式插件系统（`PluginLoader`/`Activator`）与 trim 不兼容，StarPie 同样不裁剪。
3. **发布脚本并入 `dev.ps1` 新子命令 `publish`**：与 build/test/commit 同族，复用其 env 修补；版本从 csproj 读取注入产物名。
4. **单实例互斥**：实现若已有则验证两形态一致性；若缺失，本变更补齐（命名 Mutex，per-machine 对安装版 / per-user 对便携版可接受的折衷：统一 per-user 全局名）。
5. **升级安全边界**：安装器只写 `Program Files`，绝不触碰 `%AppData%\Pulsar`；卸载段显式声明保留用户数据。

## Risks / Trade-offs

- **未签名分发的 SmartScreen 拦截**：首版必然遇到——README/发布说明写清「更多信息 → 仍要运行」；签名列为后续商业化（M5）项。
- **self-contained 单文件体积 ~100MB+**：是换取零依赖的已知代价；同时提供 framework-dependent 轻量包可选产出（脚本开关，非默认交付物）。
- **Inno Setup 构建机依赖**：需安装 Inno Setup（`ISCC.exe`）；CI 无 GitHub Actions Windows runner 限制，但 v1 先本地产出，CI 自动化留待 Release 流程（Change 5）观察后再接。
- **trim/reflect 冲突**：不裁剪规避，但后续若有人开启 trim 会静默破坏插件加载——在 csproj 注释与 ADR 中写明禁裁剪原因。
