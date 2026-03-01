// [Path]: Pulsar/Pulsar/Services/PluginHealthMonitor.cs

using Microsoft.Extensions.Logging;
using Pulsar.Models;
using Pulsar.Services.Interfaces;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace Pulsar.Services
{
    /// <summary>
    /// 插件健康监控服务
    /// 负责监控插件健康状态、错误率、Circuit Breaker 状态等
    /// </summary>
    public class PluginHealthMonitor : IPluginHealthMonitor
    {
        private readonly ILogger<PluginHealthMonitor> _logger;
        private readonly IPluginUsageTracker _usageTracker;

        // 错误记录（最近 100 次执行）
        private readonly ConcurrentDictionary<string, CircularBuffer<bool>> _recentExecutions = new();

        // Circuit Breaker 触发记录（最近 24 小时）
        private readonly ConcurrentDictionary<string, List<DateTime>> _circuitBreakerTrips = new();

        // Circuit Breaker 当前状态
        private readonly ConcurrentDictionary<string, bool> _circuitBreakerStates = new();

        // 最后错误信息
        private readonly ConcurrentDictionary<string, (DateTime Time, string Message)> _lastErrors = new();

        private const int RecentExecutionBufferSize = 100;
        private const int CircuitBreakerTripWindowHours = 24;

        public PluginHealthMonitor(ILogger<PluginHealthMonitor> logger, IPluginUsageTracker usageTracker)
        {
            _logger = logger;
            _usageTracker = usageTracker;
        }

        /// <summary>
        /// 记录插件错误
        /// </summary>
        public void RecordError(string pluginId, Exception exception, string? action = null)
        {
            try
            {
                // 记录到最近执行缓冲区
                var buffer = _recentExecutions.GetOrAdd(pluginId, _ => new CircularBuffer<bool>(RecentExecutionBufferSize));
                buffer.Add(false); // false = 失败

                // 记录最后错误
                var errorMessage = action != null
                    ? $"Action '{action}' failed: {exception.Message}"
                    : exception.Message;
                _lastErrors[pluginId] = (DateTime.UtcNow, errorMessage);

                _logger.LogDebug("[PluginHealthMonitor] Recorded error for {PluginId}: {Message}", pluginId, errorMessage);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[PluginHealthMonitor] Failed to record error for {PluginId}", pluginId);
            }
        }

        /// <summary>
        /// 记录成功执行（内部使用）
        /// </summary>
        public void RecordSuccess(string pluginId)
        {
            try
            {
                var buffer = _recentExecutions.GetOrAdd(pluginId, _ => new CircularBuffer<bool>(RecentExecutionBufferSize));
                buffer.Add(true); // true = 成功
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[PluginHealthMonitor] Failed to record success for {PluginId}", pluginId);
            }
        }

        /// <summary>
        /// 记录 Circuit Breaker 触发
        /// </summary>
        public void RecordCircuitBreakerTrip(string pluginId)
        {
            try
            {
                var trips = _circuitBreakerTrips.GetOrAdd(pluginId, _ => new List<DateTime>());
                lock (trips)
                {
                    trips.Add(DateTime.UtcNow);
                    CleanupOldTrips(trips);
                }

                _circuitBreakerStates[pluginId] = true; // Open

                _logger.LogWarning("[PluginHealthMonitor] Circuit Breaker tripped for {PluginId}", pluginId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[PluginHealthMonitor] Failed to record Circuit Breaker trip for {PluginId}", pluginId);
            }
        }

        /// <summary>
        /// 记录 Circuit Breaker 恢复
        /// </summary>
        public void RecordCircuitBreakerRecovery(string pluginId)
        {
            try
            {
                _circuitBreakerStates[pluginId] = false; // Closed
                _logger.LogInformation("[PluginHealthMonitor] Circuit Breaker recovered for {PluginId}", pluginId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[PluginHealthMonitor] Failed to record Circuit Breaker recovery for {PluginId}", pluginId);
            }
        }

        /// <summary>
        /// 获取插件健康报告
        /// </summary>
        public PluginHealthReport GetHealthReport(string pluginId)
        {
            var report = new PluginHealthReport
            {
                PluginId = pluginId
            };

            // 获取使用统计
            var stats = _usageTracker.GetStats(pluginId);

            // 计算错误率
            if (_recentExecutions.TryGetValue(pluginId, out var buffer))
            {
                var executions = buffer.GetAll();
                report.RecentExecutionCount = executions.Count;
                report.RecentErrorCount = executions.Count(e => !e);
                report.ErrorRate = report.RecentExecutionCount > 0
                    ? (double)report.RecentErrorCount / report.RecentExecutionCount
                    : 0.0;
            }

            // Circuit Breaker 触发次数
            if (_circuitBreakerTrips.TryGetValue(pluginId, out var trips))
            {
                lock (trips)
                {
                    CleanupOldTrips(trips);
                    report.CircuitBreakerTrips = trips.Count;
                }
            }

            // 最后错误信息
            if (_lastErrors.TryGetValue(pluginId, out var lastError))
            {
                report.LastErrorTime = lastError.Time;
                report.LastErrorMessage = lastError.Message;
            }

            // 计算健康评分
            report.HealthScore = CalculateHealthScore(pluginId);

            // 判定健康状态
            report.Status = DetermineHealthStatus(pluginId, report, stats);

            return report;
        }

        /// <summary>
        /// 获取所有插件健康报告
        /// </summary>
        public Dictionary<string, PluginHealthReport> GetAllHealthReports()
        {
            var reports = new Dictionary<string, PluginHealthReport>();
            var allStats = _usageTracker.GetAllStats();

            foreach (var pluginId in allStats.Keys)
            {
                reports[pluginId] = GetHealthReport(pluginId);
            }

            return reports;
        }

        /// <summary>
        /// 获取有问题的插件列表
        /// </summary>
        public List<string> GetUnhealthyPlugins()
        {
            var allReports = GetAllHealthReports();
            return allReports
                .Where(kvp => kvp.Value.Status == PluginHealthStatus.Warning ||
                              kvp.Value.Status == PluginHealthStatus.Critical)
                .Select(kvp => kvp.Key)
                .ToList();
        }

        /// <summary>
        /// 计算健康评分（0-100）
        /// 算法: HealthScore = 100 - (ErrorRate * 50) - (CircuitBreakerTrips * 10) - (DaysSinceLastUse * 0.5)
        /// </summary>
        public int CalculateHealthScore(string pluginId)
        {
            double score = 100.0;

            // 错误率惩罚（最多 -50 分）
            if (_recentExecutions.TryGetValue(pluginId, out var buffer))
            {
                var executions = buffer.GetAll();
                if (executions.Count > 0)
                {
                    var errorRate = (double)executions.Count(e => !e) / executions.Count;
                    score -= errorRate * 50;
                }
            }

            // Circuit Breaker 触发惩罚（每次 -10 分）
            if (_circuitBreakerTrips.TryGetValue(pluginId, out var trips))
            {
                lock (trips)
                {
                    CleanupOldTrips(trips);
                    score -= trips.Count * 10;
                }
            }

            // 未使用惩罚（每天 -0.5 分）
            var stats = _usageTracker.GetStats(pluginId);
            if (stats.LastUsed.HasValue)
            {
                var daysSinceLastUse = (DateTime.UtcNow - stats.LastUsed.Value).TotalDays;
                score -= daysSinceLastUse * 0.5;
            }
            else
            {
                // 从未使用过
                score -= 30;
            }

            return Math.Max(0, Math.Min(100, (int)score));
        }

        /// <summary>
        /// 检查插件是否处于 Circuit Breaker Open 状态
        /// </summary>
        public bool IsCircuitBreakerOpen(string pluginId)
        {
            return _circuitBreakerStates.TryGetValue(pluginId, out var isOpen) && isOpen;
        }

        /// <summary>
        /// 判定健康状态
        /// </summary>
        private PluginHealthStatus DetermineHealthStatus(string pluginId, PluginHealthReport report, PluginUsageStats stats)
        {
            // 检查是否被禁用（需要从 PluginRegistry 获取，这里暂时跳过）
            // if (isDisabled) return PluginHealthStatus.Disabled;

            // 检查 Circuit Breaker 状态
            if (IsCircuitBreakerOpen(pluginId))
            {
                return PluginHealthStatus.Critical;
            }

            // 检查是否未使用（30 天内未使用）
            if (stats.LastUsed == null || (DateTime.UtcNow - stats.LastUsed.Value).TotalDays > 30)
            {
                return PluginHealthStatus.Unused;
            }

            // 检查错误率
            if (report.ErrorRate > 0.1 || report.CircuitBreakerTrips > 0)
            {
                return PluginHealthStatus.Warning;
            }

            return PluginHealthStatus.Healthy;
        }

        /// <summary>
        /// 清理超过 24 小时的 Circuit Breaker 触发记录
        /// </summary>
        private void CleanupOldTrips(List<DateTime> trips)
        {
            var cutoff = DateTime.UtcNow.AddHours(-CircuitBreakerTripWindowHours);
            trips.RemoveAll(t => t < cutoff);
        }
    }

    /// <summary>
    /// 循环缓冲区（固定大小，FIFO）
    /// </summary>
    internal class CircularBuffer<T>
    {
        private readonly T[] _buffer;
        private int _head = 0;
        private int _count = 0;
        private readonly object _lock = new();

        public CircularBuffer(int capacity)
        {
            _buffer = new T[capacity];
        }

        public void Add(T item)
        {
            lock (_lock)
            {
                _buffer[_head] = item;
                _head = (_head + 1) % _buffer.Length;
                if (_count < _buffer.Length)
                    _count++;
            }
        }

        public List<T> GetAll()
        {
            lock (_lock)
            {
                var result = new List<T>(_count);
                if (_count == _buffer.Length)
                {
                    // 缓冲区已满，按顺序读取
                    for (int i = 0; i < _buffer.Length; i++)
                    {
                        result.Add(_buffer[(_head + i) % _buffer.Length]);
                    }
                }
                else
                {
                    // 缓冲区未满，直接读取
                    for (int i = 0; i < _count; i++)
                    {
                        result.Add(_buffer[i]);
                    }
                }
                return result;
            }
        }
    }
}
