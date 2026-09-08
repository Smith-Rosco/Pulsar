using System;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Pulsar.Core.Localization;
using Pulsar.Models.Settings;
using Pulsar.Services;
using Pulsar.Services.Interfaces;
using Pulsar.ViewModels.Settings;
using Wpf.Ui.Controls;
using Xunit;

namespace Pulsar.Tests.Services
{
    /// <summary>
    /// 临时页（动态标签页）生命周期测试（openspec 2026-09-08-dynamic-settings-tabs），
    /// 覆盖 settings-transient-pages / settings-shell-navigation / settings-dirty-state-guard
    /// 三个 spec 的场景：按类型单例、干净页导航离开自动回收、脏页保留与关闭确认、
    /// 语义分组末尾插入、导航项随注册增删、语言重建保留（目录层）。
    /// </summary>
    public class SettingsTransientPagesTests
    {
        private static SettingsPageCatalog CreateCatalog() =>
            new(new LocalizationService(NullLogger<LocalizationService>.Instance));

        private static SettingsPageRegistration CreateTransientRegistration(
            string id = SettingsPageIds.Gesture, string? groupId = SettingsPageGroupIds.System) =>
            new(id, "SettingsPage.Gesture.Title", id, SymbolRegular.Dialpad24, typeof(object),
                groupId: groupId, isTransient: true);

        private static SettingsShellViewModel CreateShell(
            SettingsPageCatalog catalog, ISettingsNavigationGuard guard)
        {
            var prefs = new Mock<ILocalUiPreferencesService>();
            prefs.Setup(p => p.GetLastOpenedSettingsPageId()).Returns((string?)null);
            return new SettingsShellViewModel(
                catalog,
                prefs.Object,
                guard,
                NullLogger<SettingsShellViewModel>.Instance);
        }

        /// <summary>守卫桩：脏状态可配，导航确认默认放行（Moq 默认 Task 返回 false 会拦死导航）。</summary>
        private static Mock<ISettingsNavigationGuard> CreateGuard(bool dirty = false)
        {
            var guard = new Mock<ISettingsNavigationGuard>();
            guard.Setup(g => g.HasUnsavedChanges).Returns(dirty);
            guard.Setup(g => g.CanNavigateAwayAsync(It.IsAny<string?>(), It.IsAny<bool>()))
                .ReturnsAsync(true);
            return guard;
        }

        private static SettingsTransientPageService CreateService(
            SettingsPageCatalog catalog, SettingsShellViewModel shell, ISettingsNavigationGuard guard) =>
            new(catalog, shell, guard, NullLogger<SettingsTransientPageService>.Instance);

        // ---------- 目录：注册 / 注销 / 单例 ----------

        [Fact]
        public void RegisterTransient_ShouldInsertAtEndOfSemanticGroup()
        {
            var catalog = CreateCatalog();

            catalog.RegisterTransient(CreateTransientRegistration(groupId: SettingsPageGroupIds.System));

            catalog.Pages.Select(p => p.Id).Should().Equal(
                SettingsPageIds.Slots,
                SettingsPageIds.Plugins,
                SettingsPageIds.General,
                SettingsPageIds.Appearance,
                SettingsPageIds.Analytics,
                SettingsPageIds.About,
                SettingsPageIds.Gesture);
        }

        [Fact]
        public void RegisterTransient_WorkbenchGroup_ShouldInsertBeforeSystemGroup()
        {
            var catalog = CreateCatalog();

            catalog.RegisterTransient(CreateTransientRegistration(groupId: SettingsPageGroupIds.Workbench));

            catalog.Pages.Select(p => p.Id).Should().Equal(
                SettingsPageIds.Slots,
                SettingsPageIds.Plugins,
                SettingsPageIds.Gesture,
                SettingsPageIds.General,
                SettingsPageIds.Appearance,
                SettingsPageIds.Analytics,
                SettingsPageIds.About);
        }

        [Fact]
        public void RegisterTransient_DuplicateId_ShouldBeSingletonAndNotFireAgain()
        {
            var catalog = CreateCatalog();
            catalog.RegisterTransient(CreateTransientRegistration());

            var registered = 0;
            catalog.TransientPageRegistered += _ => registered++;

            catalog.RegisterTransient(CreateTransientRegistration()).Should().BeFalse();
            registered.Should().Be(0, "按类型单例：重复注册不得新增条目或再次触发事件");
        }

