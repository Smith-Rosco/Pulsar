using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Pulsar.Core.Plugin.Runtime;

namespace Pulsar.Core.Plugin
{
    /// <summary>
    /// 插件宿主 - 管理单个插件的完整生命周期
    /// 
    /// 核心职责:
    /// 1. 隔离加载 - 使用 UnloadablePluginContext 隔离插件程序集
    /// 2. 生命周期管理 - 加载、运行、卸载
    /// 3. 弱引用追踪 - 允许 GC 回收已卸载的插件
    /// 4. 异常隔离 - 插件崩溃不影响主程序
    /// </summary>
    public class PluginHost : IDisposable
    {
        private WeakReference<UnloadablePluginContext>? _contextRef;
        private WeakReference<IPulsarPlugin>? _pluginRef;
        private readonly IServiceProvider _services;
        private readonly ILogger? _logger;
        private readonly string _pluginPath;
        
        // 插件元数据（即使插件卸载后仍可访问）
        public string PluginId { get; private set; }
        public string DisplayName { get; private set; }
        public string Version { get; private set; }
        public string Author { get; private set; }
        public PluginState State { get; private set; }
        public DateTime LoadedAt { get; private set; }
        public DateTime? UnloadedAt { get; private set; }
        public Exception? LastError { get; private set; }
        
        /// <summary>
        /// 插件是否仍然存活（未被 GC 回收）
        /// </summary>
        public bool IsAlive => 
            _contextRef?.TryGetTarget(out _) == true && 
            _pluginRef?.TryGetTarget(out _) == true;

        /// <summary>
        /// 插件是否可以执行
        /// </summary>
        public bool CanExecute => 
            (State == PluginState.Loaded || State == PluginState.Enabled) && 
            IsAlive;

        public PluginHost(string pluginPath, IServiceProvider services, ILogger? logger = null)
        {
            _pluginPath = pluginPath ?? throw new ArgumentNullException(nameof(pluginPath));
            _services = services ?? throw new ArgumentNullException(nameof(services));
            _logger = logger;
            
            PluginId = string.Empty;
            DisplayName = string.Empty;
            Version = string.Empty;
            Author = string.Empty;
            State = PluginState.Unloaded;
        }

        /// <summary>
        /// 加载插件
        /// </summary>
        public async Task LoadAsync()
        {
            if (State != PluginState.Unloaded)
            {
                throw new InvalidOperationException($"Plugin is already in state: {State}");
            }

            State = PluginState.Loading;
            LoadedAt = DateTime.UtcNow;

            try
            {
                _logger?.LogInformation("[PluginHost] Loading plugin from {PluginPath}", _pluginPath);

                // 1. 创建可卸载上下文
                var context = new UnloadablePluginContext(_pluginPath, _logger);
                _contextRef = new WeakReference<UnloadablePluginContext>(context, trackResurrection: false);

                // 2. 加载插件程序集
                var assembly = context.LoadFromAssemblyPath(_pluginPath);
                _logger?.LogDebug("[PluginHost] Loaded assembly: {AssemblyName}", assembly.FullName);

                // 3. 查找实现 IPulsarPlugin 的类型
                var pluginType = assembly.GetTypes()
                    .FirstOrDefault(t => 
                        typeof(IPulsarPlugin).IsAssignableFrom(t) && 
                        !t.IsInterface && 
                        !t.IsAbstract);

                if (pluginType == null)
                {
                    throw new InvalidOperationException(
                        $"No type implementing IPulsarPlugin found in {_pluginPath}");
                }

                _logger?.LogDebug("[PluginHost] Found plugin type: {TypeName}", pluginType.FullName);

                // 4. 实例化插件
                var plugin = (IPulsarPlugin)Activator.CreateInstance(pluginType)!;
                _pluginRef = new WeakReference<IPulsarPlugin>(plugin, trackResurrection: false);

                // 5. 缓存元数据
                PluginId = plugin.Id;
                DisplayName = plugin.DisplayName;
                Version = plugin.Version;
                Author = plugin.Author;

                // 6. 初始化插件
                _logger?.LogDebug("[PluginHost] Initializing plugin: {PluginId}", PluginId);
                plugin.Initialize(_services);

                State = PluginState.Loaded;
                _logger?.LogInformation("[PluginHost] ✓ Successfully loaded plugin: {PluginId} v{Version}", 
                    PluginId, Version);
            }
            catch (Exception ex)
            {
                State = PluginState.Faulted;
                LastError = ex;
                _logger?.LogError(ex, "[PluginHost] Failed to load plugin from {PluginPath}", _pluginPath);
                throw;
            }
        }

