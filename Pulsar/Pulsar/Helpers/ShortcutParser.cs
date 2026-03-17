// [Path]: Pulsar/Pulsar/Helpers/ShortcutParser.cs

using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Extensions.Logging;

namespace Pulsar.Helpers
{
    /// <summary>
    /// 快捷方式信息
    /// </summary>
    public class ShortcutInfo
    {
        public string Name { get; set; } = string.Empty;
        public string TargetPath { get; set; } = string.Empty;
        public string Arguments { get; set; } = string.Empty;
        public string WorkingDirectory { get; set; } = string.Empty;
        public string IconLocation { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string ShortcutPath { get; set; } = string.Empty;
    }

    /// <summary>
    /// Windows 快捷方式 (.lnk) 解析器 - 使用 P/Invoke
    /// </summary>
    public class ShortcutParser
    {
        private readonly ILogger? _logger;
        private const string LogPrefix = "[ShortcutParser]";

        public ShortcutParser(ILogger? logger = null)
        {
            _logger = logger;
        }

        /// <summary>
        /// 解析快捷方式文件
        /// </summary>
        public ShortcutInfo? ParseShortcut(string lnkPath)
        {
            if (!File.Exists(lnkPath))
            {
                _logger?.LogWarning($"{LogPrefix} Shortcut file not found: {lnkPath}");
                return null;
            }

            try
            {
                var link = (IShellLinkW)new ShellLink();
                ((IPersistFile)link).Load(lnkPath, 0);

                var info = new ShortcutInfo
                {
                    Name = Path.GetFileNameWithoutExtension(lnkPath),
                    ShortcutPath = lnkPath
                };

                // 获取目标路径
                var sb = new StringBuilder(260);
                link.GetPath(sb, sb.Capacity, IntPtr.Zero, 0);
                info.TargetPath = sb.ToString();

                // 获取参数
                sb.Clear();
                link.GetArguments(sb, sb.Capacity);
                info.Arguments = sb.ToString();

                // 获取工作目录
                sb.Clear();
                link.GetWorkingDirectory(sb, sb.Capacity);
                info.WorkingDirectory = sb.ToString();

                // 获取图标位置
                sb.Clear();
                link.GetIconLocation(sb, sb.Capacity, out int iconIndex);
                info.IconLocation = sb.Length > 0 ? $"{sb},{iconIndex}" : string.Empty;

                // 获取描述
                sb.Clear();
                link.GetDescription(sb, sb.Capacity);
                info.Description = sb.ToString();

                _logger?.LogTrace($"{LogPrefix} Parsed: {info.Name} -> {info.TargetPath}");
                return info;
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, $"{LogPrefix} Failed to parse shortcut: {lnkPath}");
                return null;
            }
        }

        #region COM Interop for IShellLink

        [ComImport]
        [Guid("00021401-0000-0000-C000-000000000046")]
        [ClassInterface(ClassInterfaceType.None)]
        private class ShellLink { }

        [ComImport]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        [Guid("000214F9-0000-0000-C000-000000000046")]
        private interface IShellLinkW
        {
            void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszFile, int cchMaxPath, IntPtr pfd, uint fFlags);
            void GetIDList(out IntPtr ppidl);
            void SetIDList(IntPtr pidl);
            void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszFile, int cchMaxName);
            void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string pszName);
            void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszDir, int cchMaxPath);
            void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string pszDir);
            void GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszArgs, int cchMaxPath);
            void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string pszArgs);
            void GetHotkey(out short pwHotkey);
            void SetHotkey(short wHotkey);
            void GetShowCmd(out int piShowCmd);
            void SetShowCmd(int iShowCmd);
            void GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszIconPath, int cchIconPath, out int piIcon);
            void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string pszIconPath, int iIcon);
            void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string pszPathRel, uint dwReserved);
            void Resolve(IntPtr hwnd, uint fFlags);
            void SetPath([MarshalAs(UnmanagedType.LPWStr)] string pszFile);
        }

        [ComImport]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        [Guid("0000010B-0000-0000-C000-000000000046")]
        private interface IPersistFile
        {
            void GetClassID(out Guid pClassID);
            void IsDirty();
            void Load([MarshalAs(UnmanagedType.LPWStr)] string pszFileName, uint dwMode);
            void Save([MarshalAs(UnmanagedType.LPWStr)] string pszFileName, bool fRemember);
            void SaveCompleted([MarshalAs(UnmanagedType.LPWStr)] string pszFileName);
            void GetCurFile([MarshalAs(UnmanagedType.LPWStr)] out string ppszFileName);
        }

        #endregion
    }
}
