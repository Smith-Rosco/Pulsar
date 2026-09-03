// publish-local / core.ts
// 仅保留 /publish 命令的参数解析（薄命令架构下唯一被 index.ts 引用的逻辑）。
// 历史上的纯逻辑实现（版本推断、notes 生成、zip 回退链、产物校验）已随
// 「黑盒 handler → skill + scripts/」重构迁移：
//   - 版本推断 / notes 生成 → .agents/skills/publish/scripts/Get-ReleaseInfo.ps1
//     与 AI 按 SKILL.md 第 3 节撰写
//   - zip 打包与回退链 → .agents/skills/publish/scripts/Pack-Zips.ps1
//   - 产物 / ZIP 校验 → Pulsar.Publish.Common.ps1 的 Assert-Publish / Assert-Zip
// 如需验证这些 PowerShell 逻辑，请直接运行对应脚本（参数见 SKILL.md）。
// 约束：仅使用「可擦除 TypeScript 语法」，兼容 node 原生 type stripping。

export interface ParsedArgs {
  version?: string;
  gh: boolean;
  ghOnly: boolean;
}

export function parseArgs(raw: string | undefined): ParsedArgs {
  const tokens = (raw ?? "").trim().split(/\s+/).filter(Boolean);
  const out: ParsedArgs = { version: undefined, gh: false, ghOnly: false };
  for (const t of tokens) {
    if (t === "gh" || t === "--gh") out.gh = true;
    else if (t === "gh-only" || t === "--gh-only") out.ghOnly = true;
    else if (out.version === undefined) out.version = t;
  }
  if (out.ghOnly) out.gh = true;
  return out;
}
