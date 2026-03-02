using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Loader;

namespace Pulsar.Core.Plugin
{
    /// <summary>
    /// 插件隔离加载上下文 - 解决依赖冲突 (DLL Hell)
    /// </summary>
    public class PluginLoadContext : AssemblyLoadContext
    {
        private readonly AssemblyDependencyResolver _resolver;
        private readonly Dictionary<string, string>? _shimMap;

        public PluginLoadContext(string pluginPath, Dictionary<string, string>? shimMap = null) : base(isCollectible: true)
        {
            _resolver = new AssemblyDependencyResolver(pluginPath);
            _shimMap = shimMap;
        }

        protected override Assembly? Load(AssemblyName assemblyName)
        {
            // [关键策略] 共享契约隔离
            // 如果请求加载的是 Pulsar 主程序集 (包含 IPulsarPlugin 接口定义)，
            // 则返回 null，强制 CLR 回退到 Default Context 加载。
            // 这确保了 Plugin.IPulsarPlugin 与 Host.IPulsarPlugin 是同一个类型。
            if (assemblyName.Name == "Pulsar")
            {
                return null; 
            }

            // [新增] 检查是否存在 Shim 程序集
            if (_shimMap != null && assemblyName.Name != null && assemblyName.Version != null)
            {
                var shimKey = $"{assemblyName.Name}@{assemblyName.Version}";
                if (_shimMap.TryGetValue(shimKey, out var shimPath))
                {
                    try
                    {
                        return LoadFromAssemblyPath(shimPath);
                    }
                    catch
                    {
                        // Shim 加载失败，继续正常流程
                    }
                }
            }

            // 1. 尝试从插件目录解析依赖 (.deps.json 或本地 DLL)
            string? assemblyPath = _resolver.ResolveAssemblyToPath(assemblyName);
            if (assemblyPath != null)
            {
                return LoadFromAssemblyPath(assemblyPath);
            }

            // 2. 都不行，返回 null，尝试在 Default Context 找 (如 System.*)
            return null;
        }

        protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
        {
            string? libraryPath = _resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
            if (libraryPath != null)
            {
                return LoadUnmanagedDllFromPath(libraryPath);
            }
            return IntPtr.Zero;
        }
    }
}
