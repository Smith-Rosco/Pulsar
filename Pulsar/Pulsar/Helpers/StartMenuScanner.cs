// [Path]: Pulsar/Pulsar/Helpers/StartMenuScanner.cs

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Logging;
using Pulsar.Models;

namespace Pulsar.Helpers
{
    /// <summary>
    /// 开始菜单扫描器 - 扫描并解析开始菜单中的应用程序快捷方式
    /// </summary>
    public class StartMenuScanner
    {
        private readonly ILogger? _logger;
        private readonly ShortcutParser _shortcutParser;
        private readonly IconExtractor _iconExtractor;
        private const string LogPrefix = "[StartMenuScanner]";

        // 排除关键词 - 用于过滤非应用程序的快捷方式
        private static readonly string[] ExcludeKeywords = new[]
        {
            "uninstall", "卸载", "unins", "uninst",
            "help", "帮助", "readme", "说明",
            "documentation", "文档", "manual", "手册", "doc",
            "update", "更新", "updater", "升级", "patch",
            "license", "许可", "about", "关于",
            "website", "网站", "homepage", "主页", "web site",
            "support", "支持", "feedback", "反馈",
            "setup", "安装", "installer", "install",
            "config", "配置", "settings", "设置",
            "repair", "修复", "diagnostic", "诊断",
            "release notes", "发行说明", "changelog", "更新日志",
            "getting started", "入门", "tutorial", "教程"
        };

        // 优先类别关键词
        private static readonly Dictionary<string, int> CategoryPriority = new()
        {
            // 浏览器 (优先级 10)
            { "chrome", 10 }, { "edge", 10 }, { "firefox", 10 }, { "brave", 10 }, { "opera", 10 },
            
            // 开发工具 (优先级 9)
            { "code", 9 }, { "visual studio", 9 }, { "cursor", 9 }, { "pycharm", 9 }, { "intellij", 9 },
            { "webstorm", 9 }, { "rider", 9 }, { "android studio", 9 },
            
            // 终端 (优先级 8)
            { "terminal", 8 }, { "powershell", 8 }, { "cmd", 8 }, { "wsl", 8 },
            
            // Office (优先级 7)
            { "excel", 7 }, { "word", 7 }, { "powerpoint", 7 }, { "outlook", 7 }, { "onenote", 7 },
            
            // 通讯工具 (优先级 6)
            { "wechat", 6 }, { "微信", 6 }, { "qq", 6 }, { "slack", 6 }, { "teams", 6 }, { "discord", 6 },
            
            // 其他常用 (优先级 5)
            { "notepad", 5 }, { "explorer", 5 }, { "calculator", 5 }, { "paint", 5 }
        };

        public StartMenuScanner(ILogger? logger = null)
        {
            _logger = logger;
            _shortcutParser = new ShortcutParser(logger);
            _iconExtractor = new IconExtractor(logger);
        }

        /// <summary>
        /// 扫描开始菜单并返回应用程序列表
        /// </summary>
        public List<AppDefinition> ScanStartMenu()
        {
            _logger?.LogInformation($"{LogPrefix} Starting Start Menu scan...");

            var apps = new List<AppDefinition>();
            var startMenuPaths = GetStartMenuPaths();

            foreach (var startMenuPath in startMenuPaths)
            {
                if (!Directory.Exists(startMenuPath))
                {
                    _logger?.LogWarning($"{LogPrefix} Start Menu path not found: {startMenuPath}");
                    continue;
                }

                _logger?.LogDebug($"{LogPrefix} Scanning: {startMenuPath}");
                ScanDirectory(startMenuPath, apps);
            }

            // 智能筛选和排序
            var filteredApps = FilterAndSortApps(apps);

            _logger?.LogInformation($"{LogPrefix} Scan complete. Found {filteredApps.Count} valid applications");
            return filteredApps;
        }

