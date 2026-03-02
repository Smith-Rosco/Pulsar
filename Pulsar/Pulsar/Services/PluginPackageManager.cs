using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Pulsar.Core.Plugin;
using Pulsar.Core.Plugin.Versioning;
using Pulsar.Models;

namespace Pulsar.Services
{
    /// <summary>
    /// 插件包管理器 - 实现插件的安装、更新、卸载
    /// </summary>
    public class PluginPackageManager
    {
        private readonly PluginRepository _repository;
        private readonly string _pluginInstallDirectory;
        private readonly ILogger<PluginPackageManager>? _logger;
        private readonly HttpClient _httpClient;
        private readonly PluginVersionResolver _versionResolver;
        private readonly SemaphoreSlim _operationLock = new(1, 1);

        public event EventHandler<PluginOperationProgressEventArgs>? OperationProgress;

        public PluginPackageManager(
            PluginRepository repository,
            string pluginInstallDirectory,
            ILogger<PluginPackageManager>? logger = null)
        {
            _repository = repository;
            _pluginInstallDirectory = pluginInstallDirectory;
            _logger = logger;
            _httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromMinutes(5)
            };
            _versionResolver = new PluginVersionResolver();

            // 确保安装目录存在
            if (!Directory.Exists(_pluginInstallDirectory))
            {
                Directory.CreateDirectory(_pluginInstallDirectory);
            }
        }

