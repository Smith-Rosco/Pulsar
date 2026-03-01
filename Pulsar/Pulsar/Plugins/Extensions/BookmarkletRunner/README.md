# Bookmarklet Runner Plugin - Usage Guide

## 📖 Overview

The **Bookmarklet Runner Plugin** allows you to execute JavaScript bookmarklet scripts in your browser directly from Pulsar's radial menu. This is a migration of the standalone BookmarkletRunner tool into Pulsar's plugin system.

## 🎯 Plugin Information

- **Plugin ID**: `com.pulsar.bookmarklet`
- **Plugin Name**: Bookmarklet Runner
- **Location**: `Pulsar/Plugins/BookmarkletRunner/`

## ✨ Features

- ✅ Execute bookmarklet scripts from files
- ✅ Smart browser detection (uses context window if it's a browser)
- ✅ Clipboard backup/restore (stealth mode)
- ✅ Support for Chrome, Edge, Firefox, and Brave browsers
- ✅ Path safety validation
- ✅ Automatic script preprocessing

## 📋 Configuration

### Basic Configuration

Add the following to your `Profiles.json`:

```json
{
  "Profiles": {
    "CHROME": {
      "CommandMode": {
        "Slot_1": {
          "plugin": "com.pulsar.bookmarklet",
          "action": "run",
          "args": {
            "scriptPath": "C:\\Scripts\\my-bookmarklet.js"
          },
          "label": "Run Bookmarklet",
          "icon": "Browser"
        }
      }
    }
  }
}
```

### Global Configuration

To make a bookmarklet available from any application:

```json
{
  "Profiles": {
    "Global": {
      "CommandMode": {
        "Slot_5": {
          "plugin": "com.pulsar.bookmarklet",
          "action": "run",
          "args": {
            "scriptPath": "C:\\Scripts\\universal-script.js"
          },
          "label": "Universal Script",
          "icon": "Code"
        }
      }
    }
  }
}
```

### Multiple Bookmarklets

You can configure multiple bookmarklets in different slots:

```json
{
  "Profiles": {
    "MSEDGE": {
      "CommandMode": {
        "Slot_1": {
          "plugin": "com.pulsar.bookmarklet",
          "action": "run",
          "args": {
            "scriptPath": "C:\\Scripts\\script1.js"
          },
          "label": "Script 1",
          "icon": "Code"
        },
        "Slot_2": {
          "plugin": "com.pulsar.bookmarklet",
          "action": "run",
          "args": {
            "scriptPath": "C:\\Scripts\\script2.js"
          },
          "label": "Script 2",
          "icon": "Browser"
        }
      }
    }
  }
}
```

## 📝 Creating Bookmarklet Scripts

### Script File Format

Create a `.js` file with your JavaScript code:

```javascript
javascript:
(function(){
    alert('Hello from Pulsar!');
    console.log('Script executed');
})();
```

**Note**: The `javascript:` prefix is optional - the plugin will handle it automatically.

### Script Example 1: Simple Alert

**File**: `hello.js`
```javascript
(function(){
    alert('Current page title: ' + document.title);
})();
```

### Script Example 2: Modify Page

**File**: `change-background.js`
```javascript
(function(){
    document.body.style.backgroundColor = '#f0f0f0';
    alert('Background changed!');
})();
```

### Script Example 3: Extract Data

**File**: `extract-links.js`
```javascript
(function(){
    var links = document.querySelectorAll('a');
    var urls = Array.from(links).map(a => a.href).join('\n');
    console.log('Found ' + links.length + ' links:');
    console.log(urls);
    alert('Found ' + links.length + ' links. Check console for details.');
})();
```

## 🚀 Usage

1. **Create Your Script**: Save your bookmarklet JavaScript code to a file (e.g., `C:\Scripts\my-script.js`)

2. **Configure Pulsar**: Add the configuration to `Profiles.json` (see examples above)

3. **Run the Script**:
   - Open a browser window
   - Press your Pulsar hotkey to show the radial menu
   - Select the slot with your bookmarklet
   - The script will execute in the browser

## 🎨 Supported Icons

You can use any icon from Pulsar's icon set. Common choices:
- `Browser` - Browser icon
- `Code` - Code/script icon
- `Settings` - Settings gear
- `Star` - Star icon
- `Play` - Play button

## ⚙️ How It Works

1. **Plugin Initialization**: Loaded automatically by `PluginLoader` on startup
2. **Script Reading**: Reads and preprocesses the script file
3. **Browser Detection**: 
   - If the current window is a browser → uses it
   - Otherwise → finds the first available browser window
4. **Focus Management**: 
   - Hides Pulsar window
   - Focuses the target browser
5. **Keyboard Automation**:
   - `Ctrl+L` - Focus address bar
   - Types `j` - Bypass browser security
   - Pastes `avascript:[your script]` - Complete the URL
   - `Enter` - Execute
6. **Cleanup**: Restores clipboard to original state

## 🛡️ Security Features

- **Path Validation**: Prevents path traversal attacks
- **File Extension Check**: Only allows `.js` and `.txt` files
- **Clipboard Backup**: Restores original clipboard after execution
- **Error Handling**: All operations are wrapped in try-catch blocks

## 🔧 Troubleshooting

### Script Not Executing

**Problem**: The bookmarklet doesn't run.

**Solutions**:
1. Check the script path in `Profiles.json` is correct
2. Ensure the browser window is open
3. Verify the script file contains valid JavaScript
4. Check the Debug output window for error messages

### Browser Not Found

**Problem**: Error message "No browser window found"

**Solutions**:
1. Open a browser window (Chrome, Edge, Firefox, or Brave)
2. Make sure the browser has a visible main window (not just minimized to tray)

### Script File Not Found

**Problem**: Error message "Script file not found"

**Solutions**:
1. Verify the file path in `Profiles.json`
2. Use absolute paths (e.g., `C:\\Scripts\\test.js`)
3. Ensure the file exists and is readable

### Clipboard Issues

**Problem**: Warning about clipboard backup/restore failures

**Solutions**:
- This is usually not critical - the script still executes
- Make sure no other application is locking the clipboard
- The plugin will continue even if clipboard operations fail

## 🎯 Best Practices

1. **Use Absolute Paths**: Always use full paths (e.g., `C:\\Scripts\\...`)
2. **Test Scripts First**: Test bookmarklets manually in the browser before adding to Pulsar
3. **Keep Scripts Simple**: Bookmarklets work best with short, focused scripts
4. **Use Comments**: Add comments to your script files for future reference
5. **Organize Scripts**: Create a dedicated folder for all your bookmarklet scripts

## 📂 File Locations

- **Plugin Source**: `Pulsar/Plugins/BookmarkletRunner/`
  - `BookmarkletRunnerPlugin.cs` - Main plugin class
  - `BrowserHelper.cs` - Browser detection utilities
  - `ScriptPreprocessor.cs` - Script preprocessing utilities
- **Configuration**: `Profiles.json` in Pulsar's executable directory

## 🔍 Debug Output

To monitor plugin execution, check the Debug output window for messages like:

```
[BookmarkletRunner] Script path: C:\Scripts\test.js
[BookmarkletRunner] Script loaded successfully (123 chars)
[BookmarkletRunner] Using context browser: CHROME
[BookmarkletRunner] Pulsar window hidden
[BookmarkletRunner] Browser window focused
[BookmarkletRunner] Clipboard backed up
[BookmarkletRunner] Address bar focused (Ctrl+L)
[BookmarkletRunner] Typed 'j'
[BookmarkletRunner] Clipboard set (150 chars)
[BookmarkletRunner] Content pasted (Ctrl+V)
[BookmarkletRunner] Executed (Enter)
[BookmarkletRunner] Clipboard restored
[BookmarkletRunner] ✓ Bookmarklet executed successfully
```

## 📚 Additional Resources

- **Plugin Development Guide**: See `PLUGIN_DEVELOPMENT.md`
- **Original Project**: `Reference/BookmarkletRunner/`
- **Example Scripts**: `Reference/BookmarkletRunner/test.js`

---

**Version**: 1.0.0  
**Last Updated**: 2026-02-08  
**Author**: Migrated from BookmarkletRunner standalone tool
