## ADDED Requirements

### Requirement: KeysLexer tokenizes plain text into TextInstruction

The system SHALL provide a `KeysLexer` class whose `Parse` method converts sendkeys-formatted strings into an ordered list of `KeyInstruction` objects. Plain text characters (not `{`, `^`, `+`, `%`) SHALL be grouped into a single `TextInstruction` containing the accumulated characters.

#### Scenario: Single word tokenized as one TextInstruction

- **WHEN** `KeysLexer.Parse("hello")` is called
- **THEN** the result SHALL be a list containing exactly one `TextInstruction` with `Text = "hello"`

#### Scenario: Multiple words tokenized as one TextInstruction

- **WHEN** `KeysLexer.Parse("hello world")` is called
- **THEN** the result SHALL be a list containing exactly one `TextInstruction` with `Text = "hello world"`

#### Scenario: Empty string returns empty list

- **WHEN** `KeysLexer.Parse("")` is called
- **THEN** the result SHALL be an empty list

### Requirement: KeysLexer tokenizes named keys in curly braces

The system SHALL recognize `{TOKEN}` patterns and produce a `KeyPressInstruction` when the token matches a known virtual key name. Unknown tokens in braces SHALL be emitted as literal `TextInstruction` with the braces preserved.

#### Scenario: Named key tokenized as KeyPressInstruction

- **WHEN** `KeysLexer.Parse("{ENTER}")` is called
- **THEN** the result SHALL contain exactly one `KeyPressInstruction` with `VkCode = InputHelper.VK_RETURN`

#### Scenario: Unknown named key emitted as literal text

- **WHEN** `KeysLexer.Parse("{UNKNOWN}")` is called
- **THEN** the result SHALL contain exactly one `TextInstruction` with `Text = "{UNKNOWN}"`

#### Scenario: Unclosed brace treated as literal text

- **WHEN** `KeysLexer.Parse("abc{ENTER")` is called
- **THEN** the result SHALL contain a `TextInstruction` with `Text = "abc"` followed by a `TextInstruction` with `Text = "{"`, followed by a `TextInstruction` with `Text = "ENTER"`

### Requirement: KeysLexer tokenizes modifier-key combinations

The system SHALL recognize `^` (Control), `+` (Shift), and `%` (Alt) modifier prefixes and produce a `KeyCombinationInstruction` containing all modifiers plus the following key.

#### Scenario: Single modifier with plain character

- **WHEN** `KeysLexer.Parse("^c")` is called
- **THEN** the result SHALL contain exactly one `KeyCombinationInstruction` with `Modifiers` containing `VK_CONTROL` and `InputHelper.CharToVkCode('c')`

#### Scenario: Multiple modifiers with plain character

- **WHEN** `KeysLexer.Parse("^+a")` is called
- **THEN** the result SHALL contain exactly one `KeyCombinationInstruction` with `Modifiers` containing `VK_CONTROL`, `VK_SHIFT`, and `InputHelper.CharToVkCode('a')`

#### Scenario: Modifier followed by named key in braces

- **WHEN** `KeysLexer.Parse("^{F4}")` is called
- **THEN** the result SHALL contain exactly one `KeyCombinationInstruction` with `Modifiers` containing `VK_CONTROL` and `VK_F4`

#### Scenario: Consecutive modifier characters accumulate

- **WHEN** `KeysLexer.Parse("^%+v")` is called
- **THEN** the result SHALL contain exactly one `KeyCombinationInstruction` with `Modifiers` containing `VK_CONTROL`, `VK_MENU`, `VK_SHIFT`, and `InputHelper.CharToVkCode('v')`

#### Scenario: Modifier followed by unknown brace token falls through

- **WHEN** `KeysLexer.Parse("^{BAD}")` is called
- **THEN** the result SHALL contain a `TextInstruction` with `Text = "{BAD}"` (no key or combination instruction is emitted)

### Requirement: KeysLexer tokenizes mixed sequences correctly

The system SHALL handle sequences mixing text, named keys, and modifier combinations in a single call.

#### Scenario: Mixed sequence produces correct instruction order

- **WHEN** `KeysLexer.Parse("^c{ENTER}world")` is called
- **THEN** the result SHALL contain three instructions in order:
  1. `KeyCombinationInstruction` (Ctrl+C)
  2. `KeyPressInstruction` (Enter)
  3. `TextInstruction` ("world")

#### Scenario: Text interrupted by named key then text resumes

- **WHEN** `KeysLexer.Parse("user{TAB}pass")` is called
- **THEN** the result SHALL contain three instructions in order:
  1. `TextInstruction` ("user")
  2. `KeyPressInstruction` (Tab)
  3. `TextInstruction` ("pass")

### Requirement: KeySender executes instructions by delegating to InputHelper

The system SHALL provide a `KeySender : IKeySender` class that iterates through `KeyInstruction` objects and calls the appropriate `InputHelper` method: `SendText` for text, `SendKeyCombination` for named keys, and `SendKeyCombination(modifiers)` for combinations.

#### Scenario: TextInstruction delegates to InputHelper.SendText

- **WHEN** `KeySender.Execute(new TextInstruction("hello"))` is called
- **THEN** `InputHelper.SendText("hello")` SHALL be invoked exactly once

#### Scenario: KeyPressInstruction delegates to InputHelper.SendKeyCombination

- **WHEN** `KeySender.Execute(new KeyPressInstruction(VK_RETURN))` is called
- **THEN** `InputHelper.SendKeyCombination(VK_RETURN)` SHALL be invoked exactly once

#### Scenario: KeyCombinationInstruction delegates with modifiers in order

- **WHEN** `KeySender.Execute(new KeyCombinationInstruction([VK_CONTROL, VkCode('c')]))` is called
- **THEN** `InputHelper.SendKeyCombination(VK_CONTROL, VkCode('c'))` SHALL be invoked exactly once

#### Scenario: CancellationToken stops execution mid-sequence

- **WHEN** `KeySender.ExecuteAsync(instructions, cancellationToken)` is called with a pre-cancelled token
- **THEN** `OperationCanceledException` SHALL be thrown before any `InputHelper` call is made
