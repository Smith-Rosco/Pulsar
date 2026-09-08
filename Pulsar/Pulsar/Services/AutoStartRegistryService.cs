using System;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Win32;
using Pulsar.Services.Interfaces;

namespace Pulsar.Services
{
    /// <summary>
    /// HKCU <c>Software\Microsoft\Windows\CurrentVersion\Run</c> 读写（C2 拆分自 TrayIconService）。
    /// 键路径与 value 名可注入：测试用独立子键自清理，不触碰真实自启动注册表。
    /// 与原实现一致，注册表异常全部吞掉（自启动开关失败不应击穿托盘菜单交互）。
    /// </summary>
    public sealed class AutoStartRegistryService : IAutoStartService
    {
        public const string DefaultRunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        public const string DefaultAppName = "Pulsar";

        private readonly string _runKeyPath;
        private readonly string _appName;
        private readonly ILogger<AutoStartRegistryService> _logger;

        public AutoStartRegistryService(
            string? runKeyPath = null,
            string? appName = null,
            ILogger<AutoStartRegistryService>? logger = null)
        {
            _runKeyPath = runKeyPath ?? DefaultRunKeyPath;
            _appName = appName ?? DefaultAppName;
            _logger = logger ?? NullLogger<AutoStartRegistryService>.Instance;
        }

        public bool IsEnabled()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(_runKeyPath);
                return key?.GetValue(_appName) != null;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[AutoStartRegistryService] Failed to read autostart state for {App}", _appName);
                return false;
            }
        }

        public void Toggle()
        {
            try
            {
                // The real Run key always exists, but CreateSubKey keeps the write
                // path self-sufficient (and test keys don't have to pre-exist).
                using var key = Registry.CurrentUser.OpenSubKey(_runKeyPath, writable: true)
                    ?? Registry.CurrentUser.CreateSubKey(_runKeyPath, writable: true);
                if (key == null)
                {
                    _logger.LogWarning("[AutoStartRegistryService] Run key unavailable: {Key}", _runKeyPath);
                    return;
                }

                if (key.GetValue(_appName) != null)
                {
                    key.DeleteValue(_appName);
                    _logger.LogInformation("[AutoStartRegistryService] Autostart removed for {App}", _appName);
                }
                else
                {
                    var path = Environment.ProcessPath;
                    if (string.IsNullOrEmpty(path))
                    {
                        _logger.LogWarning("[AutoStartRegistryService] ProcessPath unavailable; autostart not registered");
                        return;
                    }

                    key.SetValue(_appName, $"\"{path}\"");
                    _logger.LogInformation("[AutoStartRegistryService] Autostart registered for {App}", _appName);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[AutoStartRegistryService] Failed to toggle autostart for {App}", _appName);
            }
        }
    }
}
