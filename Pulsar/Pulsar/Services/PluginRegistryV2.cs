using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Pulsar.Core.Plugin;
using Pulsar.Core.Plugin.Metadata;
using Pulsar.Core.Plugin.Runtime;
using Pulsar.Core.Plugin.Versioning;
using Pulsar.Core.Plugin.Security;
using Pulsar.Models;

namespace Pulsar.Services
{
    /// <summary>
    /// 增强的插件注册中心 - 支持热插拔、版本管理、运行时卸载
    /// 
    /// 新特性 (v2.0):
    /// - 运行时加载/卸载插件
    /// - 版本管理和依赖解析
    /// - 插件宿主隔离（PluginHost）
    /// - 内存安全（WeakReference + GC）
    /// - 热重载支持
    /// 
    /// 新特性 (v2.1):
    /// - 权限管理系统
    /// - 运行时权限检查
    /// </summary>
    public class PluginRegistryV2
    {
        private readonly Dictionary<string, PluginHost> _hosts = new();

        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<PluginRegistryV2> _logger;
        private readonly Services.Interfaces.ITrayService? _trayService;
        private Services.Interfaces.IConfigService? _configService;
        private readonly Services.Interfaces.IPluginUsageTracker? _usageTracker;
        private readonly Services.Interfaces.IPluginHealthMonitor? _healthMonitor;
        
        private readonly PluginVersionResolver _versionResolver;
        private readonly PluginManifestLoader _manifestLoader;
        private HotReloadManager? _hotReloadManager;
        private readonly PermissionInterceptor _permissionInterceptor;
        private readonly IPluginBreakerPolicy _breakerPolicy;

        /// <summary>
        /// 权限拦截器 - 用于管理插件权限
        /// </summary>
        public PermissionInterceptor PermissionInterceptor => _permissionInterceptor;

        public PluginRegistryV2(IServiceProvider serviceProvider, ILogger<PluginRegistryV2> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
            _trayService = _serviceProvider.GetService(typeof(Services.Interfaces.ITrayService)) as Services.Interfaces.ITrayService;
            _usageTracker = _serviceProvider.GetService(typeof(Services.Interfaces.IPluginUsageTracker)) as Services.Interfaces.IPluginUsageTracker;
            _healthMonitor = _serviceProvider.GetService(typeof(Services.Interfaces.IPluginHealthMonitor)) as Services.Interfaces.IPluginHealthMonitor;
            
            _versionResolver = new PluginVersionResolver(logger);
            _manifestLoader = new PluginManifestLoader(logger);
            _breakerPolicy = new PluginCircuitBreakerPolicy(
                _serviceProvider.GetService(typeof(ILogger<PluginCircuitBreakerPolicy>)) as ILogger<PluginCircuitBreakerPolicy>,
                _healthMonitor,
                _trayService);
            
            // 创建权限拦截器
            var permissionLogger = _serviceProvider.GetService(typeof(ILogger<PermissionInterceptor>)) as ILogger<PermissionInterceptor>;
            _permissionInterceptor = new PermissionInterceptor(permissionLogger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<PermissionInterceptor>.Instance);
        }

        /// <summary>
        /// 加载单个插件
        /// </summary>
        public async Task<bool> LoadPluginAsync(string pluginPath)
        {
            try
            {
                _logger.LogInformation("[PluginRegistryV2] Loading plugin from {PluginPath}", pluginPath);

                // 1. 创建插件宿主
                var host = new PluginHost(pluginPath, _serviceProvider, _logger);
                await host.LoadAsync();

                // 2. 检查是否已存在
                if (_hosts.ContainsKey(host.PluginId))
                {
                    _logger.LogWarning("[PluginRegistryV2] Plugin already loaded: {PluginId}", host.PluginId);
                    await host.UnloadAsync();
                    return false;
                }

                // 3. 加载清单（如果存在）
                var manifest = await _manifestLoader.LoadFromPluginPathAsync(pluginPath);
                if (manifest != null)
                {
                    _versionResolver.RegisterVersion(manifest);
                    _logger.LogDebug("[PluginRegistryV2] Loaded manifest for {PluginId}", host.PluginId);
                }

                // 4. 注册插件
                _hosts[host.PluginId] = host;

                // 5. 注册权限（根据插件层级自动授予）
                var tier = GetTier(host);
                var permissions = tier switch
                {
                    PluginTier.Core => PermissionSets.System, // 核心插件获得系统权限
                    PluginTier.Extension => PermissionSets.Standard, // 扩展插件获得标准权限
                    _ => PermissionSets.Basic
                };
                _permissionInterceptor.RegisterPluginPermissions(host.PluginId, permissions);
                _permissionInterceptor.GrantPermissions(host.PluginId, permissions);
                _logger.LogDebug("[PluginRegistryV2] Granted {Tier} permissions to {PluginId}", tier, host.PluginId);

                // 6. 注册到热重载管理器
                _hotReloadManager?.RegisterPlugin(host.PluginId, pluginPath);

                // 7. 确保配置中有插件条目
                if (_configService != null)
                {
                    var config = _configService.Current;
                    if (config != null && !config.Plugins.ContainsKey(host.PluginId))
                    {
                        config.Plugins[host.PluginId] = new Models.PluginProfile { Enabled = true };
                    }
                }

                _logger.LogInformation("[PluginRegistryV2] ✓ Successfully loaded plugin: {PluginId} v{Version}", 
                    host.PluginId, host.Version);
                
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[PluginRegistryV2] Failed to load plugin: {PluginPath}", pluginPath);
                return false;
            }
        }