        /// <summary>
        /// 安装插件
        /// </summary>
        public async Task<PluginOperationResult> InstallAsync(
            string pluginId,
            string? version = null,
            bool installDependencies = true,
            CancellationToken cancellationToken = default)
        {
            var stopwatch = Stopwatch.StartNew();

            await _operationLock.WaitAsync(cancellationToken);
            try
            {
                _logger?.LogInformation("[PluginPackageManager] Installing plugin: {PluginId} v{Version}", pluginId, version ?? "latest");

                // 1. 获取包信息
                var package = version != null
                    ? _repository.GetPackage(pluginId, version)
                    : _repository.GetLatestVersion(pluginId);

                if (package == null)
                {
                    return PluginOperationResult.Failed(pluginId, PluginOperationType.Install, $"Plugin {pluginId} not found in repository");
                }

                // 2. 检查是否已安装
                if (IsPluginInstalled(pluginId))
                {
                    return PluginOperationResult.Failed(pluginId, PluginOperationType.Install, $"Plugin {pluginId} is already installed");
                }

                // 3. 解析并安装依赖
                if (installDependencies && package.Dependencies.Any())
                {
                    ReportProgress(pluginId, PluginInstallStatus.Installing, 10, "Resolving dependencies...");

                    var dependencyResult = await InstallDependenciesAsync(package, cancellationToken);
                    if (!dependencyResult.Success)
                    {
                        return PluginOperationResult.Failed(pluginId, PluginOperationType.Install, $"Failed to install dependencies: {dependencyResult.ErrorMessage}");
                    }
                }

                // 4. 下载包（如果需要）
                if (!_repository.IsPackageDownloaded(pluginId, package.Version))
                {
                    ReportProgress(pluginId, PluginInstallStatus.Downloading, 30, "Downloading package...");

                    var downloadResult = await DownloadPackageAsync(package, cancellationToken);
                    if (!downloadResult.Success)
                    {
                        return downloadResult;
                    }
                }

                // 5. 验证包完整性
                ReportProgress(pluginId, PluginInstallStatus.Installing, 60, "Verifying package...");

                if (!await VerifyPackageAsync(package, cancellationToken))
                {
                    return PluginOperationResult.Failed(pluginId, PluginOperationType.Install, "Package verification failed");
                }

                // 6. 解压并安装
                ReportProgress(pluginId, PluginInstallStatus.Installing, 80, "Extracting package...");

                var installResult = await ExtractAndInstallAsync(package, cancellationToken);
                if (!installResult.Success)
                {
                    return installResult;
                }

                // 7. 更新包信息
                package.IsInstalled = true;
                package.InstalledVersion = package.Version;
                await _repository.AddOrUpdatePackageAsync(package, cancellationToken);

                ReportProgress(pluginId, PluginInstallStatus.Installed, 100, "Installation completed");

                stopwatch.Stop();
                _logger?.LogInformation("[PluginPackageManager] Successfully installed {PluginId} v{Version} in {Duration}ms",
                    pluginId, package.Version, stopwatch.ElapsedMilliseconds);

                return PluginOperationResult.Successful(pluginId, PluginOperationType.Install, stopwatch.Elapsed);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "[PluginPackageManager] Failed to install plugin {PluginId}", pluginId);
                ReportProgress(pluginId, PluginInstallStatus.Failed, 0, $"Installation failed: {ex.Message}");
                return PluginOperationResult.Failed(pluginId, PluginOperationType.Install, ex.Message);
            }
            finally
            {
                _operationLock.Release();
            }
        }

        /// <summary>
        /// 更新插件
        /// </summary>
        public async Task<PluginOperationResult> UpdateAsync(
            string pluginId,
            string? targetVersion = null,
            CancellationToken cancellationToken = default)
        {
            var stopwatch = Stopwatch.StartNew();

            await _operationLock.WaitAsync(cancellationToken);
            try
            {
                _logger?.LogInformation("[PluginPackageManager] Updating plugin: {PluginId}", pluginId);

                // 1. 检查是否已安装
                if (!IsPluginInstalled(pluginId))
                {
                    return PluginOperationResult.Failed(pluginId, PluginOperationType.Update, $"Plugin {pluginId} is not installed");
                }

                // 2. 获取当前版本和目标版本
                var currentPackage = _repository.GetAllPackages()
                    .FirstOrDefault(p => p.Id == pluginId && p.IsInstalled);

                var targetPackage = targetVersion != null
                    ? _repository.GetPackage(pluginId, targetVersion)
                    : _repository.GetLatestVersion(pluginId);

                if (targetPackage == null)
                {
                    return PluginOperationResult.Failed(pluginId, PluginOperationType.Update, "Target version not found");
                }

                if (currentPackage != null && currentPackage.Version == targetPackage.Version)
                {
                    return PluginOperationResult.Failed(pluginId, PluginOperationType.Update, "Already at target version");
                }

                ReportProgress(pluginId, PluginInstallStatus.Updating, 10, "Preparing update...");

                // 3. 卸载旧版本
                ReportProgress(pluginId, PluginInstallStatus.Updating, 30, "Removing old version...");

                var uninstallResult = await UninstallAsync(pluginId, keepData: true, cancellationToken: cancellationToken);
                if (!uninstallResult.Success)
                {
                    return PluginOperationResult.Failed(pluginId, PluginOperationType.Update, $"Failed to uninstall old version: {uninstallResult.ErrorMessage}");
                }

                // 4. 安装新版本
                ReportProgress(pluginId, PluginInstallStatus.Updating, 50, "Installing new version...");

                var installResult = await InstallAsync(pluginId, targetPackage.Version, installDependencies: true, cancellationToken: cancellationToken);
                if (!installResult.Success)
                {
                    return PluginOperationResult.Failed(pluginId, PluginOperationType.Update, $"Failed to install new version: {installResult.ErrorMessage}");
                }

                ReportProgress(pluginId, PluginInstallStatus.Installed, 100, "Update completed");

                stopwatch.Stop();
                _logger?.LogInformation("[PluginPackageManager] Successfully updated {PluginId} to v{Version} in {Duration}ms",
                    pluginId, targetPackage.Version, stopwatch.ElapsedMilliseconds);

                return PluginOperationResult.Successful(pluginId, PluginOperationType.Update, stopwatch.Elapsed);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "[PluginPackageManager] Failed to update plugin {PluginId}", pluginId);
                ReportProgress(pluginId, PluginInstallStatus.Failed, 0, $"Update failed: {ex.Message}");
                return PluginOperationResult.Failed(pluginId, PluginOperationType.Update, ex.Message);
            }
            finally
            {
                _operationLock.Release();
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

                ReportProgress(pluginId, PluginInstallStatus.Uninstalling, 80, "Updating registry...");

                // 3. 更新包信息
                var package = _repository.GetAllPackages()
                    .FirstOrDefault(p => p.Id == pluginId && p.IsInstalled);

                if (package != null)
                {
                    package.IsInstalled = false;
                    package.InstalledVersion = null;
                    await _repository.AddOrUpdatePackageAsync(package, cancellationToken);
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
        /// 下载插件包
        /// </summary>
        private async Task<PluginOperationResult> DownloadPackageAsync(
            PluginPackageInfo package,
            CancellationToken cancellationToken)
        {
            try
            {
                if (string.IsNullOrEmpty(package.DownloadUrl))
                {
                    return PluginOperationResult.Failed(package.Id, PluginOperationType.Download, "Download URL is not specified");
                }

                var packagePath = _repository.GetPackagePath(package.Id, package.Version);
                if (!Directory.Exists(packagePath))
                {
                    Directory.CreateDirectory(packagePath);
                }

                var packageFilePath = _repository.GetPackageFilePath(package.Id, package.Version);

                _logger?.LogInformation("[PluginPackageManager] Downloading {PluginId} from {Url}", package.Id, package.DownloadUrl);

                using var response = await _httpClient.GetAsync(package.DownloadUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                response.EnsureSuccessStatusCode();

                var totalBytes = response.Content.Headers.ContentLength ?? 0;
                var downloadedBytes = 0L;

                using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
                using var fileStream = new FileStream(packageFilePath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);

                var buffer = new byte[8192];
                int bytesRead;

                while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) > 0)
                {
                    await fileStream.WriteAsync(buffer, 0, bytesRead, cancellationToken);
                    downloadedBytes += bytesRead;

                    if (totalBytes > 0)
                    {
                        var progress = (int)((downloadedBytes * 100) / totalBytes);
                        ReportProgress(package.Id, PluginInstallStatus.Downloading, 30 + (progress * 30 / 100), $"Downloading... {downloadedBytes}/{totalBytes} bytes");
                    }
                }

                package.LocalPath = packageFilePath;
                await _repository.AddOrUpdatePackageAsync(package, cancellationToken);

                _logger?.LogInformation("[PluginPackageManager] Downloaded {PluginId} to {Path}", package.Id, packageFilePath);

                return PluginOperationResult.Successful(package.Id, PluginOperationType.Download, TimeSpan.Zero);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "[PluginPackageManager] Failed to download {PluginId}", package.Id);
                return PluginOperationResult.Failed(package.Id, PluginOperationType.Download, ex.Message);
            }
        }

        /// <summary>
        /// 验证包完整性
        /// </summary>
        private async Task<bool> VerifyPackageAsync(PluginPackageInfo package, CancellationToken cancellationToken)
        {
            try
            {
                if (string.IsNullOrEmpty(package.LocalPath) || !File.Exists(package.LocalPath))
                {
                    return false;
                }

                // 如果提供了 SHA256 校验和，验证文件完整性
                if (!string.IsNullOrEmpty(package.Sha256))
                {
                    using var sha256 = SHA256.Create();
                    using var stream = File.OpenRead(package.LocalPath);
                    var hash = await sha256.ComputeHashAsync(stream, cancellationToken);
                    var hashString = BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();

                    if (hashString != package.Sha256.ToLowerInvariant())
                    {
                        _logger?.LogError("[PluginPackageManager] SHA256 mismatch for {PluginId}: expected {Expected}, got {Actual}",
                            package.Id, package.Sha256, hashString);
                        return false;
                    }
                }

                // 验证 ZIP 文件完整性
                try
                {
                    using var archive = ZipFile.OpenRead(package.LocalPath);
                    // 如果能打开，说明文件完整
                    return true;
                }
                catch
                {
                    _logger?.LogError("[PluginPackageManager] Invalid ZIP file for {PluginId}", package.Id);
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "[PluginPackageManager] Failed to verify package {PluginId}", package.Id);
                return false;
            }
        }

        /// <summary>
        /// 解压并安装包
        /// </summary>
        private async Task<PluginOperationResult> ExtractAndInstallAsync(
            PluginPackageInfo package,
            CancellationToken cancellationToken)
        {
            try
            {
                if (string.IsNullOrEmpty(package.LocalPath) || !File.Exists(package.LocalPath))
                {
                    return PluginOperationResult.Failed(package.Id, PluginOperationType.Install, "Package file not found");
                }

                var installPath = Path.Combine(_pluginInstallDirectory, package.Id);

                // 删除旧的安装目录（如果存在）
                if (Directory.Exists(installPath))
                {
                    Directory.Delete(installPath, recursive: true);
                }

                Directory.CreateDirectory(installPath);

                // 解压 ZIP 文件
                await Task.Run(() => ZipFile.ExtractToDirectory(package.LocalPath, installPath), cancellationToken);

                _logger?.LogInformation("[PluginPackageManager] Extracted {PluginId} to {Path}", package.Id, installPath);

                return PluginOperationResult.Successful(package.Id, PluginOperationType.Install, TimeSpan.Zero);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "[PluginPackageManager] Failed to extract package {PluginId}", package.Id);
                return PluginOperationResult.Failed(package.Id, PluginOperationType.Install, ex.Message);
            }
        }

        /// <summary>
        /// 安装依赖项
        /// </summary>
        private async Task<PluginOperationResult> InstallDependenciesAsync(
            PluginPackageInfo package,
            CancellationToken cancellationToken)
        {
            try
            {
                _logger?.LogInformation("[PluginPackageManager] Installing dependencies for {PluginId}", package.Id);

                foreach (var dependency in package.Dependencies.Where(d => !d.IsOptional))
                {
                    // 检查依赖是否已安装
                    if (IsPluginInstalled(dependency.PluginId))
                    {
                        _logger?.LogDebug("[PluginPackageManager] Dependency {DependencyId} is already installed", dependency.PluginId);
                        continue;
                    }

                    // 解析版本约束
                    var availableVersions = _repository.GetPackageVersions(dependency.PluginId);
                    
                    // 注册可用版本到 resolver
                    foreach (var pkg in availableVersions)
                    {
                        var manifest = new PluginManifest
                        {
                            Id = pkg.Id,
                            Version = pkg.Version,
                            DisplayName = pkg.Name,
                            Description = pkg.Description
                        };
                        _versionResolver.RegisterVersion(manifest);
                    }

                    var resolvedManifest = _versionResolver.ResolveVersion(
                        dependency.PluginId,
                        dependency.VersionConstraint);

                    if (resolvedManifest == null)
                    {
                        return PluginOperationResult.Failed(
                            package.Id,
                            PluginOperationType.Install,
                            $"Cannot resolve dependency {dependency.PluginId} {dependency.VersionConstraint}");
                    }

                    // 递归安装依赖
                    var installResult = await InstallAsync(dependency.PluginId, resolvedManifest.Version, installDependencies: true, cancellationToken: cancellationToken);
                    if (!installResult.Success)
                    {
                        return installResult;
                    }
                }

                return PluginOperationResult.Successful(package.Id, PluginOperationType.Install, TimeSpan.Zero);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "[PluginPackageManager] Failed to install dependencies for {PluginId}", package.Id);
                return PluginOperationResult.Failed(package.Id, PluginOperationType.Install, ex.Message);
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

        public void Dispose()
        {
            _httpClient?.Dispose();
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
