# ADR-031: Rename the PKI Module to Secret Fill (Symbols Only; Plugin Id Frozen)

**Status**: Accepted — **partially superseded** (item 1, the layer-3 freeze, is reversed by [032](./032-secret-fill-plugin-id-migration.md); items 2–6 remain in force)
**Date**: 2026-09-10
**Deciders**: milo (owner), WorkBuddy agent (implementation)
**Related**: [CONTEXT.md](../../CONTEXT.md) glossary, [027-documentation-structure-v6-single-authoritative-sources.md](./027-documentation-structure-v6-single-authoritative-sources.md), [007-external-plugin-permission-consent.md](./007-external-plugin-permission-consent.md), [032-secret-fill-plugin-id-migration.md](./032-secret-fill-plugin-id-migration.md)

---

## Context

The core credential module was named **PKI**. That abbreviation has a settled
industry meaning — Public Key Infrastructure: certificate authorities, X.509
certificates, public/private key pairs, chains of trust. The module implements
**none** of it. What it actually does is DPAPI-encrypted secret storage plus
queue-ordered keystroke (SendKeys) injection into a captured target window.

`CONTEXT.md` already recorded the mismatch implicitly: its glossary entry read
"The Core module that stores Secrets and injects credentials into target
windows" with `_Avoid_: Password manager` — a definition that never mentioned
certificates, keys, or trust chains, because the module has nothing to do with
them.

The name was therefore actively misleading to anyone who knows the term: it
predicts a certificate lifecycle where the code has a credential vault. The
user-visible surface had already been corrected years earlier — the plugin's
`DisplayName` is `AutoFill`, and `Strings.resx` contained **zero** mentions of
PKI. The misnomer survived only in internal symbols.

A measured survey (2026-09-10) found the identifier spread across four layers
with sharply different risk profiles:

| Layer | Scope | Count | Risk |
|---|---|---|---|
| 1 | Build-time symbols (`PkiPlugin`, `IPkiSecretStore`, …) | ~240 | Compile-checked |
| 2 | Cross-module internal references (17 files) | ~70 | Compile-checked |
| 3 | Persisted plugin id `com.pulsar.pki` | 59 | **Frozen contract** |
| 4 | Docs and examples | ~113 | Text only |

## Decision

**Rename layers 1, 2 and 4. Freeze layer 3.**

