// publish-local / index.ts
// 注册 /publish 命令与 publish_local 工具（LLM 可调用）。
// 流程: 版本决策 → 步骤确认 → 构建 → 校验 → 打包 → (可选) commit+tag → (可选) GitHub Release
// GitHub 发布默认不勾选（gh 参数显式开启）。

import type { ExtensionAPI, ExtensionCommandContext } from "@earendil-works/pi-coding-agent";
import type { AutocompleteItem } from "@earendil-works/pi-tui";
import { Type } from "typebox";
import * as fs from "node:fs";
import * as os from "node:os";
import * as path from "node:path";
import {
  SEMVER_RE,
  incrementVersion,
  parseArgs,
  readCsprojVersions,
  applyCsprojVersion,
  restoreFile,
  findRepoRoot,
  run,
  lastTag,
  gitLogRange,
  inferNextVersion,
  buildNotes,
  verifyPublishDir,
  verifyZipEntries,
  zipDirContents,
  remoteOwnerRepo,
  lastLine,
  type RunResult,
  type ParsedArgs,
} from "./core";

const WIDGET_ID = "publish-local";

function tail(r: RunResult, n = 6): string {
  return r.lastLines.slice(-n).join("\n");
}

export default function (pi: ExtensionAPI): void {
  pi.registerCommand("publish", {
    description:
      "发布 Pulsar 本地版本（构建+打包 zip），可选发布到 GitHub。用法: /publish [patch|minor|major|1.x.y] [gh] [gh-only]",
    getArgumentCompletions: (prefix: string): AutocompleteItem[] | null => {
      const items: AutocompleteItem[] = ["patch", "minor", "major", "gh", "gh-only"].map((v) => ({
        value: v,
        label: v,
      }));
      const filtered = prefix ? items.filter((i) => i.value.startsWith(prefix.toLowerCase())) : items;
      return filtered.length > 0 ? filtered : null;
    },
    handler: async (args, ctx) => {
      await runPublish(ctx, parseArgs(args));
    },
  });

  pi.registerTool({
    name: "publish_local",
    label: "Publish Local Release",
    description:
      "发布 Pulsar 本地版本：更新版本号、dotnet publish、校验产物、打包 Artifacts zip；可选发布到 GitHub。所有破坏性步骤会向用户确认。",
    promptSnippet: "Publish a local Pulsar release (build + zip), optionally to GitHub",
    promptGuidelines: [
      "Use publish_local when the user asks to 发布本地版本 / publish a release / 打包发布. It queues /publish as a follow-up command; interactive confirmations remain with the user.",
      "Pass github: true only when the user explicitly asks to publish to GitHub (requires gh CLI). Default is local-only.",
    ],
    parameters: Type.Object({
      version: Type.Optional(
        Type.String({ description: '目标版本 "1.6.0" 或 bump 类型 "patch" | "minor" | "major"；省略则自动推断' }),
      ),
      github: Type.Optional(Type.Boolean({ description: "同时发布到 GitHub（默认 false；需要 gh CLI）" })),
      githubOnly: Type.Optional(
        Type.Boolean({ description: "跳过构建，用现有 Artifacts zip 直接发布到 GitHub（默认 false）" }),
      ),
    }),
    async execute(_toolCallId, params, _signal, _onUpdate, _ctx) {
      const parts: string[] = [];
      if (params.version) parts.push(params.version);
      if (params.githubOnly) parts.push("gh-only");
      else if (params.github) parts.push("gh");
      const cmd = `/publish${parts.length > 0 ? " " + parts.join(" ") : ""}`;
      pi.sendUserMessage(cmd, { deliverAs: "followUp", expandPromptTemplates: true });
      return {
        content: [{ type: "text", text: `已排队执行 ${cmd}。后续步骤确认会直接询问你。` }],
        details: { command: cmd },
      };
    },
  });
}

