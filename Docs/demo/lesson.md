# Pulsar Demo 开发教训（lesson.md）

> 汇总 `Documents\Pulsar\Demo` 演示环境开发过程中踩过的坑。
> 格式沿用 [Docs/lessons/](../lessons/) 约定：症状 → 根因 → 修复。
> 分镜执行层面的细节以 [RECORDING-GUIDE.md](C:\Users\milo\Documents\Pulsar\Demo\RECORDING-GUIDE.md) 为准（§8 常见问题 / §9 / §10 修订清单）。
>
> 最近更新：2026-09-07（UX 重构后）

---

## 1. HTML 资产被写成乱码（mojibake）

- **症状**：`mail-login.html` / `expense-login.html` 打开全是 `鐧诲綍鎴愬姛` 类乱码，且两页 `<title>` 都错成「OA 协同办公系统」。
- **根因**：文件在某次编辑中被按错误编码读写（GBK 内容被当 UTF-8 保存 / 双重编码），且从 OA 页复制后只改了文件名没改内容，坏字节被静默传播。
- **修复**：整页重写。**HTML 一律 UTF-8 无 BOM**，文件头 `<meta charset="UTF-8">`；任何资产改动后必须回读一遍中文内容自检。复制文件改名 ≠ 复制完成，内容要逐页核对。

## 2. `pki/fill` 是纯键盘注入，焦点即生死

- **症状**：滑轮盘执行「登录」槽位后什么都没填进去。
- **根因**：`pki/fill` 走 SendKeys（`账号 → TAB → 密码 → ENTER`），不定位元素；光标不在用户名框里就全部打空。
- **修复**：
  1. 登录页 `<input id="username">` 加 **`autofocus`**，从根上兜底（呼出轮盘、切标签页后焦点仍在框内）；
  2. 手动补点一次输入框作为保险动作；
  3. **ENTER 必须能提交**：用真 `<form>` + `type="submit"` 按钮 + `onsubmit` 处理，不能只靠按钮 `onclick`（SendKeys 的 ENTER 在输入框里触发的是表单隐式提交）。

## 3. 配音 WAV 头是流式占位符，ffprobe 读出 7.5 小时

- **症状**：`ffprobe` 报时长约 7.5 小时，剪映/合成器时长错乱。
- **根因**：TTS 导出的 WAV `data` chunk size 写的是 `0xFFFFFFFF`（流式占位），多数工具不校验、个别工具直接当真。
- **修复**：`fix-voiceover.py` 重写 RIFF/data 长度后输出到 `voiceover\fixed\`；原始文件只留档不再使用。

## 4. Pulsar 槽位参数不做环境变量展开

- **症状**：槽位参数里写 `%USERPROFILE%\...\report-data.xlsx`，执行时找不到文件。
- **根因**：Pulsar 槽位参数按字面量处理，`%VAR%` 不展开。
- **修复**：`demo-profile.ps1 switch` 注入配置时先把 `%USERPROFILE%` 替换为绝对路径。

## 5. Excel VBA 注入三连坑

- **`.bas` 首行**：带 `Attribute VB_Name` 行则注入编译失败——导出/手写 `.bas` 时删掉首行。
- **类型判断顺序**：VBA 的 `IsDate("1,200") = True`（带千分位的数字被当日期）。必须先剥逗号判 `IsNumeric` 再判 `IsDate`，顺序不能反；且必须**先推断列类型、后清样式**（`NumberFormat` 清成 General 后 COM 把日期单元格返回为 Double，`IsDate(46255) = False`）。
- **环境前提**：信任中心「信任对 VBA 工程对象模型的访问」需开启（`AccessVBOM=1`）；录制前检查清单里单列一条。

## 6. 录屏可见性：小字 = 白录

- **症状**：录完回看，状态栏 12px 的变化完全看不见；光标根本不出现，看不出「滑」。
- **根因**：1080p 全屏录制下页面元素 12px 只剩几个像素；OBS 默认不采集光标。
- **修复**：状态栏字号提到 18px、加中部日志面板；OBS 侧开「显示指针轨迹」+ 黑色特大指针方案；**正式录制前先录 5 秒测试片确认光标可见**。

## 7. 配音定稿后改分镜：时间轴不动、画面重映射

- **症状**：UX 重构要删掉全部设置页展示分镜，但配音/字幕已定稿，怕声画错位。
- **根因**：字幕 .srt 是按分镜切的时间轴，动了分镜不动台词就会对不上。
- **修复**：逐段台词找「画面等价物」做映射，不动时间轴。例：「配置只要三步：宏名、工作簿、方位」→ 轮盘三要素特写（槽位标签=宏名、背后窗口=工作簿、方位=方位）；「绑到轮盘上」→ 槽位悬停特写。**先锁台词再排画面**，不要反过来。

## 8. 重构演示页必须保住脚本 DOM 契约

- **症状**：改版页面后 Pulsar 注入脚本全部失灵。
- **根因**：`legacy-report.js` / `legacy-login.js` / `pki/fill` 靠固定选择器与键序工作（`#login / #query / #export / #username / #password`、TAB 顺序、Enter 提交），换皮不改约定就断。
- **修复**：重构前先列出契约清单（选择器、TAB 焦点链、Enter 行为、`id` 列表），重构后 grep 逐项回验。本次 legacy-portal v3 与三登录页 v2 均零契约变更。

## 9. blob 下载伪装 `.xls` 会触发 Chrome 确认条

- **症状**：用 `Blob` + HTML 表格伪装 `.xls` 下载时，Chrome 偶尔弹「此文件类型可能有害」确认条，录屏多一次点击。
- **根因**：Chrome 对部分来自 blob/file:// 的文档类扩展名走启发式告警。
- **修复**：改用 **CSV + UTF-8 BOM**（`\ufeff` 前缀），Excel 双击即开、中文不乱码、从不触发告警；文件名按查询区间取月份（`经营报表_YYYYMM.csv`）。真实下载栏入镜反而比状态栏摆拍更有说服力。

## 10. Bookmarklet 注入后立即返回，动作要拉开节奏

- **症状**：四步同步点完，录屏里只有「结果瞬间出现」，看不出流程。
- **根因**：`BookmarkletRunner` 注入后立即返回，同步执行无过程感。
- **修复**：`legacy-report.js` 用 `setTimeout` 分步（0 / 400 / 900ms），每步都有可见状态变化，正好对应配音「呼出、滑动、松开——三秒，流程跑完」。

---

## 环境备注（Windows / 工具链）

- **curl 下载 SkillHub 资产**：schannel 报 `CRYPT_E_REVOCATION_OFFLINE`，需加 `--ssl-no-revoke`。
- **`dotnet build` / 长命令**：沿用仓库既有约定（WorkBuddy bash + env 前缀），见 `Docs/lessons/` 与用户级记忆。
