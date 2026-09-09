using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using Pulsar.Core.Plugin;
using Pulsar.Models;
using Pulsar.Services.ActionFeedback;
using Pulsar.Services.Interfaces;
using Pulsar.Tests.TestHelpers;
using Pulsar.ViewModels;
using Pulsar.ViewModels.Strategies;
using Xunit;

namespace Pulsar.Tests.ViewModels
{
    /// <summary>
    /// [S1 2026-09-09] CommandPageProvider / ProcessPageProvider 直接测试——
    /// ADR-023 承诺的 page-provider test surface（RadialMenuLayoutCoordinator
    /// 已由 R1 补齐，本文件补齐剩下两个 provider）。策略分配、分页、中心
    /// 文本与 Badge 语义全部钉死，防止回归。
    /// </summary>
    public class PageProviderTests
    {
        private static Mock<IConfigService> ConfigMock(int perPage = 8)
        {
            var mock = new Mock<IConfigService>();
            mock.Setup(c => c.GetValidatedSlotsPerPage()).Returns(perPage);
            return mock;
        }

        private static PluginSlot Slot(int no, string pluginId = "com.pulsar.command", string label = "")
            => new()
            {
                Slot = no,
                PluginId = pluginId,
                Action = "sendkeys",
                Label = label,
                Args = new Dictionary<string, string> { ["keys"] = "hi" }
            };

        private static ObservableCollection<SlotViewModel> VisualSlots(int count)
            => new(Enumerable.Range(1, count).Select(i => new SlotViewModel(i, 0, 0, 40)));

        private static CommandPageProvider CreateCommandProvider(
            List<PluginSlot> slots,
            IPluginRegistry? registry = null,
            IConfigService? configService = null,
            PulsarContext? context = null)
        {
            return new CommandPageProvider(
                slots,
                registry ?? new Mock<IPluginRegistry>().Object,
                context ?? PulsarContextFactory.CreateTestContext(),
                Mock.Of<ITrayService>(),
                configService,
                Mock.Of<IPluginExecutor>(),
                Mock.Of<IActionFeedbackService>(),
                loc: null,
                usageTracker: null,
                feedbackPresenter: null,
                settingsWindowFactory: null);
        }

        // ---- CommandPageProvider ----

        [Fact]
        public void Constructor_OrdersSlotsBySlotNumber()
        {
            var provider = CreateCommandProvider(new List<PluginSlot>
            {
                Slot(3, label: "three"),
                Slot(1, label: "one"),
                Slot(2, label: "two"),
            });

            var visuals = VisualSlots(3);
            var center = new SlotViewModel(0, 0, 0, 40);
            provider.RefreshVisuals(visuals, center);

            visuals[0].Label.Should().Be("one", "slots must render ordered by their persisted Slot number");
            visuals[1].Label.Should().Be("two");
            visuals[2].Label.Should().Be("three");
        }

        [Theory]
        [InlineData(8, 2)]
        [InlineData(4, 3)]
        public void TotalPages_PaginatesByConfiguredItemsPerPage(int perPage, int expected)
        {
            var provider = CreateCommandProvider(
                Enumerable.Range(1, 9).Select(i => Slot(i)).ToList(),
                configService: ConfigMock(perPage).Object);

            provider.TotalPages.Should().Be(expected);
        }

        [Fact]
        public void RefreshVisuals_EnabledPlugin_GetsPluginActionStrategy()
        {
            var registry = new Mock<IPluginRegistry>();
            registry.Setup(r => r.IsPluginEnabled(It.IsAny<string>())).Returns(true);
            var provider = CreateCommandProvider(
                new List<PluginSlot> { Slot(1) }, registry.Object);
            var visuals = VisualSlots(8);
            provider.RefreshVisuals(visuals, new SlotViewModel(0, 0, 0, 40));

            visuals[0].ActionStrategy.Should().BeOfType<PluginActionStrategy>();
            visuals[0].IsEnabled.Should().BeTrue();
            visuals[0].Type.Should().Be(SlotType.Action);
        }

        [Fact]
        public void RefreshVisuals_DisabledPlugin_GetsNoOpStrategyAndGreyedSlot()
        {
            var registry = new Mock<IPluginRegistry>();
            registry.Setup(r => r.IsPluginEnabled(It.IsAny<string>())).Returns(false);
            var provider = CreateCommandProvider(
                new List<PluginSlot> { Slot(1) }, registry.Object);

            var visuals = VisualSlots(8);
            provider.RefreshVisuals(visuals, new SlotViewModel(0, 0, 0, 40));

            visuals[0].ActionStrategy.Should().BeOfType<NoOpStrategy>();
            visuals[0].IsEnabled.Should().BeFalse();
        }

