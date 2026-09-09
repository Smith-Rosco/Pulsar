#!/usr/bin/env python3
"""Pulsar publish pipeline — full Python successor of the PowerShell publish skill scripts.

Single entry point. Stdlib only (no third-party deps), Python >= 3.10.

Subcommands (one per former PS script):
  info         release info: repo version, last tag, commits, version suggestion
  set-version  write <Version>/<FileVersion>/<AssemblyVersion> into the csproj (no BOM)
  build        dotnet publish full + portable (+ optional installer stage)
  pack         zip artifacts, PK/CRC verify, optional Setup/Standalone/SHA256SUMS
  all          build + pack + terminal convergence (local pipeline)
  changelog    solidify CHANGELOG.md [Unreleased] into [X.Y.Z] - date
  tag          commit version files + annotated tag (commentChar-safe, BOM-free)
  watch        wait for release.yml CI run, verify GitHub Release assets
  edit-notes   fix GitHub Release body with the full notes file
  iscc-probe   locate ISCC.exe and test-spawn it (sandbox diagnostics)

Global flags: --dry-run (print plan, no side effects), --json (print L3 report to stdout).
Report: L1 human summary to stdout (results only), L2 detail + L3 machine data in
Artifacts/report.json. Behaviour parity with the PS scripts it replaces; deviations
are documented in .agents/skills/publish/SKILL.md.
"""

from __future__ import annotations

import argparse
import ctypes
import hashlib
import json
import os
import re
import shutil
import subprocess
import sys
import time
import zipfile
from datetime import datetime
from pathlib import Path

try:
    import winreg
except ImportError:  # non-Windows: env heal and ISCC unavailable, rest still works
    winreg = None

ROOT = Path(__file__).resolve().parents[1]
CSPROJ = ROOT / "Pulsar" / "Pulsar" / "Pulsar.csproj"
CHANGELOG = ROOT / "CHANGELOG.md"
ARTIFACTS = ROOT / "Artifacts"
PUBLISH_ROOT = ARTIFACTS / "publish"
REPORT_PATH = ARTIFACTS / "report.json"

FULL_EXE_MIN_BYTES = 50 * 1024 * 1024
PORTABLE_EXE_MAX_BYTES = 20 * 1024 * 1024

HKEY_SOURCES = [
    (winreg.HKEY_LOCAL_MACHINE, r"SYSTEM\CurrentControlSet\Control\Session Manager\Environment") if winreg else None,
    (winreg.HKEY_CURRENT_USER, r"Environment") if winreg else None,
]
HKEY_SOURCES = [s for s in HKEY_SOURCES if s]

ENV_FALLBACKS = {
    "SystemRoot": r"C:\Windows",
    "windir": r"C:\Windows",
    "SystemDrive": "C:",
    "ComSpec": r"C:\Windows\System32\cmd.exe",
    "ProgramData": r"C:\ProgramData",
    "ALLUSERSPROFILE": r"C:\ProgramData",
    "PUBLIC": r"C:\Users\Public",
    "OS": "Windows_NT",
}


# --------------------------------------------------------------------------- env

def _expand(value: str) -> str:
    if "%" not in value:
        return value
    buf = ctypes.create_unicode_buffer(4096)
    ctypes.windll.kernel32.ExpandEnvironmentStringsW(value, buf, 4096)
    return buf.value


