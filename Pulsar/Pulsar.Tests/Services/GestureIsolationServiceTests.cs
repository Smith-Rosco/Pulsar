using System;
using FluentAssertions;
using Moq;
using Pulsar.Models;
using Pulsar.Native;
using Pulsar.Services;
using Pulsar.Services.Interfaces;
using Xunit;

namespace Pulsar.Tests.Services
{
    /// <summary>
    /// Pure decision tests for <see cref="GestureIsolationService"/>. The native
    /// adapter is faked; the decision logic is exercised against plain facts.
    /// </summary>
    public class GestureIsolationServiceTests
    {
        private static readonly PulsarNative.RECT Monitor1920 = new()
        {
            Left = 0,
            Top = 0,
            Right = 1920,
            Bottom = 1080
        };

        private static readonly PulsarNative.RECT FullscreenRect = new()
        {
            Left = 0,
            Top = 0,
            Right = 1920,
            Bottom = 1080
        };

        private static readonly PulsarNative.RECT WindowedRect = new()
        {
            Left = 100,
            Top = 100,
            Right = 900,
            Bottom = 700
        };

        private static ForegroundWindowFacts Facts(
            string className = "Chrome_WidgetWin_1",
            string processName = "chrome",
            PulsarNative.RECT? windowRect = null,
            PulsarNative.RECT? monitorBounds = null) => new(
            className,
            processName,
            windowRect ?? WindowedRect,
            monitorBounds ?? Monitor1920);

        private static ProfileSettings Settings(bool enabled = true, bool blockFullscreen = true)
            => new()
            {
                GestureIsolationEnabled = enabled,
                GestureIsolationBlockFullscreen = blockFullscreen,
                GestureIsolationMode = GestureIsolationMode.Allowlist
            };

