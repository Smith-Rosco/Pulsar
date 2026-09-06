# FAQ

## Install & Launch

### "Windows protected your PC" when running the installer

Pulsar does not ship a code-signing certificate yet, so SmartScreen flags unsigned programs. Click **More info** → **Run anyway**. If in doubt, verify the [SHA256](#how-do-i-verify-the-sha256-of-a-downloaded-file) first.

### Does installation require admin rights?

The installer does (Program Files). The Standalone zip does not — ideal for locked-down corporate machines.

### Double-click does nothing / no tray icon?

1. Standalone: make sure you extracted to a **fixed folder** (don't run from inside the zip);
2. Check your antivirus quarantine and add an exclusion;
3. Pulsar lives in the system tray (bottom-right) — click the tray icon to open its menu.

### Will uninstalling lose my configuration?

No. Configuration and credentials live in `%AppData%\Pulsar`; uninstall keeps them by default (stated in the uninstaller). A reinstall picks up where you left off.

## Usage

### Forgot the hotkeys / want to change them?

Defaults: `Ctrl+Shift+Q` command mode, `Ctrl+Q` switch mode. Change them in Settings → Hotkeys.

### Flicking triggers the wrong action?

Directions are fixed — review slot directions in Settings. Lock high-frequency actions to stable directions and build muscle memory.

### Macro execution fails?

1. Run the macro manually in Excel/WPS to confirm it works on its own;
2. Check the macro name in Pulsar matches exactly (case-sensitive);
3. For WPS, confirm the VBA environment is installed and the workbook is macro-enabled (`.xlsm`).

### Web script didn't take effect?

1. Make sure the target page is the **foreground** window and fully loaded;
2. Legacy pages vary widely — adjust the script to the page's actual DOM (start from an example);
3. Some intranet pages nest content in frames — target the right frame.

## Security & Privacy

### Does Pulsar phone home?

Core features run fully locally. The current version only contacts GitHub-related hosts when you manually check for updates on the About page; no machine-identifying information is sent. Automatic checks can be disabled in Settings.

### How are passwords stored?

Encrypted with Windows DPAPI (bound to the current Windows user + machine) under `%AppData%\Pulsar`. Plaintext exists only for the instant of injection, visibly on the login page in front of you.

## Other

### How do I verify the SHA256 of a downloaded file?

Compare against the SHA256 manifest attached to the Release:

```powershell
Get-FileHash .\Pulsar-v1.10.0-Setup.exe -Algorithm SHA256
```

Matching output means the file is authentic.

### Can I switch the UI to Chinese?

Yes. Settings → Language: 简体中文 / English anytime.

### Still stuck?

- Documentation hub: [Docs/README.md](../../README.md)
- File an issue: [GitHub Issues](https://github.com/Smith-Rosco/Pulsar/issues)
