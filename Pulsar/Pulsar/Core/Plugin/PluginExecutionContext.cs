// [Path]: Pulsar/Pulsar/Core/Plugin/PluginExecutionContext.cs

using System;
using System.Threading;

namespace Pulsar.Core.Plugin
{
    /// <summary>
    /// 插件执行上下文 - 使用 AsyncLocal 在异步调用链中传递插件元数据
    /// 用于 Serilog Enricher 自动标记日志
    /// </summary>
    public sealed class PluginExecutionContext : IDisposable
    {
        private static readonly AsyncLocal<PluginExecutionContext?> _current = new();

        /// <summary>
        /// 获取当前执行上下文（如果存在）
        /// </summary>
        public static PluginExecutionContext? Current => _current.Value;

        /// <summary>
        /// 插件唯一标识符
        /// </summary>
        public string PluginId { get; }

        /// <summary>
        /// 执行的动作名称
        /// </summary>
        public string Action { get; }

        /// <summary>
        /// 执行唯一 ID（用于关联同一次执行的所有日志）
        /// </summary>
        public Guid ExecutionId { get; }

        /// <summary>
        /// 执行开始时间（UTC）
        /// </summary>
        public DateTime StartTimeUtc { get; }

        /// <summary>
        /// 目标进程名称（可选）
        /// </summary>
        public string? TargetProcessName { get; }

        private readonly PluginExecutionContext? _previous;

        private PluginExecutionContext(
            string pluginId,
            string action,
            Guid executionId,
            string? targetProcessName,
            PluginExecutionContext? previous)
        {
            PluginId = pluginId;
            Action = action;
            ExecutionId = executionId;
            StartTimeUtc = DateTime.UtcNow;
            TargetProcessName = targetProcessName;
            _previous = previous;
        }

        /// <summary>
        /// 开始一个新的插件执行上下文作用域
        /// </summary>
        public static PluginExecutionContext BeginScope(
            string pluginId,
            string action,
            Guid? executionId = null,
            string? targetProcessName = null)
        {
            var previous = _current.Value;
            var context = new PluginExecutionContext(
                pluginId,
                action,
                executionId ?? Guid.NewGuid(),
                targetProcessName,
                previous
            );

            _current.Value = context;
            return context;
        }

        /// <summary>
        /// 释放上下文作用域。AsyncLocal 作用域是栈式的：释放当前作用域会恢复
        /// 进入该作用域之前的值，因此嵌套插件执行不会丢失外层关联信息。
        /// </summary>
        public void Dispose()
        {
            _current.Value = _previous;
        }
    }
}
