## ADDED Requirements

### Requirement: PKI plugin uses native SendInput for keystroke injection

The PKI plugin's `WindowsSendKeysWriter` SHALL use `InputHelper.SendText()` (native `SendInput` with `KEYEVENTF_UNICODE`) instead of `SendKeys.SendWait()`. The `ISendKeysWriter.SendWait(string keys)` method SHALL accept arbitrary text including Unicode characters and special characters without escaping.

#### Scenario: Plain text password injection
- **WHEN** `WindowsSendKeysWriter.SendWait("mypassword123")` is called
- **THEN** `InputHelper.SendText("mypassword123")` is invoked
- **AND** the target window receives the exact text without modifications

#### Scenario: Special characters pass through without escaping
- **WHEN** `WindowsSendKeysWriter.SendWait("user@domain{tab}secret")` is called
- **THEN** `InputHelper.SendText("user@domain{tab}secret")` is invoked
- **AND** the characters `{` and `}` are sent literally (not interpreted as SendKeys escape sequences)

### Requirement: ISendKeysWriter.EscapeForSendKeys is a no-op on native writer

The `ISendKeysWriter.EscapeForSendKeys(string?)` method SHALL return the input string unchanged when the underlying implementation uses `SendInput`. Callers that previously relied on SendKeys-format escaping (adding `{}` around special characters) SHALL be audited and updated, as `SendInput` does not require escaping.

#### Scenario: Nil escaping for native injection
- **WHEN** `EscapeForSendKeys("hello+world")` is called on the native writer
- **THEN** the method returns `"hello+world"` unchanged

### Requirement: SimpleCommandPlugin uses native SendInput for key sequences

The `SimpleCommandPlugin.SendKeysAsync` method SHALL translate SendKeys-format key sequences (e.g., `{ENTER}`, `^{TAB}`, `+{F4}`) to `InputHelper.SendKeyCombination()` calls using virtual key codes. Pure text portions SHALL use `InputHelper.SendText()`.

#### Scenario: Named key token translation
- **WHEN** `SendKeysAsync` receives keys `"hello{ENTER}world"`
- **THEN** `InputHelper.SendText("hello")` is called, then `InputHelper.SendKeyCombination(VK_RETURN)`, then `InputHelper.SendText("world")`

#### Scenario: Modifier key combination translation
- **WHEN** `SendKeysAsync` receives keys `"^c"`
- **THEN** `InputHelper.SendKeyCombination(VK_CONTROL, 'C')` is called

#### Scenario: Shifted character translation
- **WHEN** `SendKeysAsync` receives keys `"+{TAB}"`
- **THEN** `InputHelper.SendKeyCombination(VK_SHIFT, VK_TAB)` is called
