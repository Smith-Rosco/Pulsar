# Tasks — Repositioning Narrative Rollout

## 1. 插件显示名叙事对齐（先做：README 截图需用到新名称）

- [ ] 1.1 BookmarkletRunner `GetMetadata()`：DisplayName/Description 改为叙事对齐口径（网页脚本 / 老旧系统助手方向），Id 与配置键不动
- [ ] 1.2 `Strings.resx` / `Strings.zh-CN.resx` 同步插件相关标签；复核无硬编码用户可见字符串（`ILocalizationService` 纪律）
- [ ] 1.3 复核 WinSwitcher / VbaRunner / PkiPlugin / Command / SystemCommand 的描述是否符合三支柱叙事（只调措辞，不改语义）
- [ ] 1.4 全仓扫描旧显示名引用（`Docs/`、`PLUGIN_DEVELOPMENT.md`、`AGENTS.md` 等）并同步
- [ ] 1.5 补/更新 `plugin-display-identity` 相关测试；`scripts/dev.ps1 build` + `test` 全绿

## 2. 宣传截图（E2E 管线）

- [ ] 2.1 新增宣传用 E2E 工作流（fixture 注入真实感配置：办公场景 slot + 级联 + 渲染器预设），Dark 主题 1920×1080
- [ ] 2.2 产出首屏用图：主界面全景 / 轮盘呼出 / Excel 跑宏瞬间 / 窗口切换子菜单 ≥4 张
- [ ] 2.3 确定发布资产目录约定（如 `Docs/media/release/`）并入 git；确认与「UI 验证截图 never commit」纪律的边界并写入该目录 README

## 3. Demo 视频

- [ ] 3.1 三支脚本定稿（Excel 跑宏 / 老旧网页脚本注入 / 登录填表自动化；各 30–60s，分镜 + 台词）
- [ ] 3.2 录制 + 剪辑（录屏即可），输出 mp4（与 E2E recording.mp4 管线兼容）
- [ ] 3.3 视频与脚本入库（`Docs/media/release/videos/`）

## 4. README 重写

- [ ] 4.1 `README.md`（zh）首屏：定位语 + 三支柱场景 + 真实截图 + 与竞品差异化一句话（vs Quicker/Flow 话术取自重定位方案 §1.2）
- [ ] 4.2 `README_EN.md` 同步重写，中英叙事一致
- [ ] 4.3 自查：全文不再以「启动器」自居；「老旧系统」叙事不窄化（§9 反噬风险条款）；AI 愿景一句话 + roadmap 标注

## 5. 发布说明模板 & 验证

- [ ] 5.1 建立 `RELEASE_NOTES` 模板（版本、亮点、下载、系统要求占位）供 Change 5 使用
- [ ] 5.2 `scripts/dev.ps1 build` 0 警告 0 错误；`scripts/dev.ps1 test` 全量通过
- [ ] 5.3 journal 记录 + （如显示名语义有变）ADR 记录
