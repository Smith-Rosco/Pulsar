// publish-core-smoke.ts — 冒烟测试（仅测试 core.ts 残留逻辑：参数解析）
// 运行: node .pi/extensions/publish-local/smoke.ts
// 说明：发布主流程已迁移至 .agents/skills/publish/scripts/（PowerShell），
// 其正确性由脚本内置断言保障，不再在此重复测试。
import { parseArgs } from "./core.ts";

let failures = 0;
function check(name: string, cond: boolean, detail?: string): void {
  console.log(`${cond ? "PASS" : "FAIL"} ${name}${cond ? "" : "  <- " + detail}`);
  if (!cond) failures++;
}

// ---- 参数解析 ----
check("parseArgs plain", JSON.stringify(parseArgs(undefined)) === JSON.stringify({ version: undefined, gh: false, ghOnly: false }));
check("parseArgs gh", parseArgs("gh").gh === true && parseArgs("gh").ghOnly === false);
check("parseArgs gh-only implies gh", parseArgs("gh-only").gh === true && parseArgs("gh-only").ghOnly === true);
check("parseArgs version+gh", parseArgs("1.6.0 gh").version === "1.6.0" && parseArgs("1.6.0 gh").gh === true);
check("parseArgs minor gh-only", parseArgs("minor gh-only").version === "minor" && parseArgs("minor gh-only").ghOnly === true);
check("parseArgs bump types", parseArgs("patch").version === "patch" && parseArgs("major").version === "major");

console.log(failures === 0 ? "\nALL PASS" : `\n${failures} FAILURE(S)`);
process.exit(failures === 0 ? 0 : 1);
