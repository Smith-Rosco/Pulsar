// [Path]: Pulsar.Tests/Plugin/Security/PermissionInterceptorTests.cs

using Xunit;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Pulsar.Core.Plugin.Security;
using System;
using System.Threading.Tasks;

namespace Pulsar.Tests.Plugin.Security
{
    /// <summary>
    /// 权限拦截器单元测试
    /// </summary>
    public class PermissionInterceptorTests
    {
        private readonly Mock<ILogger<PermissionInterceptor>> _loggerMock;
        private readonly PermissionInterceptor _interceptor;

        public PermissionInterceptorTests()
        {
            _loggerMock = new Mock<ILogger<PermissionInterceptor>>();
            _interceptor = new PermissionInterceptor(_loggerMock.Object);
        }

        [Fact]
        public void RegisterPluginPermissions_ShouldStoreRequestedPermissions()
        {
            // Arrange
            var pluginId = "test.plugin";
            var permissions = PluginPermission.ReadClipboard | PluginPermission.WriteClipboard;

            // Act
            _interceptor.RegisterPluginPermissions(pluginId, permissions);

            // Assert
            var requested = _interceptor.GetRequestedPermissions(pluginId);
            requested.Should().Be(permissions);
        }

        [Fact]
        public void GrantPermissions_ShouldAllowPermissionCheck()
        {
            // Arrange
            var pluginId = "test.plugin";
            var permission = PluginPermission.ReadClipboard;

            // Act
            _interceptor.GrantPermissions(pluginId, permission);

            // Assert
            _interceptor.HasPermission(pluginId, permission).Should().BeTrue();
        }

        [Fact]
        public void HasPermission_WithoutGrant_ShouldReturnFalse()
        {
            // Arrange
            var pluginId = "test.plugin";
            var permission = PluginPermission.ReadClipboard;

            // Act & Assert
            _interceptor.HasPermission(pluginId, permission).Should().BeFalse();
        }

        [Fact]
        public void CheckPermission_WithoutGrant_ShouldThrowUnauthorizedException()
        {
            // Arrange
            var pluginId = "test.plugin";
            var permission = PluginPermission.ReadClipboard;

            // Act
            Action act = () => _interceptor.CheckPermission(pluginId, permission, "TestOperation");

            // Assert
            act.Should().Throw<UnauthorizedAccessException>()
                .WithMessage("*does not have permission*");
        }

        [Fact]
        public void CheckPermission_WithGrant_ShouldNotThrow()
        {
            // Arrange
            var pluginId = "test.plugin";
            var permission = PluginPermission.ReadClipboard;
            _interceptor.GrantPermissions(pluginId, permission);

            // Act
            Action act = () => _interceptor.CheckPermission(pluginId, permission, "TestOperation");

            // Assert
            act.Should().NotThrow();
        }

        [Fact]
        public void RevokePermissions_ShouldRemovePermission()
        {
            // Arrange
            var pluginId = "test.plugin";
            var permissions = PluginPermission.ReadClipboard | PluginPermission.WriteClipboard;
            _interceptor.GrantPermissions(pluginId, permissions);

            // Act
            _interceptor.RevokePermissions(pluginId, PluginPermission.WriteClipboard);

            // Assert
            _interceptor.HasPermission(pluginId, PluginPermission.ReadClipboard).Should().BeTrue();
            _interceptor.HasPermission(pluginId, PluginPermission.WriteClipboard).Should().BeFalse();
        }

        [Fact]
        public void DenyPermission_ShouldPreventPermissionGrant()
        {
            // Arrange
            var pluginId = "test.plugin";
            var permission = PluginPermission.ReadClipboard;

            // Act
            _interceptor.DenyPermission(pluginId, permission);
            _interceptor.GrantPermissions(pluginId, permission);

            // Assert
            _interceptor.HasPermission(pluginId, permission).Should().BeFalse();
        }

