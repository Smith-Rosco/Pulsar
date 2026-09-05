# Tasks — Installer & Portable Packaging

## 1. 发布脚本（Standalone 先行）

- [ ] 1.1 `dev.ps1` 新增 `publish` 子命令：`dotnet publish`（self-contained single-file, win-x64, 不裁剪）+ 版本号读取 + 产物命名 `Pulsar-v{version}-Standalone-win-x64.zip` + SHA256 清单
- [ ] 1.2 csproj 复核 publish 属性（产品名/图标/`IncludeNativeLibrariesForSelfExtract`）；禁 trim 原因注释（插件系统反射依赖）
- [ ] 1.3 干净环境冒烟：无 .NET Runtime 机器/VM 启动 Standalone → 主界面可用、About 页版本正确

## 2. 安装器

- [ ] 2.1 `scripts/installer/pulsar.iss`：安装到 Program Files、开始菜单入口、卸载条目、可选开机自启勾选、版本号从 csproj 注入
- [ ] 2.2 升级/卸载语义：覆盖安装不改 `%AppData%\Pulsar`；卸载默认保留用户数据并明示文案
- [ ] 2.3 publish 脚本扩展：产出 `Pulsar-v{version}-Setup.exe` 并入 SHA256 清单
- [ ] 2.4 冒烟：安装 → 启动 → 覆盖升级（配置保留验证）→ 卸载（数据保留 + 条目清除）

## 3. 单实例与两形态一致性

- [ ] 3.1 核实单实例互斥实现（App.xaml.cs / 启动协调器）；缺失则补命名 Mutex
- [ ] 3.2 两形态交叉验证：安装版运行中启动 Standalone（及反向）第二实例退出并聚焦
- [ ] 3.3 相关测试/手动验证记录

## 4. 文档 & 验证

- [ ] 4.1 `Docs/ops/BUILD_AND_RUN.md` 新增「发布与分发」章节（双形态、publish 命令、SmartScreen 未签名提示处理）
- [ ] 4.2 ADR：安装器选型（Inno vs WiX）+ 禁裁剪决策
- [ ] 4.3 `scripts/dev.ps1 build` + `test` 全绿；publish 幂等性验证（重跑产物布局一致）
