using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Pulsar.Models;
using Pulsar.Services;
using Pulsar.Services.Interfaces;
using Pulsar.Services.WindowSwitching;
using Xunit;

namespace Pulsar.Tests.Services
{
    /// <summary>
    /// Discovery Exclusion Policy 单一所有者测试：进程黑名单 / 身份规则 / 切换诊断
    /// 三个配置键的解析与应用只有一份实现（此前散落在 WinSwitcherPlugin relay 与
    /// 各写者调用点）。见架构审查 2026-09-10 候选 W2。
    /// </summary>
    public class DiscoveryExclusionPolicyTests
    {
        private const string OwnerPluginId = "com.pulsar.winswitcher";

        private readonly Mock<IWindowEligibilityEvaluator> _evaluator = new();
        private readonly Mock<IConfigService> _configService = new();
        private List<ProfilesConfig> _savedConfigs = null!;

        private DiscoveryExclusionPolicy CreatePolicy()
        {
            return new DiscoveryExclusionPolicy(
                _evaluator.Object,
                _configService.Object,
                NullLogger<DiscoveryExclusionPolicy>.Instance);
        }

        // ----------------------
        // ApplyFromConfig — ExcludeProcesses
        // ----------------------

        [Fact]
        public void ApplyFromConfig_ExcludeProcesses_ParsesTrimsAndCaseFolds()
        {
            var policy = CreatePolicy();

            policy.ApplyFromConfig(new Dictionary<string, object>
            {
                ["ExcludeProcesses"] = " Chrome , notepad ,,OBS64.exe,"
            });

            _evaluator.Verify(e => e.UpdateBlacklist(It.Is<IEnumerable<string>>(set =>
                set.ToHashSet(StringComparer.OrdinalIgnoreCase).SetEquals(new[] { "Chrome", "notepad", "OBS64.exe" })
                && set.All(name => name == name.Trim()))),
                Times.Once);
        }

        [Fact]
        public void ApplyFromConfig_ExcludeProcesses_EmptyString_ClearsBlacklist()
        {
            var policy = CreatePolicy();

            policy.ApplyFromConfig(new Dictionary<string, object> { ["ExcludeProcesses"] = "" });

            _evaluator.Verify(e => e.UpdateBlacklist(It.Is<IEnumerable<string>>(set => !set.Any())), Times.Once);
        }

        [Fact]
        public void ApplyFromConfig_WithoutExcludeKeys_MakesNoEvaluatorCalls()
        {
            var policy = CreatePolicy();

            policy.ApplyFromConfig(new Dictionary<string, object> { ["Unrelated"] = "value" });

            _evaluator.Verify(e => e.UpdateBlacklist(It.IsAny<IEnumerable<string>>()), Times.Never);
            _evaluator.Verify(e => e.UpdateRules(It.IsAny<IReadOnlyList<WindowEligibilityRule>>()), Times.Never);
            policy.DiagnosticsEnabled.Should().BeFalse();
        }

        // ----------------------
        // ApplyFromConfig — ExcludeRules
        // ----------------------

        [Fact]
        public void ApplyFromConfig_ExcludeRules_ValidJson_ReplacesRules()
        {
            var policy = CreatePolicy();

            policy.ApplyFromConfig(new Dictionary<string, object>
            {
                ["ExcludeRules"] = "[{\"Allow\":false,\"WindowClass\":\"GhostClass\"}]"
            });

            _evaluator.Verify(e => e.UpdateRules(It.Is<IReadOnlyList<WindowEligibilityRule>>(rules =>
                rules.Count == 1 && rules[0].WindowClass == "GhostClass")), Times.Once);
        }

        [Fact]
        public void ApplyFromConfig_ExcludeRules_EmptyString_ClearsRules()
        {
            var policy = CreatePolicy();

            policy.ApplyFromConfig(new Dictionary<string, object> { ["ExcludeRules"] = "" });

            _evaluator.Verify(e => e.UpdateRules(It.Is<IReadOnlyList<WindowEligibilityRule>>(rules => rules.Count == 0)), Times.Once);
        }

        [Fact]
        public void ApplyFromConfig_ExcludeRules_InvalidJson_IsIgnored()
        {
            var policy = CreatePolicy();

            policy.ApplyFromConfig(new Dictionary<string, object> { ["ExcludeRules"] = "{ not json" });

            _evaluator.Verify(e => e.UpdateRules(It.IsAny<IReadOnlyList<WindowEligibilityRule>>()), Times.Never);
        }

        // ----------------------
        // ApplyFromConfig — EnableSwitchDiagnostics
        // ----------------------

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void ApplyFromConfig_EnableSwitchDiagnostics_ParsesBoolean(bool enabled)
        {
            var policy = CreatePolicy();

            policy.ApplyFromConfig(new Dictionary<string, object> { ["EnableSwitchDiagnostics"] = enabled });

            policy.DiagnosticsEnabled.Should().Be(enabled);
        }

