// [Path]: Pulsar/Pulsar/Logging/RateLimitingSink.cs

using System;
using System.Collections.Concurrent;
using Serilog.Core;
using Serilog.Events;

namespace Pulsar.Logging
{
    /// <summary>
    /// Serilog Sink - 日志限流和采样
    /// 防止高频日志导致磁盘 I/O 饱和和日志文件爆炸
    /// 
    /// 策略：
    /// 1. 滑动窗口限流：每个时间窗口内限制日志数量
    /// 2. 相同消息去重：短时间内相同消息只记录一次（附带计数）
    /// 3. 级别优先：Error/Fatal 级别不受限流影响
    /// </summary>
    public class RateLimitingSink : ILogEventSink
    {
        private readonly ILogEventSink _innerSink;
        private readonly int _maxLogsPerWindow;
        private readonly TimeSpan _windowDuration;
        private readonly bool _exemptHighSeverity;

        // 滑动窗口计数器
        private readonly ConcurrentQueue<DateTime> _logTimestamps = new();
        private long _droppedLogsCount = 0;

        // 消息去重缓存 (MessageHash -> (FirstSeen, Count, LastLogEvent))
        private readonly ConcurrentDictionary<int, (DateTime FirstSeen, int Count, LogEvent LastEvent)> _messageCache = new();
        private readonly TimeSpan _deduplicationWindow = TimeSpan.FromSeconds(10);

        // 定期清理任务
        private readonly System.Threading.Timer _cleanupTimer;

        public RateLimitingSink(
            ILogEventSink innerSink,
            int maxLogsPerWindow = 1000,
            TimeSpan? windowDuration = null,
            bool exemptHighSeverity = true)
        {
            _innerSink = innerSink ?? throw new ArgumentNullException(nameof(innerSink));
            _maxLogsPerWindow = maxLogsPerWindow;
            _windowDuration = windowDuration ?? TimeSpan.FromMinutes(1);
            _exemptHighSeverity = exemptHighSeverity;

            // 每 30 秒清理一次过期数据
            _cleanupTimer = new System.Threading.Timer(CleanupExpiredData, null, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));
        }

        public void Emit(LogEvent logEvent)
        {
            // 策略 1: 高优先级日志豁免限流
            if (_exemptHighSeverity && logEvent.Level >= LogEventLevel.Error)
            {
                _innerSink.Emit(logEvent);
                return;
            }

            // 策略 2: 消息去重
            var messageHash = GetMessageHash(logEvent);
            if (_messageCache.TryGetValue(messageHash, out var cached))
            {
                var timeSinceFirst = DateTime.UtcNow - cached.FirstSeen;
                
                if (timeSinceFirst < _deduplicationWindow)
                {
                    // 更新计数
                    _messageCache[messageHash] = (cached.FirstSeen, cached.Count + 1, logEvent);
                    
                    // 每 10 次重复记录一次汇总日志
                    if (cached.Count % 10 == 0)
                    {
                        EmitDuplicateSummary(logEvent, cached.Count + 1);
                    }
                    return;
                }
                else
                {
                    // 窗口过期，发送最终汇总并重置
                    if (cached.Count > 1)
                    {
                        EmitDuplicateSummary(cached.LastEvent, cached.Count);
                    }
                    _messageCache[messageHash] = (DateTime.UtcNow, 1, logEvent);
                }
            }
            else
            {
                _messageCache[messageHash] = (DateTime.UtcNow, 1, logEvent);
            }

            // 策略 3: 滑动窗口限流
            var now = DateTime.UtcNow;
            _logTimestamps.Enqueue(now);

            // 移除窗口外的时间戳
            while (_logTimestamps.TryPeek(out var oldest) && (now - oldest) > _windowDuration)
            {
                _logTimestamps.TryDequeue(out _);
            }

            // 检查是否超过限流阈值
            if (_logTimestamps.Count > _maxLogsPerWindow)
            {
                Interlocked.Increment(ref _droppedLogsCount);
                
                // 每丢弃 100 条日志，记录一次警告
                if (_droppedLogsCount % 100 == 0)
                {
                    EmitRateLimitWarning();
                }
                return;
            }

            // 通过限流检查，发送日志
            _innerSink.Emit(logEvent);
        }

