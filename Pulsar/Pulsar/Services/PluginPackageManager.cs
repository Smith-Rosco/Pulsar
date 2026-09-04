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
using Microsoft.Extensions.Logging.Abstractions;
using Pulsar.Core.Plugin;
using Pulsar.Core.Plugin.Metadata;
using Pulsar.Models;
using Pulsar.Services.Interfaces;

namespace Pulsar.Services
{
    /// <summary>
    /// 插件包管理器 - 实现插件的安装、卸载（从本地文件）
    /// </summary>
    public class PluginPackageManager : IPluginPackageManager
    {
        private readonly string _pluginInstallDirectory;
        private readonly ILogger<PluginPackageManager>? _logger;
        private readonly IPluginPackageIntegrityVerifier _integrityVerifier;
        private readonly SemaphoreSlim _operationLock = new(1, 1);

        public event EventHandler<PluginOperationProgressEventArgs>? OperationProgress;

        public PluginPackageManager(
            string pluginInstallDirectory,
            ILogger<PluginPackageManager>? logger = null,
            IPluginPackageIntegrityVerifier? integrityVerifier = null)
        {
            _pluginInstallDirectory = pluginInstallDirectory;
            _logger = logger;
            _integrityVerifier = integrityVerifier
                ?? new PluginPackageIntegrityService(NullLogger<PluginPackageIntegrityService>.Instance);

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
                if (!TryGetSafeInstallPath(pluginId, out var pluginPath, out var pathError))
                {
                    return PluginOperationResult.Failed(pluginId, PluginOperationType.Uninstall, pathError);
                }

                if (Directory.Exists(pluginPath))
                {
                    // 如果保留数据，备份配置文件
                    string? backupPath = null;
                    if (keepData)
                    {
                        backupPath = await BackupPluginDataAsync(pluginId, cancellationToken);
                    }

                    try
                    {
                        DeleteDirectoryWithRetry(pluginPath);
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogError(ex, "[PluginPackageManager] Failed to delete plugin directory {Path}. The plugin assembly may still be loaded; deactivate the plugin first.", pluginPath);
                        throw;
                    }
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
        /// 检查插件是否已安装。仅目录存在不算：必须是包含有效 manifest
        /// 的完整安装。失败卸载留下的残骸目录（只剩 DLL）会被视为未安装，
        /// 使覆盖安装可以自行清理。
        /// </summary>
        private bool IsPluginInstalled(string pluginId)
        {
            return TryGetSafeInstallPath(pluginId, out var pluginPath, out _)
                && Directory.Exists(pluginPath)
                && HasValidManifest(pluginPath);
        }

        private static bool HasValidManifest(string pluginPath)
        {
            // File-name resolution is single-sourced in PluginManifestReader.
            return PluginManifestReader.TryResolveManifestPath(pluginPath) != null;
        }

        /// <summary>
        /// 删除目录，带重试。可收集 ALC 的卸载与 GC 异步完成，文件句柄
        /// 释放可能有短暂延迟；立即重试几次通常就能成功。
        /// </summary>
        private void DeleteDirectoryWithRetry(string path)
        {
            const int maxAttempts = 5;
            for (var attempt = 1; attempt <= maxAttempts; attempt++)
            {
                try
                {
                    if (Directory.Exists(path))
                    {
                        Directory.Delete(path, recursive: true);
                    }
                    return;
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException && attempt < maxAttempts)
                {
                    _logger?.LogWarning("[PluginPackageManager] Directory delete attempt {Attempt}/{Max} failed for {Path}: {Message}",
                        attempt, maxAttempts, path, ex.Message);
                    Thread.Sleep(250);
                }
            }
        }

        /// <summary>
        /// Resolves a plugin install path while ensuring the manifest-controlled
        /// plugin ID cannot escape the plugin store via "..", rooted paths, or
        /// alternate directory separators.
        /// </summary>
        private bool TryGetSafeInstallPath(string pluginId, out string installPath, out string error)
        {
            installPath = string.Empty;
            error = string.Empty;

            if (string.IsNullOrWhiteSpace(pluginId))
            {
                error = "Invalid plugin package: plugin Id is empty.";
                return false;
            }

            var root = Path.GetFullPath(_pluginInstallDirectory)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            var candidate = Path.GetFullPath(Path.Combine(root, pluginId));
            var requiredPrefix = root + Path.DirectorySeparatorChar;

            if (!candidate.Equals(root, StringComparison.OrdinalIgnoreCase)
                && candidate.StartsWith(requiredPrefix, StringComparison.OrdinalIgnoreCase))
            {
                installPath = candidate;
                return true;
            }

            error = $"Invalid plugin package: plugin Id '{pluginId}' is not a safe relative path.";
            return false;
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
        /// Reads and validates a plugin ZIP manifest without installing it. Used by
        /// the settings UI to display the permission consent prompt.
        /// </summary>
        public async Task<PluginPackageInspectionResult> InspectPackageAsync(
            string zipFilePath,
            CancellationToken cancellationToken = default)
        {
            if (!File.Exists(zipFilePath))
            {
                return PluginPackageInspectionResult.Failed($"File not found: {zipFilePath}");
            }

            if (!Path.GetExtension(zipFilePath).Equals(".zip", StringComparison.OrdinalIgnoreCase))
            {
                return PluginPackageInspectionResult.Failed("File must be a .zip archive");
            }

            var tempExtractPath = Path.Combine(Path.GetTempPath(), $"Pulsar_Inspect_{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempExtractPath);

            try
            {
                await Task.Run(() => ZipFile.ExtractToDirectory(zipFilePath, tempExtractPath), cancellationToken);
                return ReadAndValidateManifest(tempExtractPath);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "[PluginPackageManager] Failed to inspect plugin package {Path}", zipFilePath);
                return PluginPackageInspectionResult.Failed(ex.Message);
            }
            finally
            {
                try
                {
                    if (Directory.Exists(tempExtractPath))
                    {
                        Directory.Delete(tempExtractPath, recursive: true);
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "[PluginPackageManager] Failed to delete inspect temp directory: {Path}", tempExtractPath);
                }
            }
        }

        private PluginPackageInspectionResult ReadAndValidateManifest(string extractPath)
        {
            // File resolution (new format first, legacy fallback) and the
            // case-insensitive parse are single-sourced in PluginManifestReader;
            // content validation stays here with its own error messages.
            var manifestPath = PluginManifestReader.TryResolveManifestPath(extractPath);
            if (manifestPath == null)
            {
                return PluginPackageInspectionResult.Failed("Invalid plugin package: manifest.json not found");
            }

            try
            {
                var manifest = PluginManifestReader.Parse(File.ReadAllText(manifestPath));

                if (manifest == null || string.IsNullOrWhiteSpace(manifest.Id))
                {
                    return PluginPackageInspectionResult.Failed("Invalid manifest.json: missing Id field");
                }

                var unknownPermissions = manifest.Permissions
                    .Where(permission => !PluginPermissions.IsKnown(permission))
                    .Distinct(StringComparer.Ordinal)
                    .ToArray();

                if (unknownPermissions.Length > 0)
                {
                    return PluginPackageInspectionResult.Failed(
                        $"Invalid manifest.json: unknown permission(s) {string.Join(", ", unknownPermissions)}");
                }

                return PluginPackageInspectionResult.Succeeded(manifest);
            }
            catch (JsonException ex)
            {
                return PluginPackageInspectionResult.Failed($"Invalid manifest.json: {ex.Message}");
            }
        }

        /// <summary>
        /// 从本地 ZIP 文件安装插件
        /// </summary>
        public async Task<PluginOperationResult> InstallFromFileAsync(
            string zipFilePath,
            IReadOnlyCollection<string>? approvedPermissions = null,
            CancellationToken cancellationToken = default)
        {
            var stopwatch = Stopwatch.StartNew();
            string? createdInstallPath = null;

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
                    var archiveIntegrity = await _integrityVerifier.VerifyArchiveAsync(zipFilePath, cancellationToken);
                    if (!archiveIntegrity.IsValid)
                    {
                        return PluginOperationResult.Failed(
                            "unknown",
                            PluginOperationType.Verify,
                            archiveIntegrity.Error ?? "Package integrity verification failed.");
                    }

                    await Task.Run(() => ZipFile.ExtractToDirectory(zipFilePath, tempExtractPath), cancellationToken);

                    // 4. Validate manifest.json / plugin.manifest.json. This helper
                    // also rejects unknown permission tokens before any file copy.
                    var inspection = ReadAndValidateManifest(tempExtractPath);
                    if (!inspection.Success || inspection.Manifest == null)
                    {
                        return PluginOperationResult.Failed(
                            "unknown",
                            PluginOperationType.Install,
                            inspection.ErrorMessage ?? "Invalid plugin package manifest");
                    }

                    var manifest = inspection.Manifest;

                    var extractedIntegrity = await _integrityVerifier.VerifyExtractedAsync(
                        tempExtractPath,
                        manifest.FileHashes,
                        cancellationToken);
                    if (!extractedIntegrity.IsValid)
                    {
                        return PluginOperationResult.Failed(
                            manifest.Id,
                            PluginOperationType.Verify,
                            extractedIntegrity.Error ?? "Extracted package hash verification failed.");
                    }

                    var approved = approvedPermissions?.ToHashSet(StringComparer.Ordinal) ?? new HashSet<string>(StringComparer.Ordinal);
                    var missingPermissions = manifest.Permissions
                        .Where(permission => !approved.Contains(permission))
                        .Distinct(StringComparer.Ordinal)
                        .ToArray();

                    if (missingPermissions.Length > 0)
                    {
                        return PluginOperationResult.Failed(
                            manifest.Id,
                            PluginOperationType.Install,
                            $"Permission approval required: {string.Join(", ", missingPermissions)}");
                    }

                    var pluginId = manifest.Id;
                    ReportProgress(pluginId, PluginInstallStatus.Installing, 30, $"Installing {manifest.DisplayName ?? pluginId}...");

                    // 6. 检查是否已安装（要求有效 manifest，残骸目录不算）
                    if (IsPluginInstalled(pluginId))
                    {
                        return PluginOperationResult.Failed(pluginId, PluginOperationType.Install, $"Plugin {pluginId} is already installed. Please uninstall it first.");
                    }

                    // 7. 移动到插件目录
                    if (!TryGetSafeInstallPath(pluginId, out var installPath, out var pathError))
                    {
                        return PluginOperationResult.Failed(pluginId, PluginOperationType.Install, pathError);
                    }

                    // 残骸目录（无 manifest，通常是上次运行中卸载/安装失败留下的）：
                    // 尝试清理后继续安装；清不掉（文件仍被锁定）则明确报错。
                    if (Directory.Exists(installPath))
                    {
                        try
                        {
                            DeleteDirectoryWithRetry(installPath);
                        }
                        catch (Exception ex)
                        {
                            return PluginOperationResult.Failed(
                                pluginId,
                                PluginOperationType.Install,
                                $"Leftover plugin directory could not be removed (files may be locked by a running instance): {ex.Message}");
                        }
                    }

                    Directory.CreateDirectory(installPath);
                    createdInstallPath = installPath;

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

                    var installRecord = await _integrityVerifier.WriteInstallRecordAsync(
                        installPath,
                        archiveIntegrity.PackageSha256,
                        cancellationToken);
                    if (!installRecord.IsValid)
                    {
                        throw new InvalidOperationException(installRecord.Error ?? "Failed to write plugin integrity record.");
                    }

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

                // Roll back any partial install directory so a failed package copy
                // never leaves a half-installed plugin that is visible to the loader.
                if (!string.IsNullOrEmpty(createdInstallPath))
                {
                    try
                    {
                        if (Directory.Exists(createdInstallPath))
                        {
                            Directory.Delete(createdInstallPath, recursive: true);
                            _logger?.LogInformation("[PluginPackageManager] Removed partial install directory: {Path}", createdInstallPath);
                        }
                    }
                    catch (Exception cleanupEx)
                    {
                        _logger?.LogWarning(cleanupEx, "[PluginPackageManager] Failed to remove partial install directory: {Path}", createdInstallPath);
                    }
                }

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
