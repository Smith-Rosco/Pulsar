// [Path]: Pulsar/Pulsar/Logging/LoggingScopeEnricher.cs

using System;
using System.Collections.Generic;
using System.Linq;
using Serilog.Core;
using Serilog.Events;

namespace Pulsar.Logging
{
    /// <summary>
    /// Serilog Enricher - 支持 ILogger.BeginScope() 的作用域上下文
    /// 将 Microsoft.Extensions.Logging 的 Scope 数据自动添加到 Serilog 日志事件
    /// 
    /// 使用示例:
    /// using (_logger.BeginScope(new Dictionary&lt;string, object&gt; { ["UserId"] = 123, ["Operation"] = "Export" }))
    /// {
    ///     _logger.LogInformation("Processing data"); // 自动包含 UserId 和 Operation
    /// }
    /// </summary>
    public class LoggingScopeEnricher : ILogEventEnricher
    {
        private const string ScopePropertyPrefix = "Scope_";

        public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
        {
            // 从 LogContext 中提取 Scope 数据
            // Microsoft.Extensions.Logging 的 Scope 会通过 Serilog.Extensions.Logging 桥接到 LogContext
            
            // 检查是否有 Scope 属性
            if (logEvent.Properties.TryGetValue("Scope", out var scopeProperty))
            {
                if (scopeProperty is SequenceValue sequenceValue)
                {
                    // Scope 是一个数组，包含所有嵌套的 Scope
                    var scopeIndex = 0;
                    foreach (var scopeItem in sequenceValue.Elements)
                    {
                        EnrichFromScopeItem(logEvent, propertyFactory, scopeItem, scopeIndex++);
                    }
                }
                else if (scopeProperty is StructureValue structureValue)
                {
                    // 单个 Scope 对象
                    EnrichFromScopeItem(logEvent, propertyFactory, scopeProperty, 0);
                }
            }
        }

        /// <summary>
        /// 从单个 Scope 项提取属性
        /// </summary>
        private void EnrichFromScopeItem(LogEvent logEvent, ILogEventPropertyFactory propertyFactory, LogEventPropertyValue scopeItem, int scopeIndex)
        {
            if (scopeItem is StructureValue structureValue)
            {
                // 提取结构化 Scope 的所有属性
                foreach (var property in structureValue.Properties)
                {
                    var enrichedKey = $"{ScopePropertyPrefix}{property.Name}";
                    logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty(enrichedKey, property.Value));
                }
            }
            else if (scopeItem is ScalarValue scalarValue)
            {
                // 简单值 Scope (如字符串)
                var enrichedKey = $"{ScopePropertyPrefix}Value_{scopeIndex}";
                logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty(enrichedKey, scalarValue.Value));
            }
            else if (scopeItem is DictionaryValue dictionaryValue)
            {
                // 字典类型 Scope
                foreach (var kvp in dictionaryValue.Elements)
                {
                    if (kvp.Key is ScalarValue keyScalar && keyScalar.Value is string keyString)
                    {
                        var enrichedKey = $"{ScopePropertyPrefix}{keyString}";
                        logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty(enrichedKey, kvp.Value));
                    }
                }
            }
        }
    }

    /// <summary>
    /// 插件执行作用域辅助类
    /// 简化插件日志的 Scope 使用
    /// </summary>
    public static class PluginLoggingScope
    {
    /// <summary>
    /// 创建插件执行作用域
    /// </summary>
    /// <param name="logger">日志记录器</param>
    /// <param name="pluginId">插件 ID</param>
    /// <param name="action">动作名称</param>
    /// <param name="executionId">执行 ID（可选，自动生成）</param>
    /// <returns>作用域 Disposable</returns>
    public static IDisposable? BeginPluginScope(
        this Microsoft.Extensions.Logging.ILogger logger,
        string pluginId,
        string action,
        string? executionId = null)
    {
        var scopeData = new Dictionary<string, object>
        {
            ["PluginId"] = pluginId,
            ["Action"] = action,
            ["ExecutionId"] = executionId ?? Guid.NewGuid().ToString("N").Substring(0, 8),
            ["StartTime"] = DateTime.UtcNow
        };

        return logger.BeginScope(scopeData);
    }

    /// <summary>
    /// 创建操作作用域（用于跟踪长时间操作）
    /// </summary>
    public static IDisposable? BeginOperationScope(
        this Microsoft.Extensions.Logging.ILogger logger,
        string operationName,
        Dictionary<string, object>? additionalData = null)
    {
        var scopeData = new Dictionary<string, object>
        {
            ["Operation"] = operationName,
            ["OperationId"] = Guid.NewGuid().ToString("N").Substring(0, 8),
            ["StartTime"] = DateTime.UtcNow
        };

        if (additionalData != null)
        {
            foreach (var kvp in additionalData)
            {
                scopeData[kvp.Key] = kvp.Value;
            }
        }

        return logger.BeginScope(scopeData);
    }

    /// <summary>
    /// 创建用户操作作用域
    /// </summary>
    public static IDisposable? BeginUserActionScope(
        this Microsoft.Extensions.Logging.ILogger logger,
        string actionName,
        string? targetProcess = null)
    {
        var scopeData = new Dictionary<string, object>
        {
            ["UserAction"] = actionName,
            ["ActionId"] = Guid.NewGuid().ToString("N").Substring(0, 8),
            ["Timestamp"] = DateTime.UtcNow
        };

        if (!string.IsNullOrEmpty(targetProcess))
        {
            scopeData["TargetProcess"] = targetProcess;
        }

        return logger.BeginScope(scopeData);
    }
    }
}
