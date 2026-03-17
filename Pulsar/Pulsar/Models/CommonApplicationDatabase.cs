// [Path]: Pulsar/Pulsar/Models/CommonApplicationDatabase.cs

using System;
using System.Collections.Generic;
using System.Linq;

namespace Pulsar.Models
{
    /// <summary>
    /// 应用程序定义 - 用于检测和配置
    /// </summary>
    public class AppDefinition
    {
        /// <summary>
        /// 进程名称 (不含 .exe 后缀)
        /// </summary>
        public string ProcessName { get; set; } = string.Empty;

        /// <summary>
        /// 显示名称
        /// </summary>
        public string DisplayName { get; set; } = string.Empty;

        /// <summary>
        /// 注册表键名 (用于 App Paths 查询)
        /// </summary>
        public string[] RegistryKeys { get; set; } = Array.Empty<string>();

        /// <summary>
        /// 文件系统搜索路径 (相对于 Program Files)
        /// </summary>
        public string[] SearchPaths { get; set; } = Array.Empty<string>();

        /// <summary>
        /// WPF UI 图标键 (Segoe MDL2 Assets)
        /// </summary>
        public string IconKey { get; set; } = "\uE8B7"; // Default: Folder icon

        /// <summary>
        /// 优先级 (1-10, 10 最高)
        /// </summary>
        public int Priority { get; set; } = 5;

        /// <summary>
        /// 应用类别
        /// </summary>
        public string Category { get; set; } = "General";
    }

