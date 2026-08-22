// publish-core-smoke.ts — 冒烟测试（仅测试核心逻辑，不执行真实发布）
// 运行: node .pi/extensions/publish-local/smoke.ts
import {
  SEMVER_RE,
  incrementVersion,
  parseArgs,
  readCsprojVersions,
  applyCsprojVersion,
  findRepoRoot,
  verifyPublishDir,
  buildNotes,
  buildNotesPrompt,
  inferNextVersion,
} from "./core.ts";
import * as fs from "node:fs";
import * as os from "node:os";
import * as path from "node:path";

let failures = 0;
function check(name: string, cond: boolean, detail?: string): void {
  console.log(`${cond ? "PASS" : "FAIL"} ${name}${cond ? "" : "  <- " + detail}`);
  if (!cond) failures++;
}

// ---- 版本 ----
check("increment patch", incrementVersion("1.5.0", "patch") === "1.5.1");
check("increment minor", incrementVersion("1.5.0", "minor") === "1.6.0");
check("increment major", incrementVersion("1.5.0", "major") === "2.0.0");
check("semver prerelease ok", SEMVER_RE.test("1.6.0-beta.1") === true);

// ---- 参数解析 ----
check("parseArgs plain", JSON.stringify(parseArgs(undefined)) === JSON.stringify({ version: undefined, gh: false, ghOnly: false }));
check("parseArgs gh", parseArgs("gh").gh === true && parseArgs("gh").ghOnly === false);
check("parseArgs gh-only implies gh", parseArgs("gh-only").gh === true && parseArgs("gh-only").ghOnly === true);
check("parseArgs version+gh", parseArgs("1.6.0 gh").version === "1.6.0" && parseArgs("1.6.0 gh").gh === true);
check("parseArgs minor gh-only", parseArgs("minor gh-only").version === "minor" && parseArgs("minor gh-only").ghOnly === true);

// ---- release notes ----
const notes = buildNotes(
  [
    "feat(menu): right-drag summon gesture",
    "fix(quick-switch): measure window from menu visibility",
    "chore: bump version to 1.4.2",
    "refactor(settings): extract SlotEditorWorkspace core",
    "docs: update build docs",
  ],
  "1.6.0",
);
console.log("---- notes draft ----\n" + notes + "---- end ----");
check("notes has Features", notes.includes("### Features") && notes.includes("- right-drag summon gesture"));
check("notes has Fixes", notes.includes("### Fixes") && notes.includes("- measure window from menu visibility"));
check("notes excludes bump commit", !notes.includes("bump version to 1.4.2"));
check("notes has Other", notes.includes("### Other") && notes.includes("- refactor(settings): extract SlotEditorWorkspace core"));

// ---- AGENT release notes 提示词 ----
const agentPrompt = buildNotesPrompt("1.6.0", ["feat(menu): right-drag summon gesture", "fix(quick-switch): measure window from menu visibility"], notes);
check("prompt contains version", agentPrompt.includes("v1.6.0"));
check("prompt contains commit lines", agentPrompt.includes("feat(menu): right-drag summon gesture"));
check("prompt contains draft", agentPrompt.includes("### Features"));
check("prompt has formatting rules", agentPrompt.includes("### 新功能") && agentPrompt.includes("- "));

// ---- csproj 读写（临时副本，不碰真实文件）----
const repoRoot = findRepoRoot(process.cwd());
check("findRepoRoot", repoRoot !== null && path.resolve(process.cwd()) === repoRoot, String(repoRoot));
if (repoRoot) {
  const src = path.join(repoRoot, "Pulsar", "Pulsar", "Pulsar.csproj");
  const tmp = path.join(os.tmpdir(), `csproj-test-${Date.now()}.csproj`);
  fs.copyFileSync(src, tmp);
  const before = readCsprojVersions(tmp);
  const backup = applyCsprojVersion(tmp, "9.8.7");
  const after = readCsprojVersions(tmp);
  check("apply Version", after.Version === "9.8.7", JSON.stringify(after));
  check("apply FileVersion (x.y.z.0)", after.FileVersion === "9.8.7.0");
  check("apply AssemblyVersion (x.y.z.0)", after.AssemblyVersion === "9.8.7.0");
  check("apply returns backup", backup.includes(before.Version));
  fs.writeFileSync(tmp, backup, "utf8");
  const restored = readCsprojVersions(tmp);
  check(
    "restore exact",
    restored.Version === before.Version && restored.FileVersion === before.FileVersion && restored.AssemblyVersion === before.AssemblyVersion,
    JSON.stringify(restored),
  );
  fs.rmSync(tmp, { force: true });
  console.log(`csproj 当前版本: ${before.Version}`);

  // ---- 版本建议（真实 git 仓库，断言结构而非具体值）----
  const sug = await inferNextVersion(repoRoot, before.Version);
  check("suggestion version+reason", SEMVER_RE.test(sug.version) && sug.reason.length > 0, JSON.stringify(sug));
  check(
    "suggestion stats present",
    typeof sug.commitCount === "number" && typeof sug.featCount === "number" && typeof sug.fixCount === "number",
  );
}

// ---- 产物校验 ----
const t = fs.mkdtempSync(path.join(os.tmpdir(), "publish-verify-"));
check("verify detects all missing", verifyPublishDir(t).length === 4, JSON.stringify(verifyPublishDir(t)));
fs.writeFileSync(path.join(t, "Pulsar.exe"), "x");
fs.writeFileSync(path.join(t, "Pulsar.pdb"), "x");
fs.mkdirSync(path.join(t, "Assets"));
fs.writeFileSync(path.join(t, "x_cor3.dll"), "x");
check("verify passes with all entries", verifyPublishDir(t).length === 0, JSON.stringify(verifyPublishDir(t)));
fs.rmSync(t, { recursive: true, force: true });

console.log(failures === 0 ? "\nALL PASS" : `\n${failures} FAILURE(S)`);
process.exit(failures === 0 ? 0 : 1);
