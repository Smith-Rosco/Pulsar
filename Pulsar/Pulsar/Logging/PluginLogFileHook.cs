// [Path]: Pulsar/Pulsar/Logging/PluginLogFileHook.cs

using System;
using System.IO;
using System.Text;
using Serilog.Core;
using Serilog.Events;
using Serilog.Formatting;

namespace Pulsar.Logging
{
    /// <summary>
    /// Serilog File Hook - 根据插件 ID 动态路由日志到不同文件
    /// 文件命名格式: {PluginId}-yyyyMMdd.log
    /// </summary>
    public class PluginLogFileHook : ILogEventSink
    {
        private readonly string _baseDirectory;
        private readonly ITextFormatter _formatter;
        private readonly object _syncRoot = new();

        public PluginLogFileHook()
        {
            _baseDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Pulsar",
                "Logs",
                "Plugins"
            );
            Directory.CreateDirectory(_baseDirectory);

            // 使用简单的文本格式化器
            _formatter = new Serilog.Formatting.Display.MessageTemplateTextFormatter(
                "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] [{Action}] [ExecId:{ExecutionId}] {Message:lj}{NewLine}{Exception}",
                null
            );
        }

        public void Emit(LogEvent logEvent)
        {
            // 提取插件 ID
            if (!logEvent.Properties.TryGetValue("PluginId", out var pluginIdProperty))
                return;

            var pluginId = pluginIdProperty.ToString().Trim('"');
            var dateStr = logEvent.Timestamp.ToString("yyyyMMdd");
            var fileName = $"{pluginId}-{dateStr}.log";
            var filePath = Path.Combine(_baseDirectory, fileName);

            // 格式化日志
            var sb = new StringBuilder();
            using (var writer = new StringWriter(sb))
            {
                _formatter.Format(logEvent, writer);
            }

            // 写入文件（线程安全）
            lock (_syncRoot)
            {
                try
                {
                    File.AppendAllText(filePath, sb.ToString());
                }
                catch
                {
                    // 静默失败，避免影响主程序
                }
            }
        }
    }
}