        /// <summary>
        /// 执行插件动作
        /// </summary>
        public async Task<PluginResult> ExecuteAsync(
            string action, 
            IReadOnlyDictionary<string, string> args, 
            PulsarContext context)
        {
            if (!CanExecute)
            {
                return PluginResult.Error($"Plugin is not in executable state: {State}");
            }

            if (!_pluginRef!.TryGetTarget(out var plugin))
            {
                State = PluginState.Faulted;
                return PluginResult.Error("Plugin has been garbage collected");
            }

            var previousState = State;
            State = PluginState.Running;

            try
            {
                _logger?.LogDebug("[PluginHost] Executing {PluginId}.{Action}", PluginId, action);
                var result = await plugin.ExecuteAsync(action, args, context);
                
                State = PluginState.Enabled;
                return result;
            }
            catch (Exception ex)
            {
                State = PluginState.Faulted;
                LastError = ex;
                _logger?.LogError(ex, "[PluginHost] Plugin execution failed: {PluginId}.{Action}", 
                    PluginId, action);
                throw;
            }
        }

        /// <summary>
        /// 卸载插件
        /// </summary>
        public async Task UnloadAsync()
        {
            if (State == PluginState.Unloaded || State == PluginState.Unloading)
            {
                _logger?.LogWarning("[PluginHost] Plugin already unloading/unloaded: {PluginId}", PluginId);
                return;
            }

            State = PluginState.Unloading;
            UnloadedAt = DateTime.UtcNow;

            try
            {
                _logger?.LogInformation("[PluginHost] Unloading plugin: {PluginId}", PluginId);

                // 1. 调用生命周期钩子
                if (_pluginRef?.TryGetTarget(out var plugin) == true)
                {
                    if (plugin is IPluginLifecycle lifecycle)
                    {
                        try
                        {
                            await lifecycle.OnUnloadAsync();
                            _logger?.LogDebug("[PluginHost] Called OnUnloadAsync for {PluginId}", PluginId);
                        }
                        catch (Exception ex)
                        {
                            _logger?.LogWarning(ex, "[PluginHost] OnUnloadAsync failed for {PluginId}", PluginId);
                        }
                    }
                }

                // 2. 清除插件引用
                _pluginRef?.SetTarget(null!);
                _pluginRef = null;

                // 3. 卸载上下文
                if (_contextRef?.TryGetTarget(out var context) == true)
                {
                    context.UnloadContext();
                }

                _contextRef = null;

                // 4. 强制 GC 回收（多次尝试确保回收）
                for (int i = 0; i < 3; i++)
                {
                    GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced);
                    GC.WaitForPendingFinalizers();
                }

                State = PluginState.Unloaded;
                _logger?.LogInformation("[PluginHost] ✓ Successfully unloaded plugin: {PluginId}", PluginId);
            }
            catch (Exception ex)
            {
                State = PluginState.Faulted;
                LastError = ex;
                _logger?.LogError(ex, "[PluginHost] Failed to unload plugin: {PluginId}", PluginId);
                throw;
            }
        }

        /// <summary>
        /// 获取插件实例（用于诊断，不推荐在生产代码中使用）
        /// </summary>
        public IPulsarPlugin? GetPluginInstance()
        {
            if (_pluginRef?.TryGetTarget(out var plugin) == true)
            {
                return plugin;
            }
            return null;
        }

        public void SetRuntimeState(PluginState state, Exception? error = null)
        {
            State = state;
            LastError = error;
        }

        /// <summary>
        /// 获取加载上下文（用于诊断）
        /// </summary>
        public UnloadablePluginContext? GetLoadContext()
        {
            if (_contextRef?.TryGetTarget(out var context) == true)
            {
                return context;
            }
            return null;
        }

        public void Dispose()
        {
            if (State != PluginState.Unloaded)
            {
                UnloadAsync().GetAwaiter().GetResult();
            }
        }
    }
}