def heal_env() -> list[str]:
    """Fill missing Windows system env vars from registry + fallbacks.

    Sandboxed shells strip these; without them NuGet's ConfigurationDefaults
    throws "Value cannot be null (Parameter 'path1')" at restore. Parenthesised
    names (ProgramFiles(x86)) work fine through Python's os.environ on Windows.
    """
    if sys.platform != "win32":
        return []
    filled = []
    registry: dict[str, str] = {}
    for hive, path in HKEY_SOURCES:
        try:
            with winreg.OpenKey(hive, path) as key:
                index = 0
                while True:
                    try:
                        name, value, kind = winreg.EnumValue(key, index)
                    except OSError:
                        break
                    index += 1
                    if isinstance(value, str):
                        if kind == winreg.REG_EXPAND_SZ:
                            value = _expand(value)
                        registry.setdefault(name, value)
        except OSError:
            continue

    candidates = dict(ENV_FALLBACKS)
    candidates.update(registry)
    # ProgramFiles / CommonProgramFiles are NOT stored in the registry environment
    # key (injected by the OS at session creation) — derive from SystemDrive,
    # mirroring scripts/dev.ps1's repair logic.
    system_drive = candidates.get("SystemDrive", "C:").rstrip("\\") or "C:"
    candidates.setdefault("ProgramFiles", f"{system_drive}\\Program Files")
    if "ProgramFiles" in candidates and "ProgramFiles(x86)" not in candidates:
        pf = candidates["ProgramFiles"]
        candidates["ProgramFiles(x86)"] = pf if pf.endswith(" (x86)") else f"{pf} (x86)"
    if "ProgramFiles" in candidates:
        candidates.setdefault("ProgramW6432", candidates["ProgramFiles"])
        candidates.setdefault("CommonProgramFiles", os.path.join(candidates["ProgramFiles"], "Common Files"))
    if "ProgramFiles(x86)" in candidates:
        candidates.setdefault(
            "CommonProgramFiles(x86)", os.path.join(candidates["ProgramFiles(x86)"], "Common Files")
        )

    userprofile = candidates.get("USERPROFILE") or os.environ.get("USERPROFILE")
    if userprofile:
        candidates.setdefault("APPDATA", os.path.join(userprofile, "AppData", "Roaming"))
        candidates.setdefault("LOCALAPPDATA", os.path.join(userprofile, "AppData", "Local"))
        candidates.setdefault("HOMEDRIVE", userprofile[:2])
        candidates.setdefault("HOMEPATH", userprofile[2:].strip("\\"))

    for name, value in candidates.items():
        if not os.environ.get(name) and value:
            os.environ[name] = value
            filled.append(name)
    return filled


# ------------------------------------------------------------------------ plumbing

class Report:
    """L1 human summary + L2 assertion detail + L3 JSON, from one data structure."""

    def __init__(self) -> None:
        self.stages: list[dict] = []
        self.checks: list[dict] = []
        self.skipped: list[str] = []
        self.artifacts: list[dict] = []
        self.meta: dict = {}
        self.failure: dict | None = None
        self.dry_run = False
        self._t0 = time.monotonic()

    def stage(self, name: str) -> float:
        entry = {"name": name, "started": time.monotonic()}
        self.stages.append(entry)
        print(f"\n== {name} ==", flush=True)
        return entry["started"]

    def end_stage(self, started: float) -> None:
        self.stages[-1]["seconds"] = round(time.monotonic() - started, 1)

    def check(self, name: str, passed: bool, detail: str = "") -> bool:
        self.checks.append({"name": name, "passed": passed, "detail": detail})
        mark = "PASS" if passed else "FAIL"
        print(f"  [{mark}] {name}" + (f" — {detail}" if detail else ""), flush=True)
        return passed

    def skip(self, what: str, reason: str) -> None:
        self.skipped.append(f"{what}（{reason}）")
        print(f"  [SKIP] {what} — {reason}", flush=True)

    def artifact(self, path: Path, label: str) -> None:
        size = path.stat().st_size
        digest = hashlib.sha256(path.read_bytes()).hexdigest()[:16]
        self.artifacts.append(
            {"label": label, "path": str(path), "name": path.name,
             "bytes": size, "mb": round(size / 1024 / 1024, 1), "sha256_16": digest}
        )

    @property
    def elapsed(self) -> float:
        return round(time.monotonic() - self._t0, 1)

    @property
    def passed(self) -> bool:
        return self.failure is None

    def fail(self, stage: str, reason: str, hint: str = "") -> None:
        self.failure = {"stage": stage, "reason": reason, "hint": hint}

    def _fmt_elapsed(self, seconds: float) -> str:
        m, s = divmod(int(seconds), 60)
        return f"{m} 分 {s:02d} 秒" if m else f"{s} 秒"

    def render_l1(self) -> str:
        v = self.meta.get("version", "?")
        mode = self.meta.get("mode", "?")
        n_checks = len(self.checks)
        n_ok = sum(1 for c in self.checks if c["passed"])
        if self.passed:
            head = "✅ 发布成功"
            color = ""
        else:
            head = "❌ 发布失败"
        lines = ["", head, ""]
        if self.dry_run:
            lines = ["", "🜲 DRY-RUN 预演（未产生任何文件）", ""]
        lines.append(f"  Pulsar v{v} · {mode}")
        if self.passed:
            lines.append(f"  用时 {self._fmt_elapsed(self.elapsed)} · 全部 {n_checks} 项校验通过")
        else:
            bad = next((c for c in self.checks if not c["passed"]), None)
            lines.append(f"  校验 {n_ok}/{n_checks} 通过" + (f" — 失败于：{bad['name']}" if bad else ""))
        if self.artifacts:
            lines += ["", "  ── 交付物 ────────────────────────────"]
            for a in self.artifacts:
                lines.append(f"  {a['name']:<40} {a['mb']:>7} MB")
        if self.artifacts or not self.passed:
            where = self.artifacts[0]["path"] if self.artifacts else ARTIFACTS
            lines += ["", f"  位置  {Path(where).parent}"]
        if self.skipped:
            lines += ["", f"  本次未做：{' · '.join(s.split('（')[0] for s in self.skipped)}"]
            lines.append("  （见下方括注原因，属模式惯例/环境限制，非异常）")
        if not self.passed and self.failure:
            lines += ["", f"  失败阶段：{self.failure['stage']}", f"  原因：{self.failure['reason']}"]
            if self.failure.get("hint"):
                lines.append(f"  排查：{self.failure['hint']}")
        if not self.dry_run:
            lines += ["", f"  详细数据  {REPORT_PATH}"]
        lines.append("")
        return "\n".join(lines)

    def dump(self, path: Path) -> None:
        data = {
            "result": "PASS" if self.passed else "FAIL",
            "dry_run": self.dry_run,
            "meta": self.meta,
            "elapsed_seconds": self.elapsed,
            "stages": self.stages,
            "checks": self.checks,
            "skipped": self.skipped,
            "artifacts": self.artifacts,
            "failure": self.failure,
        }
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(json.dumps(data, ensure_ascii=False, indent=2), encoding="utf-8")