        [Fact]
        public void RegisterTransient_ShouldFireRegisteredEvent()
        {
            var catalog = CreateCatalog();
            SettingsPageRegistration? received = null;
            catalog.TransientPageRegistered += r => received = r;

            catalog.RegisterTransient(CreateTransientRegistration());

            received.Should().NotBeNull();
            received!.Id.Should().Be(SettingsPageIds.Gesture);
        }

        [Fact]
        public void RegisterTransient_PermanentRegistration_ShouldThrow()
        {
            var catalog = CreateCatalog();

            var act = () => catalog.RegisterTransient(new SettingsPageRegistration(
                "fake", "k", "fake", SymbolRegular.Info24, typeof(object),
                groupId: SettingsPageGroupIds.System, isTransient: false));

            act.Should().Throw<ArgumentException>();
        }

        [Fact]
        public void UnregisterTransient_ShouldRemoveRegistration_AndFireEvent()
        {
            var catalog = CreateCatalog();
            catalog.RegisterTransient(CreateTransientRegistration());

            var removed = 0;
            catalog.TransientPageUnregistered += _ => removed++;

            catalog.UnregisterTransient(SettingsPageIds.Gesture).Should().BeTrue();
            removed.Should().Be(1);
            catalog.TryGetRegistration(SettingsPageIds.Gesture, out _).Should().BeFalse();
        }

        [Fact]
        public void UnregisterTransient_UnknownOrPermanentId_ShouldReturnFalse()
        {
            var catalog = CreateCatalog();

            catalog.UnregisterTransient("does-not-exist").Should().BeFalse();
            catalog.UnregisterTransient(SettingsPageIds.General).Should().BeFalse("常驻页不可注销");
        }

        [Fact]
        public void IsTransient_ShouldBeTrueOnlyForOpenTransientPages()
        {
            var catalog = CreateCatalog();

            catalog.IsTransient(SettingsPageIds.General).Should().BeFalse();
            catalog.IsTransient(SettingsPageIds.Gesture).Should().BeFalse("未注册的临时页类型不算打开");
            catalog.RegisterTransient(CreateTransientRegistration());
            catalog.IsTransient(SettingsPageIds.Gesture).Should().BeTrue();
        }

        // ---------- 服务：打开 / 关闭 / 导航离开回收 ----------

        [Fact]
        public async Task OpenTransientPageAsync_ShouldRegisterAndNavigate()
        {
            var catalog = CreateCatalog();
            var guard = CreateGuard();
            var shell = CreateShell(catalog, guard.Object);
            var service = CreateService(catalog, shell, guard.Object);
            service.RegisterDefinition(CreateTransientRegistration());

            var result = await service.OpenTransientPageAsync(SettingsPageIds.Gesture);

            result.Should().BeTrue();
            shell.CurrentPageId.Should().Be(SettingsPageIds.Gesture);
            catalog.IsTransient(SettingsPageIds.Gesture).Should().BeTrue();
        }

        [Fact]
        public async Task OpenTransientPageAsync_UnknownType_ShouldReject()
        {
            var catalog = CreateCatalog();
            var shell = CreateShell(catalog, new Mock<ISettingsNavigationGuard>().Object);
            var service = CreateService(catalog, shell, new Mock<ISettingsNavigationGuard>().Object);

            var result = await service.OpenTransientPageAsync("no-such-type");

            result.Should().BeFalse();
            shell.CurrentPageId.Should().NotBe("no-such-type");
        }

        [Fact]
        public async Task OpenTransientPageAsync_RepeatedTrigger_ShouldActivateExistingInstance()
        {
            var catalog = CreateCatalog();
            var guard = CreateGuard();
            var shell = CreateShell(catalog, guard.Object);
            var service = CreateService(catalog, shell, guard.Object);
            service.RegisterDefinition(CreateTransientRegistration());

            await service.OpenTransientPageAsync(SettingsPageIds.Gesture);
            // 用户跳到别的常驻页（临时页仍开着）
            await shell.NavigateAsync(SettingsPageIds.General, userInitiated: true);
            var countBefore = catalog.Pages.Count;
            var secondOpen = await service.OpenTransientPageAsync(SettingsPageIds.Gesture);

            secondOpen.Should().BeTrue();
            catalog.Pages.Count.Should().Be(countBefore, "按类型单例：重复触发不得新增条目");
            shell.CurrentPageId.Should().Be(SettingsPageIds.Gesture, "重复触发应激活既有实例");
        }

