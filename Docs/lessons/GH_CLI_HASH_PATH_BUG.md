# gh CLI Truncates Local Paths Containing `#` (URL Fragment)

**Status**: Published  
**Scope**: Lesson  
**Applies To**: `.pi/extensions/publish-local/index.ts` (4.7 GitHub Release), any script that passes local file paths to `gh`  
**Last Updated**: 2026-08-22

---

## Rule (TL;DR)

**Never pass a local file path containing `#` to `gh` (e.g. `gh release create`).** gh CLI parses `#` as a URL fragment separator and truncates the path there, producing a cryptic `GetFileAttributesEx <truncated-path>: The system cannot find the file specified.` error. Copy the files to a `#`-free temp directory and pass the absolute temp paths instead.

---

## Symptom

`/publish gh-only` fails at the GitHub Release step with:

```
Error: gh release create 失败:
GetFileAttributesEx E:\8_Project\10_C: The system cannot find the file specified.
```

Repo path is `E:\8_Project\10_C#\Pulsar_Project` — the `#` in the path truncates the argument to `E:\8_Project\10_C` inside gh. The local release (build/zip/commit/tag) succeeds; only the `gh` step fails. Verified against gh CLI 2.98.0: any local path argument containing `#` (asset zip, `--notes-file`) fails the same way.

## Root Cause

gh CLI (Go) internally treats `#` in path arguments as a URL fragment separator (it runs path-like args through URL parsing). `E:\8_Project\10_C#\Pulsar_Project\Artifacts\Pulsar-v1.6.1.zip` becomes `E:\8_Project\10_C`, and `os.Stat` on that truncated path yields the `GetFileAttributesEx` error. This is not a pi/Node spawn issue — it reproduces when invoking `gh.exe` directly. Other tools (git, dotnet, pwsh, Compress-Archive) handle `#` paths fine.

## Fix

`.pi/extensions/publish-local/index.ts` step 4.7:

- `fs.mkdtempSync(path.join(os.tmpdir(), "pulsar-upload-"))` → copy zip + notes into it (temp paths never contain `#`)
- pass the temp absolute paths to `gh release create`
- clean up the temp dir in `finally`

## Verification

- Reproduce: `gh release create vTEST "E:\8_Project\10_C#\Pulsar_Project\Artifacts\x.zip" --draft` → `GetFileAttributesEx E:\8_Project\10_C`
- Fixed: same command with the file copied to `%TEMP%` (no `#`) → succeeds (draft release URL returned)
