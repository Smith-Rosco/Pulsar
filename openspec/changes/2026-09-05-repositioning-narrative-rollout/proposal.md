# Repositioning Narrative Rollout（M0 定位文案 + 演示物料）

## Why

09-03 重定位方案（`Docs/reports/2026-09-03-REPOSITIONING_PLAN.html`）已确认 Pulsar 定位从「生产力启动器」切换为「重度办公效率工作台 / 驯服老旧办公系统」，但对外触点尚未切换：README 首屏仍是启动器叙事且截图为占位符，BookmarkletRunner 的可视名称仍是开发者向术语，无 demo 视频。这是 M0（定位文案落地）+ M1 中演示物料部分的整体落地变更，是 M1 发布前的叙事前提。

## What Changes

- **README 首屏重写（中英双份）**：首屏 = 定位语（「一键跑宏、安全填表登录、快速切换窗口」）+ 三支柱场景（自动化领衔 / 凭据护城河 / 窗口打底）+ 真实截图；不再以「启动器」自居；叙事红线（不窄化为「只解决老问题」）贯穿。
- **真实截图产出**：复用 Pulsar.E2E 录屏/截图管线（`Pulsar/Pulsar.E2E/`，run `20260905-013738` 已验证）捕获主界面、轮盘、设置页等真实画面，替换 README 占位图；UI 验证截图不入 git 的既有纪律不适用于**对外展示资产**（发布专用目录单独约定）。
- **Demo 视频脚本与录制**：三支 30–60 秒场景片（Excel 跑宏 / 老旧网页脚本注入 / 登录填表自动化），录屏方式产出。
- **插件显示名叙事对齐**：BookmarkletRunner 可视名称/描述向「网页脚本 / 老旧系统助手」方向对齐（`GetMetadata()` DisplayName/Description + 本地化标签同步），WinSwitcher/VbaRunner/PkiPlugin 描述复核同一叙事口径。
- **发布说明模板**：建立 `RELEASE_NOTES` 模板（供 Change 5 的 Release 资产使用）。

## Capabilities

### New Capabilities

（无——README/截图/视频为文档与媒体资产，不构成系统能力。）

### Modified Capabilities

- `plugin-display-identity`: 新增「内置插件显示身份与办公自动化产品叙事对齐」要求——canonical 显示名/描述须用产品叙事语言（BookmarkletRunner 呈现为网页脚本/老旧系统入口，而非开发术语），并覆盖文档引用一致性场景。

## Impact

- **Affected code**:
  - `README.md` / `README_EN.md`（重写）。
  - `Pulsar/Pulsar/Plugins/Extensions/BookmarkletRunner/BookmarkletRunnerPlugin.cs`（`DisplayName`/`Description`）及其余内置插件 `GetMetadata()` 描述复核。
  - `Resources/Strings.resx` + `Strings.zh-CN.resx`（插件相关本地化标签，禁硬编码纪律不变）。
  - 插件文档（`Docs/architecture/PLUGIN_SYSTEM.md` 等引用处名称同步）。
- **New assets**: `assets/` 或 `Docs/media/` 发布专用截图目录（命名与入库范围在本变更 tasks 中定）、三支 demo 视频文件 + 脚本文档。
- **Compatibility**: 插件 Id 不变（`bookmarklet` 等），仅显示层变更；已保存配置不受影响。
- **Out of scope**: 安装器/自动更新/standalone 打包（Change 4）、用户手册与 GitHub Release 资产（Change 5）。