        [Fact]
        public async Task NotifyNavigatedAwayFrom_CleanTransientPage_ShouldRecycle()
        {
            var catalog = CreateCatalog();
            var guard = new Mock<ISettingsNavigationGuard>();
            guard.Setup(g => g.HasUnsavedChanges).Returns(false);
            var shell = CreateShell(catalog, guard.Object);
            var service = CreateService(catalog, shell, guard.Object);
            service.RegisterDefinition(CreateTransientRegistration());
            await service.OpenTransientPageAsync(SettingsPageIds.Gesture);

            service.NotifyNavigatedAwayFrom(SettingsPageIds.Gesture);

            catalog.IsTransient(SettingsPageIds.Gesture).Should().BeFalse("干净临时页导航离开即回收");
            catalog.TryGetRegistration(SettingsPageIds.Gesture, out _).Should().BeFalse(
                "回收后导航到该 id 应被安全拒绝（settings-shell-navigation 场景）");
        }

        [Fact]
        public async Task NotifyNavigatedAwayFrom_DirtyTransientPage_ShouldKeepOpen()
        {
            var catalog = CreateCatalog();
            var guard = new Mock<ISettingsNavigationGuard>();
            guard.Setup(g => g.HasUnsavedChanges).Returns(true);
            var shell = CreateShell(catalog, guard.Object);
            var service = CreateService(catalog, shell, guard.Object);
            service.RegisterDefinition(CreateTransientRegistration());
            await service.OpenTransientPageAsync(SettingsPageIds.Gesture);

            service.NotifyNavigatedAwayFrom(SettingsPageIds.Gesture);

            catalog.IsTransient(SettingsPageIds.Gesture).Should().BeTrue("脏临时页保留不回收");
        }

        [Fact]
        public void NotifyNavigatedAwayFrom_PermanentOrUnknownSource_ShouldBeNoOp()
        {
            var catalog = CreateCatalog();
            var service = CreateService(
                catalog,
                CreateShell(catalog, new Mock<ISettingsNavigationGuard>().Object),
                new Mock<ISettingsNavigationGuard>().Object);

            var act = () => service.NotifyNavigatedAwayFrom(SettingsPageIds.General);

            act.Should().NotThrow();
            catalog.Pages.Count.Should().Be(6);
        }

        [Fact]
        public async Task CloseTransientPageAsync_Clean_ShouldRecycleImmediately()
        {
            var catalog = CreateCatalog();
            var guard = CreateGuard();
            var shell = CreateShell(catalog, guard.Object);
            var service = CreateService(catalog, shell, guard.Object);
            // 直接经目录注册 + 免确认导航进入临时页，保证"关闭"路径本身零守卫调用。
            catalog.RegisterTransient(CreateTransientRegistration());
            await shell.NavigateAsync(SettingsPageIds.Gesture, userInitiated: false);

            var result = await service.CloseTransientPageAsync(SettingsPageIds.Gesture);

            result.Should().BeTrue();
            catalog.IsTransient(SettingsPageIds.Gesture).Should().BeFalse();
            guard.Verify(g => g.CanNavigateAwayAsync(It.IsAny<string?>(), It.IsAny<bool>()), Times.Never,
                "干净页关闭不需要确认");
        }

        [Fact]
        public async Task CloseTransientPageAsync_Dirty_Cancelled_ShouldKeepOpen()
        {
            var catalog = CreateCatalog();
            var guard = new Mock<ISettingsNavigationGuard>();
            guard.Setup(g => g.HasUnsavedChanges).Returns(true);
            guard.Setup(g => g.CanNavigateAwayAsync(It.IsAny<string?>(), It.IsAny<bool>()))
                .ReturnsAsync(false);
            var shell = CreateShell(catalog, guard.Object);
            var service = CreateService(catalog, shell, guard.Object);
            catalog.RegisterTransient(CreateTransientRegistration());
            await shell.NavigateAsync(SettingsPageIds.Gesture, userInitiated: false);

            var result = await service.CloseTransientPageAsync(SettingsPageIds.Gesture);

            result.Should().BeFalse("用户取消关闭：脏临时页保持打开");
            catalog.IsTransient(SettingsPageIds.Gesture).Should().BeTrue();
        }

