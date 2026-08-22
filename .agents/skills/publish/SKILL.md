---
name: publish
description: Pulsar 发布/打包流程（版本决策→构建→校验→打包 zip→release notes→commit/tag→GitHub Release）。Use when the user says 发布/打包/publish a release/发到 GitHub，或运行 /publish 命令后。按本 skill 的流程逐步执行，每步用 bash 执行并展示结果，失败先排障再重试。
---

# Pulsar 发布（Release）流程

发布 Pulsar 的完整流程。所有命令在仓库根目录执行（`git rev-parse --show-toplevel`）。**每一步用 bash 执行并展示结果**；失败时按「排障」节处理，修复后从失败步骤重试，不要重复已成功的步骤。

## 流程总览

| # | 步骤 | 说明 |
|---|------|------|
| 1 | 版本决策 | 询问用户确认（AI 建议 + 提交统计） |
| 2 | 构建 | `dotnet publish`（Release / win-x64 / self-contained 单文件） |
| 3 | 校验产物 | Pulsar.exe / Pulsar.pdb / `*_cor3.dll` / Assets/ |
| 4 | 打包 zip | **用 pwsh**（见坑 1） |
| 5 | 校验 zip | PK 魔数 + 条目 |
| 6 | Release notes | AI 撰写中文 notes，**展示给用户确认** |
| 7 | commit + tag | `chore: bump version to X.Y.Z`（本地发布可选） |
| 8 | GitHub Release | **先 push tag**（见坑 2、3） |

**gh-only 模式**：跳过 2-7，只执行第 8 步（用现有 zip 补发远端）。先确认 `Artifacts/Pulsar-v{ver}.zip` 与 csproj 版本一致、本地 tag 存在。

## 1. 版本决策

- 用户给了显式版本（如 `/publish 1.6.1`）→ 直接使用
- 否则：`grep -E "<Version>" Pulsar/Pulsar/Pulsar.csproj` 取当前版本；`git tag --sort=-creatordate | head -1` 取上次 tag；`git log --no-merges --pretty=format:%s <lastTag>..HEAD` 统计提交（剔除 `chore: bump version`）
  - 含 `feat` 提交 → minor；含 `fix` → patch；否则保守 patch
- **向用户展示建议版本和依据，确认后再继续**；用户指定版本时跳过

## 2. 构建

```bash
# 清空目标目录
rm -rf "Artifacts/publish/v{ver}" && mkdir -p "Artifacts/publish/v{ver}"
dotnet publish Pulsar/Pulsar/Pulsar.csproj -c Release -r win-x64 --self-contained true \
  -p:PublishSingleFile=true -p:PublishReadyToRun=true \
  "-p:PublishDir=Artifacts\publish\v{ver}\"
```

## 3. 校验产物

`Artifacts/publish/v{ver}/` 下必须存在：`Pulsar.exe`、`Pulsar.pdb`、至少一个 `*_cor3.dll`（缺失则自包含应用无法启动）、`Assets/`。

## 4. 打包 zip — 坑 1（PowerShell 5.1 Compress-Archive 失效）

**不要**直接调 `powershell` 的 `Compress-Archive`：进程 PSModulePath 被 PS7 目录污染时，5.1 加载 PS7 的模块副本被执行策略拦截 → `CommandNotFoundException`。

按顺序尝试（成功后即止）：

```bash
# 首选 pwsh（PowerShell 7，模块在自己目录）
pwsh -NoProfile -Command "Compress-Archive -Path 'Artifacts\publish\v{ver}\*' -DestinationPath 'Artifacts\Pulsar-v{ver}.zip' -CompressionLevel Optimal -Force"
# 备选 5.1 + Bypass
powershell -NoProfile -ExecutionPolicy Bypass -Command "Compress-Archive -Path 'Artifacts\publish\v{ver}\*' -DestinationPath 'Artifacts\Pulsar-v{ver}.zip' -CompressionLevel Optimal -Force"
# 兜底：System32 bsdtar（注意：PATH 里的 tar 是 Git 的 GNU tar，只会生成假 .zip，勿用）
"C:\Windows\System32\tar.exe" -a -c -f "Artifacts/Pulsar-v{ver}.zip" -C "Artifacts/publish/v{ver}" <目录条目>
```

