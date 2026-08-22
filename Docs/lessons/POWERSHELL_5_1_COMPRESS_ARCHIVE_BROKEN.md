# PowerShell 5.1 Compress-Archive Broken by PS7 PSModulePath (and Fake-Zip GNU tar)

**Status**: Published  
**Scope**: Lesson  
**Applies To**: `.pi/extensions/publish-local/core.ts` (`zipDirContents` / `verifyZipEntries`), any script that shells out to `powershell` / `tar`  
**Last Updated**: 2026-08-22

---

## Rule (TL;DR)

**Never call `powershell.exe` (Windows PowerShell 5.1) with `Compress-Archive` from a process whose `PSModulePath` contains the PowerShell 7 module directory.** Prefer `pwsh`, then `powershell -ExecutionPolicy Bypass`, then `%SystemRoot%\System32\tar.exe` (bsdtar). When verifying/creating zips, **always use the System32 bsdtar, never the PATH `tar`** (Git for Windows ships GNU tar, which cannot read or write real zips and can silently produce a POSIX-tar file named `.zip`).

---

## Symptom

- `/publish` builds successfully (`dotnet publish` writes `Artifacts\publish\v1.6.0\`), but the 打包 step fails:

  ```
  Compress-Archive : 无法加载模块“Microsoft.PowerShell.Archive”...无法加载文件
  C:\program files\powershell\7\Modules\...\Microsoft.PowerShell.Archive.psm1，
  因为在此系统上禁止运行脚本
  CommandNotFoundException / CouldNotAutoloadMatchingModule
  ```

- The csproj version bump gets rolled back (release aborts with no zip produced).
- Related trap: `tar -tf <real.zip>` fails with `This does not look like a tar archive` even though the zip opens fine in Explorer; or a `tar -a -c -f out.zip` run "succeeds" but produces a file that is actually a POSIX tar archive.

---

## Root Cause

Two independent environment traps collide on Windows:

1. **PSModulePath pollution breaks PowerShell 5.1's module autoload.** PowerShell 7 prepends its module dirs (`C:\Program Files\PowerShell\7\Modules`, etc.) to the *process* `PSModulePath`. When `powershell.exe` (5.1) inherits that environment, autoloading `Compress-Archive` finds **PS7's copy of `Microsoft.PowerShell.Archive` first** (it precedes `C:\Windows\System32\WindowsPowerShell\v1.0\Modules` in the path list). The PS7 `.psm1` is not subject to the special trust 5.1 grants modules in its own `$PSHOME\Modules`, so 5.1's execution policy (Restricted) blocks it → `CommandNotFoundException`. The System32 copy is never reached. (Same failure hits any built-in module, e.g. `Get-ExecutionPolicy` / `Microsoft.PowerShell.Security`.)

2. **PATH `tar` may be GNU tar, not bsdtar.** In a Git-for-Windows shell, `tar` resolves to `/usr/bin/tar` (GNU tar 1.35). GNU tar cannot read zip files, treats `E:\...` drive-letter paths as remote-host syntax, and with `-a` happily writes a **tar archive into a file named `.zip`** (magic `./` instead of `PK`). Only `C:\Windows\System32\tar.exe` (libarchive bsdtar) reads/writes real zips.

---

## Fix

`.pi/extensions/publish-local/core.ts`:

- `zipDirContents()` tries, in order:
  1. `pwsh -NoProfile -Command "Compress-Archive ..."` — PS7's module lives in its own dir and its default policy is `RemoteSigned`;
  2. `powershell -NoProfile -ExecutionPolicy Bypass -Command "Compress-Archive ..."` — Bypass overrides the script-block, and the PS7 module (PowerShellVersion="3.0") loads fine under 5.1;
  3. `zipTarPath()` (System32 bsdtar) with entries from `fs.readdirSync(dir)` and `-C dir` (relative entries, no `./` prefix).
- Every produced zip must pass `isZipFile()` (magic bytes `PK`) — catches fake zips.
- `verifyZipEntries()` lists entries with `zipTarPath()` (System32 bsdtar), never PATH `tar`.

---

## Verification

- `node .pi/extensions/publish-local/smoke.ts` — core logic smoke test.
- Manual zip of a publish dir succeeds and `verifyZipEntries` reports no missing entries.