        [Fact]
        public async Task CloseTransientPageAsync_Dirty_Resolved_ShouldRecycle()
        {
            var catalog = CreateCatalog();
            var guard = new Mock<ISettingsNavigationGuard>();
            guard.Setup(g => g.HasUnsavedChanges).Returns(true);
            guard.Setup(g => g.CanNavigateAwayAsync(It.IsAny<string?>(), It.IsAny<bool>()))
                .ReturnsAsync(true);
            var shell = CreateShell(catalog, guard.Object);
            var service = CreateService(catalog, shell, guard.Object);
            catalog.RegisterTransient(CreateTransientRegistration());
            await shell.NavigateAsync(SettingsPageIds.Gesture, userInitiated: false);

            var result = await service.CloseTransientPageAsync(SettingsPageIds.Gesture);

            result.Should().BeTrue("保存/放弃后完成回收");
            catalog.IsTransient(SettingsPageIds.Gesture).Should().BeFalse();
        }

        // ---------- Shell：脏守卫分流与 LastOpened 偏好 ----------

        [Fact]
        public async Task NavigateAsync_FromTransientSource_WithDirtyState_ShouldSkipGuard()
        {
            var catalog = CreateCatalog();
            catalog.RegisterTransient(CreateTransientRegistration());
            var guard = new Mock<ISettingsNavigationGuard>();
            guard.Setup(g => g.HasUnsavedChanges).Returns(true);
            var shell = new SettingsShellViewModel(
                catalog,
                new Pulsar.Tests.TestInfrastructure.DelegateUiPreferences(
                    () => null,
                    _ => { }),
                guard.Object,
                NullLogger<SettingsShellViewModel>.Instance);
            await shell.NavigateAsync(SettingsPageIds.Gesture, userInitiated: false);

            var result = await shell.NavigateAsync(SettingsPageIds.General, userInitiated: true);

            result.Should().BeTrue("从脏临时页导航离开不提示（settings-dirty-state-guard delta）");
            guard.Verify(
                g => g.CanNavigateAwayAsync(It.IsAny<string?>(), It.IsAny<bool>()),
                Times.Never);
        }

        [Fact]
        public async Task NavigateAsync_FromPermanentSource_WithDirtyState_ShouldPromptGuard()
        {
            var catalog = CreateCatalog();
            var guard = new Mock<ISettingsNavigationGuard>();
            guard.Setup(g => g.HasUnsavedChanges).Returns(true);
            guard.Setup(g => g.CanNavigateAwayAsync(It.IsAny<string?>(), It.IsAny<bool>()))
                .ReturnsAsync(true);
            var shell = CreateShell(catalog, guard.Object);
            await shell.NavigateAsync(SettingsPageIds.General, userInitiated: false);

            var result = await shell.NavigateAsync(SettingsPageIds.Analytics, userInitiated: true);

            result.Should().BeTrue();
            guard.Verify(
                g => g.CanNavigateAwayAsync(It.IsAny<string?>(), It.IsAny<bool>()),
                Times.Once,
                "常驻页之间的导航维持原有 save/discard/cancel 提示");
        }

        [Fact]
        public async Task NavigateAsync_ToTransientPage_ShouldNotPersistLastOpenedPreference()
        {
            var catalog = CreateCatalog();
            var guard = new Mock<ISettingsNavigationGuard>();
            guard.Setup(g => g.HasUnsavedChanges).Returns(false);
            var lastOpened = (string?)null;
            var shell = new SettingsShellViewModel(
                catalog,
                new Pulsar.Tests.TestInfrastructure.DelegateUiPreferences(
                    () => lastOpened,
                    id => lastOpened = id),
                guard.Object,
                NullLogger<SettingsShellViewModel>.Instance);
            var service = CreateService(catalog, shell, guard.Object);
            service.RegisterDefinition(CreateTransientRegistration());

            await service.OpenTransientPageAsync(SettingsPageIds.Gesture);

            lastOpened.Should().BeNull("临时页是会话级的，不能作为'上次打开的设置页'跨会话恢复");
        }
    }
}

namespace Pulsar.Tests.TestInfrastructure
{
    using Pulsar.Services.Interfaces;

    /// <summary>委托版 UI 偏好桩：直接观察 SetLastOpenedSettingsPageId 调用。</summary>
    public sealed class DelegateUiPreferences : ILocalUiPreferencesService
    {
        private readonly Func<string?> _getLast;
        private readonly Action<string?> _setLast;

        public DelegateUiPreferences(Func<string?> getLast, Action<string?> setLast)
        {
            _getLast = getLast;
            _setLast = setLast;
        }

        public LocalUiPreferences Load() => new LocalUiPreferences();

        public string? GetLastOpenedSettingsPageId() => _getLast();

        public void SetLastOpenedSettingsPageId(string? pageId) => _setLast(pageId);
    }
}
