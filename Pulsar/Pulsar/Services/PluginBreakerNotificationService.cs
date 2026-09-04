// [Path]: Pulsar/Pulsar/Services/PluginBreakerNotificationService.cs

using System;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Pulsar.Core.Localization;
using Pulsar.Core.Plugin.Runtime;
using Pulsar.Models;
using Pulsar.Services.Interfaces;

namespace Pulsar.Services
{
    /// <summary>
    /// 熔断器状态迁移的观察者 adapter（ADR-013）。
    ///
    /// <see cref="PluginCircuitBreakerPolicy"/> 是纯状态机：它决定何时打开/关闭熔断，
    /// 然后通过 <c>Tripped</c> / <c>Recovered</c> 事件广播迁移。本服务订阅这两个事件，
    /// 把迁移转成副作用：
    ///   * Trip    → 健康遥测记录（<see cref="IPluginHealthMonitor.RecordCircuitBreakerTrip"/>）
    ///               + 托盘通知（<see cref="ITrayService.ShowNotification"/>，沿用本地化文案）
    ///   * Recovery → 仅健康遥测记录（<see cref="IPluginHealthMonitor.RecordCircuitBreakerRecovery"/>）
    ///
    /// 生命周期：本服务与策略都是容器单例（应用生命周期），事件源不会比订阅者先销毁，
    /// 订阅不构成泄漏。订阅发生在构造器——一旦本服务在启动协调器中解析即激活转发。
    /// 事件处理器必须隔离异常：观察者抛错不能反向污染熔断决策/执行管线。
    /// </summary>
    public sealed class PluginBreakerNotificationService
    {
        private readonly IPluginHealthMonitor _healthMonitor;
        private readonly ITrayService _trayService;
        private readonly ILocalizationService _loc;
        private readonly ILogger<PluginBreakerNotificationService> _logger;

        public PluginBreakerNotificationService(
            PluginCircuitBreakerPolicy breakerPolicy,
            IPluginHealthMonitor healthMonitor,
            ITrayService trayService,
            ILocalizationService localizationService,
            ILogger<PluginBreakerNotificationService>? logger = null)
        {
            ArgumentNullException.ThrowIfNull(breakerPolicy);
            _healthMonitor = healthMonitor ?? throw new ArgumentNullException(nameof(healthMonitor));
            _trayService = trayService ?? throw new ArgumentNullException(nameof(trayService));
            _loc = localizationService ?? throw new ArgumentNullException(nameof(localizationService));
            _logger = logger ?? NullLogger<PluginBreakerNotificationService>.Instance;

            breakerPolicy.Tripped += OnBreakerTripped;
            breakerPolicy.Recovered += OnBreakerRecovered;
        }

        private void OnBreakerTripped(object? sender, PluginBreakerTrippedEventArgs e)
        {
            try
            {
                _healthMonitor.RecordCircuitBreakerTrip(e.PluginId);
                _trayService.ShowNotification(
                    _loc["Plugin.CircuitBreakerTitle"],
                    string.Format(_loc["Plugin.CircuitBreakerBody"], e.PluginId, e.Cooldown.TotalSeconds),
                    PulsarNotificationIcon.Error);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[PluginBreakerNotificationService] Failed to relay circuit breaker trip for {PluginId}", e.PluginId);
            }
        }

        private void OnBreakerRecovered(object? sender, PluginBreakerRecoveredEventArgs e)
        {
            try
            {
                _healthMonitor.RecordCircuitBreakerRecovery(e.PluginId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[PluginBreakerNotificationService] Failed to relay circuit breaker recovery for {PluginId}", e.PluginId);
            }
        }
    }
}