        [Fact]
        public void RefreshVisuals_CreateProfileSlot_AlwaysEnabledWithCreateProfileStrategy()
        {
            var registry = new Mock<IPluginRegistry>();
            registry.Setup(r => r.IsPluginEnabled(It.IsAny<string>())).Returns(false);
            var provider = CreateCommandProvider(
                new List<PluginSlot> { Slot(1, pluginId: "internal:create_profile") },
                registry.Object,
                configService: ConfigMock().Object);

            var visuals = VisualSlots(8);
            provider.RefreshVisuals(visuals, new SlotViewModel(0, 0, 0, 40));

            visuals[0].ActionStrategy.Should().BeOfType<CreateProfileStrategy>(
                "the create-profile slot must keep its dedicated strategy even when plugins are disabled");
            visuals[0].IsEnabled.Should().BeTrue("the creator slot is always enabled");
            provider.HasCreatorSlot().Should().BeTrue();
        }

        [Fact]
        public async Task LoadAsync_ResetsCurrentPageToZero()
        {
            var provider = CreateCommandProvider(
                Enumerable.Range(1, 9).Select(i => Slot(i)).ToList(),
                configService: ConfigMock(8).Object);

            provider.NextPage();
            provider.CurrentPage.Should().Be(1);

            await provider.LoadAsync();
            provider.CurrentPage.Should().Be(0);
        }

        [Fact]
        public void NextPage_WrapsAroundToFirstPage()
        {
            var provider = CreateCommandProvider(
                Enumerable.Range(1, 9).Select(i => Slot(i)).ToList(),
                configService: ConfigMock(8).Object);

            provider.NextPage();
            provider.NextPage();

            provider.CurrentPage.Should().Be(0, "paging must wrap after the last page");
        }

        [Fact]
        public void RefreshVisuals_MultiPage_CenterShowsPagedFallbackText()
        {
            var provider = CreateCommandProvider(
                Enumerable.Range(1, 9).Select(i => Slot(i)).ToList(),
                configService: ConfigMock(8).Object);

            provider.NextPage(); // page 2 of 2
            var center = new SlotViewModel(0, 0, 0, 40);
            provider.RefreshVisuals(VisualSlots(8), center);

            center.Label.Should().Be("Page 2/2",
                "loc is null in tests so the localized PageOfFormat falls back to 'Page {0}/{1}'");
            center.ActionStrategy.Should().BeOfType<NoOpStrategy>();
            center.BadgeCount.Should().Be(0);
        }

        [Fact]
        public void RefreshVisuals_SubActions_SurfacedAsSubSlots()
        {
            var slot = Slot(1);
            slot.SubActions = new System.Collections.Generic.List<SubSlotDescriptor>
            {
                new SubSlotDescriptor("com.pulsar.command", "sendkeys",
                    new Dictionary<string, string> { ["keys"] = "^c" }, "a", string.Empty, string.Empty),
                new SubSlotDescriptor("com.pulsar.command", "sendkeys",
                    new Dictionary<string, string> { ["keys"] = "^v" }, "b", string.Empty, string.Empty),
            };
            var provider = CreateCommandProvider(new List<PluginSlot> { slot });

            var visuals = VisualSlots(8);
            provider.RefreshVisuals(visuals, new SlotViewModel(0, 0, 0, 40));

            visuals[0].SubSlots.Should().HaveCount(2);
            visuals[0].BadgeCount.Should().Be(2,
                "cascade slots show a sub-action count bubble so users can tell them apart from leaf slots");
        }

        // ---- ProcessPageProvider ----

        private static ProcessPageProvider CreateProcessProvider(
            ProfilesConfig config,
            List<ProcessWindowInfo>? seeded = null,
            Mock<IWindowInventoryCoordinator>? coordinator = null)
        {
            return new ProcessPageProvider(
                Mock.Of<IWindowService>(),
                (coordinator ?? new Mock<IWindowInventoryCoordinator>()).Object,
                config,
                PulsarContextFactory.CreateTestContext(),
                configService: ConfigMock().Object,
                loc: null,
                usageTracker: null,
                healthMonitor: null,
                logService: null,
                Mock.Of<ITrayService>(),
                Mock.Of<IPluginExecutor>(),
                Mock.Of<IActionFeedbackService>(),
                feedbackPresenter: null,
                seededWindows: seeded);
        }

        private static ProcessWindowInfo Window(string processName, string title = "t")
            => new() { ProcessName = processName, Title = title, Handle = System.IntPtr.Zero };

