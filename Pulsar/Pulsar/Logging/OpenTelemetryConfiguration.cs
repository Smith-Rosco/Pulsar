// [Path]: Pulsar/Pulsar/Logging/OpenTelemetryConfiguration.cs

using System;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using OpenTelemetry.Metrics;

namespace Pulsar.Logging
{
    /// <summary>
    /// OpenTelemetry 配置和工具类
    /// 提供分布式追踪、性能监控和自定义指标
    /// 
    /// 功能：
    /// 1. 分布式追踪：跟踪插件执行流程和性能瓶颈
    /// 2. 自定义指标：插件执行次数、成功率、执行时长等
    /// 3. 运行时监控：CPU、内存、GC 等系统指标
    /// 4. 导出支持：Console、OTLP (Jaeger/Prometheus)
    /// </summary>
    public static class OpenTelemetryConfiguration
    {
        // ActivitySource 用于创建 Span（追踪单元）
        public static readonly ActivitySource ActivitySource = new("Pulsar", "2.4.0");

        // Meter 用于创建自定义指标
        public static readonly Meter Meter = new("Pulsar", "2.4.0");

        // 自定义指标
        public static readonly Counter<long> PluginExecutionCounter = Meter.CreateCounter<long>(
            "pulsar.plugin.executions",
            description: "Total number of plugin executions");

        public static readonly Histogram<double> PluginExecutionDuration = Meter.CreateHistogram<double>(
            "pulsar.plugin.duration",
            unit: "ms",
            description: "Plugin execution duration in milliseconds");

        public static readonly Counter<long> PluginErrorCounter = Meter.CreateCounter<long>(
            "pulsar.plugin.errors",
            description: "Total number of plugin execution errors");

        public static readonly Histogram<double> RadialMenuOpenDuration = Meter.CreateHistogram<double>(
            "pulsar.radialmenu.open_duration",
            unit: "ms",
            description: "Radial menu open duration in milliseconds");

        public static readonly Counter<long> HotkeyTriggerCounter = Meter.CreateCounter<long>(
            "pulsar.hotkey.triggers",
            description: "Total number of hotkey triggers");

        /// <summary>
        /// 配置 OpenTelemetry（在 App.xaml.cs 中调用）
        /// </summary>
        public static TracerProvider ConfigureTracing(bool enableConsoleExporter = false, string? otlpEndpoint = null)
        {
            var builder = Sdk.CreateTracerProviderBuilder()
                .SetResourceBuilder(ResourceBuilder.CreateDefault()
                    .AddService("Pulsar", serviceVersion: "2.4.0")
                    .AddAttributes(new[]
                    {
                        new System.Collections.Generic.KeyValuePair<string, object>("deployment.environment", "production"),
                        new System.Collections.Generic.KeyValuePair<string, object>("host.name", Environment.MachineName)
                    }))
                .AddSource("Pulsar")
                .SetSampler(new TraceIdRatioBasedSampler(1.0)); // 100% 采样（生产环境可降低）

            // Console Exporter（开发调试）
            if (enableConsoleExporter)
            {
                builder.AddConsoleExporter();
            }

            // OTLP Exporter（Jaeger/Tempo/Prometheus）
            if (!string.IsNullOrEmpty(otlpEndpoint))
            {
                builder.AddOtlpExporter(options =>
                {
                    options.Endpoint = new Uri(otlpEndpoint);
                });
            }

            return builder.Build();
        }

        /// <summary>
        /// 配置 Metrics（在 App.xaml.cs 中调用）
        /// </summary>
        public static MeterProvider ConfigureMetrics(bool enableConsoleExporter = false, string? otlpEndpoint = null)
        {
            var builder = Sdk.CreateMeterProviderBuilder()
                .SetResourceBuilder(ResourceBuilder.CreateDefault()
                    .AddService("Pulsar", serviceVersion: "2.4.0"))
                .AddMeter("Pulsar")
                .AddRuntimeInstrumentation(); // .NET 运行时指标（CPU、内存、GC）

            // Console Exporter（开发调试）
            if (enableConsoleExporter)
            {
                builder.AddConsoleExporter();
            }

            // OTLP Exporter（Prometheus）
            if (!string.IsNullOrEmpty(otlpEndpoint))
            {
                builder.AddOtlpExporter(options =>
                {
                    options.Endpoint = new Uri(otlpEndpoint);
                });
            }

            return builder.Build();
        }
    }

