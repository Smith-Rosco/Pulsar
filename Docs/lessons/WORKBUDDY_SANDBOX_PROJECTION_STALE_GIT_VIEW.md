# WORKBUDDY_SANDBOX_PROJECTION_STALE_GIT_VIEW

> WorkBuddy 沙箱投影导致会话内 git 读到陈旧 `.git` 视图（2026-09-08 实锤案例）

## 症状

会话进程树内（WorkBuddy 的 Bash/PowerShell 工具，含 `dangerouslyDisableSandbox`）观察 Pulsar 仓库：

- `git fetch` 每次都报 `2101f1e..50bffea main -> origin/main` 更新成功，但 `git rev-parse refs/remotes/origin/main` 读回仍是旧值 `2101f1e`（09-03 的 v1.9.1），`git status` 显示 ahead 166（虚假）；
- `update-ref` exit 0 但读回旧值；`refs/remotes/origin/` 目录"消失"又"复活"（FileSystemWatcher 捕获周期性 Deleted/Created，连早已删除的 `probe*.lock` 都"复活"）；
- 同一会话内读值在 50bffea 与 2101f1e 之间跳变；
- `.git/refs` 上存在陌生 SID 的 DENY(W,D) ACL；`.git` 属主 SID 与当前用户不同（在 `.git` 内部跑 git 报 dubious ownership fatal；工作树根已被 safe.directory 覆盖）。

**磁盘真相（用户在原生 cmd 验证）**：`refs/remotes/origin/main = 50bffea`，`git status -sb` = ahead 4（正确），全部提交完好。**磁盘从未被回滚**。

## 根因

WorkBuddy 沙箱投影机制（`cli/vendor/sandbox/5.5.5/`，随 WorkBuddy 5.5.3）对工作区 `.git` 的**读取**可能命中陈旧投影快照：

- 会话内写入（未关沙箱）→ 进入投影层，按设计隔离（`--gc_retain_sec 600`）；
- `dangerouslyDisableSandbox` 写入 → 真实落盘（原生 cmd 已验证）；
- 会话内**读取** → 可能命中陈旧投影 → 旧值 / 目录缺失 / 假回滚；
- procmon 定责：回滚窗口内 `.git/refs` 上除 git.exe（探针本身）外**零写入/删除/改名**——还原不经过用户态文件操作，属过滤驱动/投影层行为。

## 排查中走过的弯路（引以为戒）

1. **MaxsunSync 冤案**：进程名含 "Sync" 即怀疑同步回滚，查其 config.json 后发现是铭瑄主板 RGB 灯效软件。教训：定责要靠 procmon，不靠名字联想。
2. **把视图层事件当真实删除**：FSW 捕获的 Deleted/Created 是投影层事件，磁盘未变。教训：目录"消失/复活"≠ 磁盘变化。
3. **会话内反复"修复"**：多次手写 loose ref，实际是把已正确的值重写。教训：所有排查工具都在 WorkBuddy 进程树内、共享同一套视图，**自己无法自证**。

## 正确的排查顺序（playbook）

1. **Ground truth 对照（5 秒，必做第一步）**：请用户在 WorkBuddy 之外的**原生终端**执行 `git status -sb` + `type <可疑文件>`，与会话内读值对比；
2. **procmon 定责**：过滤 `Path contains <workspace>\.git\refs`，看 Process Name——既能实锤也能洗清；
3. 最后才怀疑外部软件；文件监视器（FSW）事件只能证明"何时动了什么"，定责到进程必须 procmon。

## 规避措施

- **git 写操作（commit / push / fetch）默认走原生终端**；WorkBuddy 会话内只做只读查询，结果存疑时以外侧为准；
- 已向 WorkBuddy 反馈（反馈文本见下节）；
- 后续若 WorkBuddy 修复，验证方法：会话内 `git rev-parse refs/remotes/origin/main` 与原生终端对照。

## 给 WorkBuddy 的反馈文本（可直接粘贴）

> WorkBuddy 桌面版 5.5.3（Windows x64），内置沙箱组件 5.5.5（`resources\app.asar.unpacked\cli\vendor\sandbox\5.5.5\`）：会话进程树内对工作区 `.git` 目录的**读取**会命中陈旧的投影快照——磁盘上已更新的 refs（remote-tracking 引用为主）在会话内读到旧值。表现为：`git fetch` 报已更新但读回仍是旧值、`.git/refs` 子目录时有时无、FileSystemWatcher 捕获周期性 Deleted/Created（含已删除文件的"复活"）、ahead/behind 数乱跳。procmon（Sysinternals）证明回滚窗口内无任何用户态进程对 `.git/refs` 做写入/删除，还原发生在投影层。磁盘数据未损坏（原生终端验证完好），但会话内 git 状态展示与 AI 代理的 git 操作判断均被误导。建议：沙箱投影范围排除 `.git`，或修正投影层的读失效策略。复现：仓库内触发 `git fetch` 后在会话内 `git rev-parse` 与原生终端对照。

## 证据文件

- procmon 导出：`Docs/Logfile.CSV`（2026-09-08 22:35:53–22:37:10，已挪至 `artifacts/procmon-refs-20260908.csv`）
- 哨兵日志：`artifacts/refs-watch.log`
- 排查全程：`Docs/journal/2026-09-08.md`（含两次结论修正的诚实记录）