1. **Layer 3 is frozen.** The literal `com.pulsar.pki` is not touched anywhere.
   It is persisted in user `Profiles.json` as each slot's `plugin` field
   (verified: 5 occurrences in the owner's live config) and is a dictionary key
   in `ProfilesConfig.Plugins`. Renaming it would require a config migration and
   a legacy-alias read path, for zero user-visible benefit — the user never sees
   the id, only the `AutoFill` display name.

2. **New name: `SecretFill`.** Chosen over `AutoFill` (already the display name,
   but collides with the existing `Preset.*.FormFill` / `Example.FormFill`
   family and with other fill-style plugins) and over `Credential`/`CredentialFill`
   (less specific than the established `Secret*` vocabulary, which already
   covers 844 occurrences via `SecretRepository`, `SecretPayload`,
   `ISecretProtector`). `SecretFill` joins the dominant existing family and
   states precisely what the module does — fills, it does not submit.

3. **Namespace segment renamed.** `Pulsar.Plugins.Core.Pki` →
   `Pulsar.Plugins.Core.SecretFill`. Verified safe: the namespace string appears
   in no persisted artifact, and `IsManifestEntryPointMatch`
   (`PluginLoader.cs:379`) only compares `entryPoint` for *external* plugins,
   which carry a `manifest.json`. Built-in plugins have no manifest, and an
   empty `entryPoint` short-circuits to `true` regardless.

4. **`Execution` stays qualified.** `PkiExecutionStage` → `SecretFillExecutionStage`,
   not bare `ExecutionStage`. `Execution` is high-frequency vocabulary in the
   plugin pipeline; an unqualified type would be ambiguous at a glance.

5. **Historical records are not rewritten.** `CHANGELOG.md`, `Docs/ops/RELEASE_NOTES-*`,
   `Docs/archive/**`, `Docs/journal/**`, and `openspec/changes/**` keep the old
   name. These record decisions taken under the old name; rewriting them would
   falsify history. Live sources renamed: `openspec/specs/**` (capability specs,
   explicitly outside ADR-027's doc-routing scope but still living documents),
   `AGENTS.md`, `ARCHITECTURE.md`, `CONTEXT.md`, `Docs/architecture/**`,
   `Docs/plugins/**`, `openwiki/**`.

6. **The glossary now records the ban.** `CONTEXT.md` gains a `Secret Fill`
   entry listing `PKI` under `_Avoid_` with the reason, plus a `Plugin Id`
   entry explaining that `com.pulsar.pki` is a frozen historical identifier.
   A future contributor searching for "PKI" finds an explanation instead of
   a silent mismatch.

### Symbol mapping

| Before | After |
|---|---|
| `Plugins/Core/Pki/` | `Plugins/Core/SecretFill/` |
| `Pulsar.Plugins.Core.Pki` | `Pulsar.Plugins.Core.SecretFill` |
| `PkiPlugin` | `SecretFillPlugin` |
| `PkiExecutionService` | `SecretFillExecutionService` |
| `IPkiExecutionService` | `ISecretFillExecutionService` |
| `PkiExecutionStage` / `PkiExecutionResult` | `SecretFillExecutionStage` / `SecretFillExecutionResult` |
| `PkiSecretMetadataResolver` / `IPkiSecretMetadataResolver` | `SecretFillMetadataResolver` / `ISecretFillMetadataResolver` |
| `IPkiSecretStore` | `ISecretStore` |
| `PkiPluginSettings` | `SecretFillPluginSettings` |
| `DebugPkiRedaction` | `DebugSecretFillRedaction` |
| `PkiPluginId` | `SecretFillPluginId` |
| `com.pulsar.pki` | **unchanged (frozen)** |

## Consequences

**Positive**
- The module name now predicts its behaviour; the PKI/certificate false signal is gone.
- No config migration, no legacy alias, no disk-format change.
- The rename is provably contained: layer 3 counters were identical before and
  after (source tree 59, user `Profiles.json` 5, `PluginUsageStats.json` 1).

**Negative / accepted**
- One residual inconsistency: the persisted plugin id still reads
  `com.pulsar.pki` while the module is called Secret Fill. This is deliberate and
  is documented in `CONTEXT.md` so it reads as a decision, not an oversight.
- Historical documents use the old name. A reader jumping into
  `Docs/archive/2026-03/2026-03-01-PKI_IMPLEMENTATION.md` sees "PKI" with no
  in-file pointer; the glossary entry is the bridge.

**Reversal cost**: low for symbols (a second mechanical rename), high for the
decision itself — the frozen id is what makes the rename cheap, so undoing
layer 3 later would cost a migration regardless of direction.

## Verification

- `powershell -File scripts/dev.ps1 build` → **0 errors, 0 warnings**
- `powershell -File scripts/dev.ps1 test` → **1521/1521 passed, 0 failed**
  (identical to the pre-rename baseline)
- `com.pulsar.pki` occurrence count: **59 → 59** (source tree), **5 → 5** (live
  user `Profiles.json`), **1 → 1** (`PluginUsageStats.json`)
- Residual `PKI` tokens in live sources: only `CONTEXT.md`'s deliberate `_Avoid_`
  entry and one `WorkbenchPillarCatalogTests.cs` assertion that verifies the
  frozen id matches case-insensitively (`"COM.PULSAR.PKI"`)

## Change History

- `v1.0.0` (2026-09-10): initial decision — symbols renamed to `SecretFill`,
  persisted plugin id frozen, historical records preserved.
- `v1.0.1` (2026-09-10): item 1 (layer-3 freeze) **superseded** by ADR-032,
  which renamed the id to `com.pulsar.secretfill` behind a load-time migration.
  The body above is left as written; see ADR-032 for the reversal. Items 2–6
  (naming rationale, namespace rename, qualified `Execution`, no history rewrite,
  glossary ban) remain in force.