        [Fact]
        public void GrantPermissions_ShouldCombineWithExisting()
        {
            // Arrange
            var pluginId = "test.plugin";
            _interceptor.GrantPermissions(pluginId, PluginPermission.ReadClipboard);

            // Act
            _interceptor.GrantPermissions(pluginId, PluginPermission.WriteClipboard);

            // Assert
            _interceptor.HasPermission(pluginId, PluginPermission.ReadClipboard).Should().BeTrue();
            _interceptor.HasPermission(pluginId, PluginPermission.WriteClipboard).Should().BeTrue();
        }

        [Fact]
        public void ClearPluginPermissions_ShouldRemoveAllPermissions()
        {
            // Arrange
            var pluginId = "test.plugin";
            _interceptor.RegisterPluginPermissions(pluginId, PluginPermission.ReadClipboard);
            _interceptor.GrantPermissions(pluginId, PluginPermission.ReadClipboard);

            // Act
            _interceptor.ClearPluginPermissions(pluginId);

            // Assert
            _interceptor.GetRequestedPermissions(pluginId).Should().Be(PluginPermission.None);
            _interceptor.GetGrantedPermissions(pluginId).Should().Be(PluginPermission.None);
        }

        [Fact]
        public void GetAllPermissionSummaries_ShouldReturnAllPlugins()
        {
            // Arrange
            _interceptor.RegisterPluginPermissions("plugin1", PluginPermission.ReadClipboard);
            _interceptor.GrantPermissions("plugin1", PluginPermission.ReadClipboard);
            _interceptor.RegisterPluginPermissions("plugin2", PluginPermission.WriteClipboard);

            // Act
            var summaries = _interceptor.GetAllPermissionSummaries();

            // Assert
            summaries.Should().HaveCount(2);
            summaries.Should().ContainKey("plugin1");
            summaries.Should().ContainKey("plugin2");
        }

        [Fact]
        public async Task RequestPermissionAsync_WithGrant_ShouldReturnTrue()
        {
            // Arrange
            var pluginId = "test.plugin";
            var permission = PluginPermission.ReadClipboard;
            
            // Subscribe to event and auto-grant
            _interceptor.PermissionRequested += (sender, args) =>
            {
                args.Complete(granted: true, remember: false);
            };

            // Act
            var result = await _interceptor.RequestPermissionAsync(pluginId, permission, "Test reason");

            // Assert
            result.Should().BeTrue();
            _interceptor.HasPermission(pluginId, permission).Should().BeTrue();
        }

        [Fact]
        public async Task RequestPermissionAsync_WithDeny_ShouldReturnFalse()
        {
            // Arrange
            var pluginId = "test.plugin";
            var permission = PluginPermission.ReadClipboard;
            
            // Subscribe to event and deny
            _interceptor.PermissionRequested += (sender, args) =>
            {
                args.Complete(granted: false, remember: false);
            };

            // Act
            var result = await _interceptor.RequestPermissionAsync(pluginId, permission, "Test reason");

            // Assert
            result.Should().BeFalse();
            _interceptor.HasPermission(pluginId, permission).Should().BeFalse();
        }

        [Fact]
        public async Task RequestPermissionAsync_AlreadyGranted_ShouldReturnTrueImmediately()
        {
            // Arrange
            var pluginId = "test.plugin";
            var permission = PluginPermission.ReadClipboard;
            _interceptor.GrantPermissions(pluginId, permission);

            // Act
            var result = await _interceptor.RequestPermissionAsync(pluginId, permission, "Test reason");

            // Assert
            result.Should().BeTrue();
        }

        [Fact]
        public async Task RequestPermissionAsync_PreviouslyDenied_ShouldReturnFalseImmediately()
        {
            // Arrange
            var pluginId = "test.plugin";
            var permission = PluginPermission.ReadClipboard;
            _interceptor.DenyPermission(pluginId, permission);

            // Act
            var result = await _interceptor.RequestPermissionAsync(pluginId, permission, "Test reason");

            // Assert
            result.Should().BeFalse();
        }
    }
}
