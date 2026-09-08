using System;
using FluentAssertions;
using Microsoft.Win32;
using Pulsar.Services;
using Xunit;
using Xunit.Abstractions;

namespace Pulsar.Tests.Services
{
    /// <summary>
    /// AutoStartRegistryService（C2 拆分）注册表往返。用专用测试子键 +
    /// 测试 value 名，绝不触碰真实 Pulsar 自启动项；测试结束清理子键。
    /// </summary>
    public class AutoStartRegistryServiceTests : IDisposable
    {
        private const string TestRunKeyPath = @"Software\Pulsar\Tests\AutoStart";
        private const string TestAppName = "PulsarTest";

        private readonly ITestOutputHelper _output;

        public AutoStartRegistryServiceTests(ITestOutputHelper output)
        {
            _output = output;
            DeleteTestKey();
        }

        [Fact]
        public void Toggle_ShouldRegisterThenUnregister_AutoStartValue()
        {
            var service = new AutoStartRegistryService(TestRunKeyPath, TestAppName);

            service.IsEnabled().Should().BeFalse("the dedicated test Run value does not exist initially");

            service.Toggle();
            service.IsEnabled().Should().BeTrue("first Toggle registers the executable path");
            ReadValue().Should().StartWith("\"").And.EndWith("\"", "the path is quoted to survive spaces");

            service.Toggle();
            service.IsEnabled().Should().BeFalse("second Toggle removes the value");
        }

        [Fact]
        public void IsEnabled_ShouldReturnFalse_WhenRunKeyMissing()
        {
            // 随机子键：读态应降级为 false 而不是抛异常；Toggle 落在一次性键上，
            // 既不污染真实自启动项，也不被上一轮测试残留污染（测试结束由 Cleanup 删除）。
            var missingKeyPath = $@"Software\Pulsar\Tests\{Guid.NewGuid():N}";
            var service = new AutoStartRegistryService(missingKeyPath, TestAppName);

            service.IsEnabled().Should().BeFalse();
            service.Toggle();

            Registry.CurrentUser.DeleteSubKeyTree($@"Software\Pulsar\Tests", throwOnMissingSubKey: false);
        }

        private string ReadValue()
        {
            using var key = Registry.CurrentUser.OpenSubKey(TestRunKeyPath);
            return (string?)key?.GetValue(TestAppName) ?? string.Empty;
        }

        private void DeleteTestKey()
        {
            try
            {
                Registry.CurrentUser.DeleteSubKeyTree(TestRunKeyPath, throwOnMissingSubKey: false);
            }
            catch (Exception ex)
            {
                _output.WriteLine($"cleanup failed (harmless): {ex.Message}");
            }
        }

        public void Dispose() => DeleteTestKey();
    }
}
