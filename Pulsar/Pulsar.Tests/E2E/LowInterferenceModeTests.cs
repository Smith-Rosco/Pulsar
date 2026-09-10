// [Path]: Pulsar/Pulsar.Tests/E2E/LowInterferenceModeTests.cs

using FluentAssertions;
using Pulsar.Core.Debug;
using Xunit;

namespace Pulsar.Tests.E2E
{
    /// <summary>
    /// Guards the low-interference E2E mode flag: <c>--ui-debug-low-interference</c>
    /// must only take effect together with <c>--ui-debug</c>, and must default to
    /// off so production and existing debug runs are byte-for-byte unchanged.
    /// </summary>
    public class LowInterferenceModeTests
    {
        [Fact]
        public void FromArgs_WithoutUiDebug_ReturnsDisabled_EvenWhenLowInterferencePresent()
        {
            var options = DebugModeOptions.FromArgs(new[] { "--ui-debug-low-interference" });

            options.IsUiDebug.Should().BeFalse();
            options.LowInterference.Should().BeFalse();
        }

        [Fact]
        public void FromArgs_UiDebugAlone_LowInterferenceOff()
        {
            var options = DebugModeOptions.FromArgs(new[] { "--ui-debug" });

            options.IsUiDebug.Should().BeTrue();
            options.LowInterference.Should().BeFalse();
        }

        [Fact]
        public void FromArgs_UiDebugWithLowInterference_FlagOn()
        {
            var options = DebugModeOptions.FromArgs(new[] { "--ui-debug", "--ui-debug-low-interference" });

            options.IsUiDebug.Should().BeTrue();
            options.LowInterference.Should().BeTrue();
        }

        [Fact]
        public void FromArgs_FlagOrderIndependent()
        {
            var options = DebugModeOptions.FromArgs(new[] { "--ui-debug-low-interference", "--ui-debug" });

            options.LowInterference.Should().BeTrue();
        }

        [Fact]
        public void FromArgs_HooksAndLowInterferenceCoexist()
        {
            var options = DebugModeOptions.FromArgs(new[] { "--ui-debug", "--ui-debug-hooks", "--ui-debug-low-interference" });

            options.EnableHotkeyHooks.Should().BeTrue();
            options.LowInterference.Should().BeTrue();
        }

        [Fact]
        public void Disabled_HasLowInterferenceOff()
        {
            DebugModeOptions.Disabled.LowInterference.Should().BeFalse();
        }
    }
}
