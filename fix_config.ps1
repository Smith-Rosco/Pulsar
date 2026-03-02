$json = @'
{
  "settings": {
    "centerSlotBehavior": "MRU_Window",
    "triggerDistance": 100,
    "launcherTheme": "Light",
    "settingsTheme": "Light",
    "hoverScale": 1.2,
    "springiness": 6,
    "maxDisplacement": 20,
    "hotkeys": {
      "ShowGrid": {
        "key": "Q",
        "modifiers": "Control,Shift"
      },
      "ShowSwitcher": {
        "key": "Q",
        "modifiers": "Control"
      }
    }
  },
  "plugins": {},
  "profiles": {
    "Global": {
      "icon": null,
      "alias": null,
      "commandMode": [],
      "switchMode": [
        {
          "plugin": "com.pulsar.winswitcher",
          "action": "activate",
          "args": {
            "app": "MSEDGE",
            "path": "C:\\Program Files (x86)\\Microsoft\\Edge\\Application\\msedge.exe"
          },
          "label": "Edge",
          "icon": "C:\\Users\\milo\\AppData\\Roaming\\Pulsar\\Cache\\Icons\\msedge.png",
          "color": "",
          "order": 1,
          "slot": 1
        }
      ]
    }
  }
}
'@

$json | Out-File -FilePath "$env:APPDATA\Pulsar\Profiles.json" -Encoding UTF8 -NoNewline
Write-Host "✅ Configuration file created successfully"