详见 `Docs/lessons/POWERSHELL_5_1_COMPRESS_ARCHIVE_BROKEN.md`。

## 5. 校验 zip

- 魔数：`xxd Artifacts/Pulsar-v{ver}.zip | head -1` 必须 `504b`（PK）
- 条目：`"C:\Windows\System32\tar.exe" -tf Artifacts/Pulsar-v{ver}.zip` 应含 `Pulsar.exe`、`Pulsar.pdb`、`Assets/`、`*_cor3.dll`

## 6. Release notes

用简洁中文撰写（面向最终用户），章节固定为 `### 新功能` / `### 修复` / `### 性能优化` / `### 其他`（无内容章节省略），每条 `- ` 一行，基于 git 提交提炼用户可感知的变化，剔除 bump/纯内部重构/文档噪音。**展示给用户确认**，确认后再用于 tag message 和 GitHub Release。

## 7. commit + tag（本地发布；gh-only 跳过）

```bash
git add -- Pulsar/Pulsar/Pulsar.csproj
git commit -m "chore: bump version to {ver}" -- Pulsar/Pulsar/Pulsar.csproj   # "nothing to commit" 则跳过
git tag -a v{ver} -m "{notes}"                                                # 已存在则跳过
```

## 8. GitHub Release — 坑 2（顺序）与坑 3（# 路径）

**顺序**：`gh release create` 要求 tag 已推送到远端，否则报
`tag v{ver} exists locally but has not been pushed...`。**必须先生成并推送 tag**（第 7 步完成或 tag 已存在时直接推）：

```bash
# gh 不在 PATH 时用完整路径："/c/Program Files/GitHub CLI/gh.exe"（或 C:\Program Files\GitHub CLI\gh.exe）
git push origin v{ver}   # ← 先推 tag！
```

**# 路径坑**：仓库路径含 `#`（如 `E:\8_Project\10_C#\Pulsar_Project`）时，gh CLI 把路径在 `#` 处截断（URL fragment 解析）→ `GetFileAttributesEx E:\8_Project\10_C: ...`。**zip 和 notes 必须复制到无 # 的临时目录再上传**：

```bash
mkdir -p "$TMP/pulsar-upload"
cp "Artifacts/Pulsar-v{ver}.zip" "$TMP/pulsar-upload/"
# notes 写入 $TMP/pulsar-upload/notes.md（第 6 步确认后的内容）
gh release create "v{ver}" "$TMP/pulsar-upload/Pulsar-v{ver}.zip" \
  --title "Pulsar v{ver}" --notes-file "$TMP/pulsar-upload/notes.md"
git push origin HEAD   # 最后推分支
```

详见 `Docs/lessons/GH_CLI_HASH_PATH_BUG.md`。

## 9. 排障

- **失败后先读完整错误输出**，再查状态：
  - 远端：`gh release list`、`git ls-remote --tags origin`
  - 本地：`git status`、`git tag`、`git log --oneline -3`、`ls Artifacts/`
- 已知坑：`Docs/lessons/POWERSHELL_5_1_COMPRESS_ARCHIVE_BROKEN.md`、`Docs/lessons/GH_CLI_HASH_PATH_BUG.md`
- 常见情况：
  - `tag exists locally but has not been pushed` → 先 `git push origin v{ver}`
  - `GetFileAttributesEx <截断路径>` → gh 的 # 坑，走临时目录
  - `CommandNotFoundException` → zip 的 PowerShell 坑，换 pwsh
  - release 已存在 → 用 `gh release edit` 更新或 `gh release delete` 后重建（询问用户）
- 扩展遗留的旧流程逻辑在 `.pi/extensions/publish-local/core.ts`（可参考/复用，smoke 测试：`node .pi/extensions/publish-local/smoke.ts`）

## 10. 完成报告

结束时向用户报告：产物路径与大小、zip 校验结果、commit/tag、GitHub Release 链接、推送状态。若有跳过/降级（如 gh 未登录、无 tag），明确说明。
