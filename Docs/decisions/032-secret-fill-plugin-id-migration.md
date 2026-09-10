# ADR-032: Unfreeze Layer 3 — Rename the Secret Fill Plugin Id with a Load-Time Migration

**Status**: Accepted
**Date**: 2026-09-10
**Deciders**: milo (owner), WorkBuddy agent (implementation)
**Supersedes (partially)**: [031-secret-fill-rename-plugin-id-frozen.md](./031-secret-fill-rename-plugin-id-frozen.md) — item 1 ("Layer 3 is frozen") is reversed here; items 2–6 remain in force.
**Related**: [005-config-single-writer.md](./005-config-single-writer.md), [009-config-snapshot-seam.md](./009-config-snapshot-seam.md), [019-journal-single-source.md](./019-journal-single-source.md)

---

## Context

ADR-031 renamed the credential module from `PKI` to `SecretFill` but **froze**
the persisted plugin id `com.pulsar.pki`. The stated reason was benefit-to-cost:
the id is persisted in user configuration, changing it requires a migration plus
a legacy-alias read path, and the user never sees the id — only the `AutoFill`
display name.

That reasoning was sound **at that time**, and the freeze is what made the
symbol rename cheap and reviewable. But it left a documented inconsistency: the
module's symbols say `SecretFill` while its identifier says `pki`.

The owner subsequently directed the id be brought in line, accepting the
migration cost. A follow-up survey sharpened the picture and revealed that the
original cost estimate was **dominated by a factor the freeze did not address**:

| Surface | Count | Notes |
|---|---|---|
| Live source literals (`com.pulsar.pki`) | **12 sites** | only **1** went through a constant |
| Test assertions pinning the literal | 29 sites / 11 files | incl. a case-insensitivity test |
| E2E fixtures + Simulator scripts | 6 | `office-workbench-dark.json`, `PluginUsageStats.json`, 2 run scripts |
| Persisted user data | `Profiles.json` 5 slots, `plugins` `{}`, `PluginUsageStats.json` 1 record (19 executions) | `secrets.json` **0** |

The decisive finding was the **12 hand-written literals with no shared constant**.
Whatever the freeze decided, the id was one careless edit away from drift: any
new code path comparing `slot.PluginId == "com.pulsar.pki"` would silently be
the 13th copying site. The freeze protected the *value*, not the *reference
discipline*.

Two pieces of existing infrastructure made the unfreeze tractable rather than
speculative:

- `ConfigService.LoadAsync` already runs load-time normalization followed by an
  **idempotent self-healing persist** (`SaveAsync(loaded, expectedRevision: null)`),
  with a working precedent (`NormalizeSwitchLaunchPaths`, `ConfigService.cs:274-280`)
  and a test recipe (`ConfigServiceLoadTests.LoadAsync_ShouldMigrateRelativeSwitchPath_ToAbsolute`).
- `Core/Converters/LegacySlotConverter.cs` establishes that reading a legacy
  on-disk shape and rewriting it in memory is an accepted pattern here.

Critically, **no plugin-id-level alias mechanism existed**. `PluginBase.DispatchAsync`'s
`aliases` dictionary normalizes the **action** only (`PluginBase.cs:229-234`);
runtime lookup of an unknown id fails hard with `PluginResult.Error("Plugin not found")`
(`PluginRuntimeKernel.cs:871-878`). So the choice was binary: migrate the data, or
build an id-level alias table. Migration was chosen — it reuses a proven seam,
leaves no permanent indirection in the hot path, and heals the user's disk once.

## Decision

**Unfreeze layer 3. Rename the Secret Fill plugin id to `com.pulsar.secretfill`,
and migrate existing user data at load time.**

Five parts:

1. **New id: `com.pulsar.secretfill`.** All-lowercase, no separator — matching
   the existing family (`com.pulsar.winswitcher`, `com.pulsar.command`,
   `com.pulsar.bookmarklet`, `com.pulsar.vbarunner`, `com.pulsar.system`), none
   of which use hyphens. Rejected `com.pulsar.secret-fill` (breaks family
   casing/separator convention) and `com.pulsar.autofill` (would re-couple the
   id to the display name, which is already deliberately decoupled and may
   change again).

2. **One constants class, all literals eliminated.** New
   `Core/Plugin/PluginIds.cs` declares every built-in id as `const string`, plus
   `LegacySecretFill = "com.pulsar.pki"` and two helpers, `Normalize` and
   `IsSecretFill`, which accept **both** the current and legacy ids. All 12 live
   literal sites now reference it. Declared `const` rather than `static readonly`
   deliberately: `SlotPresentation`'s `switch` expressions require compile-time
   constant patterns.

3. **Load-time migration, idempotent, in `ConfigService`.** A new
   `MigrateLegacyPluginIds` runs before the existing `Profiles` normalization and
   covers both persisted positions: each profile's `CommandMode`/`SwitchMode`
   slot `PluginId`, and the root `Plugins` dictionary key. It joins the existing
   persist trigger, so the file heals once on disk and a second load is a no-op.
   On a key collision (both ids present) the **current-id profile wins** and the
   stale legacy entry is dropped — never overwrite config the user wrote under
   the new id.

