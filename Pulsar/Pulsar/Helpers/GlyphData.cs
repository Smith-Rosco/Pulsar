// [Path]: Pulsar/Pulsar/Helpers/GlyphData.cs
using System.Collections.Generic;
using System.Linq;

namespace Pulsar.Helpers
{
    public class IconItem
    {
        public string Name { get; set; }
        public string Code { get; set; } // e.g., "E713"
        public string Character { get; set; } // e.g., "\uE713"
    }

    public static class GlyphData
    {
        // 精选常用图标 (Segoe Fluent Icons)
        public static readonly List<IconItem> CommonIcons = new()
        {
            // --- 通用 ---
            new() { Name = "Apps", Code = "E945" },
            new() { Name = "Settings", Code = "E713" },
            new() { Name = "Home", Code = "E80F" },
            new() { Name = "Add / Plus", Code = "E710" },
            new() { Name = "Delete / Trash", Code = "E74D" },
            new() { Name = "Edit / Pencil", Code = "E70F" },
            new() { Name = "Save", Code = "E74E" },
            new() { Name = "Cancel / Clear", Code = "E894" },
            new() { Name = "Refresh / Sync", Code = "E72C" },
            new() { Name = "Search", Code = "E721" },
            new() { Name = "Share", Code = "E72D" },
            new() { Name = "Link", Code = "E71B" },
            new() { Name = "Lock", Code = "E72E" },
            new() { Name = "Unlock", Code = "E785" },
            
            // --- 媒体 ---
            new() { Name = "Play", Code = "E768" },
            new() { Name = "Pause", Code = "E769" },
            new() { Name = "Stop", Code = "E71A" },
            new() { Name = "Previous", Code = "E892" },
            new() { Name = "Next", Code = "E893" },
            new() { Name = "Volume", Code = "E767" },
            new() { Name = "Mute", Code = "E74F" },

            // --- 系统/窗口 ---
            new() { Name = "Power", Code = "E7E8" },
            new() { Name = "Window Close", Code = "E8BB" },
            new() { Name = "Window Min", Code = "E921" },
            new() { Name = "Full Screen", Code = "E740" },
            new() { Name = "Copy", Code = "E8C8" },
            new() { Name = "Paste", Code = "E77F" },
            new() { Name = "Cut", Code = "E8C6" },

            // --- 应用/工具 ---
            new() { Name = "Mail", Code = "E715" },
            new() { Name = "Calendar", Code = "E787" },
            new() { Name = "Calculator", Code = "E8EF" },
            new() { Name = "Camera", Code = "E722" },
            new() { Name = "Folder", Code = "E8B7" },
            new() { Name = "File", Code = "E7C3" },
            new() { Name = "Terminal / Code", Code = "E756" },
            new() { Name = "Browser / Globe", Code = "E774" }
        };

        static GlyphData()
        {
            // 初始化 Character 属性，方便 XAML 显示
            foreach (var item in CommonIcons)
            {
                if (int.TryParse(item.Code, System.Globalization.NumberStyles.HexNumber, null, out int codePoint))
                {
                    item.Character = char.ConvertFromUtf32(codePoint);
                }
            }
        }
    }
}