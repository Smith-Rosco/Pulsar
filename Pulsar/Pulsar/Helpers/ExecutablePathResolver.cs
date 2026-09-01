using System;
using System.IO;

namespace Pulsar.Helpers
{
    /// <summary>
    /// 将相对可执行文件名解析为绝对路径（System32 → PATH）。
    /// 解析失败时回退到原始值，绝不抛异常。
    /// </summary>
    public static class ExecutablePathResolver
    {
        /// <summary>
        /// 尝试将 <paramref name="launchPath"/> 解析为绝对可执行文件路径。
        /// 查找顺序：相对路径 → System32 → PATH；已绝对路径则直接校验存在性。
        /// 找不到时回退到 <paramref name="launchPath"/>（若为空则为 {processName}.exe）。
        /// </summary>
        public static string Resolve(string processName, string launchPath)
        {
            if (!string.IsNullOrWhiteSpace(launchPath))
            {
                string fullPath = launchPath;
                if (!Path.IsPathRooted(fullPath))
                {
                    string systemDir = Environment.GetFolderPath(Environment.SpecialFolder.System);
                    string sysPath = Path.Combine(systemDir, fullPath);
                    if (File.Exists(sysPath)) return sysPath;

                    var paths = Environment.GetEnvironmentVariable("PATH")?.Split(Path.PathSeparator) ?? Array.Empty<string>();
                    foreach (var dir in paths)
                    {
                        if (string.IsNullOrWhiteSpace(dir)) continue;
                        string candidate = Path.Combine(dir, fullPath);
                        if (File.Exists(candidate)) return candidate;
                    }
                }
                else if (File.Exists(fullPath))
                {
                    return fullPath;
                }
            }

            string systemDir2 = Environment.GetFolderPath(Environment.SpecialFolder.System);
            string exePath2 = Path.Combine(systemDir2, $"{processName}.exe");
            if (File.Exists(exePath2)) return exePath2;

            return string.IsNullOrWhiteSpace(launchPath) ? $"{processName}.exe" : launchPath;
        }
    }
}