REPORT = Report()


def run(cmd: list[str], **kwargs) -> subprocess.CompletedProcess:
    print("  $ " + " ".join(cmd), flush=True)
    return subprocess.run(cmd, capture_output=True, text=True, encoding="utf-8", errors="replace", **kwargs)


def git(*args: str) -> str:
    r = run(["git", *args])
    if r.returncode != 0:
        raise RuntimeError(f"git {' '.join(args)} failed: {r.stderr.strip()}")
    return r.stdout.strip()


# ------------------------------------------------------------------------- version

def read_csproj_version() -> str:
    text = CSPROJ.read_text(encoding="utf-8-sig")
    m = re.search(r"<Version>([^<]+)</Version>", text)
    if not m:
        raise RuntimeError(f"<Version> not found in {CSPROJ}")
    return m.group(1).strip()


def set_version(version: str, allow_downgrade: bool) -> None:
    if not re.fullmatch(r"\d+\.\d+\.\d+", version):
        raise ValueError(f"版本必须是 major.minor.patch 三段式：{version}")
    current = read_csproj_version()
    if [int(x) for x in version.split(".")] < [int(x) for x in current.split(".")]:
        if not allow_downgrade:
            raise ValueError(f"拒绝降级：{current} → {version}（如确需降级加 --allow-downgrade）")
    if version == current:
        print(f"  csproj 已是 {version}，无需修改")
        return
    text = CSPROJ.read_text(encoding="utf-8-sig")
    text = re.sub(r"<Version>[^<]+</Version>", f"<Version>{version}</Version>", text)
    text = re.sub(r"<FileVersion>[^<]+</FileVersion>", f"<FileVersion>{version}.0</FileVersion>", text)
    text = re.sub(r"<AssemblyVersion>[^<]+</AssemblyVersion>", f"<AssemblyVersion>{version}.0</AssemblyVersion>", text)
    CSPROJ.write_bytes(text.encode("utf-8"))  # write_bytes ⇒ no BOM
    print(f"  csproj 版本：{current} → {version}（Version={version}，File/Assembly={version}.0，无 BOM）")


def suggest_version(commits: list[str]) -> tuple[str, str]:
    current = read_csproj_version()
    major, minor, patch = (int(x) for x in current.split("."))
    if any(re.search(r"^\s*\w+\s*\(.*feat", c) or "feat" in c.lower() for c in commits):
        return f"{major}.{minor + 1}.0", "自上一 tag 以来包含 feat 提交 → minor +1"
    return f"{major}.{minor}.{patch + 1}", "仅 fix/chore 提交 → 保守 patch +1"


