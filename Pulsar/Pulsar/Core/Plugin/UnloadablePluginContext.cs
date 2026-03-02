using System;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using Microsoft.Extensions.Logging;

namespace Pulsar.Core.Plugin
{
    /// <summary>
    /// 可卸载的插件加载上下文 - 支持运行时热插拔
    /// 
    /// 关键特性:
    /// 1. isCollectible: true - 允许 GC 回收
    /// 2. 共享程序集白名单 - 避免重复加载核心 API
    /// 3. 依赖隔离 - 每个插件使用独立的依赖版本
    /// 4. 卸载追踪 - WeakReference 监控生命周期
    /// </summary>
    public class UnloadablePluginContext : AssemblyLoadContext
    {
        private readonly AssemblyDependencyResolver _resolver;
        private readonly ILogger? _logger;
        private readonly string _pluginPath;
        
        // 共享程序集白名单 - 这些程序集从默认上下文加载，避免类型不兼容
        private static readonly string[] SharedAssemblyPrefixes = new[]
        {
            "Pulsar",                    // 主程序集（包含插件接口）
            "Pulsar.Core.Plugin",        // 插件核心 API
            "System.",                   // .NET BCL
            "Microsoft.Extensions.",     // DI/Logging/Configuration
            "netstandard",               // .NET Standard
            "mscorlib"                   // 核心库
        };

        public string PluginPath => _pluginPath;
        public DateTime LoadedAt { get; }
        public bool IsUnloading { get; private set; }

        public UnloadablePluginContext(string pluginPath, ILogger? logger = null) 
            : base(name: $"Plugin_{System.IO.Path.GetFileNameWithoutExtension(pluginPath)}_{Guid.NewGuid():N}", isCollectible: true)
        {
            _pluginPath = pluginPath;
            _resolver = new AssemblyDependencyResolver(pluginPath);
            _logger = logger;
            LoadedAt = DateTime.UtcNow;
            
            _logger?.LogDebug("[UnloadablePluginContext] Created context for {PluginPath}", pluginPath);
        }

        protected override Assembly? Load(AssemblyName assemblyName)
        {
            // 策略 1: 共享程序集白名单 - 从默认上下文加载
            if (IsSharedAssembly(assemblyName))
            {
                _logger?.LogTrace("[UnloadablePluginContext] Shared assembly: {AssemblyName}", assemblyName.Name);
                return null; // 返回 null 让 CLR 从默认上下文加载
            }

            // 策略 2: 插件本地依赖 - 从插件目录加载
            string? assemblyPath = _resolver.ResolveAssemblyToPath(assemblyName);
            if (assemblyPath != null)
            {
                _logger?.LogTrace("[UnloadablePluginContext] Loading local dependency: {AssemblyName} from {Path}", 
                    assemblyName.Name, assemblyPath);
                return LoadFromAssemblyPath(assemblyPath);
            }

            // 策略 3: 其他情况 - 尝试默认上下文
            _logger?.LogTrace("[UnloadablePluginContext] Fallback to default context: {AssemblyName}", assemblyName.Name);
            return null;
        }

        protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
        {
            // 解析非托管 DLL（如 native libraries）
            string? libraryPath = _resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
            if (libraryPath != null)
            {
                _logger?.LogTrace("[UnloadablePluginContext] Loading unmanaged DLL: {DllName} from {Path}", 
                    unmanagedDllName, libraryPath);
                return LoadUnmanagedDllFromPath(libraryPath);
            }
            
            return IntPtr.Zero;
        }

        /// <summary>
        /// 判断程序集是否应该从默认上下文加载（共享）
        /// </summary>
        private bool IsSharedAssembly(AssemblyName assemblyName)
        {
            if (string.IsNullOrEmpty(assemblyName.Name))
                return false;

            return SharedAssemblyPrefixes.Any(prefix => 
                assemblyName.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// 卸载上下文（触发 GC 回收）
        /// </summary>
        public void UnloadContext()
        {
            if (IsUnloading)
            {
                _logger?.LogWarning("[UnloadablePluginContext] Context already unloading: {Name}", Name);
                return;
            }

            IsUnloading = true;
            _logger?.LogInformation("[UnloadablePluginContext] Unloading context: {Name}", Name);
            
            try
            {
                Unload();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "[UnloadablePluginContext] Error during unload: {Name}", Name);
                throw;
            }
        }

        /// <summary>
        /// 获取上下文中已加载的程序集列表（用于诊断）
        /// </summary>
        public Assembly[] GetLoadedAssemblies()
        {
            return Assemblies.ToArray();
        }
    }
}
