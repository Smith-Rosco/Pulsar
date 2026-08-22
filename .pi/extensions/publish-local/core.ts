// publish-local / core.ts
// 发布流程核心逻辑（纯 Node，无 pi 依赖，可独立冒烟测试）。
// 约束：仅使用「可擦除 TypeScript 语法」（无 enum / namespace / 参数属性），
// 以兼容 node --experimental-strip-types 直接运行。

import { spawn } from "node:child_process";
import * as fs from "node:fs";
import * as path from "node:path";

// ---------- 版本 ----------

export const SEMVER_RE = /^(\d+)\.(\d+)\.(\d+)(?:-[0-9A-Za-z.-]+)?$/;

export type BumpKind = "patch" | "minor" | "major";

export function incrementVersion(v: string, which: BumpKind): string {
  const m = v.match(SEMVER_RE);
  if (!m) throw new Error(`无法解析版本号: ${v}`);
  const parts = [Number(m[1]), Number(m[2]), Number(m[3])];
  if (which === "patch") parts[2] += 1;
  else if (which === "minor") {
    parts[1] += 1;
    parts[2] = 0;
  } else {
    parts[0] += 1;
    parts[1] = 0;
    parts[2] = 0;
  }
  return parts.join(".");
}

// ---------- 命令行参数 ----------

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

// ---------- csproj ----------

export const VERSION_TAGS = ["Version", "FileVersion", "AssemblyVersion"] as const;

export function readCsprojVersions(csprojPath: string): Record<string, string> {
  const xml = fs.readFileSync(csprojPath, "utf8");
  const out: Record<string, string> = {};
  for (const tag of VERSION_TAGS) {
    const m = xml.match(new RegExp(`<${tag}>([^<]+)</${tag}>`));
    if (!m) throw new Error(`csproj 中缺少 <${tag}> 标签: ${csprojPath}`);
    out[tag] = m[1];
  }
  return out;
}

// 按仓库惯例: Version = x.y.z, FileVersion / AssemblyVersion = x.y.z.0
export function applyCsprojVersion(csprojPath: string, version: string): string {
  const xml = fs.readFileSync(csprojPath, "utf8");
  const replacements: Array<[string, string]> = [
    ["Version", version],
    ["FileVersion", `${version}.0`],
    ["AssemblyVersion", `${version}.0`],
  ];
  let next = xml;
  for (const [tag, value] of replacements) {
    next = next.replace(new RegExp(`(<${tag}>)[^<]*(</${tag}>)`), `$1${value}$2`);
  }
  if (next === xml) throw new Error("csproj 版本标签未变化（版本号相同）");
  fs.writeFileSync(csprojPath, next, "utf8");
  return xml;
}

export function restoreFile(filePath: string, content: string): void {
  fs.writeFileSync(filePath, content, "utf8");
}

export function findRepoRoot(start: string): string | null {
  let dir = path.resolve(start);
  for (let i = 0; i < 8; i++) {
    if (fs.existsSync(path.join(dir, "Pulsar", "Pulsar", "Pulsar.csproj"))) return dir;
    const parent = path.dirname(dir);
    if (parent === dir) return null;
    dir = parent;
  }
  return null;
}

// ---------- 进程 ----------

export interface RunOptions {
  cwd: string;
  signal?: AbortSignal;
  onRecent?: (recent: string[]) => void;
  maxLines?: number; // 滚动窗口上限；不传则保留全部
}

export interface RunResult {
  code: number | null;
  killed: boolean;
  error?: Error;
  lastLines: string[];
}

export function run(cmd: string, args: string[], opts: RunOptions): Promise<RunResult> {
  return new Promise((resolve) => {
    const child = spawn(cmd, args, { cwd: opts.cwd, windowsHide: true, stdio: ["ignore", "pipe", "pipe"] });
    const windowSize = opts.maxLines ?? Number.POSITIVE_INFINITY;
    const lastLines: string[] = [];
    const push = (line: string): void => {
      if (line === "") return;
      lastLines.push(line);
      if (lastLines.length > windowSize) lastLines.shift();
      opts.onRecent?.(lastLines.slice());
    };
    let buf = "";
    const onData = (chunk: Buffer): void => {
      buf += chunk.toString("utf8");
      const lines = buf.split(/\r?\n/);
      buf = lines.pop() ?? "";
      for (const l of lines) push(l);
    };
    child.stdout.on("data", onData);
    child.stderr.on("data", onData);

    const onAbort = (): void => {
      if (child.pid !== undefined) {
        // Windows 下杀整棵进程树（msbuild 有子进程，直接 kill 会泄漏）
        spawn("taskkill", ["/pid", String(child.pid), "/T", "/F"], { windowsHide: true, stdio: "ignore" });
      }
    };
    opts.signal?.addEventListener("abort", onAbort, { once: true });

    child.on("error", (err) => {
      opts.signal?.removeEventListener("abort", onAbort);
      resolve({ code: null, killed: opts.signal?.aborted ?? false, error: err, lastLines: lastLines.slice() });
    });
    child.on("close", (code) => {
      opts.signal?.removeEventListener("abort", onAbort);
      if (buf !== "") push(buf);
      resolve({ code, killed: opts.signal?.aborted ?? false, lastLines: lastLines.slice() });
    });
  });
}