        /// <summary>
        /// 生成消息哈希用于去重（基于模板和关键属性）
        /// </summary>
        private int GetMessageHash(LogEvent logEvent)
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + logEvent.MessageTemplate.Text.GetHashCode();
                hash = hash * 31 + logEvent.Level.GetHashCode();

                // 包含插件 ID 和动作名称（如果有）
                if (logEvent.Properties.TryGetValue("PluginId", out var pluginId))
                {
                    hash = hash * 31 + pluginId.ToString().GetHashCode();
                }
                if (logEvent.Properties.TryGetValue("Action", out var action))
                {
                    hash = hash * 31 + action.ToString().GetHashCode();
                }

                return hash;
            }
        }

        /// <summary>
        /// 发送重复消息汇总日志
        /// </summary>
        private void EmitDuplicateSummary(LogEvent originalEvent, int count)
        {
            var summaryEvent = new LogEvent(
                DateTimeOffset.UtcNow,
                originalEvent.Level,
                null,
                new Serilog.Events.MessageTemplate(
                    "[RateLimiting] Duplicate message suppressed (Count: {DuplicateCount}): {OriginalMessage}",
                    new[]
                    {
                        new Serilog.Parsing.PropertyToken("DuplicateCount", "{DuplicateCount}"),
                        new Serilog.Parsing.PropertyToken("OriginalMessage", "{OriginalMessage}")
                    }),
                new[]
                {
                    new LogEventProperty("DuplicateCount", new ScalarValue(count)),
                    new LogEventProperty("OriginalMessage", new ScalarValue(originalEvent.MessageTemplate.Text))
                });

            // 复制原始事件的属性
            foreach (var prop in originalEvent.Properties)
            {
                summaryEvent.AddPropertyIfAbsent(new LogEventProperty(prop.Key, prop.Value));
            }

            _innerSink.Emit(summaryEvent);
        }

        /// <summary>
        /// 发送限流警告日志
        /// </summary>
        private void EmitRateLimitWarning()
        {
            var warningEvent = new LogEvent(
                DateTimeOffset.UtcNow,
                LogEventLevel.Warning,
                null,
                new Serilog.Events.MessageTemplate(
                    "[RateLimiting] Log rate limit exceeded. Dropped {DroppedCount} logs in the last window. Consider reducing log verbosity.",
                    new[] { new Serilog.Parsing.PropertyToken("DroppedCount", "{DroppedCount}") }),
                new[]
                {
                    new LogEventProperty("DroppedCount", new ScalarValue(_droppedLogsCount))
                });

            _innerSink.Emit(warningEvent);
        }

        /// <summary>
        /// 定期清理过期的缓存数据
        /// </summary>
        private void CleanupExpiredData(object? state)
        {
            try
            {
                var now = DateTime.UtcNow;
                var expiredKeys = new System.Collections.Generic.List<int>();

                foreach (var kvp in _messageCache)
                {
                    if ((now - kvp.Value.FirstSeen) > _deduplicationWindow * 2)
                    {
                        expiredKeys.Add(kvp.Key);
                    }
                }

                foreach (var key in expiredKeys)
                {
                    _messageCache.TryRemove(key, out _);
                }

                // 重置丢弃计数器（每个清理周期）
                if (_droppedLogsCount > 0)
                {
                    Interlocked.Exchange(ref _droppedLogsCount, 0);
                }
            }
            catch
            {
                // 忽略清理错误，避免影响主日志流程
            }
        }

        public void Dispose()
        {
            _cleanupTimer?.Dispose();
        }
    }
}