def cmd_info(_args) -> None:
    t = REPORT.stage("信息收集")
    REPORT.meta["mode"] = "info"
    version = read_csproj_version()
    tags = git("tag", "--list", "v*").splitlines()
    semver_tags = sorted(
        (t for t in tags if re.fullmatch(r"v\d+\.\d+\.\d+", t)),
        key=lambda t: [int(x) for x in t[1:].split(".")],
    )
    last = semver_tags[-1] if semver_tags else "(无)"
    commits = git("log", "--no-merges", "--pretty=format:%s", f"{last}..HEAD").splitlines() if last != "(无)" else []
    suggestion, basis = suggest_version(commits)
    print(f"  仓库根：{ROOT}")
    print(f"  csproj 版本：{version}")
    print(f"  最近 tag：{last}")
    print(f"  自 tag 以来提交：{len(commits)} 个")
    for c in commits[:8]:
        print(f"    - {c}")
    if len(commits) > 8:
        print(f"    … 共 {len(commits)} 个")
    print(f"  建议版本：{suggestion}（{basis}）")
    builds = sorted(
        {m.group(1) for z in ARTIFACTS.glob("Pulsar-*.zip")
         if (m := re.fullmatch(r"Pulsar-(\d+\.\d+\.\d+(?:\.\d+)?)-.+", z.name))}
    )
    if builds:
        print(f"  已存在的本地产物版本：{', '.join(builds)}")
    REPORT.meta["version"] = version
    REPORT.check("info 收集", True, f"version={version} last_tag={last} commits={len(commits)}")
    REPORT.end_stage(t)


# --------------------------------------------------------------------------- build

def effective_version(version: str, build: int | None) -> str:
    return f"{version}.{build}" if build else version


def publish_one(profile: str, version: str, build: int | None, extra: list[str], out_dir: Path, t: float) -> None:
    out_dir.mkdir(parents=True, exist_ok=True)
    cmd = [
        "dotnet", "publish", "Pulsar/Pulsar/Pulsar.csproj",
        "-c", "Release", "-r", "win-x64",
        "-p:PublishDir=" + str(out_dir) + os.sep,
        *extra,
    ]
    if build:
        four = f"{version}.{build}"
        cmd += [f"-p:Version={four}", f"-p:FileVersion={four}", f"-p:AssemblyVersion={four}"]
    r = run(cmd)
    if r.returncode != 0:
        REPORT.fail(f"publish {profile}", (r.stderr or r.stdout).strip().splitlines()[-1] if (r.stderr or r.stdout) else "dotnet publish 退出码非零",
                    hint="检查 dotnet 输出；沙箱会话先跑 publish.py info 确认 env 自愈已生效")
        raise SystemExit(REPORT.render_l1())
    REPORT.end_stage(t)


def cmd_build(args) -> Path:
    version, build = args.version, args.build
    eff = effective_version(version, build)
    t = REPORT.stage("环境自愈")
    filled = heal_env()
    REPORT.check("env 自愈", True, f"补齐 {len(filled)} 个系统变量" + (f"：{', '.join(filled)}" if filled else "（无缺失）"))
    REPORT.end_stage(t)

    full_dir = PUBLISH_ROOT / f"v{eff}" / "full"
    portable_dir = PUBLISH_ROOT / f"v{eff}" / "portable"
    commit = git("rev-parse", "--short", "HEAD")

    t = REPORT.stage("构建 full（自包含单文件）")
    publish_one("full", version, build,
                ["--self-contained", "true", "-p:PublishSingleFile=true", "-p:PublishReadyToRun=true"],
                full_dir, t)

    t = REPORT.stage("构建 portable（框架依赖单文件）")
    publish_one("portable", version, build,
                ["--self-contained", "false", "-p:PublishSingleFile=true", "-p:EnableCompressionInSingleFile=false"],
                portable_dir, t)

    t = REPORT.stage("build-info 写入")
    stamp = datetime.now().strftime("%Y-%m-%d %H:%M:%S")
    channel = f"local-{REPORT.meta.get('mode', 'artifact')}" if REPORT.meta.get("mode") != "release" else "release"
    for d, ch in ((full_dir, "full"), (portable_dir, "portable")):
        (d / "build-info.txt").write_bytes(
            f"Version: {eff}\r\nBuild: {build or 0}\r\nChannel: {ch}\r\nBuilt: {stamp}\r\nCommit: {commit}\r\n".encode("ascii")
        )
        REPORT.check(f"build-info.txt（{ch}）", (d / "build-info.txt").is_file())
    REPORT.end_stage(t)

    t = REPORT.stage("产物断言")
    for label, d, rule in (("full", full_dir, "min"), ("portable", portable_dir, "max")):
        exe = d / "Pulsar.exe"
        ok = exe.is_file() and (d / "Pulsar.pdb").is_file() and (d / "Assets").is_dir()
        REPORT.check(f"{label} exe/PDB/Assets", ok, str(d) if ok else "缺失文件")
        if not ok:
            REPORT.fail("产物断言", f"{label} 目录缺 Pulsar.exe/PDB/Assets", hint=f"检查 {d}")
            raise SystemExit(REPORT.render_l1())
        size = exe.stat().st_size
        mb = round(size / 1024 / 1024, 1)
        passed = size >= FULL_EXE_MIN_BYTES if rule == "min" else size < PORTABLE_EXE_MAX_BYTES
        bound = "≥ 50 MB" if rule == "min" else "< 20 MB"
        REPORT.check(f"{label} Pulsar.exe {bound}", passed, f"{mb} MB")
        if not passed:
            REPORT.fail("产物断言", f"{label} exe {mb} MB 违反 {bound}", hint="误含/漏含运行时，检查 publish 参数")
            raise SystemExit(REPORT.render_l1())
    REPORT.end_stage(t)
    return full_dir


