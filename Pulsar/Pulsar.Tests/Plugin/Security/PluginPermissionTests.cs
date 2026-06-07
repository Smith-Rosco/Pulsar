// [Path]: Pulsar.Tests/Plugin/Security/PluginPermissionTests.cs

using Xunit;
using FluentAssertions;
using Pulsar.Core.Plugin.Security;

namespace Pulsar.Tests.Plugin.Security
{
    /// <summary>
    /// 插件权限枚举和扩展方法测试
    /// </summary>
    public class PluginPermissionTests
    {
        [Fact]
        public void HasPermission_WithSinglePermission_ShouldReturnTrue()
        {
            // Arrange
            var granted = PluginPermission.ReadClipboard;
            var required = PluginPermission.ReadClipboard;

            // Act
            var result = granted.HasPermission(required);

            // Assert
            result.Should().BeTrue();
        }

        [Fact]
        public void HasPermission_WithMultiplePermissions_ShouldReturnTrue()
        {
            // Arrange
            var granted = PluginPermission.ReadClipboard | PluginPermission.WriteClipboard;
            var required = PluginPermission.ReadClipboard;

            // Act
            var result = granted.HasPermission(required);

            // Assert
            result.Should().BeTrue();
        }

        [Fact]
        public void HasPermission_WithoutPermission_ShouldReturnFalse()
        {
            // Arrange
            var granted = PluginPermission.ReadClipboard;
            var required = PluginPermission.WriteClipboard;

            // Act
            var result = granted.HasPermission(required);

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public void HasPermission_WithCombinedRequired_ShouldCheckAll()
        {
            // Arrange
            var granted = PluginPermission.ReadClipboard | PluginPermission.WriteClipboard;
            var required = PluginPermission.ReadClipboard | PluginPermission.WriteClipboard;

            // Act
            var result = granted.HasPermission(required);

            // Assert
            result.Should().BeTrue();
        }

        [Fact]
        public void HasPermission_WithPartialGrant_ShouldReturnFalse()
        {
            // Arrange
            var granted = PluginPermission.ReadClipboard;
            var required = PluginPermission.ReadClipboard | PluginPermission.WriteClipboard;

            // Act
            var result = granted.HasPermission(required);

            // Assert
            result.Should().BeFalse();
        }

        [Theory]
        [InlineData(PluginPermission.ReadWindowInfo, "Read Window Information")]
        [InlineData(PluginPermission.ReadClipboard, "Read Clipboard")]
        [InlineData(PluginPermission.WriteClipboard, "Write Clipboard")]
        [InlineData(PluginPermission.StartProcess, "Start Process")]
        public void GetDisplayName_ShouldReturnCorrectName(PluginPermission permission, string expectedName)
        {
            // Act
            var displayName = permission.GetDisplayName();

            // Assert
            displayName.Should().Be(expectedName);
        }

        [Theory]
        [InlineData(PluginPermission.ReadWindowInfo, PermissionRiskLevel.Low)]
        [InlineData(PluginPermission.ReadClipboard, PermissionRiskLevel.Medium)]
        [InlineData(PluginPermission.StartProcess, PermissionRiskLevel.High)]
        [InlineData(PluginPermission.AccessCredentials, PermissionRiskLevel.Critical)]
        public void GetRiskLevel_ShouldReturnCorrectLevel(PluginPermission permission, PermissionRiskLevel expectedLevel)
        {
            // Act
            var riskLevel = permission.GetRiskLevel();

            // Assert
            riskLevel.Should().Be(expectedLevel);
        }

        [Fact]
        public void GetDescription_ShouldReturnNonEmptyString()
        {
            // Arrange
            var permission = PluginPermission.ReadClipboard;

            // Act
            var description = permission.GetDescription();

            // Assert
            description.Should().NotBeNullOrEmpty();
            description.Should().Contain("clipboard");
        }

        [Fact]
        public void PermissionSets_Basic_ShouldContainBasicPermissions()
        {
            // Act
            var basic = PermissionSets.Basic;

            // Assert
            basic.HasPermission(PluginPermission.ReadWindowInfo).Should().BeTrue();
            basic.HasPermission(PluginPermission.ReadProcessPath).Should().BeTrue();
            basic.HasPermission(PluginPermission.ShowNotification).Should().BeTrue();
            basic.HasPermission(PluginPermission.ReadClipboard).Should().BeFalse();
        }

        [Fact]
        public void PermissionSets_Standard_ShouldContainStandardPermissions()
        {
            // Act
            var standard = PermissionSets.Standard;

            // Assert
            standard.HasPermission(PluginPermission.ReadWindowInfo).Should().BeTrue();
            standard.HasPermission(PluginPermission.ReadClipboard).Should().BeTrue();
            standard.HasPermission(PluginPermission.WriteClipboard).Should().BeTrue();
            standard.HasPermission(PluginPermission.SimulateKeyboard).Should().BeTrue();
            standard.HasPermission(PluginPermission.StartProcess).Should().BeFalse();
        }

        [Fact]
        public void PermissionSets_Advanced_ShouldContainAdvancedPermissions()
        {
            // Act
            var advanced = PermissionSets.Advanced;

            // Assert
            advanced.HasPermission(PluginPermission.ReadClipboard).Should().BeTrue();
            advanced.HasPermission(PluginPermission.StartProcess).Should().BeTrue();
            advanced.HasPermission(PluginPermission.ReadFileSystem).Should().BeTrue();
            advanced.HasPermission(PluginPermission.AccessCredentials).Should().BeFalse();
        }

        [Fact]
        public void PermissionSets_Full_ShouldContainFullPermissions()
        {
            // Act
            var full = PermissionSets.Full;

            // Assert
            full.HasPermission(PluginPermission.ReadClipboard).Should().BeTrue();
            full.HasPermission(PluginPermission.StartProcess).Should().BeTrue();
            full.HasPermission(PluginPermission.AccessCredentials).Should().BeTrue();
            full.HasPermission(PluginPermission.AccessNetwork).Should().BeTrue();
            full.HasPermission(PluginPermission.RegisterHotkey).Should().BeFalse();
        }

        [Fact]
        public void PermissionSets_System_ShouldContainSystemPermissions()
        {
            // Act
            var system = PermissionSets.System;

            // Assert
            system.HasPermission(PluginPermission.ReadClipboard).Should().BeTrue();
            system.HasPermission(PluginPermission.AccessCredentials).Should().BeTrue();
            system.HasPermission(PluginPermission.RegisterHotkey).Should().BeTrue();
            system.HasPermission(PluginPermission.BypassPermissionCheck).Should().BeTrue();
        }

        [Fact]
        public void PermissionFlags_ShouldCombineCorrectly()
        {
            // Arrange
            var permission1 = PluginPermission.ReadClipboard;
            var permission2 = PluginPermission.WriteClipboard;

            // Act
            var combined = permission1 | permission2;

            // Assert
            combined.HasPermission(permission1).Should().BeTrue();
            combined.HasPermission(permission2).Should().BeTrue();
        }

        [Fact]
        public void PermissionFlags_ShouldRemoveCorrectly()
        {
            // Arrange
            var combined = PluginPermission.ReadClipboard | PluginPermission.WriteClipboard;

            // Act
            var removed = combined & ~PluginPermission.WriteClipboard;

            // Assert
            removed.HasPermission(PluginPermission.ReadClipboard).Should().BeTrue();
            removed.HasPermission(PluginPermission.WriteClipboard).Should().BeFalse();
        }
    }
}