    /// <summary>
    /// 插件执行追踪辅助类
    /// 简化插件中的 OpenTelemetry 使用
    /// </summary>
    public static class PluginTracingExtensions
    {
        /// <summary>
        /// 开始插件执行追踪
        /// </summary>
        /// <param name="pluginId">插件 ID</param>
        /// <param name="action">动作名称</param>
        /// <param name="targetProcess">目标进程（可选）</param>
        /// <returns>Activity（追踪 Span），使用 using 语句自动结束</returns>
        public static Activity? StartPluginActivity(string pluginId, string action, string? targetProcess = null)
        {
            var activity = OpenTelemetryConfiguration.ActivitySource.StartActivity(
                $"Plugin.{pluginId}.{action}",
                ActivityKind.Internal);

            if (activity != null)
            {
                activity.SetTag("plugin.id", pluginId);
                activity.SetTag("plugin.action", action);
                
                if (!string.IsNullOrEmpty(targetProcess))
                {
                    activity.SetTag("target.process", targetProcess);
                }
            }

            return activity;
        }

        /// <summary>
        /// 记录插件执行成功
        /// </summary>
        public static void RecordPluginSuccess(Activity? activity, string pluginId, string action, double durationMs)
        {
            // 记录指标
            OpenTelemetryConfiguration.PluginExecutionCounter.Add(1,
                new System.Collections.Generic.KeyValuePair<string, object?>("plugin.id", pluginId),
                new System.Collections.Generic.KeyValuePair<string, object?>("plugin.action", action),
                new System.Collections.Generic.KeyValuePair<string, object?>("status", "success"));

            OpenTelemetryConfiguration.PluginExecutionDuration.Record(durationMs,
                new System.Collections.Generic.KeyValuePair<string, object?>("plugin.id", pluginId),
                new System.Collections.Generic.KeyValuePair<string, object?>("plugin.action", action));

            // 更新 Activity
            if (activity != null)
            {
                activity.SetTag("execution.duration_ms", durationMs);
                activity.SetStatus(ActivityStatusCode.Ok);
            }
        }

        /// <summary>
        /// 记录插件执行失败
        /// </summary>
        public static void RecordPluginError(Activity? activity, string pluginId, string action, Exception exception, double durationMs)
        {
            // 记录指标
            OpenTelemetryConfiguration.PluginExecutionCounter.Add(1,
                new System.Collections.Generic.KeyValuePair<string, object?>("plugin.id", pluginId),
                new System.Collections.Generic.KeyValuePair<string, object?>("plugin.action", action),
                new System.Collections.Generic.KeyValuePair<string, object?>("status", "error"));

            OpenTelemetryConfiguration.PluginErrorCounter.Add(1,
                new System.Collections.Generic.KeyValuePair<string, object?>("plugin.id", pluginId),
                new System.Collections.Generic.KeyValuePair<string, object?>("plugin.action", action),
                new System.Collections.Generic.KeyValuePair<string, object?>("error.type", exception.GetType().Name));

            OpenTelemetryConfiguration.PluginExecutionDuration.Record(durationMs,
                new System.Collections.Generic.KeyValuePair<string, object?>("plugin.id", pluginId),
                new System.Collections.Generic.KeyValuePair<string, object?>("plugin.action", action));

            // 更新 Activity
            if (activity != null)
            {
                activity.SetTag("execution.duration_ms", durationMs);
                activity.SetTag("error.type", exception.GetType().Name);
                activity.SetTag("error.message", exception.Message);
                activity.SetStatus(ActivityStatusCode.Error, exception.Message);
                activity.AddException(exception);
            }
        }

        /// <summary>
        /// 记录 Radial Menu 打开事件
        /// </summary>
        public static void RecordRadialMenuOpen(double durationMs)
        {
            OpenTelemetryConfiguration.RadialMenuOpenDuration.Record(durationMs);
        }

        /// <summary>
        /// 记录热键触发事件
        /// </summary>
        public static void RecordHotkeyTrigger(string hotkeyName)
        {
            OpenTelemetryConfiguration.HotkeyTriggerCounter.Add(1,
                new System.Collections.Generic.KeyValuePair<string, object?>("hotkey.name", hotkeyName));
        }
    }
}
