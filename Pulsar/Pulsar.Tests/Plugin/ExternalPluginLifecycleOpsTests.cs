using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Pulsar.Core.Plugin;
using Pulsar.Core.Plugin.Metadata;
using Pulsar.Models;
using Pulsar.Services;
using Pulsar.Services.Interfaces;
using Xunit;

namespace Pulsar.Tests.Plugin
{
    /// <summary>
    /// 外部插件生命周期运维模块（ExternalPluginLifecycleOps）时序测试。
    /// 时序顺序是最近安装/卸载/启用 bug 的根源，这里用调用记录锁死顺序：
    /// - 安装：InstallFromFile → RefreshDiscovery → GrantPermissions → GetOrActivate
    /// - 卸载：GrantPermissions([]) → DeactivatePlugin → UninstallAsync（停用失败中止删文件）
    /// - 启用：SetPluginState → GetOrActivate
    /// </summary>
    public class ExternalPluginLifecycleOpsTests
    {
        private const string PluginId = "test.external.plugin";

        private static PluginDescriptor CreateExternalDescriptor(IReadOnlyList<string>? permissions = null)
        {
            return new PluginDescriptor
            {
                Id = PluginId,
                DisplayName = PluginId,
                Version = "1.0.0",
                Author = "Test",
                Description = "Test descriptor",
                Icon = "T",
                CanDisable = true,
                Tier = PluginTier.Extension,
                IsExternal = true,
                Permissions = permissions ?? Array.Empty<string>(),
                ImplementationType = typeof(ExternalPluginLifecycleOpsTests),
                Dependencies = new List<string>(),
                Metadata = new PluginMetadata
                {
                    Id = PluginId,
                    Display = new DisplayInfo
                    {
                        Name = PluginId,
                        Description = "Test descriptor",
                        IconKey = "T",
                        Category = "Tests",
                        Version = "1.0.0",
                        Author = "Test",
                        License = "MIT"
                    },
                    Schema = null,
                    UI = new UIHints
                    {
                        Badge = "Test",
                        AccentColor = "#4A90E2",
                        ShowInQuickAccess = false,
                        SortOrder = 0
                    },
                    Capabilities = new PluginCapabilities()
                },
                IsConfigurable = false
            };
        }

        private static (ExternalPluginLifecycleOps ops, Mock<IPluginRegistry> registry, Mock<IPluginPackageManager> pkg) CreateOps()
        {
            var registry = new Mock<IPluginRegistry>(MockBehavior.Strict);
            var pkg = new Mock<IPluginPackageManager>(MockBehavior.Loose);
            var ops = new ExternalPluginLifecycleOps(registry.Object, pkg.Object, NullLogger<ExternalPluginLifecycleOps>.Instance);
            return (ops, registry, pkg);
        }

        [Fact]
        public async Task Install_WithPermissions_RefreshesThenGrantsThenActivates_InOrder()
        {
            var (ops, registry, pkg) = CreateOps();
            var approved = new[] { PluginPermissions.InputInject };
            var calls = new List<string>();

            pkg.Setup(x => x.InstallFromFileAsync("pkg.zip", It.IsAny<IReadOnlyCollection<string>?>(), It.IsAny<CancellationToken>()))
                .Callback(() => calls.Add("files"))
                .ReturnsAsync(PluginOperationResult.Successful(PluginId, PluginOperationType.Install, TimeSpan.FromSeconds(1)));

            // 时序：refresh → grant → activate（最近 bug 的根源：顺序错了 grant 被拒为 unknown plugin）
            registry.Setup(x => x.RefreshDiscoveryAsync())
                .Callback(() => calls.Add("refresh")).Returns(Task.CompletedTask);
            registry.Setup(x => x.GrantPermissionsAsync(PluginId, It.IsAny<IEnumerable<string>>()))
                .Callback(() => calls.Add("grant")).Returns(Task.CompletedTask);
            registry.Setup(x => x.GetOrActivatePluginAsync(PluginId))
                .Callback(() => calls.Add("activate")).ReturnsAsync((IPulsarPlugin?)null);

            var result = await ops.InstallAsync("pkg.zip", approved);

            result.Success.Should().BeTrue();
            result.Phase.Should().Be(ExternalPluginOpPhase.Activated);
            result.Warning.Should().BeNull();
            calls.Should().Equal("files", "refresh", "grant", "activate");
        }

