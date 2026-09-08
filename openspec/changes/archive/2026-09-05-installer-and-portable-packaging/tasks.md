# Tasks — Installer & Portable Packaging

## 1. 发布脚本（Standalone 先行）

- [x] 1.1 `dev.ps1` 新增 `publish` 子命令：`dotnet publish`（self-contained single-file, win-x64, 不裁剪）+ 版本号读取 + 产物命名 `Pulsar-v{version}-Standalone-win-x64.zip` + SHA256 清单
- [x] 1.2 csproj 复核 publish 属性（产品名/图标/`IncludeNativeLibrariesForSelfExtract`）；禁 trim 原因注释（插件系统反射依赖）
- [x] 1.3 干净环境冒烟：无 .NET Runtime 机器/VM 启动 Standalone → 主界面可用、About 页版本正确
  - **2026-09-08 真机收口**：v1.12.0 Standalone zip（commit `b5c0ed4` 构建的本地包）在无 .NET 的新机器解压运行通过 —— 自包含运行时冒烟 ✅（同时覆盖窗口切换回归与托盘菜单/主题图标观察）。

## 2. 安装器

- [x] 2.1 `scripts/installer/pulsar.iss`：安装到 Program Files、开始菜单入口、卸载条目、可选开机自启勾选、版本号从 csproj 注入
- [x] 2.2 升级/卸载语义：覆盖安装不改 `%AppData%\Pulsar`；卸载默认保留用户数据并明示文案
- [x] 2.3 publish 脚本扩展：产出 `Pulsar-v{version}-Setup.exe` 并入 SHA256 清单
- [x] 2.4 冒烟：安装 → 启动 → 覆盖升级（配置保留验证）→ 卸载（数据保留 + 条目清除）
  - **2026-09-06 真机收口**（journal，commit `d346b3f`）：静默安装 → 运行 → 静默卸载全链路通过，`%AppData%\Pulsar` 103 文件保留。

## 3. 单实例与两形态一致性

- [x] 3.1 核实单实例互斥实现（App.xaml.cs / 启动协调器）；缺失则补命名 Mutex — **已补**：`Local\Pulsar-SingleInstance-9F3A2C1E` 命名 Mutex + 已有窗口激活（SetForegroundWindow/ShowWindow）；UI Debug 模式跳过以允许 E2E 多实例
- [x] 3.2 两形态交叉验证：安装版运行中启动 Standalone（及反向）第二实例退出并聚焦
  - **2026-09-06 真机收口**：Mutex 单实例保护验证通过，第二实例 5s 内自动退出并聚焦首个实例。
- [x] 3.3 相关测试/手动验证记录
  - 见 3.2 同一 journal 条目；单实例由 `Local\Pulsar-SingleInstance-9F3A2C1E` 命名 Mutex 保证（3.1）。

## 4. 文档 & 验证

- [x] 4.1 `Docs/ops/BUILD_AND_RUN.md` 新增「发布与分发」章节（双形态、publish 命令、SmartScreen 未签名提示处理）
- [x] 4.2 ADR：安装器选型（Inno vs WiX）+ 禁裁剪决策
- [x] 4.3 `scripts/dev.ps1 build` + `test` 全绿；publish 幂等性验证（重跑产物布局一致）（bash+env 等效验证：build 0 警告 0 错误、全量 1227/1227；publish 实跑两遍 Pulsar.exe SHA256 一致 0491866c...，单文件 75.9MB 自包含，native 库已内嵌；Setup.exe 路径因本机无 ISCC 未实机验证）