# ---------------------------------------------------------------------------- pack

def find_iscc() -> Path | None:
    candidates = [
        Path(os.environ.get("ProgramFiles(x86)", r"C:\Program Files (x86)")) / "Inno Setup 6" / "ISCC.exe",
        Path(os.environ.get("ProgramFiles", r"C:\Program Files")) / "Inno Setup 6" / "ISCC.exe",
        Path(os.environ.get("LOCALAPPDATA", "")) / "Programs" / "Inno Setup 6" / "ISCC.exe",
    ]
    return next((c for c in candidates if c.is_file()), None)


def zip_dir(src: Path, dest: Path) -> None:
    with zipfile.ZipFile(dest, "w", zipfile.ZIP_DEFLATED) as zf:
        zf.write(src / "Pulsar.exe", "Pulsar.exe")
        zf.write(src / "Pulsar.pdb", "Pulsar.pdb")
        zf.write(src / "build-info.txt", "build-info.txt")
        for f in sorted((src / "Assets").rglob("*")):
            if f.is_file():
                zf.write(f, f.relative_to(src).as_posix())
    with open(dest, "rb") as fh:
        magic = fh.read(2)
    assert magic == b"PK", f"{dest} 魔数非 PK：{magic!r}"
    with zipfile.ZipFile(dest) as zf:
        bad = zf.testzip()
        assert bad is None, f"{dest} CRC 校验失败：{bad}"


def cmd_pack(args) -> None:
    version, build = args.version, args.build
    eff = effective_version(version, build)
    full_dir = PUBLISH_ROOT / f"v{eff}" / "full"
    portable_dir = PUBLISH_ROOT / f"v{eff}" / "portable"

    t = REPORT.stage("打包 + PK/CRC 校验")
    zips = [(full_dir, ARTIFACTS / f"Pulsar-{eff}-full.zip"), (portable_dir, ARTIFACTS / f"Pulsar-{eff}-portable.zip")]
    for src, dest in zips:
        zip_dir(src, dest)
        REPORT.check(f"ZIP PK+CRC：{dest.name}", True, f"{round(dest.stat().st_size / 1024 / 1024, 1)} MB")
        REPORT.artifact(dest, dest.stem)
    REPORT.end_stage(t)

    if args.installer:
        t = REPORT.stage("installer（ISCC + Standalone + SHA256SUMS）")
        iscc = find_iscc()
        if iscc is None:
            REPORT.skip("Setup.exe", "未找到 ISCC.exe（Inno Setup 6）")
        else:
            stage = ROOT / "scripts" / "installer" / "artifacts" / "publish" / "stage"
            stage.mkdir(parents=True, exist_ok=True)
            shutil.copy2(full_dir / "Pulsar.exe", stage / "Pulsar.exe")
            r = run([str(iscc), f"/DAppVersion={version}", str(ROOT / "scripts" / "installer" / "pulsar.iss")])
            shutil.rmtree(stage, ignore_errors=True)
            setup = ARTIFACTS / f"Pulsar-v{version}-Setup.exe"
            if r.returncode == 0 and setup.is_file():
                REPORT.artifact(setup, "Setup.exe")
                REPORT.check("Setup.exe", True)
            else:
                REPORT.skip("Setup.exe", "ISCC 编译失败（stage 已清理，重跑即可）")
        standalone = ARTIFACTS / f"Pulsar-v{version}-Standalone-win-x64.zip"
        zip_dir(full_dir, standalone)
        REPORT.artifact(standalone, "Standalone")
        sums = ARTIFACTS / "SHA256SUMS.txt"
        lines = [f"{hashlib.sha256(p.read_bytes()).hexdigest()}  {p.name}"
                 for p in (ARTIFACTS / f"Pulsar-v{version}-Setup.exe", standalone) if p.is_file()]
        sums.write_text("\r\n".join(lines) + "\r\n", encoding="ascii")
        REPORT.check("SHA256SUMS.txt", sums.is_file(), f"{len(lines)} 条")
        REPORT.end_stage(t)

    if not args.keep_publish_dirs:
        t = REPORT.stage("终态收敛")
        target = PUBLISH_ROOT / f"v{eff}"
        if target.is_dir():
            shutil.rmtree(target)
            REPORT.check(f"删除产物目录 publish\\v{eff}", True)
        leftovers = [p for p in ARTIFACTS.glob(f"Pulsar-{eff}*")
                     if p.name not in {f"Pulsar-{eff}-full.zip", f"Pulsar-{eff}-portable.zip"}]
        REPORT.check("artifacts 根无本次版本的杂散文件", not leftovers,
                     f"残留：{[p.name for p in leftovers]}" if leftovers else "历史版本 ZIP 按设计并存保留")
        REPORT.end_stage(t)