        [Fact]
        public async Task Install_WithoutPermissions_SkipsGrantButStillActivates()
        {
            var (ops, registry, pkg) = CreateOps();
            var calls = new List<string>();

            pkg.Setup(x => x.InstallFromFileAsync("pkg.zip", It.IsAny<IReadOnlyCollection<string>?>(), It.IsAny<CancellationToken>()))
                .Callback(() => calls.Add("files"))
                .ReturnsAsync(PluginOperationResult.Successful(PluginId, PluginOperationType.Install, TimeSpan.FromSeconds(1)));
            registry.Setup(x => x.RefreshDiscoveryAsync())
                .Callback(() => calls.Add("refresh")).Returns(Task.CompletedTask);
            registry.Setup(x => x.GetOrActivatePluginAsync(PluginId))
                .Callback(() => calls.Add("activate")).ReturnsAsync((IPulsarPlugin?)null);

            var result = await ops.InstallAsync("pkg.zip", Array.Empty<string>());

            result.Success.Should().BeTrue();
            result.Phase.Should().Be(ExternalPluginOpPhase.Activated);
            calls.Should().Equal("files", "refresh", "activate");
            registry.Verify(x => x.GrantPermissionsAsync(It.IsAny<string>(), It.IsAny<IEnumerable<string>>()), Times.Never);
        }

