using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Pulsar.Core.Plugin;
using Pulsar.Core.Plugin.Versioning;
using Pulsar.Models;

namespace Pulsar.Services
{
    /// <summary>
    /// 插件包管理器 - 实现插件的安装、卸载（从本地文件）
    /// </summary>
    public class PluginPackageManager
    {
        private readonly string _pluginInstallDirectory;
        private readonly ILogger<PluginPackageManager>? _logger;
        private readonly SemaphoreSlim _operationLock = new(1, 1);

        public event EventHandler<PluginOperationProgressEventArgs>? OperationProgress;

        public PluginPackageManager(
            string pluginInstallDirectory,
            ILogger<PluginPackageManager>? logger = null)
        {
            _pluginInstallDirectory = pluginInstallDirectory;
            _logger = logger;

            // 确保安装目录存在
            if (!Directory.Exists(_pluginInstallDirectory))
            {
                Directory.CreateDirectory(_pluginInstallDirectory);
            }
        }



        /// <summary>
        /// 卸载插件
        /// </summary>
        public async Task<PluginOperationResult> UninstallAsync(
            string pluginId,
            bool keepData = false,
            CancellationToken cancellationToken = default)
        {
            var stopwatch = Stopwatch.StartNew();

            await _operationLock.WaitAsync(cancellationToken);
            try
            {
                _logger?.LogInformation("[PluginPackageManager] Uninstalling plugin: {PluginId}", pluginId);

                // 1. 检查是否已安装
                if (!IsPluginInstalled(pluginId))
                {
                    return PluginOperationResult.Failed(pluginId, PluginOperationType.Uninstall, $"Plugin {pluginId} is not installed");
                }

                ReportProgress(pluginId, PluginInstallStatus.Uninstalling, 20, "Removing plugin files...");

                // 2. 删除插件目录
                var pluginPath = Path.Combine(_pluginInstallDirectory, pluginId);
                if (Directory.Exists(pluginPath))
                {
                    // 如果保留数据，备份配置文件
                    string? backupPath = null;
                    if (keepData)
                    {
                        backupPath = await BackupPluginDataAsync(pluginId, cancellationToken);
                    }

                    Directory.Delete(pluginPath, recursive: true);
                    _logger?.LogInformation("[PluginPackageManager] Deleted plugin directory: {Path}", pluginPath);

                    // 恢复数据
                    if (keepData && backupPath != null)
                    {
                        await RestorePluginDataAsync(pluginId, backupPath, cancellationToken);
                    }
                }

                ReportProgress(pluginId, PluginInstallStatus.NotInstalled, 100, "Uninstallation completed");

                stopwatch.Stop();
                _logger?.LogInformation("[PluginPackageManager] Successfully uninstalled {PluginId} in {Duration}ms",
                    pluginId, stopwatch.ElapsedMilliseconds);

                return PluginOperationResult.Successful(pluginId, PluginOperationType.Uninstall, stopwatch.Elapsed);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "[PluginPackageManager] Failed to uninstall plugin {PluginId}", pluginId);
                ReportProgress(pluginId, PluginInstallStatus.Failed, 0, $"Uninstallation failed: {ex.Message}");
                return PluginOperationResult.Failed(pluginId, PluginOperationType.Uninstall, ex.Message);
            }
            finally
            {
                _operationLock.Release();
            }
        }