    /// <summary>
    /// 常用应用程序数据库 - 包含 Windows 平台常见应用的检测信息
    /// </summary>
    public static class CommonApplicationDatabase
    {
        /// <summary>
        /// 获取所有预定义的应用程序
        /// </summary>
        public static List<AppDefinition> GetAllApplications()
        {
            return new List<AppDefinition>
            {
                // ==================== 浏览器 (优先级 9-10) ====================
                new AppDefinition
                {
                    ProcessName = "chrome",
                    DisplayName = "Google Chrome",
                    RegistryKeys = new[] { "chrome.exe" },
                    SearchPaths = new[]
                    {
                        @"Google\Chrome\Application\chrome.exe"
                    },
                    IconKey = "\uE774", // Globe icon
                    Priority = 10,
                    Category = "Browser"
                },

                new AppDefinition
                {
                    ProcessName = "msedge",
                    DisplayName = "Microsoft Edge",
                    RegistryKeys = new[] { "msedge.exe" },
                    SearchPaths = new[]
                    {
                        @"Microsoft\Edge\Application\msedge.exe"
                    },
                    IconKey = "\uE774",
                    Priority = 9,
                    Category = "Browser"
                },

                new AppDefinition
                {
                    ProcessName = "firefox",
                    DisplayName = "Mozilla Firefox",
                    RegistryKeys = new[] { "firefox.exe" },
                    SearchPaths = new[]
                    {
                        @"Mozilla Firefox\firefox.exe"
                    },
                    IconKey = "\uE774",
                    Priority = 9,
                    Category = "Browser"
                },

                // ==================== 开发工具 (优先级 8-9) ====================
                new AppDefinition
                {
                    ProcessName = "Code",
                    DisplayName = "Visual Studio Code",
                    RegistryKeys = new[] { "Code.exe" },
                    SearchPaths = new[]
                    {
                        @"Microsoft VS Code\Code.exe"
                    },
                    IconKey = "\uE943", // Code icon
                    Priority = 9,
                    Category = "Development"
                },

                new AppDefinition
                {
                    ProcessName = "cursor",
                    DisplayName = "Cursor",
                    RegistryKeys = new[] { "Cursor.exe" },
                    SearchPaths = new[]
                    {
                        @"Cursor\Cursor.exe"
                    },
                    IconKey = "\uE943",
                    Priority = 9,
                    Category = "Development"
                },

                new AppDefinition
                {
                    ProcessName = "devenv",
                    DisplayName = "Visual Studio",
                    RegistryKeys = new[] { "devenv.exe" },
                    SearchPaths = new[]
                    {
                        @"Microsoft Visual Studio\2022\Community\Common7\IDE\devenv.exe",
                        @"Microsoft Visual Studio\2022\Professional\Common7\IDE\devenv.exe",
                        @"Microsoft Visual Studio\2022\Enterprise\Common7\IDE\devenv.exe"
                    },
                    IconKey = "\uE943",
                    Priority = 8,
                    Category = "Development"
                },

                // ==================== 终端 (优先级 7-8) ====================
                new AppDefinition
                {
                    ProcessName = "WindowsTerminal",
                    DisplayName = "Windows Terminal",
                    RegistryKeys = new[] { "wt.exe" },
                    SearchPaths = new[]
                    {
                        @"WindowsApps\Microsoft.WindowsTerminal_*\wt.exe"
                    },
                    IconKey = "\uE756", // Console icon
                    Priority = 8,
                    Category = "Terminal"
                },

                new AppDefinition
                {
                    ProcessName = "powershell",
                    DisplayName = "PowerShell",
                    RegistryKeys = new[] { "powershell.exe" },
                    SearchPaths = new[]
                    {
                        @"PowerShell\7\pwsh.exe"
                    },
                    IconKey = "\uE756",
                    Priority = 7,
                    Category = "Terminal"
                },

                // ==================== Office (优先级 6-7) ====================
                new AppDefinition
                {
                    ProcessName = "EXCEL",
                    DisplayName = "Microsoft Excel",
                    RegistryKeys = new[] { "excel.exe" },
                    SearchPaths = new[]
                    {
                        @"Microsoft Office\root\Office16\EXCEL.EXE"
                    },
                    IconKey = "\uE8A5", // Table icon
                    Priority = 7,
                    Category = "Office"
                },

                new AppDefinition
                {
                    ProcessName = "WINWORD",
                    DisplayName = "Microsoft Word",
                    RegistryKeys = new[] { "winword.exe" },
                    SearchPaths = new[]
                    {
                        @"Microsoft Office\root\Office16\WINWORD.EXE"
                    },
                    IconKey = "\uE8A5",
                    Priority = 6,
                    Category = "Office"
                },

                new AppDefinition
                {
                    ProcessName = "POWERPNT",
                    DisplayName = "Microsoft PowerPoint",
                    RegistryKeys = new[] { "powerpnt.exe" },
                    SearchPaths = new[]
                    {
                        @"Microsoft Office\root\Office16\POWERPNT.EXE"
                    },
                    IconKey = "\uE8A5",
                    Priority = 6,
                    Category = "Office"
                },

                new AppDefinition
                {
                    ProcessName = "OUTLOOK",
                    DisplayName = "Microsoft Outlook",
                    RegistryKeys = new[] { "outlook.exe" },
                    SearchPaths = new[]
                    {
                        @"Microsoft Office\root\Office16\OUTLOOK.EXE"
                    },
                    IconKey = "\uE8A5",
                    Priority = 6,
                    Category = "Office"
                },

                // ==================== 通讯工具 (优先级 5-6) ====================
                new AppDefinition
                {
                    ProcessName = "WeChat",
                    DisplayName = "WeChat",
                    RegistryKeys = new[] { "WeChat.exe" },
                    SearchPaths = new[]
                    {
                        @"Tencent\WeChat\WeChat.exe"
                    },
                    IconKey = "\uE8BD", // Message icon
                    Priority = 6,
                    Category = "Communication"
                },

                new AppDefinition
                {
                    ProcessName = "QQ",
                    DisplayName = "QQ",
                    RegistryKeys = new[] { "QQ.exe" },
                    SearchPaths = new[]
                    {
                        @"Tencent\QQ\Bin\QQ.exe"
                    },
                    IconKey = "\uE8BD",
                    Priority = 5,
                    Category = "Communication"
                },

                // ==================== Windows 内置 (优先级 1-3, Fallback) ====================
                new AppDefinition
                {
                    ProcessName = "notepad",
                    DisplayName = "Notepad",
                    RegistryKeys = new[] { "notepad.exe" },
                    SearchPaths = new[] { "notepad.exe" },
                    IconKey = "\uE70F", // Document icon
                    Priority = 2,
                    Category = "System"
                },

                new AppDefinition
                {
                    ProcessName = "explorer",
                    DisplayName = "File Explorer",
                    RegistryKeys = new[] { "explorer.exe" },
                    SearchPaths = new[] { "explorer.exe" },
                    IconKey = "\uE8B7", // Folder icon
                    Priority = 3,
                    Category = "System"
                },

                new AppDefinition
                {
                    ProcessName = "calc",
                    DisplayName = "Calculator",
                    RegistryKeys = new[] { "calc.exe" },
                    SearchPaths = new[] { "calc.exe" },
                    IconKey = "\uE8EF", // Calculator icon
                    Priority = 1,
                    Category = "System"
                }
            };
        }

        /// <summary>
        /// 获取 Fallback 应用列表 (Windows 内置应用)
        /// </summary>
        public static List<AppDefinition> GetFallbackApplications()
        {
            return GetAllApplications()
                .Where(app => app.Category == "System")
                .OrderByDescending(app => app.Priority)
                .ToList();
        }

        /// <summary>
        /// 根据优先级获取 Top N 应用
        /// </summary>
        public static List<AppDefinition> GetTopApplications(List<AppDefinition> installedApps, int maxCount)
        {
            return installedApps
                .OrderByDescending(app => app.Priority)
                .Take(maxCount)
                .ToList();
        }
    }
}