        private static IGestureIsolationService CreateService()
        {
            var native = new Mock<IGestureIsolationNative>();
            native.Setup(n => n.IsFullscreenShellClass(It.IsAny<string>()))
                .Returns((string className) =>
                    string.Equals(className, "Progman", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(className, "WorkerW", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(className, "Shell_TrayWnd", StringComparison.OrdinalIgnoreCase));
            return new GestureIsolationService(native.Object);
        }

        // ===== Filter disabled ⇒ allow =====

        [Fact]
        public void IsGestureAllowed_DisabledFilter_ShouldAllow()
        {
            var service = CreateService();
            var facts = Facts();

            service.IsGestureAllowed(facts, Settings(enabled: false)).Should().BeTrue();
        }

        [Fact]
        public void IsGestureAllowed_DisabledFilter_ShouldAllowEvenFullscreen()
        {
            var service = CreateService();
            var facts = Facts(windowRect: FullscreenRect);

            service.IsGestureAllowed(facts, Settings(enabled: false, blockFullscreen: true)).Should().BeTrue(
                "fullscreen protection is inert when the master switch is off");
        }

        // ===== Fullscreen ⇒ deny (with shell-class bypass) =====

        [Fact]
        public void IsGestureAllowed_Fullscreen_ShouldDeny()
        {
            var service = CreateService();
            var facts = Facts(windowRect: FullscreenRect);

            service.IsGestureAllowed(facts, Settings(enabled: true, blockFullscreen: true)).Should().BeFalse();
        }

        [Fact]
        public void IsGestureAllowed_Fullscreen_WhenBlockOff_ShouldFallThroughToLists()
        {
            var service = CreateService();
            var facts = Facts(windowRect: FullscreenRect, processName: "chrome");

            // Block off → fullscreen is ignored → allow-list decides (chrome not listed).
            service.IsGestureAllowed(facts, Settings(enabled: true, blockFullscreen: false)).Should().BeFalse();
        }

        [Fact]
        public void IsGestureAllowed_ShellClass_ShouldNotBeClassifiedFullscreen()
        {
            var service = CreateService();
            // Shell surface that covers the full monitor → must NOT be denied by the
            // fullscreen branch; it falls through to the process allow/block lists.
            var facts = Facts(className: "Progman", processName: "explorer", windowRect: FullscreenRect);

            var allowListSettings = new ProfileSettings
            {
                GestureIsolationEnabled = true,
                GestureIsolationBlockFullscreen = true,
                GestureIsolationMode = GestureIsolationMode.Allowlist,
                GestureIsolationProcesses = { "explorer" }
            };

            service.IsGestureAllowed(facts, allowListSettings).Should().BeTrue();
        }

        [Fact]
        public void IsGestureAllowed_WorkerW_ShouldNotBeClassifiedFullscreen()
        {
            var service = CreateService();
            var facts = Facts(className: "WorkerW", processName: "explorer", windowRect: FullscreenRect);

            var allowListSettings = new ProfileSettings
            {
                GestureIsolationEnabled = true,
                GestureIsolationBlockFullscreen = true,
                GestureIsolationMode = GestureIsolationMode.Allowlist,
                GestureIsolationProcesses = { "explorer" }
            };

            service.IsGestureAllowed(facts, allowListSettings).Should().BeTrue();
        }

        [Fact]
        public void IsGestureAllowed_ShellTrayWnd_ShouldNotBeClassifiedFullscreen()
        {
            var service = CreateService();
            var facts = Facts(className: "Shell_TrayWnd", processName: "explorer", windowRect: FullscreenRect);

            var allowListSettings = new ProfileSettings
            {
                GestureIsolationEnabled = true,
                GestureIsolationBlockFullscreen = true,
                GestureIsolationMode = GestureIsolationMode.Allowlist,
                GestureIsolationProcesses = { "explorer" }
            };

            service.IsGestureAllowed(facts, allowListSettings).Should().BeTrue();
        }

        // ===== Allow-list =====

        [Fact]
        public void IsGestureAllowed_AllowListHit_ShouldAllow()
        {
            var service = CreateService();
            var facts = Facts(processName: "chrome");

            var settings = new ProfileSettings
            {
                GestureIsolationEnabled = true,
                GestureIsolationBlockFullscreen = true,
                GestureIsolationMode = GestureIsolationMode.Allowlist,
                GestureIsolationProcesses = { "chrome", "notepad" }
            };

            service.IsGestureAllowed(facts, settings).Should().BeTrue();
        }

        [Fact]
        public void IsGestureAllowed_AllowListMiss_ShouldDeny()
        {
            var service = CreateService();
            var facts = Facts(processName: "msedge");

            var settings = new ProfileSettings
            {
                GestureIsolationEnabled = true,
                GestureIsolationBlockFullscreen = true,
                GestureIsolationMode = GestureIsolationMode.Allowlist,
                GestureIsolationProcesses = { "chrome" }
            };

            service.IsGestureAllowed(facts, settings).Should().BeFalse();
        }

        [Fact]
        public void IsGestureAllowed_AllowListEmpty_ShouldDenyAll()
        {
            var service = CreateService();
            var facts = Facts(processName: "chrome");

            service.IsGestureAllowed(facts, Settings(enabled: true)).Should().BeFalse();
        }

        // ===== Block-list =====

        [Fact]
        public void IsGestureAllowed_BlockListHit_ShouldDeny()
        {
            var service = CreateService();
            var facts = Facts(processName: "game");

            var settings = new ProfileSettings
            {
                GestureIsolationEnabled = true,
                GestureIsolationBlockFullscreen = true,
                GestureIsolationMode = GestureIsolationMode.Blocklist,
                GestureIsolationProcesses = { "game" }
            };

            service.IsGestureAllowed(facts, settings).Should().BeFalse();
        }

        [Fact]
        public void IsGestureAllowed_BlockListMiss_ShouldAllow()
        {
            var service = CreateService();
            var facts = Facts(processName: "notepad");

            var settings = new ProfileSettings
            {
                GestureIsolationEnabled = true,
                GestureIsolationBlockFullscreen = true,
                GestureIsolationMode = GestureIsolationMode.Blocklist,
                GestureIsolationProcesses = { "game" }
            };

            service.IsGestureAllowed(facts, settings).Should().BeTrue();
        }

        [Fact]
        public void IsGestureAllowed_BlockListEmpty_ShouldDenyNone()
        {
            var service = CreateService();
            var facts = Facts(processName: "anything");

            var settings = new ProfileSettings
            {
                GestureIsolationEnabled = true,
                GestureIsolationBlockFullscreen = true,
                GestureIsolationMode = GestureIsolationMode.Blocklist,
                GestureIsolationProcesses = { }
            };

            service.IsGestureAllowed(facts, settings).Should().BeTrue();
        }

        // ===== Case-insensitivity + malformed entries =====

        [Fact]
        public void IsGestureAllowed_AllowList_ShouldMatchCaseInsensitive()
        {
            var service = CreateService();
            var facts = Facts(processName: "Code");

            var settings = new ProfileSettings
            {
                GestureIsolationEnabled = true,
                GestureIsolationBlockFullscreen = true,
                GestureIsolationMode = GestureIsolationMode.Allowlist,
                GestureIsolationProcesses = { "  code  ", " notepad " }
            };

            service.IsGestureAllowed(facts, settings).Should().BeTrue();
        }

        [Fact]
        public void IsGestureAllowed_BlockList_ShouldMatchCaseInsensitive()
        {
            var service = CreateService();
            var facts = Facts(processName: "GAME");

            var settings = new ProfileSettings
            {
                GestureIsolationEnabled = true,
                GestureIsolationBlockFullscreen = true,
                GestureIsolationMode = GestureIsolationMode.Blocklist,
                GestureIsolationProcesses = { "game", "notepad" }
            };

            service.IsGestureAllowed(facts, settings).Should().BeFalse();
        }

        [Fact]
        public void IsGestureAllowed_MalformedEntry_ShouldBeIgnored()
        {
            var service = CreateService();
            var facts = Facts(processName: "chrome");

            var settings = new ProfileSettings
            {
                GestureIsolationEnabled = true,
                GestureIsolationBlockFullscreen = true,
                GestureIsolationMode = GestureIsolationMode.Allowlist,
                GestureIsolationProcesses = { "", "   ", null!, "chrome" }
            };

            service.IsGestureAllowed(facts, settings).Should().BeTrue();
        }
    }
}