        /// <summary>
        /// 备份插件数据
        /// </summary>
        private async Task<string?> BackupPluginDataAsync(string pluginId, CancellationToken cancellationToken)
        {
            try
            {
                var pluginPath = Path.Combine(_pluginInstallDirectory, pluginId);
                var dataPath = Path.Combine(pluginPath, "data");

                if (!Directory.Exists(dataPath))
                {
                    return null;
                }

                var backupPath = Path.Combine(Path.GetTempPath(), $"Pulsar_Backup_{pluginId}_{Guid.NewGuid()}");
                Directory.CreateDirectory(backupPath);

                await Task.Run(() =>
                {
                    foreach (var file in Directory.GetFiles(dataPath, "*", SearchOption.AllDirectories))
                    {
                        var relativePath = Path.GetRelativePath(dataPath, file);
                        var targetPath = Path.Combine(backupPath, relativePath);
                        var targetDir = Path.GetDirectoryName(targetPath);

                        if (targetDir != null && !Directory.Exists(targetDir))
                        {
                            Directory.CreateDirectory(targetDir);
                        }

                        File.Copy(file, targetPath, overwrite: true);
                    }
                }, cancellationToken);

                _logger?.LogInformation("[PluginPackageManager] Backed up data for {PluginId} to {Path}", pluginId, backupPath);

                return backupPath;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "[PluginPackageManager] Failed to backup data for {PluginId}", pluginId);
                return null;
            }
        }

        /// <summary>
        /// 恢复插件数据
        /// </summary>
        private async Task RestorePluginDataAsync(string pluginId, string backupPath, CancellationToken cancellationToken)
        {
            try
            {
                var pluginPath = Path.Combine(_pluginInstallDirectory, pluginId);
                var dataPath = Path.Combine(pluginPath, "data");

                if (!Directory.Exists(backupPath))
                {
                    return;
                }

                Directory.CreateDirectory(dataPath);

                await Task.Run(() =>
                {
                    foreach (var file in Directory.GetFiles(backupPath, "*", SearchOption.AllDirectories))
                    {
                        var relativePath = Path.GetRelativePath(backupPath, file);
                        var targetPath = Path.Combine(dataPath, relativePath);
                        var targetDir = Path.GetDirectoryName(targetPath);

                        if (targetDir != null && !Directory.Exists(targetDir))
                        {
                            Directory.CreateDirectory(targetDir);
                        }

                        File.Copy(file, targetPath, overwrite: true);
                    }

                    // 删除备份
                    Directory.Delete(backupPath, recursive: true);
                }, cancellationToken);

                _logger?.LogInformation("[PluginPackageManager] Restored data for {PluginId} from {Path}", pluginId, backupPath);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "[PluginPackageManager] Failed to restore data for {PluginId}", pluginId);
            }
        }

        /// <summary>
        /// 检查插件是否已安装
        /// </summary>
        private bool IsPluginInstalled(string pluginId)
        {
            var pluginPath = Path.Combine(_pluginInstallDirectory, pluginId);
            return Directory.Exists(pluginPath);
        }

        /// <summary>
        /// 报告操作进度
        /// </summary>
        private void ReportProgress(string pluginId, PluginInstallStatus status, int progress, string message)
        {
            OperationProgress?.Invoke(this, new PluginOperationProgressEventArgs
            {
                PluginId = pluginId,
                Status = status,
                Progress = progress,
                Message = message
            });
        }

        /// <summary>
        /// 从本地 ZIP 文件安装插件
        /// </summary>
        public async Task<PluginOperationResult> InstallFromFileAsync(
            string zipFilePath,
            CancellationToken cancellationToken = default)
        {
            var stopwatch = Stopwatch.StartNew();

            await _operationLock.WaitAsync(cancellationToken);
            try
            {
                _logger?.LogInformation("[PluginPackageManager] Installing plugin from file: {Path}", zipFilePath);

                // 1. 验证文件存在
                if (!File.Exists(zipFilePath))
                {
                    return PluginOperationResult.Failed("unknown", PluginOperationType.Install, $"File not found: {zipFilePath}");
                }

                // 2. 验证是否为有效的 ZIP 文件
                if (!Path.GetExtension(zipFilePath).Equals(".zip", StringComparison.OrdinalIgnoreCase))
                {
                    return PluginOperationResult.Failed("unknown", PluginOperationType.Install, "File must be a .zip archive");
                }

                ReportProgress("unknown", PluginInstallStatus.Installing, 10, "Validating package...");

                // 3. 解压到临时目录并读取 manifest.json
                var tempExtractPath = Path.Combine(Path.GetTempPath(), $"Pulsar_Install_{Guid.NewGuid()}");
                Directory.CreateDirectory(tempExtractPath);

                try
                {
                    await Task.Run(() => ZipFile.ExtractToDirectory(zipFilePath, tempExtractPath), cancellationToken);

                    // 4. 查找 manifest.json
                    var manifestPath = Path.Combine(tempExtractPath, "manifest.json");
                    if (!File.Exists(manifestPath))
                    {
                        return PluginOperationResult.Failed("unknown", PluginOperationType.Install, "Invalid plugin package: manifest.json not found");
                    }

                    // 5. 读取 manifest.json
                    var manifestJson = await File.ReadAllTextAsync(manifestPath, cancellationToken);
                    var manifest = JsonSerializer.Deserialize<PluginManifest>(manifestJson);

                    if (manifest == null || string.IsNullOrEmpty(manifest.Id))
                    {
                        return PluginOperationResult.Failed("unknown", PluginOperationType.Install, "Invalid manifest.json: missing Id field");
                    }

                    var pluginId = manifest.Id;
                    ReportProgress(pluginId, PluginInstallStatus.Installing, 30, $"Installing {manifest.DisplayName ?? pluginId}...");

                    // 6. 检查是否已安装
                    if (IsPluginInstalled(pluginId))
                    {
                        return PluginOperationResult.Failed(pluginId, PluginOperationType.Install, $"Plugin {pluginId} is already installed. Please uninstall it first.");
                    }

                    // 7. 移动到插件目录
                    var installPath = Path.Combine(_pluginInstallDirectory, pluginId);
                    Directory.CreateDirectory(installPath);

                    ReportProgress(pluginId, PluginInstallStatus.Installing, 60, "Copying files...");

                    await Task.Run(() =>
                    {
                        foreach (var file in Directory.GetFiles(tempExtractPath, "*", SearchOption.AllDirectories))
                        {
                            var relativePath = Path.GetRelativePath(tempExtractPath, file);
                            var targetPath = Path.Combine(installPath, relativePath);
                            var targetDir = Path.GetDirectoryName(targetPath);

                            if (targetDir != null && !Directory.Exists(targetDir))
                            {
                                Directory.CreateDirectory(targetDir);
                            }

                            File.Copy(file, targetPath, overwrite: true);
                        }
                    }, cancellationToken);

                    ReportProgress(pluginId, PluginInstallStatus.Installed, 100, "Installation completed");

                    stopwatch.Stop();
                    _logger?.LogInformation("[PluginPackageManager] Successfully installed {PluginId} v{Version} from file in {Duration}ms",
                        pluginId, manifest.Version, stopwatch.ElapsedMilliseconds);

                    return PluginOperationResult.Successful(pluginId, PluginOperationType.Install, stopwatch.Elapsed);
                }
                finally
                {
                    // 清理临时目录
                    if (Directory.Exists(tempExtractPath))
                    {
                        try
                        {
                            Directory.Delete(tempExtractPath, recursive: true);
                        }
                        catch (Exception ex)
                        {
                            _logger?.LogWarning(ex, "[PluginPackageManager] Failed to delete temp directory: {Path}", tempExtractPath);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "[PluginPackageManager] Failed to install plugin from file");
                ReportProgress("unknown", PluginInstallStatus.Failed, 0, $"Installation failed: {ex.Message}");
                return PluginOperationResult.Failed("unknown", PluginOperationType.Install, ex.Message);
            }
            finally
            {
                _operationLock.Release();
            }
        }

        public void Dispose()
        {
            _operationLock?.Dispose();
        }
    }

    /// <summary>
    /// 插件操作进度事件参数
    /// </summary>
    public class PluginOperationProgressEventArgs : EventArgs
    {
        public string PluginId { get; set; } = string.Empty;
        public PluginInstallStatus Status { get; set; }
        public int Progress { get; set; }
        public string Message { get; set; } = string.Empty;
    }
}
