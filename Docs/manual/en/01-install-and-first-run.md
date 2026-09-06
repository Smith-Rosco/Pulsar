# Install & First Run

> This page walks you through: download → install (or unzip) → SmartScreen → first launch → your first menu.

## 1. Download

Head to the [Releases page](https://github.com/Smith-Rosco/Pulsar/releases):

| Package | Description | Best for |
| :--- | :--- | :--- |
| **Installer (Setup.exe)** | Installs to Program Files with a Start Menu entry and uninstaller | Most users |
| **Standalone (zip)** | Bundles the .NET runtime — unzip and run, no installation | Portable use / no admin rights |

> Verify the SHA256 manifest attached to each Release before running (see the [FAQ](05-faq.md#how-do-i-verify-the-sha256-of-a-downloaded-file)).

## 2. Install

### Installer

1. Double-click `Pulsar-v{version}-Setup.exe`;
2. When SmartScreen shows "Windows protected your PC": click **More info** → **Run anyway** (Pulsar does not ship a code-signing certificate yet — this is expected);
3. Follow the wizard: installs to Program Files by default, with an optional "start with Windows" checkbox;
4. Pulsar starts automatically and waits quietly in the system tray.

> Note: installation requires admin rights (Program Files). Your configuration and data live in `%AppData%\Pulsar` — **upgrades keep it, and uninstall keeps it by default**.

### Standalone

1. Unzip `Pulsar-v{version}-Standalone-win-x64.zip` into a **fixed folder** (e.g. `C:\Pulsar` — not a temp or downloads folder);
2. Double-click `Pulsar.exe`.

## 3. First Launch

The **onboarding tutorial** appears on first launch — follow it once (a few minutes) to learn:

- How to summon the radial menu (`Ctrl+Shift+Q` command mode / `Ctrl+Q` switch mode);
- How to flick toward a target and release to trigger it;
- How to add your own actions in Settings.

![Main interface](../../media/release/01-main-interface.png)

After onboarding, install the ready-made **office action presets**, or browse the example library.

## 4. Your First Menu

1. Press **`Ctrl+Shift+Q`** in any app;
2. A radial menu opens next to your cursor:

![Radial menu summoned](../../media/release/02-radial-summoned.png)

3. Flick toward an action and release — it runs immediately.

## 5. Uninstall

- **Installer**: uninstall via Windows Settings → Apps, or the Start Menu entry; user data (`%AppData%\Pulsar`) is kept by default and clearly stated during uninstall;
- **Standalone**: just delete the folder; remove `%AppData%\Pulsar` for a full cleanup.

## Next Steps

- [Run Macros (Excel/WPS)](02-run-macros.md)
- [Sign Into Legacy Systems](03-legacy-systems.md)
- [Switch Windows](04-switch-windows.md)
