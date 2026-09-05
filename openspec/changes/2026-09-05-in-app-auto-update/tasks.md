# Tasks — In-App Auto Update

## 1. 版本检测核心（纯逻辑，先行）

- [ ] 1.1 新增 `Services/Interfaces/IUpdateHttpGateway.cs`（抽象 HTTP 面）+ `Services/Updates/` 目录骨架
- [ ] 1.2 实现 `UpdateVersionComparer`：`{Major}.{Minor}.{Patch}` 语义比较 → 三态；补 `UpdateVersionComparerTests`（相等/落后/领先/带 v 前缀/脏字符串）
- [ ] 1.3 实现 `GitHubReleaseInfoParser`：REST JSON 与 Atom XML 双解析（tag + 资产列表 + digest 可得性）；补解析测试（真实响应样例固化为 fixture）
- [ ] 1.4 实现 `UpdateCheckService`：Tier1 API(5s) → Tier2 Atom(6s) → Tier3 302 探测的顺序降级 + 短路；补 `UpdateCheckServiceTests`（fake gateway：各层成功/全败/单层超时组合矩阵）

## 2. 下载与完整性

- [ ] 2.1 实现 `UpdateDownloadService`：官方直连 + 内置镜像表按序故障转移；请求按 kind 隔离 Accept 头；进度事件；失败源自动切下一源
- [ ] 2.2 实现 SHA256 校验 + 无 digest 时大小校验降级（UI 明示）；失败删除残留文件
- [ ] 2.3 补 `UpdateDownloadServiceTests`：故障转移矩阵（404/502/超时/中途断流）、头隔离断言、校验通过/失败分支

## 3. 集成：DI / 启动 / 设置

- [ ] 3.1 `UpdateSettings`（检查开关、镜像只读展示）入 `ProfilesConfig` 体系（经 `ConfigEditSession` 写入，禁直接改快照）
- [ ] 3.2 `App.xaml.cs` DI 注册 + `AppStartupCoordinator` 延迟阶段后台检查（对齐 ADR-013 时序；设置关闭则不启动）
- [ ] 3.3 `AboutViewModel` / `SettingsAboutPage`：当前版本、三态徽标、手动检查按钮、下载进度、ReadyToInstall → 「运行安装包」移交；`Strings.resx`/`Strings.zh-CN.resx` 全部新键双语
- [ ] 3.4 新版本检测 → `ITrayService.ShowNotification` 单次提示（防重复轰炸）

## 4. 决策文档 & 验证

- [ ] 4.1 新 ADR：记录三级容灾架构取舍 + StarPie（MIT）出处注明
- [ ] 4.2 `scripts/dev.ps1 build` 0 警告 0 错误；`scripts/dev.ps1 test` 全量通过（新增测试并入基线）
- [ ] 4.3 人工 QA：真实网络下手动检查（正常路径 + 断网路径）；对 `Smith-Rosco/Pulsar` 打一个高于本地的测试 tag 验证 UpdateAvailable 全链路（QA checklist 文件留档）