async function runPublish(ctx: ExtensionCommandContext, parsed: ParsedArgs): Promise<void> {
  const ui = ctx.ui;
  if (!ctx.hasUI) {
    ui.notify("publish 命令需要交互模式（TUI 或 RPC）", "error");
    return;
  }

  const repoRoot = findRepoRoot(ctx.cwd);
  if (!repoRoot) {
    ui.notify("未找到 Pulsar 仓库（从当前目录向上未发现 Pulsar/Pulsar/Pulsar.csproj）", "error");
    return;
  }

  const csprojPath = path.join(repoRoot, "Pulsar", "Pulsar", "Pulsar.csproj");
  let current: Record<string, string>;
  try {
    current = readCsprojVersions(csprojPath);
  } catch (err) {
    ui.notify(`读取版本失败: ${err instanceof Error ? err.message : String(err)}`, "error");
    return;
  }
  const currentV = current.Version;

  // ---- 1. 目标版本 ----
  let target: string;
  if (parsed.version) {
    const v = parsed.version;
    if (!SEMVER_RE.test(v)) {
      if (v === "patch" || v === "minor" || v === "major") {
        target = incrementVersion(currentV, v);
      } else {
        ui.notify(`无法识别的版本参数 "${v}"（支持 x.y.z / patch / minor / major）`, "error");
        return;
      }
    } else {
      target = v;
    }
  } else {
    const suggestion = await inferNextVersion(repoRoot, currentV);
    const answer = await ui.input(
      `当前版本 ${currentV}。${suggestion.reason}\n建议: ${suggestion.version}（直接回车使用建议值）`,
      suggestion.version,
    );
    if (answer === undefined) {
      ui.notify("已取消", "info");
      return;
    }
    target = answer.trim() || suggestion.version;
    if (!SEMVER_RE.test(target)) {
      ui.notify(`无效版本号 "${target}"`, "error");
      return;
    }
  }

  const artifacts = path.join(repoRoot, "Artifacts");
  const publishDir = path.join(artifacts, "publish", `v${target}`);
  const zipPath = path.join(artifacts, `Pulsar-v${target}.zip`);
  const zipExists = fs.existsSync(zipPath);
  const dirExists = fs.existsSync(publishDir);

  // ---- 2. gh 可用性（GitHub 发布默认不勾选，gh 参数显式开启）----
  let gh = parsed.gh;
  if (gh) {
    const g = await run("gh", ["--version"], { cwd: repoRoot });
    if (g.code !== 0) {
      gh = false;
      const cont = await ui.confirm(
        "gh CLI 未安装",
        "GitHub 发布需要 gh CLI。\n安装: winget install GitHub.cli  然后: gh auth login\n是否跳过 GitHub 发布，仅执行本地发布？",
      );
      if (!cont) {
        ui.notify("已取消", "info");
        return;
      }
      ui.notify("已跳过 GitHub 发布", "warning");
    }
  }

  // ---- 3. 步骤确认 ----
  const versionChanged = target !== currentV;
  const doBump = versionChanged
    ? await ui.confirm(
        "更新版本",
        `更新 ${path.basename(csprojPath)}: ${currentV} → ${target}\n（Version / FileVersion / AssemblyVersion 三处统一）`,
      )
    : true;
  if (versionChanged && !doBump) {
    ui.notify("已取消：未更新版本", "info");
    return;
  }

  const doBuild = parsed.ghOnly
    ? false
    : await ui.confirm(
        "执行发布构建",
        `dotnet publish → ${publishDir}${dirExists ? "\n⚠ 该目录已存在，将被清空。" : ""}`,
      );
  if (!parsed.ghOnly && !doBuild) {
    ui.notify("已取消", "info");
    return;
  }

  const doZip = parsed.ghOnly
    ? false
    : await ui.confirm("打包 zip", `生成并校验 ${zipPath}${zipExists ? "\n⚠ 该 zip 已存在，将被覆盖。" : ""}`);
  if (!parsed.ghOnly && !doZip) {
    ui.notify("已取消", "info");
    return;
  }

  let doGithub = false;
  if (gh) {
    const br = await run("git", ["branch", "--show-current"], { cwd: repoRoot });
    const branch = br.code === 0 && lastLine(br) !== "" ? lastLine(br) : "(detached HEAD)";
    doGithub = await ui.confirm(
      "发布到 GitHub",
      `创建 GitHub Release v${target}（上传 ${zipPath}）\n并推送 tag v${target} 与当前分支 ${branch} 到 origin。\nRelease notes 将在下一步编写。`,
    );
  }

  let doCommitTag = doGithub
    ? true
    : await ui.confirm(
        "git commit + tag",
        `创建提交 "chore: bump version to ${target}" 并打 annotated tag v${target}（不推送）?`,
      );

  // ---- 4. 执行 ----
  ui.setStatus(WIDGET_ID, `publish v${target}`);
  let lastWidget = 0;
  const widget = (phase: string, lines?: string[]): void => {
    ui.setWidget(WIDGET_ID, [phase, ...(lines ?? []).slice(-9)]);
  };
  const throttledRecent = (recent: string[]): void => {
    const now = Date.now();
    if (now - lastWidget > 200) {
      lastWidget = now;
      widget("执行中…", recent);
    }
  };

  let csprojBackup: string | null = null;
  const restoreCsproj = (): void => {
    if (csprojBackup !== null) {
      restoreFile(csprojPath, csprojBackup);
      csprojBackup = null;
    }
  };
  const fail = (msg: string): void => {
    restoreCsproj();
    ui.setWidget(WIDGET_ID, undefined);
    ui.setStatus(WIDGET_ID, undefined);
    ui.notify(msg, "error");
  };

  let notesFilePath: string | null = null;
  try {
    // 4.1 bump 版本
    if (versionChanged && doBump) {
      csprojBackup = applyCsprojVersion(csprojPath, target);
      widget("✓ 已更新版本号");
    }

    // 4.2 构建
    if (doBuild) {
      fs.rmSync(publishDir, { recursive: true, force: true });
      fs.mkdirSync(publishDir, { recursive: true });
      widget("dotnet publish …");
      const build = await run(
        "dotnet",
        [
          "publish",
          "Pulsar/Pulsar/Pulsar.csproj",
          "-c",
          "Release",
          "-r",
          "win-x64",
          "--self-contained",
          "true",
          "-p:PublishSingleFile=true",
          "-p:PublishReadyToRun=true",
          `-p:PublishDir=${publishDir}\\`,
        ],
        { cwd: repoRoot, maxLines: 80, signal: ctx.signal, onRecent: throttledRecent },
      );
      if (build.killed) {
        fail("已取消：dotnet publish 被中断");
        return;
      }
      if (build.code !== 0) {
        fail(`dotnet publish 失败 (exit ${build.code})\n${tail(build)}`);
        return;
      }

      const missing = verifyPublishDir(publishDir);
      if (missing.length > 0) {
        fail(`发布产物校验失败，缺少: ${missing.join(", ")}`);
        return;
      }
      widget("✓ 产物校验通过");
    }

    // 4.3 打包
    if (doZip) {
      widget("Compress-Archive …");
      const z = await zipDirContents(repoRoot, publishDir, zipPath, throttledRecent);
      if (z.code !== 0) {
        fail(`打包失败 (exit ${z.code})\n${tail(z)}`);
        return;
      }
      const zMissing = await verifyZipEntries(zipPath);
      if (zMissing.length > 0) {
        fail(`zip 校验失败，缺少: ${zMissing.join(", ")}`);
        return;
      }
      widget("✓ zip 已生成并校验");
    }

    // 4.4 gh-only 前置检查
    if (parsed.ghOnly && !fs.existsSync(zipPath)) {
      fail(`缺少 ${zipPath}，无法执行 gh-only 发布`);
      return;
    }

    // 4.5 release notes
    if (doGithub || doCommitTag) {
      const from = await lastTag(repoRoot);
      const lines = await gitLogRange(repoRoot, from);
      const draft = buildNotes(lines, target);
      const edited = await ui.editor(`Release notes / tag message (v${target})`, draft);
      if (edited === undefined) {
        ui.notify("已取消 commit/tag/GitHub 步骤（本地产物已就绪）", "warning");
        doGithub = false;
        doCommitTag = false;
      } else {
        notesFilePath = path.join(os.tmpdir(), `pulsar-release-notes-v${target}.md`);
        fs.writeFileSync(notesFilePath, edited, "utf8");
        widget("✓ Release notes 已保存");
      }
    }

    // 4.6 commit + tag（GitHub 发布的前置步骤，此时失败会回滚）
    if (doCommitTag && notesFilePath !== null) {
      widget("git commit + tag …");
      await run("git", ["add", "--", "Pulsar/Pulsar/Pulsar.csproj"], { cwd: repoRoot });
      const commit = await run(
        "git",
        ["commit", "-m", `chore: bump version to ${target}`, "--", "Pulsar/Pulsar/Pulsar.csproj"],
        { cwd: repoRoot },
      );
      const commitOut = tail(commit, 4);
      if (commit.code !== 0 && !/nothing to commit/i.test(commitOut)) {
        ui.notify(`git commit 失败:\n${commitOut}`, "error");
        doGithub = false;
        doCommitTag = false;
      } else {
        const tag = await run("git", ["tag", "-a", `v${target}`, "-F", notesFilePath], { cwd: repoRoot });
        const tagOut = tail(tag, 4);
        if (tag.code !== 0 && !/already exists/i.test(tagOut)) {
          ui.notify(`git tag 失败:\n${tagOut}`, "error");
          doGithub = false;
        } else {
          widget("✓ commit + tag 完成");
        }
      }
    }

    // 4.7 GitHub Release
    if (doGithub && notesFilePath !== null) {
      widget("gh release create …");
      const rel = await run(
        "gh",
        ["release", "create", `v${target}`, zipPath, "--title", `Pulsar v${target}`, "--notes-file", notesFilePath],
        { cwd: repoRoot, maxLines: 40, onRecent: throttledRecent },
      );
      if (rel.code !== 0) {
        ui.notify(`gh release create 失败:\n${tail(rel, 8)}`, "error");
      } else {
        const p1 = await run("git", ["push", "origin", `v${target}`], { cwd: repoRoot });
        if (p1.code !== 0) ui.notify(`tag 推送失败:\n${tail(p1, 4)}`, "warning");
        const p2 = await run("git", ["push", "origin", "HEAD"], { cwd: repoRoot });
        if (p2.code !== 0) ui.notify(`分支推送失败:\n${tail(p2, 4)}`, "warning");
        widget("✓ GitHub Release 已创建");
      }
    }

    // 4.8 摘要
    ui.setWidget(WIDGET_ID, undefined);
    ui.setStatus(WIDGET_ID, undefined);
    let sizeMb = 0;
    try {
      if (fs.existsSync(zipPath)) sizeMb = Math.round(fs.statSync(zipPath).size / 1024 / 1024);
    } catch {
      /* 忽略 */
    }
    const ownerRepo = await remoteOwnerRepo(repoRoot);
    const lines: string[] = [`✅ Pulsar v${target} 发布完成`];
    if (fs.existsSync(zipPath)) lines.push(`  产物: ${zipPath} (${sizeMb} MB)`);
    if (doBuild) lines.push(`  目录: ${publishDir}`);
    if (doCommitTag) lines.push(`  git: commit + tag v${target}`);
    if (doGithub) {
      const url = ownerRepo ? `https://github.com/${ownerRepo}/releases/tag/v${target}` : "(GitHub Release 已创建)";
      lines.push(`  GitHub: ${url}`);
    }
    ui.notify(lines.join("\n"), "info");
  } catch (err) {
    fail(`发布中断: ${err instanceof Error ? err.message : String(err)}`);
  } finally {
    if (notesFilePath !== null) fs.rmSync(notesFilePath, { force: true });
  }
}
