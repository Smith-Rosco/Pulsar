// [Path]: Pulsar/Pulsar/Helpers/ApplicationDetector.cs

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Pulsar.Models;

namespace Pulsar.Helpers
{
    /// <summary>
    /// 应用程序检测器 - 基于开始菜单的智能检测
    /// </summary>
    public class ApplicationDetector
    {
        private readonly ILogger? _logger;
        private readonly StartMenuScanner _startMenuScanner;
        private const string LogPrefix = "[AppDetector]";

        public ApplicationDetector(ILogger? logger = null)
        {
            _logger = logger;
            _startMenuScanner = new StartMenuScanner(logger);
        }

        /// <summary>
        /// 异步检测已安装的应用程序
        /// </summary>
        public async Task<List<AppDefinition>> DetectInstalledApplicationsAsync()
        {
            return await Task.Run(() => DetectInstalledApplications());
        }

        /// <summary>
        /// 同步检测已安装的应用程序 - 使用开始菜单扫描
        /// </summary>
        public List<AppDefinition> DetectInstalledApplications()
        {
            _logger?.LogInformation($"{LogPrefix} Starting application detection via Start Menu scan...");

            try
            {
                // 主要方法: 扫描开始菜单
                var apps = _startMenuScanner.ScanStartMenu();

                if (apps.Count > 0)
                {
                    _logger?.LogInformation($"{LogPrefix} Detection complete. Found {apps.Count} applications from Start Menu");
                    return apps;
                }

                // Fallback: 如果开始菜单扫描失败或应用太少，使用 Windows 内置应用
                _logger?.LogWarning($"{LogPrefix} Start Menu scan returned no apps, using Fallback");
                return GetFallbackApplications();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, $"{LogPrefix} Application detection failed, using Fallback");
                return GetFallbackApplications();
            }
        }

        /// <summary>
        /// 获取 Fallback 应用列表 (Windows 内置应用)
        /// </summary>
        private List<AppDefinition> GetFallbackApplications()
        {
            _logger?.LogInformation($"{LogPrefix} Using Fallback applications (Windows built-in)");

            return CommonApplicationDatabase.GetFallbackApplications();
        }
    }
}
