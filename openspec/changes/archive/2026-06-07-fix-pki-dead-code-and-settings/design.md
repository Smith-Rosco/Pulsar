## Context

PKI injection currently has two parallel input abstraction layers:
1. **`IInjectionExecutor` → `SendKeysInjectionExecutor`** — the actual execution path, wired in DI
2. **`IInputSimulator` → `WindowsInputSimulator`** — registered in DI but never invoked by any production code

The spec (`pki-runtime-architecture`) explicitly mandates SendKeys-first policy. The UIA-first pathway (`IUiaTextWriter` → `WindowsUiaTextWriter` → `UiaHelper.TrySetFocusedElementText`) was built but never wired into the `SendKeysInjectionExecutor`'s step loop. The `WindowsInputSimulator` is only used by its own unit tests.

Settings `injectionDelay` and `useUiaFirst` are declared in schema, accepted by `PkiPlugin.UpdateSettings`, enriched into execution args — but `SendKeysInjectionExecutor` and `BuildPlan` completely ignore them.

The `ISendKeysWriter` abstraction is partially bypassed: `SendText` steps use `_sendKeysWriter.SendWait()`, but `SendKey` steps call static `InputHelper.GetNamedKey()` / `InputHelper.SendKeyCombination()` directly, making that code path untestable via DI mocks.

## Goals / Non-Goals

**Goals:**
- Remove dead `IInputSimulator` / `WindowsInputSimulator` / `IUiaTextWriter` / `WindowsUiaTextWriter` code and tests
- Remove `useUiaFirst` setting from schema, metadata, and `PkiPluginSettings`
- Wire `injectionDelay` into `BuildPlan` so inter-step delays are configurable (replacing hardcoded 10ms)
- Route `SendKey` injection through `ISendKeysWriter` (add `SendKeyCombination` method)
- Rename `EscapeForSendKeys` → `SanitizeInput` to accurately reflect behavior
- Log exceptions in `CredentialsManager.Decrypt` before returning empty string
- Fix `SecretRepository` retry bugs (unreachable return, silent save failure)
- Add execution timeout to `SendKeysInjectionExecutor`

**Non-Goals:**
- Changing the actual injection mechanism (SendInput via InputHelper remains)
- Adding UIA-first fallback logic (spec mandates SendKeys-first; UIA-first is removed)
- Changing the `SecretPayload` shape or `secrets.json` format
- Altering the focus restoration flow or `IFocusManager` behavior

## Decisions

### 1. Remove UIA pathway entirely vs. wire UIA-first fallback

**Decision**: Remove entirely.

**Rationale**: The spec (`pki-runtime-architecture`) explicitly mandates SendKeys-first as the supported policy. The UIA pathway was already bypassed in production. If UIA-first behavior is desired in the future, it should be reintroduced as a spec-level change with proper UIA→SendKeys fallback logic, not kept as dead code. Keeping dead code adds maintenance burden (4 source files, 1 test file, DI registrations, settings schema).

### 2. Expand `ISendKeysWriter` vs. add separate `IKeySender` interface

**Decision**: Add `SendKeyCombination(string key)` method to existing `ISendKeysWriter`.

**Rationale**: `SendKeysInjectionExecutor` already depends on `ISendKeysWriter`. Adding a method keeps the single-injection-abstraction design. An alternative would be to have `ExecuteSendKey` call `SendWait` with the key string (e.g., `"{TAB}"`) and let `WindowsSendKeysWriter` detect SendKeys-format tokens, but that reintroduces SendKeys-format parsing into the writer, which conflicts with the Unicode-based `InputHelper.SendText` approach. The `SendKeyCombination` method cleanly maps to `InputHelper.SendKeyCombination`.

### 3. `injectionDelay` wiring: control ALL inter-step delays vs. only SendText/SendKey gaps

**Decision**: The `injectionDelay` setting controls delays between keystroke steps (account→TAB→password→ENTER). The initial stabilization delay (100ms after RestoreFocus) remains hardcoded as a separate concern.

**Rationale**: The 100ms startup delay is about focus stabilization, not keystroke timing. User-configured `injectionDelay` is about compatibility with slow applications that can't process rapid keystroke sequences. These are distinct concerns.

### 4. Timeout: hardcoded 15s vs. configurable via args

**Decision**: Hardcoded 15-second overall timeout for the injection sequence, not a user-facing setting.

**Rationale**: ATimeout should prevent hung injection, not be a tuning knob. If users need to adjust it, it can be promoted to a setting later. The PkiPlugin already accepts a `CancellationToken` from the framework — the timeout should create a linked CTS.

## Risks / Trade-offs

- **Risk**: Removing `useUiaFirst` setting silently drops it from existing profiles → **Mitigation**: Setting was never consumed, so removal has zero behavioral impact. The key is silently dropped from the `enrichedArgs` dictionary, same as before.
- **Risk**: `CredentialsManager.Decrypt` now logs exception details → **Mitigation**: Logging the exception type and message, not the encrypted data. DPAPI failures don't expose plaintext.
- **Risk**: `injectionDelay` now actually affects timing → **Mitigation**: Default remains 50ms (schema default). Existing behavior uses hardcoded 10ms, so users who never set this see a slight slowdown (10ms→50ms between steps). This may actually improve reliability in slow apps.
- **Risk**: Adding `SendKeyCombination` to `ISendKeysWriter` is a breaking interface change → **Mitigation**: `ISendKeysWriter` only has one implementation (`WindowsSendKeysWriter`) and one internal test mock. No external plugin implements this interface.