        /// <summary>
        /// 获取开始菜单路径列表
        /// </summary>
        private List<string> GetStartMenuPaths()
        {
            return new List<string>
            {
                // 用户级开始菜单
                Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    @"Microsoft\Windows\Start Menu\Programs"),
                
                // 系统级开始菜单
                Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                    @"Microsoft\Windows\Start Menu\Programs")
            };
        }

        /// <summary>
        /// 递归扫描目录
        /// </summary>
        private void ScanDirectory(string directory, List<AppDefinition> apps)
        {
            try
            {
                // 扫描当前目录的 .lnk 文件
                var lnkFiles = Directory.GetFiles(directory, "*.lnk", SearchOption.TopDirectoryOnly);
                
                foreach (var lnkFile in lnkFiles)
                {
                    var app = ParseShortcutToApp(lnkFile);
                    if (app != null)
                    {
                        apps.Add(app);
                    }
                }

                // 递归扫描子目录（但不要太深，避免扫描到太多辅助工具）
                var subDirs = Directory.GetDirectories(directory);
                foreach (var subDir in subDirs)
                {
                    // 跳过一些明显的辅助工具目录
                    var dirName = Path.GetFileName(subDir).ToLower();
                    if (dirName.Contains("accessories") || dirName.Contains("附件") ||
                        dirName.Contains("administrative") || dirName.Contains("管理工具"))
                    {
                        continue;
                    }

                    ScanDirectory(subDir, apps);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, $"{LogPrefix} Failed to scan directory: {directory}");
            }
        }

        /// <summary>
        /// 解析快捷方式为 AppDefinition
        /// </summary>
        private AppDefinition? ParseShortcutToApp(string lnkPath)
        {
            try
            {
                var shortcut = _shortcutParser.ParseShortcut(lnkPath);
                if (shortcut == null || string.IsNullOrEmpty(shortcut.TargetPath))
                {
                    _logger?.LogTrace($"{LogPrefix} Skipping shortcut with empty target: {lnkPath}");
                    return null;
                }

                // 只处理 .exe 文件
                if (!shortcut.TargetPath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                {
                    _logger?.LogTrace($"{LogPrefix} Skipping non-exe target: {shortcut.TargetPath}");
                    return null;
                }

                // 【关键验证】检查目标文件是否真实存在
                if (!File.Exists(shortcut.TargetPath))
                {
                    _logger?.LogDebug($"{LogPrefix} Skipping shortcut with missing target: {shortcut.Name} -> {shortcut.TargetPath}");
                    return null;
                }

                // 【额外验证】尝试获取文件信息，确保文件可访问
                try
                {
                    var fileInfo = new FileInfo(shortcut.TargetPath);
                    if (fileInfo.Length == 0)
                    {
                        _logger?.LogDebug($"{LogPrefix} Skipping zero-byte executable: {shortcut.TargetPath}");
                        return null;
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogDebug(ex, $"{LogPrefix} Cannot access target file: {shortcut.TargetPath}");
                    return null;
                }

                // 提取进程名
                var processName = Path.GetFileNameWithoutExtension(shortcut.TargetPath);
                
                // 【额外验证】检查是否是有效的 PE 文件（Windows 可执行文件）
                if (!IsValidExecutable(shortcut.TargetPath))
                {
                    _logger?.LogDebug($"{LogPrefix} Skipping invalid executable: {shortcut.TargetPath}");
                    return null;
                }
                
                // 提取并缓存图标
                string? iconPath = null;
                if (!string.IsNullOrEmpty(shortcut.IconLocation))
                {
                    iconPath = _iconExtractor.ExtractIconFromLocation(shortcut.IconLocation, processName);
                }
                
                if (iconPath == null)
                {
                    iconPath = _iconExtractor.ExtractAndCacheIcon(shortcut.TargetPath, processName);
                }

                // 【关键验证】如果图标提取失败，可能说明应用有问题，跳过
                if (string.IsNullOrEmpty(iconPath))
                {
                    _logger?.LogDebug($"{LogPrefix} Skipping app with failed icon extraction: {shortcut.Name}");
                    return null;
                }

                // 计算优先级
                var priority = CalculatePriority(shortcut.Name, processName);

                var app = new AppDefinition
                {
                    ProcessName = processName,
                    DisplayName = shortcut.Name,
                    RegistryKeys = Array.Empty<string>(),
                    SearchPaths = new[] { shortcut.TargetPath },
                    IconKey = iconPath ?? string.Empty, // 使用图标路径而不是 Unicode 字符
                    Priority = priority,
                    Category = DetermineCategory(shortcut.Name, processName)
                };

                _logger?.LogTrace($"{LogPrefix} Parsed app: {app.DisplayName} (Priority: {priority})");
                return app;
            }
            catch (Exception ex)
            {
                _logger?.LogTrace(ex, $"{LogPrefix} Failed to parse shortcut: {lnkPath}");
                return null;
            }
        }

        /// <summary>
        /// 智能筛选和排序应用
        /// </summary>
        private List<AppDefinition> FilterAndSortApps(List<AppDefinition> apps)
        {
            return apps
                // 1. 排除非应用程序快捷方式
                .Where(app => !ShouldExclude(app.DisplayName))
                // 2. 去重（同一个进程可能有多个快捷方式）
                .GroupBy(app => app.ProcessName.ToLower())
                .Select(g => g.OrderByDescending(a => a.Priority).First())
                // 3. 按优先级排序
                .OrderByDescending(app => app.Priority)
                // 4. 按显示名称排序（同优先级）
                .ThenBy(app => app.DisplayName)
                .ToList();
        }

        /// <summary>
        /// 判断是否应该排除该应用
        /// </summary>
        private bool ShouldExclude(string displayName)
        {
            var lowerName = displayName.ToLower();
            
            // 检查排除关键词
            if (ExcludeKeywords.Any(keyword => lowerName.Contains(keyword)))
                return true;
            
            // 排除包含 "private" 的变体（如 Firefox Private Browsing）
            if (lowerName.Contains("private") && lowerName.Contains("browsing"))
                return true;
            
            // 排除包含 "for visual studio" 的辅助工具
            if (lowerName.Contains("for visual studio") && !lowerName.Contains("visual studio 20"))
                return true;
            
            // 排除 Blend（设计工具，通常不是主要应用）
            if (lowerName == "blend" || lowerName.StartsWith("blend for"))
                return true;
            
            return false;
        }

        /// <summary>
        /// 计算应用优先级
        /// </summary>
        private int CalculatePriority(string displayName, string processName)
        {
            var searchText = $"{displayName} {processName}".ToLower();
            
            foreach (var kvp in CategoryPriority)
            {
                if (searchText.Contains(kvp.Key))
                {
                    return kvp.Value;
                }
            }

            // 默认优先级
            return 3;
        }

        /// <summary>
        /// 确定应用类别
        /// </summary>
        private string DetermineCategory(string displayName, string processName)
        {
            var searchText = $"{displayName} {processName}".ToLower();

            if (searchText.Contains("chrome") || searchText.Contains("edge") || 
                searchText.Contains("firefox") || searchText.Contains("browser"))
                return "Browser";

            if (searchText.Contains("code") || searchText.Contains("visual studio") || 
                searchText.Contains("studio") || searchText.Contains("dev"))
                return "Development";

            if (searchText.Contains("terminal") || searchText.Contains("powershell") || 
                searchText.Contains("cmd"))
                return "Terminal";

            if (searchText.Contains("excel") || searchText.Contains("word") || 
                searchText.Contains("powerpoint") || searchText.Contains("outlook") || 
                searchText.Contains("office"))
                return "Office";

            if (searchText.Contains("wechat") || searchText.Contains("微信") || 
                searchText.Contains("qq") || searchText.Contains("slack") || 
                searchText.Contains("teams") || searchText.Contains("discord"))
                return "Communication";

            return "General";
        }

        /// <summary>
        /// 验证是否是有效的 Windows 可执行文件（PE 格式）
        /// </summary>
        private bool IsValidExecutable(string exePath)
        {
            try
            {
                // 读取文件头，检查 PE 签名
                using var fs = new FileStream(exePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                using var br = new BinaryReader(fs);

                // 检查 DOS 头 (MZ)
                if (fs.Length < 64)
                    return false;

                var dosHeader = br.ReadUInt16();
                if (dosHeader != 0x5A4D) // "MZ"
                    return false;

                // 跳到 PE 头偏移位置
                fs.Seek(0x3C, SeekOrigin.Begin);
                var peHeaderOffset = br.ReadInt32();

                if (peHeaderOffset < 0 || peHeaderOffset >= fs.Length - 4)
                    return false;

                // 检查 PE 签名
                fs.Seek(peHeaderOffset, SeekOrigin.Begin);
                var peSignature = br.ReadUInt32();
                if (peSignature != 0x00004550) // "PE\0\0"
                    return false;

                return true;
            }
            catch (Exception ex)
            {
                _logger?.LogTrace(ex, $"{LogPrefix} Failed to validate executable: {exePath}");
                return false;
            }
        }
    }
}
