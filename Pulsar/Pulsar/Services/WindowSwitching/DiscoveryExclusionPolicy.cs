// [Path]: Pulsar/Pulsar/Services/WindowSwitching/DiscoveryExclusionPolicy.cs

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Pulsar.Services.Interfaces;

namespace Pulsar.Services.WindowSwitching
{
    /// <summary>
    /// <see cref="IDiscoveryExclusionPolicy"/> 的唯一实现：配置键解析逻辑自
    /// WinSwitcherPlugin 的 UpdateSettings relay 收拢而来（语义逐字保持），
    /// 运行时应用点收敛为对 <see cref="IWindowEligibilityEvaluator"/> 的单次调用。
    /// </summary>
    public sealed class DiscoveryExclusionPolicy : IDiscoveryExclusionPolicy
    {
        /// <summary>策略键所在的插件配置区（WinSwitcher 是排除策略的持久化宿主）。</summary>
        public const string OwnerPluginId = "com.pulsar.winswitcher";

        public const string ExcludeProcessesKey = "ExcludeProcesses";
        public const string ExcludeRulesKey = "ExcludeRules";
        public const string EnableSwitchDiagnosticsKey = "EnableSwitchDiagnostics";

        private const string LogPrefix = "[DiscoveryExclusion]";

        private readonly IWindowEligibilityEvaluator _evaluator;
        private readonly IConfigService _configService;
        private readonly ILogger<DiscoveryExclusionPolicy> _logger;
        private volatile bool _diagnosticsEnabled;

        public DiscoveryExclusionPolicy(
            IWindowEligibilityEvaluator evaluator,
            IConfigService configService,
            ILogger<DiscoveryExclusionPolicy> logger)
        {
            _evaluator = evaluator ?? throw new ArgumentNullException(nameof(evaluator));
            _configService = configService ?? throw new ArgumentNullException(nameof(configService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc />
        public IReadOnlyList<WindowEligibilityRule> Rules => _evaluator.Rules;

        /// <inheritdoc />
        public bool DiagnosticsEnabled => _diagnosticsEnabled;

        /// <inheritdoc />
        public void ApplyFromConfig(IReadOnlyDictionary<string, object> config)
        {
            ArgumentNullException.ThrowIfNull(config);

            if (config.TryGetValue(ExcludeProcessesKey, out var excludeObj) && excludeObj != null)
            {
                var excludeStr = excludeObj.ToString() ?? string.Empty;
                var blacklist = excludeStr.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                                          .Select(p => p.Trim())
                                          .ToHashSet(StringComparer.OrdinalIgnoreCase);
                _evaluator.UpdateBlacklist(blacklist);
            }

            if (config.TryGetValue(ExcludeRulesKey, out var rulesObj) && rulesObj != null)
            {
                var rulesJson = rulesObj.ToString() ?? string.Empty;
                var rules = WindowEligibilityRuleSerializer.TryParse(rulesJson);
                if (rules != null)
                {
                    _evaluator.UpdateRules(rules);
                }
                else
                {
                    _logger.LogWarning("{Prefix} ExcludeRules JSON is invalid and was ignored", LogPrefix);
                }
            }

            if (config.TryGetValue(EnableSwitchDiagnosticsKey, out var diagnosticsObj)
                && bool.TryParse(diagnosticsObj?.ToString(), out var diagnosticsEnabled))
            {
                _diagnosticsEnabled = diagnosticsEnabled;
                _logger.LogDebug("{Prefix} Switch diagnostics enabled={Enabled}", LogPrefix, diagnosticsEnabled);
            }
        }

        /// <inheritdoc />
        public async Task SetRulesAsync(IReadOnlyList<WindowEligibilityRule> rules)
        {
            ArgumentNullException.ThrowIfNull(rules);

            // 先应用：规则本次会话立即生效，即使持久化失败也不回滚（调用方负责呈现降级提示）。
            _evaluator.UpdateRules(rules);

            var json = WindowEligibilityRuleSerializer.Serialize(rules);
            try
            {
                await ConfigEditSession.RunAsync(_configService, session =>
                    session.UpdatePluginProfile(OwnerPluginId, profile => profile.Config[ExcludeRulesKey] = json));

                _logger.LogInformation("{Prefix} Exclusion rules applied and persisted. Count={Count}", LogPrefix, rules.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "{Prefix} Failed to persist ExcludeRules (rules remain active for this session)", LogPrefix);
                throw;
            }
        }

        /// <inheritdoc />
        public void InitializeFromConfig()
        {
            var snapshot = _configService.GetSnapshot();
            var profile = snapshot.Plugins.GetValueOrDefault(OwnerPluginId);
            var config = profile?.Config;

            if (config == null || config.Count == 0)
            {
                _logger.LogDebug("{Prefix} No persisted WinSwitcher config; exclusion policy starts empty", LogPrefix);
                return;
            }

            ApplyFromConfig(config);
            _logger.LogInformation("{Prefix} Initialized from persisted config", LogPrefix);
        }
    }
}