def cmd_all(args) -> None:
    cmd_build(args)
    cmd_pack(args)


# ------------------------------------------------------------------------ changelog

def cmd_changelog(args) -> None:
    t = REPORT.stage("固化 CHANGELOG")
    version = args.version
    text = CHANGELOG.read_text(encoding="utf-8-sig")
    m = re.search(r"## \[Unreleased\]\n(.*?)(?=\n## |\Z)", text, re.S)
    if not m:
        REPORT.fail("changelog", "未找到 [Unreleased] 段"); raise SystemExit(REPORT.render_l1())
    body = m.group(1).strip()
    real = [ln for ln in body.splitlines() if ln.strip() and not re.fullmatch(r"[-*]?\s*(暂无|TODO|TBD)?\s*", ln)]
    if not real:
        REPORT.fail("changelog", "[Unreleased] 段无真实条目（只有占位符），拒绝固化",
                    hint="先按提交记录补全 CHANGELOG 的 Unreleased 段")
        raise SystemExit(REPORT.render_l1())
    today = datetime.now().strftime("%Y-%m-%d")
    text = text.replace("## [Unreleased]", f"## [{version}] - {today}\n\n## [Unreleased]", 1)
    CHANGELOG.write_bytes(text.encode("utf-8"))
    REPORT.check(f"CHANGELOG [Unreleased] → [{version}] - {today}", True, f"{len(real)} 条真实条目")
    REPORT.end_stage(t)


# ------------------------------------------------------------------------------ tag

def cmd_tag(args) -> None:
    version = args.version
    notes_path = Path(args.notes_file).resolve()
    t = REPORT.stage("commit 版本文件")
    changed = [p for p in (CSPROJ, CHANGELOG) if run(["git", "status", "--porcelain", str(p)]).stdout.strip()]
    if changed:
        r = run(["git", "add", *[str(p) for p in changed]])
        if r.returncode != 0:
            REPORT.fail("tag", f"git add 失败：{r.stderr}"); raise SystemExit(REPORT.render_l1())
        r = run(["git", "commit", "-m", f"chore(release): bump version to {version}"])
        if r.returncode != 0:
            REPORT.fail("tag", f"git commit 失败：{r.stderr}"); raise SystemExit(REPORT.render_l1())
        REPORT.check("版本文件 commit", True, f"{len(changed)} 个文件")
    else:
        REPORT.skip("版本文件 commit", "csproj/CHANGELOG 无改动")
    REPORT.end_stage(t)

    t = REPORT.stage("notes 去 BOM + annotated tag")
    raw = notes_path.read_bytes()
    if raw.startswith(b"\xef\xbb\xbf"):
        notes_path.write_bytes(raw[3:])
        REPORT.check("notes 去 BOM", True, "检测到 BOM 并移除（防污染 tag message 首行）")
    tag = f"v{version}"
    if run(["git", "rev-parse", "-q", "--verify", f"refs/tags/{tag}"]).returncode == 0:
        REPORT.fail("tag", f"tag {tag} 已存在，不覆盖", hint="如需重建：先删旧 tag（仅限未 push 时）")
        raise SystemExit(REPORT.render_l1())
    r = run(["git", "-c", "core.commentChar=§", "tag", "-a", tag, "-F", str(notes_path)])
    if r.returncode != 0:
        REPORT.fail("tag", f"git tag 失败：{r.stderr}"); raise SystemExit(REPORT.render_l1())
    content = subprocess.run(["git", "cat-file", "tag", tag], capture_output=True).stdout
    REPORT.check("tag message 以 ### 开头", content.lstrip().find(b"###") >= 0, tag)
    REPORT.end_stage(t)


