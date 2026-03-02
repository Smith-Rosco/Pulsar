using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Pulsar.Core.Plugin
{
    /// <summary>
    /// 热重载管理器 - 监听插件文件变更并自动重载
    /// 
    /// 核心功能:
    /// 1. FileSystemWatcher - 监听插件目录文件变更
    /// 2. 防抖逻辑 - 避免频繁触发重载（500ms 延迟）
    /// 3. Shadow Copy - 复制文件到临时目录以避免文件锁定
    /// 4. 自动清理 - 清理旧的 Shadow Copy 文件
    /// 5. 事件通知 - 通知外部系统插件已重载
    /// </summary>
    public class HotReloadManager : IDisposable
    {
        private readonly string _pluginDirectory;
        private readonly string _shadowCopyDirectory;
        private readonly ILogger<HotReloadManager>? _logger;
        private readonly Dictionary<string, FileSystemWatcher> _watchers = new();
        private readonly Dictionary<string, System.Threading.Timer> _debounceTimers = new();
        private readonly Dictionary<string, string> _pluginPathMap = new(); // PluginId -> Original Path
        private readonly object _lock = new();
        private bool _isEnabled;
        private bool _disposed;

        /// <summary>
        /// 防抖延迟时间（毫秒）
        /// </summary>
        public int DebounceDelayMs { get; set; } = 500;

        /// <summary>
        /// 插件文件变更事件
        /// </summary>
        public event EventHandler<PluginFileChangedEventArgs>? PluginFileChanged;

        /// <summary>
        /// 插件重载完成事件
        /// </summary>
        public event EventHandler<PluginReloadedEventArgs>? PluginReloaded;

        public HotReloadManager(string pluginDirectory, ILogger<HotReloadManager>? logger = null)
        {
            _pluginDirectory = pluginDirectory ?? throw new ArgumentNullException(nameof(pluginDirectory));
            _logger = logger;

            // 创建 Shadow Copy 目录
            _shadowCopyDirectory = Path.Combine(Path.GetTempPath(), "Pulsar", "PluginShadow");
            Directory.CreateDirectory(_shadowCopyDirectory);

            _logger?.LogInformation("[HotReloadManager] Initialized with plugin directory: {PluginDirectory}", _pluginDirectory);
            _logger?.LogInformation("[HotReloadManager] Shadow copy directory: {ShadowCopyDirectory}", _shadowCopyDirectory);
        }

        /// <summary>
        /// 启用热重载
        /// </summary>
        public void Enable()
        {
            if (_isEnabled)
            {
                _logger?.LogWarning("[HotReloadManager] Hot reload is already enabled");
                return;
            }

            lock (_lock)
            {
                if (_isEnabled) return;

                _logger?.LogInformation("[HotReloadManager] Enabling hot reload...");

                if (!Directory.Exists(_pluginDirectory))
                {
                    _logger?.LogWarning("[HotReloadManager] Plugin directory does not exist: {PluginDirectory}", _pluginDirectory);
                    return;
                }

                // 扫描所有插件子目录
                var pluginFolders = Directory.GetDirectories(_pluginDirectory);
                foreach (var folder in pluginFolders)
                {
                    SetupWatcher(folder);
                }

                _isEnabled = true;
                _logger?.LogInformation("[HotReloadManager] ✓ Hot reload enabled for {Count} plugin folders", _watchers.Count);
            }
        }

        /// <summary>
        /// 禁用热重载
        /// </summary>
        public void Disable()
        {
            if (!_isEnabled)
            {
                _logger?.LogWarning("[HotReloadManager] Hot reload is already disabled");
                return;
            }

            lock (_lock)
            {
                if (!_isEnabled) return;

                _logger?.LogInformation("[HotReloadManager] Disabling hot reload...");

                // 停止所有 Watcher
                foreach (var watcher in _watchers.Values)
                {
                    watcher.EnableRaisingEvents = false;
                    watcher.Dispose();
                }
                _watchers.Clear();

                // 停止所有防抖定时器
                foreach (var timer in _debounceTimers.Values)
                {
                    timer.Dispose();
                }
                _debounceTimers.Clear();

                _isEnabled = false;
                _logger?.LogInformation("[HotReloadManager] ✓ Hot reload disabled");
            }
        }

        /// <summary>
        /// 注册插件路径映射（用于追踪插件 ID 和文件路径的关系）
        /// </summary>
        public void RegisterPlugin(string pluginId, string pluginPath)
        {
            lock (_lock)
            {
                _pluginPathMap[pluginId] = pluginPath;
                _logger?.LogDebug("[HotReloadManager] Registered plugin: {PluginId} -> {PluginPath}", pluginId, pluginPath);
            }
        }

        /// <summary>
        /// 取消注册插件
        /// </summary>
        public void UnregisterPlugin(string pluginId)
        {
            lock (_lock)
            {
                _pluginPathMap.Remove(pluginId);
                _logger?.LogDebug("[HotReloadManager] Unregistered plugin: {PluginId}", pluginId);
            }
        }

        /// <summary>
        /// 创建 Shadow Copy（复制插件文件到临时目录）
        /// </summary>
        public string CreateShadowCopy(string originalPath)
        {
            if (!File.Exists(originalPath))
            {
                throw new FileNotFoundException($"Plugin file not found: {originalPath}");
            }

            var fileName = Path.GetFileName(originalPath);
            var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff");
            var shadowFileName = $"{Path.GetFileNameWithoutExtension(fileName)}_{timestamp}{Path.GetExtension(fileName)}";
            var shadowPath = Path.Combine(_shadowCopyDirectory, shadowFileName);

            try
            {
                // 复制主 DLL
                File.Copy(originalPath, shadowPath, overwrite: true);
                _logger?.LogDebug("[HotReloadManager] Created shadow copy: {ShadowPath}", shadowPath);

                // 复制依赖文件（同目录下的其他 DLL 和 PDB）
                var directory = Path.GetDirectoryName(originalPath);
                if (!string.IsNullOrEmpty(directory))
                {
                    var dependencies = Directory.GetFiles(directory, "*.*")
                        .Where(f => 
                        {
                            var ext = Path.GetExtension(f).ToLowerInvariant();
                            return (ext == ".dll" || ext == ".pdb") && !f.Equals(originalPath, StringComparison.OrdinalIgnoreCase);
                        });

                    foreach (var dep in dependencies)
                    {
                        var depFileName = Path.GetFileName(dep);
                        var depShadowPath = Path.Combine(_shadowCopyDirectory, $"{Path.GetFileNameWithoutExtension(depFileName)}_{timestamp}{Path.GetExtension(depFileName)}");
                        File.Copy(dep, depShadowPath, overwrite: true);
                        _logger?.LogTrace("[HotReloadManager] Copied dependency: {DepFileName}", depFileName);
                    }
                }

                return shadowPath;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "[HotReloadManager] Failed to create shadow copy for {OriginalPath}", originalPath);
                throw;
            }
        }

        /// <summary>
        /// 清理旧的 Shadow Copy 文件（保留最近 5 个版本）
        /// </summary>
        public void CleanupOldShadowCopies(string pluginFileName, int keepCount = 5)
        {
            try
            {
                var baseName = Path.GetFileNameWithoutExtension(pluginFileName);
                var extension = Path.GetExtension(pluginFileName);
                var pattern = $"{baseName}_*{extension}";

                var shadowFiles = Directory.GetFiles(_shadowCopyDirectory, pattern)
                    .OrderByDescending(f => File.GetCreationTimeUtc(f))
                    .ToList();

                var filesToDelete = shadowFiles.Skip(keepCount).ToList();
                foreach (var file in filesToDelete)
                {
                    try
                    {
                        File.Delete(file);
                        _logger?.LogTrace("[HotReloadManager] Deleted old shadow copy: {File}", Path.GetFileName(file));
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogWarning(ex, "[HotReloadManager] Failed to delete shadow copy: {File}", file);
                    }
                }

                if (filesToDelete.Count > 0)
                {
                    _logger?.LogDebug("[HotReloadManager] Cleaned up {Count} old shadow copies for {PluginFileName}", 
                        filesToDelete.Count, pluginFileName);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "[HotReloadManager] Failed to cleanup shadow copies for {PluginFileName}", pluginFileName);
            }
        }

        /// <summary>
        /// 清理所有 Shadow Copy 文件
        /// </summary>
        public void CleanupAllShadowCopies()
        {
            try
            {
                if (Directory.Exists(_shadowCopyDirectory))
                {
                    var files = Directory.GetFiles(_shadowCopyDirectory);
                    foreach (var file in files)
                    {
                        try
                        {
                            File.Delete(file);
                        }
                        catch (Exception ex)
                        {
                            _logger?.LogWarning(ex, "[HotReloadManager] Failed to delete shadow copy: {File}", file);
                        }
                    }

                    _logger?.LogInformation("[HotReloadManager] Cleaned up all shadow copies ({Count} files)", files.Length);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "[HotReloadManager] Failed to cleanup all shadow copies");
            }
        }

        private void SetupWatcher(string folder)
        {
            try
            {
                var watcher = new FileSystemWatcher(folder)
                {
                    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size,
                    Filter = "*.dll",
                    EnableRaisingEvents = true
                };

                watcher.Changed += OnFileChanged;
                watcher.Created += OnFileChanged;
                watcher.Renamed += OnFileRenamed;

                _watchers[folder] = watcher;
                _logger?.LogDebug("[HotReloadManager] Setup watcher for folder: {Folder}", folder);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "[HotReloadManager] Failed to setup watcher for folder: {Folder}", folder);
            }
        }

        private void OnFileChanged(object sender, FileSystemEventArgs e)
        {
            if (_disposed) return;

            _logger?.LogDebug("[HotReloadManager] File changed: {FileName} ({ChangeType})", e.Name, e.ChangeType);

            // 防抖处理
            lock (_lock)
            {
                var key = e.FullPath;

                // 取消之前的定时器
                if (_debounceTimers.TryGetValue(key, out var existingTimer))
                {
                    existingTimer.Dispose();
                }

                // 创建新的防抖定时器
                var timer = new System.Threading.Timer(_ => OnDebouncedFileChanged(e.FullPath), null, DebounceDelayMs, Timeout.Infinite);
                _debounceTimers[key] = timer;
            }
        }

        private void OnFileRenamed(object sender, RenamedEventArgs e)
        {
            if (_disposed) return;

            _logger?.LogDebug("[HotReloadManager] File renamed: {OldName} -> {NewName}", e.OldName, e.Name);
            OnFileChanged(sender, e);
        }

        private void OnDebouncedFileChanged(string filePath)
        {
            if (_disposed) return;

            try
            {
                _logger?.LogInformation("[HotReloadManager] Processing file change: {FilePath}", filePath);

                // 查找对应的插件 ID
                string? pluginId = null;
                lock (_lock)
                {
                    pluginId = _pluginPathMap.FirstOrDefault(kvp => 
                        kvp.Value.Equals(filePath, StringComparison.OrdinalIgnoreCase)).Key;
                }

                // 触发文件变更事件
                PluginFileChanged?.Invoke(this, new PluginFileChangedEventArgs
                {
                    FilePath = filePath,
                    PluginId = pluginId,
                    ChangeTime = DateTime.UtcNow
                });

                _logger?.LogInformation("[HotReloadManager] ✓ File change processed: {FilePath}", filePath);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "[HotReloadManager] Error processing file change: {FilePath}", filePath);
            }
            finally
            {
                // 清理防抖定时器
                lock (_lock)
                {
                    if (_debounceTimers.TryGetValue(filePath, out var timer))
                    {
                        timer.Dispose();
                        _debounceTimers.Remove(filePath);
                    }
                }
            }
        }

        public void Dispose()
        {
            if (_disposed) return;

            _disposed = true;
            Disable();

            _logger?.LogInformation("[HotReloadManager] Disposed");
        }
    }

    /// <summary>
    /// 插件文件变更事件参数
    /// </summary>
    public class PluginFileChangedEventArgs : EventArgs
    {
        public required string FilePath { get; init; }
        public string? PluginId { get; init; }
        public DateTime ChangeTime { get; init; }
    }

    /// <summary>
    /// 插件重载完成事件参数
    /// </summary>
    public class PluginReloadedEventArgs : EventArgs
    {
        public required string PluginId { get; init; }
        public bool Success { get; init; }
        public string? ErrorMessage { get; init; }
        public DateTime ReloadTime { get; init; }
    }
}
