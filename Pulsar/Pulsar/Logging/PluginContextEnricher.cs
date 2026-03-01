// [Path]: Pulsar/Pulsar/Logging/PluginContextEnricher.cs

using Serilog.Core;
using Serilog.Events;
using Pulsar.Core.Plugin;

namespace Pulsar.Logging
{
    /// <summary>
    /// Serilog Enricher - 自动将插件执行上下文添加到日志事件
    /// </summary>
    public class PluginContextEnricher : ILogEventEnricher
    {
        public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
        {
            var context = PluginExecutionContext.Current;
            if (context == null)
                return;

            // 添加插件 ID
            logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("PluginId", context.PluginId));

            // 添加动作名称
            logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("Action", context.Action));

            // 添加执行 ID（用于关联同一次执行的所有日志）
            logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("ExecutionId", context.ExecutionId));

            // 添加目标进程名称（如果有）
            if (!string.IsNullOrEmpty(context.TargetProcessName))
            {
                logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("TargetProcess", context.TargetProcessName));
            }

            // 添加执行时长（毫秒）
            var elapsedMs = (DateTime.UtcNow - context.StartTimeUtc).TotalMilliseconds;
            logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("ElapsedMs", (long)elapsedMs));
        }
    }
}