        private static ProfilesConfig ConfigWithSwitchSlot(int slotNo, string app, string label)
        {
            var config = new ProfilesConfig();
            config.Profiles["Global"] = new ProcessProfile
            {
                SwitchMode = new System.Collections.Generic.List<PluginSlot>
                {
                    new()
                    {
                        Slot = slotNo,
                        PluginId = "com.pulsar.winswitcher",
                        Action = "switch",
                        Label = label,
                        Args = new Dictionary<string, string> { ["app"] = app }
                    }
                }
            };
            return config;
        }

        [Fact]
        public async Task LoadAsync_SeededWindows_BuildsFromSeedWithoutInventoryCall()
        {
            var coordinator = new Mock<IWindowInventoryCoordinator>();
            var provider = CreateProcessProvider(
                ConfigWithSwitchSlot(1, "notepad", "Notepad"),
                seeded: new List<ProcessWindowInfo> { Window("notepad") },
                coordinator: coordinator);

            await provider.LoadAsync();

            coordinator.Verify(c => c.GetActiveWindowsAsync(), Times.Never,
                "the seeded fast path must skip the desktop enumeration entirely");
            provider.TotalPages.Should().Be(1);
            provider.CurrentPage.Should().Be(0);
        }

        [Fact]
        public async Task LoadAsync_NoSeed_UsesInventoryCoordinator()
        {
            var coordinator = new Mock<IWindowInventoryCoordinator>();
            coordinator.Setup(c => c.GetActiveWindowsAsync())
                .ReturnsAsync(new List<ProcessWindowInfo> { Window("notepad") });
            var provider = CreateProcessProvider(
                ConfigWithSwitchSlot(1, "notepad", "Notepad"),
                seeded: null,
                coordinator: coordinator);

            await provider.LoadAsync();

            coordinator.Verify(c => c.GetActiveWindowsAsync(), Times.Once);
        }

        [Fact]
        public async Task RefreshVisuals_RunningUnconfiguredGroup_UsesProcessGroupStrategy()
        {
            var provider = CreateProcessProvider(
                new ProfilesConfig(),
                seeded: new List<ProcessWindowInfo> { Window("chrome") });
            await provider.LoadAsync();

            var visuals = VisualSlots(8);
            provider.RefreshVisuals(visuals, new SlotViewModel(0, 0, 0, 40));

            visuals[0].Type.Should().Be(SlotType.Process);
            visuals[0].ActionStrategy.Should().BeOfType<ProcessGroupStrategy>();
            visuals[0].CurrentOpacity.Should().Be(1.0);
            visuals[0].Label.Should().NotBeNullOrEmpty();
        }

        [Fact]
        public async Task RefreshVisuals_MultipleWindows_LabelCarriesCountAndBadge()
        {
            var provider = CreateProcessProvider(
                new ProfilesConfig(),
                seeded: new List<ProcessWindowInfo> { Window("chrome"), Window("chrome") });
            await provider.LoadAsync();

            var visuals = VisualSlots(8);
            provider.RefreshVisuals(visuals, new SlotViewModel(0, 0, 0, 40));

            visuals[0].Label.Should().EndWith("(2)");
            visuals[0].BadgeCount.Should().Be(2);
        }

        [Fact]
        public async Task RefreshVisuals_ConfiguredNotRunning_ShowsPlaceholderWithHalfOpacity()
        {
            var provider = CreateProcessProvider(
                ConfigWithSwitchSlot(1, "notepad", "My Editor"),
                seeded: new List<ProcessWindowInfo>());
            await provider.LoadAsync();

            var visuals = VisualSlots(8);
            provider.RefreshVisuals(visuals, new SlotViewModel(0, 0, 0, 40));

            visuals[0].Label.Should().Be("My Editor (Not Running)",
                "loc is null so NotRunningFormat falls back to '{0} (Not Running)'");
            visuals[0].CurrentOpacity.Should().Be(0.5);
            visuals[0].Type.Should().Be(SlotType.Process);
            visuals[0].ActionStrategy.Should().BeOfType<PluginActionStrategy>(
                "a configured-but-not-running slot still launches its plugin as fallback");
        }

        [Fact]
        public async Task RefreshVisuals_CenterSlot_IsNoOpSwitchLabel()
        {
            var provider = CreateProcessProvider(
                new ProfilesConfig(),
                seeded: new List<ProcessWindowInfo> { Window("chrome") });
            await provider.LoadAsync();

            var center = new SlotViewModel(0, 0, 0, 40);
            provider.RefreshVisuals(VisualSlots(8), center);

            center.Label.Should().Be("Switch", "loc is null so the page-0 center falls back to 'Switch'");
            center.ActionStrategy.Should().BeOfType<NoOpStrategy>();
            center.BadgeCount.Should().Be(0);
        }
    }
}
