// publish-local / index.ts
// /publish 是薄命令：不黑盒执行流程，而是把发布任务注入会话，由 AI 按
// .agents/skills/publish/SKILL.md 的流程执行（用 bash 工具逐步完成）。
// 好处：执行过程全程在会话上下文中可见，遇到问题 AI 可自行排障修复。

import type { ExtensionAPI } from "@earendil-works/pi-coding-agent";
import type { AutocompleteItem } from "@earendil-works/pi-tui";
import { Type } from "typebox";
import { parseArgs } from "./core";

export default function (pi: ExtensionAPI): void {
  pi.registerCommand("publish", {
    description:
      "发布 Pulsar 版本：AI 按 publish skill 执行（版本建议→构建→打包→notes→commit/tag→GitHub）。用法: /publish [patch|minor|major|1.x.y] [gh] [gh-only]",
    getArgumentCompletions: (prefix: string): AutocompleteItem[] | null => {
      const items: AutocompleteItem[] = ["patch", "minor", "major", "gh", "gh-only"].map((v) => ({
        value: v,
        label: v,
      }));
      const filtered = prefix ? items.filter((i) => i.value.startsWith(prefix.toLowerCase())) : items;
      return filtered.length > 0 ? filtered : null;
    },
    handler: async (args, ctx) => {
      const parsed = parseArgs(args);
      const parts: string[] = [];
      if (parsed.version) parts.push(`目标版本 ${parsed.version}`);
      if (parsed.ghOnly) parts.push("gh-only：仅补发 GitHub（跳过构建/打包/commit/tag，使用现有 zip）");
      else if (parsed.gh) parts.push("同时发布到 GitHub");
      else parts.push("本地发布");
      const instruction = [
        `请按 publish skill 执行 Pulsar 发布：${parts.join("；")}。`,
        "流程、已知坑位与排障指引见仓库根目录 .agents/skills/publish/SKILL.md。",
        "执行要点：",
        "- 关键步骤（版本号、GitHub 发布）先询问用户确认；",
        "- 每一步用 bash 执行并展示结果；失败先排障修复，再从未完成步骤重试；",
        "- release notes 用 AI 撰写，展示给用户确认后再使用。",
      ].join("\n");
      pi.sendUserMessage(instruction, { deliverAs: "followUp", expandPromptTemplates: true });
      ctx.ui.notify("已交给 AI 执行发布流程（过程在会话中可见）", "info");
    },
  });

  pi.registerTool({
    name: "publish_local",
    label: "Publish Local Release",
    description:
      "发布 Pulsar 本地版本：更新版本号、dotnet publish、校验产物、打包 Artifacts zip；可选发布到 GitHub。所有破坏性步骤会向用户确认。",
    promptSnippet: "Publish a local Pulsar release (build + zip), optionally to GitHub",
    promptGuidelines: [
      "Use publish_local when the user asks to 发布本地版本 / publish a release / 打包发布. It queues /publish as a follow-up command; the AI then runs the publish skill (visible in the session) and interactive confirmations remain with the user.",
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
        content: [{ type: "text", text: `已排队执行 ${cmd}。AI 将按 publish skill 执行，关键步骤会询问你确认。` }],
        details: { command: cmd },
      };
    },
  });
}