        [Fact]
        public void ApplyFromConfig_EnableSwitchDiagnostics_Unparsable_LeavesFlagUnchanged()
        {
            var policy = CreatePolicy();
            policy.ApplyFromConfig(new Dictionary<string, object> { ["EnableSwitchDiagnostics"] = true });

            policy.ApplyFromConfig(new Dictionary<string, object> { ["EnableSwitchDiagnostics"] = "not-a-bool" });

            policy.DiagnosticsEnabled.Should().BeTrue();
        }

        [Fact]
        public void Rules_ShouldExposeEvaluatorRules()
        {
            var expected = new List<WindowEligibilityRule> { new(false, null, "GhostClass", null) };
            _evaluator.SetupGet(e => e.Rules).Returns(expected);
            var policy = CreatePolicy();

            policy.Rules.Should().BeSameAs(expected);
        }

        // ----------------------
        // InitializeFromConfig — startup bootstrap
        // ----------------------

        [Fact]
        public void InitializeFromConfig_AppliesPersistedWinSwitcherProfile()
        {
            var snapshot = new ProfilesConfig();
            snapshot.Plugins[OwnerPluginId] = new PluginProfile
            {
                Config = new Dictionary<string, object>
                {
                    ["ExcludeProcesses"] = "chrome",
                    ["ExcludeRules"] = "[{\"Allow\":false,\"WindowClass\":\"GhostClass\"}]",
                    ["EnableSwitchDiagnostics"] = true
                }
            };
            _configService.Setup(c => c.GetSnapshot()).Returns(snapshot);
            var policy = CreatePolicy();

            policy.InitializeFromConfig();

            _evaluator.Verify(e => e.UpdateBlacklist(It.Is<IEnumerable<string>>(set =>
                set.ToHashSet(StringComparer.OrdinalIgnoreCase).SetEquals(new[] { "chrome" }))), Times.Once);
            _evaluator.Verify(e => e.UpdateRules(It.Is<IReadOnlyList<WindowEligibilityRule>>(rules =>
                rules.Count == 1 && rules[0].WindowClass == "GhostClass")), Times.Once);
            policy.DiagnosticsEnabled.Should().BeTrue();
        }

        [Fact]
        public void InitializeFromConfig_WithoutWinSwitcherProfile_MakesNoEvaluatorCalls()
        {
            _configService.Setup(c => c.GetSnapshot()).Returns(new ProfilesConfig());
            var policy = CreatePolicy();

            policy.InitializeFromConfig();

            _evaluator.Verify(e => e.UpdateBlacklist(It.IsAny<IEnumerable<string>>()), Times.Never);
            _evaluator.Verify(e => e.UpdateRules(It.IsAny<IReadOnlyList<WindowEligibilityRule>>()), Times.Never);
        }

        // ----------------------
        // SetRulesAsync — Inspector 写入通道
        // ----------------------

        [Fact]
        public async Task SetRulesAsync_AppliesToEvaluator_AndPersistsExcludeRulesKey()
        {
            SetupEditableConfigStore();
            var policy = CreatePolicy();
            var rules = new List<WindowEligibilityRule> { new(false, null, "GhostClass", null) };

            await policy.SetRulesAsync(rules);

            _evaluator.Verify(e => e.UpdateRules(It.Is<IReadOnlyList<WindowEligibilityRule>>(applied =>
                applied.Count == 1 && applied[0].WindowClass == "GhostClass")), Times.Once);
            _savedConfigs.Should().HaveCount(1);
            _savedConfigs[0].Plugins[OwnerPluginId].Config["ExcludeRules"]
                .Should().Be(WindowEligibilityRuleSerializer.Serialize(rules));
        }

        [Fact]
        public async Task SetRulesAsync_WhenPersistFails_RulesRemainApplied_AndRethrows()
        {
            _configService.Setup(c => c.LoadSnapshotAsync(It.IsAny<bool>()))
                .ReturnsAsync(new ProfilesConfig());
            _configService.SetupGet(c => c.CurrentRevision).Returns(1);
            _configService.Setup(c => c.SaveAsync(It.IsAny<ProfilesConfig>(), It.IsAny<long?>()))
                .ThrowsAsync(new InvalidOperationException("disk on fire"));
            var policy = CreatePolicy();
            var rules = new List<WindowEligibilityRule> { new(false, null, "GhostClass", null) };

            var act = async () => await policy.SetRulesAsync(rules);

            await act.Should().ThrowAsync<InvalidOperationException>();
            _evaluator.Verify(e => e.UpdateRules(It.Is<IReadOnlyList<WindowEligibilityRule>>(applied =>
                applied.Count == 1 && applied[0].WindowClass == "GhostClass")), Times.Once);
        }

        private void SetupEditableConfigStore()
        {
            _savedConfigs = new List<ProfilesConfig>();
            _configService.Setup(c => c.LoadSnapshotAsync(It.IsAny<bool>()))
                .ReturnsAsync(new ProfilesConfig());
            _configService.SetupGet(c => c.CurrentRevision).Returns(1);
            _configService.Setup(c => c.SaveAsync(It.IsAny<ProfilesConfig>(), It.IsAny<long?>()))
                .Callback<ProfilesConfig, long?>((config, _) => _savedConfigs.Add(config))
                .Returns(Task.CompletedTask);
        }
    }
}