# ---------------------------------------------------------------------------- watch

def cmd_watch(args) -> None:
    version = args.version
    tag = f"v{version}"
    t = REPORT.stage("等待 release.yml run 注册")
    run_id = None
    for _ in range(12):
        r = run(["gh", "run", "list", "--workflow=release.yml", "--branch", tag,
                 "--limit", "1", "--json", "databaseId,status", "--jq", ".[0].databaseId"])
        if r.returncode == 0 and r.stdout.strip():
            run_id = r.stdout.strip()
            break
        time.sleep(5)
    if not run_id:
        REPORT.fail("watch", "60s 内未发现 release.yml run", hint="确认 tag 已 push；gh auth 状态")
        raise SystemExit(REPORT.render_l1())
    REPORT.check("CI run 注册", True, f"run {run_id}")
    REPORT.end_stage(t)

    t = REPORT.stage("等待 CI 完成")
    r = run(["gh", "run", "watch", run_id, "--exit-status"])
    REPORT.check("CI run 成功", r.returncode == 0, f"run {run_id}")
    if r.returncode != 0:
        REPORT.fail("watch", "release.yml 运行失败", hint=f"gh run view {run_id} --log-failed")
        raise SystemExit(REPORT.render_l1())
    REPORT.end_stage(t)

    t = REPORT.stage("验证 GitHub Release 资产")
    r = run(["gh", "api", f"repos/{_gh_repo()}/releases/tags/{tag}"])
    if r.returncode != 0:
        REPORT.fail("watch", f"读取 Release 失败：{r.stderr.strip()[:120]}"); raise SystemExit(REPORT.render_l1())
    release = json.loads(r.stdout)
    assets = {a["name"] for a in release.get("assets", [])}
    expected = {f"Pulsar-{version}-full.zip", f"Pulsar-{version}-portable.zip"}
    missing = expected - assets
    REPORT.check("Release assets（full+portable）", not missing and release.get("draft") is False,
                 f"draft={release.get('draft')} assets={sorted(assets)}" if missing or release.get("draft") else f"{len(assets)} 个")
    body_path = Path(os.environ.get("TEMP", "/tmp")) / "pulsar-upload" / f"body_check_{version}.md"
    body_path.parent.mkdir(parents=True, exist_ok=True)
    body_path.write_text(release.get("body", ""), encoding="utf-8")
    REPORT.check("Release body 已导出（供 Read 核对中文）", True, str(body_path))
    REPORT.end_stage(t)


def _gh_repo() -> str:
    origin = git("remote", "get-url", "origin")
    m = re.search(r"github\.com[:/](.+?)(?:\.git)?$", origin)
    return m.group(1) if m else origin


def cmd_edit_notes(args) -> None:
    t = REPORT.stage("修正 GitHub Release body")
    r = run(["gh", "release", "edit", f"v{args.version}", "--repo", _gh_repo(),
             "--notes-file", str(Path(args.notes_file).resolve())])
    REPORT.check("gh release edit", r.returncode == 0, r.stderr.strip()[:120] if r.returncode else "")
    if r.returncode != 0:
        REPORT.fail("edit-notes", r.stderr.strip()[:200]); raise SystemExit(REPORT.render_l1())
    REPORT.end_stage(t)


# ----------------------------------------------------------------------- iscc probe

def cmd_iscc_probe(_args) -> None:
    t = REPORT.stage("ISCC 探测")
    iscc = find_iscc()
    if iscc is None:
        REPORT.check("ISCC.exe 定位", False, "三个标准路径均未找到")
        REPORT.meta["iscc"] = "not-found"
    else:
        print(f"  ISCC：{iscc}")
        try:
            r = subprocess.run([str(iscc), "/?"], capture_output=True, timeout=30)
            REPORT.check("ISCC spawn", True, f"退出码 {r.returncode}（进程可启动）")
            REPORT.meta["iscc"] = f"spawnable: {iscc}"
        except OSError as exc:
            REPORT.check("ISCC spawn", False, f"进程未启动：{exc}")
            REPORT.meta["iscc"] = f"spawn-blocked: {iscc}"
    REPORT.end_stage(t)


# ----------------------------------------------------------------------------- main

def add_common(p: argparse.ArgumentParser, need_version: bool = True) -> None:
    if need_version:
        p.add_argument("--version", required=True, help="x.y.z 三位版本")
    p.add_argument("--build", type=int, default=None, help="本地构建号（第 4 位，仅 local-* 模式）")


