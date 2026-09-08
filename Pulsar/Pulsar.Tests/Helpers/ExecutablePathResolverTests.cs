using System;
using System.IO;
using FluentAssertions;
using Pulsar.Helpers;
using Xunit;

namespace Pulsar.Tests.Helpers
{
    /// <summary>
    /// ExecutablePathResolver 环境变量展开守护（repositioning 6.2 治本回归）。
    /// 此前槽位配置里的 %USERPROFILE% / %APPDATA% 路径全链路无展开，静默失效。
    /// 测试用进程级自设变量，不依赖机器环境。
    /// </summary>
    public class ExecutablePathResolverTests : IDisposable
    {
        private const string VarName = "PULSAR_TEST_EXEC_DIR";
        private readonly string _dir;
        private readonly string _exePath;

        public ExecutablePathResolverTests()
        {
            _dir = Path.Combine(Path.GetTempPath(), "pulsar-resolver-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dir);
            _exePath = Path.Combine(_dir, "target-app.exe");
            File.Create(_exePath).Dispose();
            Environment.SetEnvironmentVariable(VarName, _dir);
        }

        public void Dispose()
        {
            Environment.SetEnvironmentVariable(VarName, null);
            try { Directory.Delete(_dir, recursive: true); } catch { /* best effort */ }
        }

        [Fact]
        public void Resolve_ShouldExpandEnvironmentVariable_WhenTargetExists()
        {
            var launchPath = $"%{VarName}%\\target-app.exe";

            var resolved = ExecutablePathResolver.Resolve("whatever", launchPath);

            resolved.Should().Be(_exePath);
        }

        [Fact]
        public void Resolve_ShouldLeaveUndefinedVariable_Unexpanded_AndFallbackToRawLaunchPath()
        {
            var launchPath = "%PULSAR_TEST_UNDEFINED_VAR%\\missing-app.exe";

            var resolved = ExecutablePathResolver.Resolve("no-such-process-xyz", launchPath);

            // 展开不了 → 绝对路径但不存在 → System32 也无 no-such-process-xyz.exe → 原样回退
            resolved.Should().Be(launchPath);
        }

        [Fact]
        public void Resolve_ShouldFallbackToSystem32_ForNullLaunchPath_WhenKnownExecutable()
        {
            var resolved = ExecutablePathResolver.Resolve("cmd", null!);

            var systemDir = Environment.GetFolderPath(Environment.SpecialFolder.System);
            resolved.Should().Be(Path.Combine(systemDir, "cmd.exe"));
        }

        [Fact]
        public void Resolve_ShouldFallbackToProcessNameExe_WhenNothingFound()
        {
            var resolved = ExecutablePathResolver.Resolve("definitely-not-real-app-xyz", null!);

            resolved.Should().Be("definitely-not-real-app-xyz.exe");
        }

        [Fact]
        public void Resolve_ShouldReturnAbsolutePath_WhenTargetExists()
        {
            var resolved = ExecutablePathResolver.Resolve("whatever", _exePath);

            resolved.Should().Be(_exePath);
        }
    }
}