4. **Usage-history migration, merged not dropped.**
   `PluginUsageTracker.LoadAsync` normalizes each record's `PluginId`. If both
   ids are present on disk, the records are **folded together** rather than one
   being discarded: counters sum, dates widen to the min/max, and per-key
   dictionaries (slot, hour, daily) sum. The user's history is visible in the
   analytics surface, so silently dropping one record would be data loss.

5. **`SlotPresentation` normalizes before matching.** Both `ResolveTypeBadge`
   and `ResolveTypeToneKey` call `PluginIds.Normalize` at entry, so a slot
   carrying the legacy id — held in memory before the load-time migration runs,
   or passed by a UI/test caller — still resolves its badge and tone. The
   presentation layer is a pure helper and should not depend on call ordering.

### What did not change

- **The `PKI`→`SecretFill` symbol rename from ADR-031 stands.** Only its
  layer-3 freeze is reversed.
- **`secrets.json` is untouched.** Secrets are keyed by GUID, not plugin id
  (verified: 0 occurrences of the plugin id). Changing the id triggers **no key
  migration** and no re-encryption.
- **Historical records are still not rewritten.** `CHANGELOG.md` (append-only
  here), `Docs/ops/RELEASE_NOTES-*`, `Docs/archive/**`, `Docs/journal/**`,
  `openspec/changes/**`, and ADR-031's own text keep the old literal. ADR-031 is
  amended by pointer, not by editing — the same rule it applied to its own
  predecessors.

## Consequences

**Positive**
- The identifier, the symbol family, and the module name finally agree. The
  `CONTEXT.md` glossary can drop its "frozen historical identifier" caveat.
- **The reference-discipline problem is fixed permanently.** Twelve scattered
  literals collapse to one constants class. A future id change now touches one
  file plus one migration, instead of a grep-and-hope sweep.
- The migration reuses a proven seam and is idempotent, so a crash mid-save
  cannot corrupt config — the next load simply re-runs the (cheap) normalization.
- User history survives: the 19 recorded executions were preserved through the
  rename and verified on real data.

**Negative / accepted**
- **This is a one-way door for the literal.** Once users run a build with the
  migration, their `Profiles.json` holds the new id; reverting the code without a
  reverse migration would orphan every slot. Reversal requires a second
  migration — the cost is symmetric, not free.
- One extra startup pass over slots and the `plugins` dictionary. Measured as
  negligible (a string comparison per slot) and it short-circuits as soon as no
  legacy value is found.
- A third-party consumer reflecting on the reference-domain string
  `com.pulsar.pki` would break. Marked **【假设】** — not verified, because no
  such consumer is known to exist; the id is not part of any published document.

## Verification

All evidence below is **【已验证】** unless marked otherwise.

| Check | Result |
|---|---|
| `dev.ps1 build` | **0 errors, 0 warnings** |
| `dev.ps1 test` | **1541/1541 passed, 0 failed** (baseline 1521 + 20 new migration/contract tests) |
| `dev.ps1 verify-rules` | OK |
| Live source `com.pulsar.pki` literals | **12 → 0** (only `PluginIds.LegacySecretFill` + its docs + migration comments remain) |
| **Real `Profiles.json` (owner's live copy)** | before **5** → after **0** legacy hits, **5** new-id hits; on-disk healed; all 5 slots migrated in memory |
| **Real `PluginUsageStats.json` (owner's live copy)** | record moved to the new id with **19/19 executions preserved**, `2026-09-07: 17` / `2026-09-09: 2` intact, **0** remaining under the legacy id |

New tests added (20):

- `ConfigServiceLoadTests` — slot migration (both slot lists), `plugins`-dictionary
  key migration with payload preserved, both-ids-collision precedence, and
  idempotence (an already-migrated file is **not** rewritten).
- `PluginUsageTrackerTests` — legacy key migration, both-ids **merge** (counters
  summed, daily/slot dictionaries summed per key, date bounds widened), and
  non-target plugins left untouched.
- `PluginIdsTests` — the constant contract: `Normalize` casing tolerance,
  pass-through for foreign ids, blank handling; `IsSecretFill` accepting both ids
  and rejecting others.

**Test blind spot (acknowledged)**: the end-to-end proof above ran against a
*copy* of the owner's live config via a temporary probe that was deleted after
the run. There is no permanently committed fixture containing real user data —
deliberately, since a real profile carries personal paths and secret ids. The
committed tests cover the same logic with synthetic data.

**Strongest counter-case**: the migration is only exercised through
`ConfigService.LoadAsync` and `PluginUsageTracker.LoadAsync`. If either service
is ever bypassed — a code path deserializing `ProfilesConfig` directly, or a tool
editing `Profiles.json` out of band — the legacy id survives into runtime and
fails with `Plugin not found`. The mitigation is that `IsSecretFill` /
`Normalize` accept both ids at the comparison sites, so the *behavioural* failure
needs a caller that both bypasses the services and compares ids directly.

## Change History

- `v1.0.0` (2026-09-10): layer 3 unfrozen — id renamed to `com.pulsar.secretfill`,
  `PluginIds` constants introduced, load-time config + usage-stats migration added,
  ADR-031 item 1 superseded by pointer.