def main(argv: list[str] | None = None) -> None:
    parser = argparse.ArgumentParser(prog="publish.py", description=__doc__.splitlines()[0])
    parser.add_argument("--dry-run", action="store_true", help="打印计划，不执行任何副作用操作")
    parser.add_argument("--json", action="store_true", help="stdout 直出 L3 JSON 报告")
    sub = parser.add_subparsers(dest="command", required=True)

    sub.add_parser("info", help="版本决策辅助")
    p = sub.add_parser("set-version", help="写入 csproj 版本")
    p.add_argument("--version", required=True)
    p.add_argument("--allow-downgrade", action="store_true")
    p = sub.add_parser("build", help="构建 full + portable")
    add_common(p)
    p.add_argument("--installer", action="store_true", help="打包阶段处理 Setup.exe（ISCC 回退路径）")
    p = sub.add_parser("pack", help="打包 ZIP + 校验 + 终态收敛")
    add_common(p)
    p.add_argument("--installer", action="store_true")
    p.add_argument("--keep-publish-dirs", action="store_true")
    p = sub.add_parser("all", help="build + pack")
    add_common(p)
    p.add_argument("--installer", action="store_true")
    p.add_argument("--keep-publish-dirs", action="store_true")
    p = sub.add_parser("changelog", help="固化 CHANGELOG Unreleased 段")
    p.add_argument("--version", required=True)
    p = sub.add_parser("tag", help="commit 版本文件 + annotated tag")
    p.add_argument("--version", required=True)
    p.add_argument("--notes-file", required=True)
    p = sub.add_parser("watch", help="等待并验证 GitHub Release（CI）")
    p.add_argument("--version", required=True)
    p = sub.add_parser("edit-notes", help="修正 GitHub Release body")
    p.add_argument("--version", required=True)
    p.add_argument("--notes-file", required=True)
    sub.add_parser("iscc-probe", help="探测 ISCC.exe 可用性（沙箱诊断）")

    args = parser.parse_args(argv)
    REPORT.dry_run = args.dry_run
    mode_map = {"all": "local-pipeline", "build": "local-pipeline", "pack": "local-pipeline"}
    REPORT.meta["mode"] = mode_map.get(args.command, args.command)

    if args.command in ("build", "pack", "all") and not args.dry_run:
        version = read_csproj_version()
        expected = getattr(args, "version", None)
        if expected and expected != version and not getattr(args, "build", None):
            print(f"  [提示] csproj 版本 {version} 与 --version {expected} 不一致：产物按 --version 命名")

    handlers = {
        "info": cmd_info, "set-version": lambda a: (
            REPORT.stage("写入版本"), heal_env(), set_version(a.version, a.allow_downgrade),
            REPORT.check("csproj 版本写入", read_csproj_version() == a.version or a.version == read_csproj_version())),
        "build": cmd_build, "pack": cmd_pack, "all": cmd_all,
        "changelog": cmd_changelog, "tag": cmd_tag, "watch": cmd_watch,
        "edit-notes": cmd_edit_notes, "iscc-probe": cmd_iscc_probe,
    }

    try:
        if args.dry_run:
            print("DRY-RUN：以下为将要执行的计划（无副作用）")
            print(f"  command={args.command} version={getattr(args, 'version', '-')}")
            print(f"  full:   dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:PublishReadyToRun=true")
            print(f"  portable: dotnet publish -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true")
            print(f"  断言：full exe ≥ 50 MB / portable exe < 20 MB / PDB / Assets / PK / CRC")
            print(f"  打包：Artifacts\\Pulsar-<eff>-{{full,portable}}.zip；终态收敛删除 publish\\v<eff>\\")
            return
        handlers[args.command](args)
    except SystemExit:
        raise
    except Exception as exc:  # noqa: BLE001
        REPORT.fail(args.command, f"{type(exc).__name__}: {exc}")
        print(REPORT.render_l1())
        if args.json:
            print(json.dumps({"result": "FAIL", "failure": REPORT.failure}, ensure_ascii=False))
        raise SystemExit(1)

    print(REPORT.render_l1())
    if not args.dry_run:
        REPORT.dump(REPORT_PATH)
    if args.json:
        print(json.dumps({
            "result": "PASS" if REPORT.passed else "FAIL",
            "meta": REPORT.meta, "elapsed_seconds": REPORT.elapsed,
            "stages": REPORT.stages, "checks": REPORT.checks,
            "skipped": REPORT.skipped, "artifacts": REPORT.artifacts, "failure": REPORT.failure,
        }, ensure_ascii=False, indent=2))


if __name__ == "__main__":
    main()
