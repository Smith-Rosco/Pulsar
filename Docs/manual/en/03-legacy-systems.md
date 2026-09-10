# Sign Into Legacy Systems (Secure Fill & Web Scripts)

> Scenario: half a dozen intranet systems demand repeated sign-ins; legacy web pages support no browser extensions or Tampermonkey; the same form gets filled eight times a day. Goal: turn all of it into one-flick radial actions.

Pulsar brings two weapons against legacy systems:

| Weapon | Use case | Plugin |
| :--- | :--- | :--- |
| 🔐 **Secure fill & sign-in** | Account/password login, form filling, auto submit | Secret Fill (SecretFillPlugin) |
| 🌐 **Web scripts** | Custom automation on legacy intranet pages | Web Scripts (BookmarkletRunner) |

## 1. Secure Fill & Sign-In

### Save a credential

1. Pulsar Settings → **Secret Fill**;
2. Create a credential: label (e.g. "OA System"), account, password;
3. Passwords are encrypted with **Windows DPAPI** on your machine — never plaintext, never uploaded.

### Bind a sign-in action

1. Create a slot with the "Secret Fill" action;
2. Map target fields: where the account goes, where the password goes, whether to auto-click the sign-in button;
3. Assign a direction on the wheel.

### One-flick sign-in

On the target system's page press `Ctrl+Shift+Q`, flick toward the credential and release — account and password are injected and submitted automatically.

> 🔒 Security note: credentials are encrypted locally. Injection happens visibly on the login page in front of you; Pulsar never transmits credentials over the network.

## 2. Legacy Web Scripts

Many corporate intranet systems are too old for userscripts or browser extensions. Pulsar injects scripts from the desktop layer, bypassing those limits.

### Start from the example library (recommended)

Not a scripter? Settings → **Example Library** ships ready-made web-script examples — pick the one closest to your scenario.

### Create your own web-script action

1. Settings → Plugins → **Web Scripts**;
2. Create an action and paste the script tailored to that page;
3. Bind it to a direction.

### Run it

Open the intranet page → `Ctrl+Shift+Q` → flick and release. Repeated data entry, paging, and export become one action.

## Typical combo: sign in + enter data

Put "OA sign-in" (Secret Fill) and "OA daily report" (Web Scripts) in adjacent directions. Each morning: summon → flick sign-in → summon → flick report — two flicks instead of ten minutes of repetition.

## Next Steps

- [Switch Windows](04-switch-windows.md)
- Trouble? Check the [FAQ](05-faq.md).