        [Fact]
        public async Task Install_WhenGrantFails_ReturnsSuccessWithWarning_AndDoesNotActivate()
        {
            var (ops, registry, pkg) = CreateOps();
            var approved = new[] { PluginPermissions.ClipboardRead };

            pkg.Setup(x => x.InstallFromFileAsync("pkg.zip", It.IsAny<IReadOnlyCollection<string>?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(PluginOperationResult.Successful(PluginId, PluginOperationType.Install, TimeSpan.FromSeconds(1)));
            registry.Setup(x => x.RefreshDiscoveryAsync()).Returns(Task.CompletedTask);
            registry.Setup(x => x.GrantPermissionsAsync(PluginId, It.IsAny<IEnumerable<string>>()))
                .ThrowsAsync(new InvalidOperationException("unknown plugin"));

            var result = await ops.InstallAsync("pkg.zip", approved);

            result.Success.Should().BeTrue("部分成功不回滚");
            result.Phase.Should().Be(ExternalPluginOpPhase.Discovered);
            result.Warning.Should().NotBeNull();
            registry.Verify(x => x.GetOrActivatePluginAsync(It.IsAny<string>()), Times.Never,
                "授权失败后不应继续激活，避免未授权插件运行");
        }

        [Fact]
        public async Task Uninstall_RevokesThenDeactivatesThenDeletesFiles_InOrder()
        {
            var (ops, registry, pkg) = CreateOps();
            var descriptor = CreateExternalDescriptor(new[] { PluginPermissions.InputInject });
            var calls = new List<string>();
            registry.Setup(x => x.GetDescriptor(PluginId)).Returns(descriptor);

            registry.Setup(x => x.GrantPermissionsAsync(PluginId, It.IsAny<IEnumerable<string>>()))
                .Callback(() => calls.Add("revoke")).Returns(Task.CompletedTask);
            registry.Setup(x => x.DeactivatePluginAsync(PluginId))
                .Callback(() => calls.Add("deactivate")).Returns(Task.CompletedTask);
            pkg.Setup(x => x.UninstallAsync(PluginId, It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .Callback(() => calls.Add("delete"))
                .ReturnsAsync(PluginOperationResult.Successful(PluginId, PluginOperationType.Uninstall, TimeSpan.FromSeconds(1)));

            var result = await ops.UninstallAsync(PluginId);

            result.Success.Should().BeTrue();
            result.Phase.Should().Be(ExternalPluginOpPhase.Uninstalled);
            result.Warning.Should().BeNull();
            calls.Should().Equal("revoke", "deactivate", "delete");
        }

        [Fact]
        public async Task Uninstall_WhenDeactivateFails_AbortsFileDelete()
        {
            var (ops, registry, pkg) = CreateOps();
            var descriptor = CreateExternalDescriptor(new[] { PluginPermissions.InputInject });
            registry.Setup(x => x.GetDescriptor(PluginId)).Returns(descriptor);
            registry.Setup(x => x.GrantPermissionsAsync(PluginId, It.IsAny<IEnumerable<string>>())).Returns(Task.CompletedTask);
            registry.Setup(x => x.DeactivatePluginAsync(PluginId)).ThrowsAsync(new InvalidOperationException("ALC still locked"));

            var result = await ops.UninstallAsync(PluginId);

            result.Success.Should().BeFalse();
            result.ErrorCode.Should().Be("DeactivateFailed");
            pkg.Verify(x => x.UninstallAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never,
                "停用失败意味着 DLL 仍被锁定，删文件会留下残骸目录");
        }

        [Fact]
        public async Task Uninstall_WhenRevokeFails_ContinuesAndReportsWarning()
        {
            var (ops, registry, pkg) = CreateOps();
            var descriptor = CreateExternalDescriptor(new[] { PluginPermissions.InputInject });
            registry.Setup(x => x.GetDescriptor(PluginId)).Returns(descriptor);
            registry.Setup(x => x.GrantPermissionsAsync(PluginId, It.IsAny<IEnumerable<string>>()))
                .ThrowsAsync(new InvalidOperationException("save failed"));
            registry.Setup(x => x.DeactivatePluginAsync(PluginId)).Returns(Task.CompletedTask);
            pkg.Setup(x => x.UninstallAsync(PluginId, It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(PluginOperationResult.Successful(PluginId, PluginOperationType.Uninstall, TimeSpan.FromSeconds(1)));

            var result = await ops.UninstallAsync(PluginId);

            result.Success.Should().BeTrue("revoke 失败不应中止卸载，残留授权对已删插件无害");
            result.Warning.Should().NotBeNull();
        }

        [Fact]
        public async Task SetEnabled_Enabled_ActivatesAfterStateChange()
        {
            var (ops, registry, _) = CreateOps();
            var calls = new List<string>();
            registry.Setup(x => x.SetPluginStateAsync(PluginId, true))
                .Callback(() => calls.Add("setstate")).Returns(Task.CompletedTask);
            registry.Setup(x => x.GetOrActivatePluginAsync(PluginId))
                .Callback(() => calls.Add("activate")).ReturnsAsync((IPulsarPlugin?)null);

            var result = await ops.SetEnabledAsync(PluginId, true);

            result.Success.Should().BeTrue();
            result.Phase.Should().Be(ExternalPluginOpPhase.Activated);
            calls.Should().Equal("setstate", "activate");
        }

        [Fact]
        public async Task SetEnabled_Disabled_DoesNotActivate()
        {
            var (ops, registry, _) = CreateOps();
            registry.Setup(x => x.SetPluginStateAsync(PluginId, false)).Returns(Task.CompletedTask);

            var result = await ops.SetEnabledAsync(PluginId, false);

            result.Success.Should().BeTrue();
            result.Phase.Should().Be(ExternalPluginOpPhase.Deactivated);
            registry.Verify(x => x.GetOrActivatePluginAsync(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task GrantAsync_WithoutManifestPermissions_IsIdempotentNoOp()
        {
            var (ops, registry, _) = CreateOps();
            var descriptor = CreateExternalDescriptor(); // 无权限
            registry.Setup(x => x.GetDescriptor(PluginId)).Returns(descriptor);

            var result = await ops.GrantAsync(PluginId);

            result.Success.Should().BeTrue();
            registry.Verify(x => x.GrantPermissionsAsync(It.IsAny<string>(), It.IsAny<IEnumerable<string>>()), Times.Never);
        }

        [Fact]
        public async Task GrantAsync_UnknownPlugin_Fails()
        {
            var (ops, registry, _) = CreateOps();
            registry.Setup(x => x.GetDescriptor(PluginId)).Returns((PluginDescriptor?)null);

            var result = await ops.GrantAsync(PluginId);

            result.Success.Should().BeFalse();
            result.ErrorCode.Should().Be("UnknownPlugin");
        }

        [Fact]
        public async Task PrepareInstallAsync_ReturnsManifestAndPendingPermissions()
        {
            var (ops, _, pkg) = CreateOps();
            var manifest = new PluginManifest
            {
                Id = PluginId,
                DisplayName = "Test",
                Permissions = new List<string> { PluginPermissions.WindowFocus }
            };
            pkg.Setup(x => x.InspectPackageAsync("pkg.zip", It.IsAny<CancellationToken>()))
                .ReturnsAsync(PluginPackageInspectionResult.Succeeded(manifest));

            var preparation = await ops.PrepareInstallAsync("pkg.zip");

            preparation.Success.Should().BeTrue();
            preparation.Manifest.Should().BeSameAs(manifest);
            preparation.PendingPermissions.Should().Contain(PluginPermissions.WindowFocus);
        }
    }
}