// ---------- git ----------

export async function lastTag(repoRoot: string): Promise<string | null> {
  const r = await run("git", ["describe", "--tags", "--abbrev=0"], { cwd: repoRoot });
  if (r.code !== 0) return null;
  return lastLine(r) || null;
}

export async function gitLogRange(repoRoot: string, from: string | null): Promise<string[]> {
  const args = ["log", "--no-merges", "--pretty=format:%s"];
  if (from) args.push(`${from}..HEAD`);
  else args.push("-n", "30");
  const r = await run("git", args, { cwd: repoRoot });
  if (r.code !== 0) return [];
  return r.lastLines.map((l) => l.trim()).filter((l) => l !== "");
}

export interface VersionSuggestion {
  version: string;
  reason: string;
  /** 上次 tag（无则 null），用于展示上下文 */
  lastTag: string | null;
  commitCount: number;
  featCount: number;
  fixCount: number;
  perfCount: number;
}

export async function inferNextVersion(repoRoot: string, current: string): Promise<VersionSuggestion> {
  const from = await lastTag(repoRoot);
  const lines = await gitLogRange(repoRoot, from);
  const meaningful = lines.filter((l) => !/^chore:\s*bump version/i.test(l));
  const hasFeat = meaningful.some((l) => /^feat(\(|:)/.test(l));
  const hasFix = meaningful.some((l) => /^fix(\(|:)/.test(l));
  const featCount = meaningful.filter((l) => /^feat(\(|:)/.test(l)).length;
  const fixCount = meaningful.filter((l) => /^fix(\(|:)/.test(l)).length;
  const perfCount = meaningful.filter((l) => /^perf(\(|:)/.test(l)).length;
  const which: BumpKind = hasFeat ? "minor" : "patch";
  const reason = hasFeat
    ? `含 ${featCount} 个 feat 提交 → minor`
    : hasFix
      ? `含 ${fixCount} 个 fix 提交 → patch`
      : "无 feat/fix → 保守 patch";
  return {
    version: incrementVersion(current, which),
    reason,
    lastTag: from,
    commitCount: lines.length,
    featCount,
    fixCount,
    perfCount,
  };
}

export async function remoteOwnerRepo(repoRoot: string): Promise<string | null> {
  const r = await run("git", ["remote", "get-url", "origin"], { cwd: repoRoot });
  if (r.code !== 0) return null;
  const m = lastLine(r).match(/(?:github\.com[/:])([^/]+)\/([^/.]+?)(?:\.git)?$/);
  return m ? `${m[1]}/${m[2]}` : null;
}

export function lastLine(r: RunResult): string {
  return r.lastLines.length > 0 ? r.lastLines[r.lastLines.length - 1].trim() : "";
}

// ---------- release notes ----------

const CONV_GROUPS: Array<{ key: string; title: string; re: RegExp }> = [
  { key: "feat", title: "### Features", re: /^feat(?:\([^)]*\))?:\s*(.*)$/i },
  { key: "fix", title: "### Fixes", re: /^fix(?:\([^)]*\))?:\s*(.*)$/i },
  { key: "perf", title: "### Performance", re: /^perf(?:\([^)]*\))?:\s*(.*)$/i },
];

export function buildNotes(lines: string[], version: string): string {
  const groups = new Map<string, string[]>();
  const other: string[] = [];
  for (const line of lines) {
    if (/^chore:\s*bump version/i.test(line)) continue;
    let matched = false;
    for (const g of CONV_GROUPS) {
      const m = line.match(g.re);
      if (m) {
        if (!groups.has(g.key)) groups.set(g.key, []);
        groups.get(g.key)!.push(m[1]);
        matched = true;
        break;
      }
    }
    if (!matched) other.push(line);
  }
  const out = [`## v${version}`, ""];
  for (const g of CONV_GROUPS) {
    const items = groups.get(g.key);
    if (items && items.length > 0) out.push(g.title, ...items.map((s) => `- ${s}`), "");
  }
  if (other.length > 0) out.push("### Other", ...other.map((s) => `- ${s}`), "");
  return out.join("\n").trimEnd() + "\n";
}

// 供扩展调 LLM 自动撰写 release notes 的提示词（纯函数，可冒烟测试）
export function buildNotesPrompt(version: string, lines: string[], draft: string): string {
  return [
    `为 Pulsar（Windows 生产力启动器）的 v${version} 版本撰写 GitHub Release notes。`,
    "",
    "要求：",
    '1. 使用简洁中文，面向最终用户；',
    '2. 章节标题固定为：### 新功能 / ### 修复 / ### 性能优化 / ### 其他（无内容的章节省略）；',
    '3. 每条变更一行，以 "- " 开头，基于下面的提交列表提炼用户可感知的改进；',
    '4. 剔除版本号 bump、纯内部重构、文档类噪音；',
    '5. 直接输出 notes 正文，不要任何前言或解释。',
    "",
    "## 版本",
    `v${version}`,
    "",
    "## 提交列表",
    ...lines.map((l) => `- ${l}`),
    "",
    "## 参考草稿（基于提交自动生成，可润色）",
    draft,
  ].join("\n");
}

// ---------- 校验 / 打包 ----------

// Windows 下可靠的 zip 读写器：System32 的 bsdtar（libarchive）。
// PATH 中的 tar 可能是 Git for Windows 的 GNU tar（无法读/写 zip，会生成假 .zip）。
export function zipTarPath(): string {
  const systemRoot = process.env.SystemRoot ?? "C:\\Windows";
  const bsdtar = path.join(systemRoot, "System32", "tar.exe");
  return fs.existsSync(bsdtar) ? bsdtar : "tar";
}

// 真实 zip 的魔数 "PK"（GNU tar 生成的伪 .zip 以 "./" 开头，可被此检查拦截）
export function isZipFile(zipPath: string): boolean {
  try {
    const fd = fs.openSync(zipPath, "r");
    const buf = Buffer.alloc(4);
    fs.readSync(fd, buf, 0, 4, 0);
    fs.closeSync(fd);
    return buf[0] === 0x50 && buf[1] === 0x4b;
  } catch {
    return false;
  }
}

export function verifyPublishDir(dir: string): string[] {
  const missing: string[] = [];
  for (const entry of ["Pulsar.exe", "Pulsar.pdb", "Assets"]) {
    if (!fs.existsSync(path.join(dir, entry))) missing.push(entry);
  }
  let hasCor3 = false;
  try {
    hasCor3 = fs.readdirSync(dir).some((f) => /_cor3\.dll$/i.test(f));
  } catch {
    /* 目录不存在 */
  }
  if (!hasCor3) missing.push("*_cor3.dll");
  return missing;
}

export async function verifyZipEntries(zipPath: string): Promise<string[]> {
  if (!isZipFile(zipPath)) return ["<zip 文件无效或缺失>"];
  const r = await run(zipTarPath(), ["-tf", zipPath], { cwd: path.dirname(zipPath) });
  if (r.code !== 0) return ["<无法读取 zip>"];
  const entries = r.lastLines.map((l) => l.replace(/\\/g, "/"));
  const missing: string[] = [];
  for (const entry of ["Pulsar.exe", "Pulsar.pdb", "Assets/"]) {
    const ok = entries.some((e) => e === entry || e.startsWith(entry));
    if (!ok) missing.push(entry);
  }
  if (!entries.some((e) => /_cor3\.dll$/i.test(e))) missing.push("*_cor3.dll");
  return missing;
}

export async function zipDirContents(
  cwd: string,
  dir: string,
  zipPath: string,
  onRecent?: (recent: string[]) => void,
): Promise<RunResult> {
  const archiveCmd = `Compress-Archive -Path '${dir}\\*' -DestinationPath '${zipPath}' -CompressionLevel Optimal -Force`;
  // PowerShell 5.1 的进程 PSModulePath 可能被 PS7 目录（先于 System32）污染：
  // 5.1 会优先自动加载 PS7 的 Microsoft.PowerShell.Archive 副本，而被其
  // 执行策略（Restricted）拦截 → CommandNotFoundException。逐级回退：
  //   1. pwsh（模块在自己目录，默认 RemoteSigned，最可靠）
  //   2. powershell -ExecutionPolicy Bypass（5.1 绕过脚本拦截）
  //   3. 系统自带 bsdtar（Win10 1803+，无需 PowerShell 模块）
  const attempts: Array<[string, string[]]> = [
    ["pwsh", ["-NoProfile", "-Command", archiveCmd]],
    ["powershell", ["-NoProfile", "-ExecutionPolicy", "Bypass", "-Command", archiveCmd]],
  ];
  for (const [cmd, args] of attempts) {
    const r = await run(cmd, args, { cwd, onRecent });
    if (r.code === 0 && fs.existsSync(zipPath)) return r;
  }
  // 兜底：System32 bsdtar 直接打 zip（条目相对 -C dir，无 ./ 前缀，可被 verifyZipEntries 识别）。
  // 注意不能用 PATH 中的 tar（Git for Windows 的 GNU tar 只会生成假的 .zip），
  // 且结果必须通过 PK 魔数校验。
  fs.rmSync(zipPath, { force: true });
  let entries: string[];
  try {
    entries = fs.readdirSync(dir);
  } catch {
    return { code: 1, killed: false, lastLines: [`目录不可读: ${dir}`] };
  }
  const r = await run(zipTarPath(), ["-a", "-c", "-f", zipPath, "-C", dir, ...entries], { cwd, onRecent });
  if (r.code === 0 && !isZipFile(zipPath)) {
    return { code: 1, killed: false, lastLines: ["tar 输出不是有效的 zip（PATH 中的 tar 可能是 GNU tar）"] };
  }
  return r;
}