        /// <summary>
        /// 卸载插件
        /// </summary>
        public async Task<bool> UnloadPluginAsync(string pluginId)
        {
            if (!_hosts.TryGetValue(pluginId, out var host))
            {
                _logger.LogWarning("[PluginRegistryV2] Plugin not found: {PluginId}", pluginId);
                return false;
            }

            try
            {
                _logger.LogInformation("[PluginRegistryV2] Unloading plugin: {PluginId}", pluginId);
                
                await host.UnloadAsync();
                _hosts.Remove(pluginId);
                
                // 从热重载管理器取消注册
                _hotReloadManager?.UnregisterPlugin(pluginId);
                
                _logger.LogInformation("[PluginRegistryV2] ✓ Successfully unloaded plugin: {PluginId}", pluginId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[PluginRegistryV2] Failed to unload plugin: {PluginId}", pluginId);
                return false;
            }
        }

        /// <summary>
        /// 热重载插件
        /// </summary>
        public async Task<bool> ReloadPluginAsync(string pluginId, string? newPluginPath = null)
        {
            if (!_hosts.TryGetValue(pluginId, out var host))
            {
                _logger.LogWarning("[PluginRegistryV2] Plugin not found for reload: {PluginId}", pluginId);
                return false;
            }

            try
            {
                _logger.LogInformation("[PluginRegistryV2] Reloading plugin: {PluginId}", pluginId);

                // 1. 获取原始路径（如果没有提供新路径）
                var pluginPath = newPluginPath ?? GetPluginPath(pluginId);
                if (string.IsNullOrEmpty(pluginPath))
                {
                    _logger.LogError("[PluginRegistryV2] Cannot determine plugin path for: {PluginId}", pluginId);
                    return false;
                }

                // 2. 卸载旧版本
                await UnloadPluginAsync(pluginId);

                // 3. 等待 GC 回收
                await Task.Delay(500);

                // 4. 加载新版本
                return await LoadPluginAsync(pluginPath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[PluginRegistryV2] Failed to reload plugin: {PluginId}", pluginId);
                return false;
            }
        }

        /// <summary>
        /// 执行插件动作（含熔断保护）
        /// </summary>
        public async Task<PluginResult> ExecuteAsync(
            string pluginId,
            string action,
            IReadOnlyDictionary<string, string> args,
            PulsarContext context)
        {
            if (!_hosts.TryGetValue(pluginId, out var host))
            {
                _logger.LogError("[PluginRegistryV2] Plugin not found: {PluginId}", pluginId);
                return PluginResult.Error($"Plugin not found: {pluginId}");
            }

            // 检查插件是否存活
            if (!host.IsAlive)
            {
                _logger.LogError("[PluginRegistryV2] Plugin has been garbage collected: {PluginId}", pluginId);
                return PluginResult.Error("Plugin has been unloaded");
            }

            var tier = GetTier(host);
            var descriptor = new PluginDescriptor
            {
                Id = pluginId,
                DisplayName = host.DisplayName,
                Version = host.Version,
                Author = host.Author,
                Description = host.DisplayName,
                Icon = string.Empty,
                CanDisable = tier != PluginTier.Core,
                Tier = tier,
                ImplementationType = host.GetPluginInstance()?.GetType() ?? typeof(object),
                Dependencies = Array.Empty<string>(),
                Metadata = host.GetPluginInstance() is IPluginMetadataProvider metadataProvider
                    ? metadataProvider.GetMetadata()
                    : new PluginMetadata
                    {
                        Id = pluginId,
                        Display = new DisplayInfo
                        {
                            Name = host.DisplayName,
                            Description = host.DisplayName,
                            IconKey = string.Empty,
                            Category = "Runtime",
                            Version = host.Version,
                            Author = host.Author,
                            License = string.Empty
                        },
                        Schema = null,
                        UI = new UIHints
                        {
                            Badge = "Plugin",
                            AccentColor = "#4A90E2",
                            ShowInQuickAccess = false,
                            SortOrder = 0
                        },
                        Capabilities = new PluginCapabilities
                        {
                            SupportedActions = new List<string>(),
                            Dependencies = new List<string>(),
                            Tier = tier,
                            MinPulsarVersion = host.Version
                        },
                        Actions = new Dictionary<string, SlotActionMetadata>(StringComparer.OrdinalIgnoreCase)
                    },
                IsConfigurable = host.GetPluginInstance() is IPluginConfigurable
            };

            // Extension 插件检查
            if (tier == PluginTier.Extension)
            {
                // 检查是否被用户禁用
                if (_configService?.Current?.Plugins.TryGetValue(pluginId, out var profile) == true)
                {
                    if (!profile.Enabled)
                    {
                        _logger.LogWarning("[PluginRegistryV2] Plugin is disabled: {PluginId}", pluginId);
                        return PluginResult.Error("Plugin is disabled.");
                    }
                }

                var availability = _breakerPolicy.CheckAvailability(descriptor, pluginId);
                if (!availability.Allowed)
                {
                    return PluginResult.Error(availability.Message ?? "Plugin unavailable.");
                }
            }

            // 创建执行上下文
            using var executionScope = PluginExecutionContext.BeginScope(
                pluginId, 
                action, 
                targetProcessName: context.TargetProcessName
            );

            // 设置权限上下文
            context.CurrentPluginId = pluginId;
            context.PermissionInterceptor = _permissionInterceptor;

            var stopwatch = Stopwatch.StartNew();
            bool success = false;
            Exception? exception = null;

            try
            {
                _logger.LogDebug("[PluginRegistryV2] Executing plugin action");
                var result = await host.ExecuteAsync(action, args, context);
                
                success = result.Success;

                if (result.Success)
                {
                    _breakerPolicy.RecordSuccess(descriptor, pluginId);
                    _logger.LogInformation("[PluginRegistryV2] Plugin execution succeeded: {Message}", 
                        result.Message ?? "OK");
                    host.SetRuntimeState(PluginState.Enabled);
                    _healthMonitor?.RecordSuccess(pluginId);
                }
                else
                {
                    host.SetRuntimeState(PluginState.Enabled);
                    _logger.LogWarning("[PluginRegistryV2] Plugin execution failed: {Message}", 
                        result.Message ?? "Unknown error");
                    
                    // 处理 Critical 错误
                    if (tier == PluginTier.Extension && result.Severity == PluginErrorSeverity.Critical)
                    {
                        _logger.LogWarning("[PluginRegistryV2] Critical error detected");
                        _breakerPolicy.RecordFailure(descriptor, pluginId, new InvalidOperationException(result.Message ?? "Critical error"));
                    }
                }
                
                return result;
            }
            catch (Exception ex)
            {
                exception = ex;
                success = false;
                _logger.LogError(ex, "[PluginRegistryV2] Plugin execution threw exception");

                // Extension 插件触发熔断器
                if (tier == PluginTier.Extension)
                {
                    _breakerPolicy.RecordFailure(descriptor, pluginId, ex);
                }

                host.SetRuntimeState(PluginState.Faulted, ex);

                return PluginResult.Error($"Plugin execution failed: {ex.Message}");
            }
            finally
            {
                stopwatch.Stop();

                // 记录统计
                _usageTracker?.RecordExecution(pluginId, success, stopwatch.ElapsedMilliseconds, context.TargetProcessName);

                if (exception != null)
                {
                    _healthMonitor?.RecordError(pluginId, exception, action);
                }
            }
        }

        /// <summary>
        /// 获取插件宿主
        /// </summary>
        public PluginHost? GetPluginHost(string pluginId)
        {
            _hosts.TryGetValue(pluginId, out var host);
            return host;
        }

        /// <summary>
        /// 获取所有已加载的插件
        /// </summary>
        public IEnumerable<PluginHost> GetAllPluginHosts()
        {
            return _hosts.Values;
        }

        /// <summary>
        /// 获取插件数量
        /// </summary>
        public int Count => _hosts.Count;

        /// <summary>
        /// 检查插件是否已加载
        /// </summary>
        public bool IsPluginLoaded(string pluginId)
        {
            return _hosts.ContainsKey(pluginId) && _hosts[pluginId].IsAlive;
        }

        /// <summary>
        /// 卸载所有插件
        /// </summary>
        public async Task UnloadAllAsync()
        {
            _logger.LogInformation("[PluginRegistryV2] Unloading all plugins...");
            
            var tasks = _hosts.Values.Select(host => host.UnloadAsync()).ToList();
            await Task.WhenAll(tasks);
            
            _hosts.Clear();
            
            _logger.LogInformation("[PluginRegistryV2] All plugins unloaded");
        }

        /// <summary>
        /// 初始化配置服务引用
        /// </summary>
        public void InitializeConfigService()
        {
            _configService = _serviceProvider.GetService(typeof(Services.Interfaces.IConfigService)) 
                as Services.Interfaces.IConfigService;
        }

        /// <summary>
        /// 启用热重载
        /// </summary>
        public void EnableHotReload(string pluginDirectory)
        {
            if (_hotReloadManager != null)
            {
                _logger.LogWarning("[PluginRegistryV2] Hot reload is already enabled");
                return;
            }

            _logger.LogInformation("[PluginRegistryV2] Enabling hot reload for directory: {PluginDirectory}", pluginDirectory);

            _hotReloadManager = new HotReloadManager(pluginDirectory, _serviceProvider.GetService(typeof(ILogger<HotReloadManager>)) as ILogger<HotReloadManager>);
            
            // 注册所有已加载的插件
            foreach (var host in _hosts.Values)
            {
                var pluginPath = GetPluginPath(host.PluginId);
                if (!string.IsNullOrEmpty(pluginPath))
                {
                    _hotReloadManager.RegisterPlugin(host.PluginId, pluginPath);
                }
            }

            // 订阅文件变更事件
            _hotReloadManager.PluginFileChanged += OnPluginFileChanged;

            // 启用监听
            _hotReloadManager.Enable();

            _logger.LogInformation("[PluginRegistryV2] ✓ Hot reload enabled");
        }

        /// <summary>
        /// 禁用热重载
        /// </summary>
        public void DisableHotReload()
        {
            if (_hotReloadManager == null)
            {
                _logger.LogWarning("[PluginRegistryV2] Hot reload is not enabled");
                return;
            }

            _logger.LogInformation("[PluginRegistryV2] Disabling hot reload...");

            _hotReloadManager.PluginFileChanged -= OnPluginFileChanged;
            _hotReloadManager.Disable();
            _hotReloadManager.Dispose();
            _hotReloadManager = null;

            _logger.LogInformation("[PluginRegistryV2] ✓ Hot reload disabled");
        }

        /// <summary>
        /// 处理插件文件变更事件
        /// </summary>
        private async void OnPluginFileChanged(object? sender, PluginFileChangedEventArgs e)
        {
            try
            {
                _logger.LogInformation("[PluginRegistryV2] Plugin file changed: {FilePath}", e.FilePath);

                // 如果有插件 ID，尝试重载
                if (!string.IsNullOrEmpty(e.PluginId))
                {
                    _logger.LogInformation("[PluginRegistryV2] Auto-reloading plugin: {PluginId}", e.PluginId);

                    // 创建 Shadow Copy
                    var shadowPath = _hotReloadManager?.CreateShadowCopy(e.FilePath);
                    if (string.IsNullOrEmpty(shadowPath))
                    {
                        _logger.LogError("[PluginRegistryV2] Failed to create shadow copy for {FilePath}", e.FilePath);
                        return;
                    }

                    // 重载插件
                    var success = await ReloadPluginAsync(e.PluginId, shadowPath);

                    // 清理旧的 Shadow Copy
                    _hotReloadManager?.CleanupOldShadowCopies(System.IO.Path.GetFileName(e.FilePath));

                    // 通知用户
                    if (success)
                    {
                        _trayService?.ShowNotification(
                            "插件已重载",
                            $"插件 '{e.PluginId}' 已自动重载。",
                            PulsarNotificationIcon.Info
                        );

                        _logger.LogInformation("[PluginRegistryV2] ✓ Plugin auto-reloaded: {PluginId}", e.PluginId);
                    }
                    else
                    {
                        _trayService?.ShowNotification(
                            "插件重载失败",
                            $"插件 '{e.PluginId}' 重载失败，请查看日志。",
                            PulsarNotificationIcon.Warning
                        );

                        _logger.LogError("[PluginRegistryV2] Failed to auto-reload plugin: {PluginId}", e.PluginId);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[PluginRegistryV2] Error handling plugin file change: {FilePath}", e.FilePath);
            }
        }

        private PluginTier GetTier(PluginHost host)
        {
            var plugin = host.GetPluginInstance();
            if (plugin is IPluginTiered tiered)
            {
                return tiered.Tier;
            }

            return plugin?.CanDisable == false ? PluginTier.Core : PluginTier.Extension;
        }

        private string? GetPluginPath(string pluginId)
        {
            // TODO: 从配置或缓存中获取插件路径
            // 临时实现：假设插件在标准目录
            var pluginDir = System.IO.Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory, 
                "Plugins", 
                pluginId
            );
            
            var dllPath = System.IO.Path.Combine(pluginDir, $"{pluginId}.dll");
            return System.IO.File.Exists(dllPath) ? dllPath : null;
        }
    }
}
