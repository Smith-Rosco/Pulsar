// [Path]: Pulsar/Pulsar/Core/Plugin/PluginFactory.cs

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Pulsar.Core.Plugin
{
    /// <summary>
    /// 插件工厂 - 支持构造函数依赖注入
    /// 
    /// 职责:
    /// 1. 通过反射创建插件实例
    /// 2. 自动解析构造函数参数
    /// 3. 支持可选依赖 (nullable 参数)
    /// 4. 提供详细的错误诊断
    /// 
    /// 使用场景:
    /// - PluginLoader 使用此工厂创建插件实例
    /// - 支持从 Service Locator 迁移到构造函数注入
    /// </summary>
    public class PluginFactory
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<PluginFactory>? _logger;

        public PluginFactory(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _logger = serviceProvider.GetService<ILogger<PluginFactory>>();
        }

        /// <summary>
        /// 创建插件实例 (支持构造函数注入)
        /// </summary>
        /// <param name="pluginType">插件类型</param>
        /// <returns>插件实例</returns>
        /// <exception cref="PluginInstantiationException">实例化失败时抛出</exception>
        public IPulsarPlugin CreatePlugin(Type pluginType)
        {
            if (pluginType == null)
                throw new ArgumentNullException(nameof(pluginType));

            if (!typeof(IPulsarPlugin).IsAssignableFrom(pluginType))
                throw new ArgumentException($"Type {pluginType.Name} does not implement IPulsarPlugin", nameof(pluginType));

            if (pluginType.IsAbstract || pluginType.IsInterface)
                throw new ArgumentException($"Cannot instantiate abstract type or interface: {pluginType.Name}", nameof(pluginType));

            try
            {
                // 1. 尝试使用 DI 容器创建 (推荐方式)
                var instance = ActivatorUtilities.CreateInstance(_serviceProvider, pluginType) as IPulsarPlugin;
                if (instance != null)
                {
                    _logger?.LogDebug("[PluginFactory] Created plugin {PluginType} via DI container", pluginType.Name);
                    return instance;
                }

                // 2. Fallback: 手动解析构造函数参数
                _logger?.LogDebug("[PluginFactory] Falling back to manual constructor resolution for {PluginType}", pluginType.Name);
                return CreatePluginManually(pluginType);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "[PluginFactory] Failed to create plugin {PluginType}", pluginType.Name);
                throw new PluginInstantiationException(
                    $"Failed to instantiate plugin {pluginType.Name}. See inner exception for details.",
                    pluginType,
                    ex
                );
            }
        }

        /// <summary>
        /// 手动解析构造函数参数并创建实例
        /// </summary>
        private IPulsarPlugin CreatePluginManually(Type pluginType)
        {
            // 获取所有公共构造函数，按参数数量降序排列 (优先使用参数最多的构造函数)
            var constructors = pluginType.GetConstructors(BindingFlags.Public | BindingFlags.Instance)
                .OrderByDescending(c => c.GetParameters().Length)
                .ToArray();

            if (constructors.Length == 0)
            {
                throw new PluginInstantiationException(
                    $"Plugin {pluginType.Name} has no public constructors",
                    pluginType
                );
            }

            // 尝试每个构造函数，直到成功
            List<string> errors = new List<string>();

            foreach (var constructor in constructors)
            {
                try
                {
                    var parameters = ResolveConstructorParameters(constructor);
                    var instance = constructor.Invoke(parameters) as IPulsarPlugin;

                    if (instance != null)
                    {
                        _logger?.LogDebug("[PluginFactory] Successfully created {PluginType} using constructor with {ParamCount} parameters",
                            pluginType.Name, parameters.Length);
                        return instance;
                    }
                }
                catch (Exception ex)
                {
                    var paramTypes = string.Join(", ", constructor.GetParameters().Select(p => p.ParameterType.Name));
                    errors.Add($"Constructor({paramTypes}): {ex.Message}");
                }
            }

            // 所有构造函数都失败
            throw new PluginInstantiationException(
                $"Failed to invoke any constructor for plugin {pluginType.Name}. Tried {constructors.Length} constructors:\n" +
                string.Join("\n", errors),
                pluginType
            );
        }

        /// <summary>
        /// 解析构造函数参数
        /// </summary>
        private object?[] ResolveConstructorParameters(ConstructorInfo constructor)
        {
            var parameters = constructor.GetParameters();
            var resolvedParams = new object?[parameters.Length];

            for (int i = 0; i < parameters.Length; i++)
            {
                var param = parameters[i];
                var paramType = param.ParameterType;

                // 尝试从 DI 容器解析
                var service = _serviceProvider.GetService(paramType);

                if (service != null)
                {
                    resolvedParams[i] = service;
                }
                else if (IsNullable(param))
                {
                    // 可选参数 (nullable 或有默认值)
                    resolvedParams[i] = param.HasDefaultValue ? param.DefaultValue : null;
                    _logger?.LogDebug("[PluginFactory] Parameter {ParamName} ({ParamType}) is optional, using null/default",
                        param.Name, paramType.Name);
                }
                else
                {
                    // 必需参数但无法解析
                    throw new InvalidOperationException(
                        $"Cannot resolve required parameter '{param.Name}' of type '{paramType.Name}'. " +
                        $"Ensure the service is registered in the DI container."
                    );
                }
            }

            return resolvedParams;
        }

        /// <summary>
        /// 检查参数是否可为空
        /// </summary>
        private bool IsNullable(ParameterInfo param)
        {
            // 1. 有默认值
            if (param.HasDefaultValue)
                return true;

            // 2. Nullable<T> 类型
            if (Nullable.GetUnderlyingType(param.ParameterType) != null)
                return true;

            // 3. 引用类型 + Nullable 注解
            if (!param.ParameterType.IsValueType)
            {
                // C# 8.0+ Nullable Reference Types
                var nullableContext = new NullabilityInfoContext();
                var nullability = nullableContext.Create(param);
                return nullability.WriteState == NullabilityState.Nullable;
            }

            return false;
        }
    }

    /// <summary>
    /// 插件实例化异常
    /// </summary>
    public class PluginInstantiationException : Exception
    {
        public Type PluginType { get; }

        public PluginInstantiationException(string message, Type pluginType)
            : base(message)
        {
            PluginType = pluginType;
        }

        public PluginInstantiationException(string message, Type pluginType, Exception innerException)
            : base(message, innerException)
        {
            PluginType = pluginType;
        }
    }
}
