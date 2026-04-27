using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Pulsar.Core.Plugin;
using Pulsar.Native;
using Pulsar.Plugins.Extensions.BookmarkletRunner;
using Pulsar.Services.Interfaces;
using Pulsar.Tests.TestHelpers;

namespace Pulsar.Tests.Plugins.Extensions.BookmarkletRunner
{
    public class BookmarkletRunnerPluginTests
    {
        [Fact]
        public void ExecuteSmartInput_ShouldFailFast_WhenUiaInjectionFails()
        {
            var plugin = new BookmarkletRunnerPlugin();
            int ctrlLCount = 0;
            int enterCount = 0;

            plugin.SendKeyCombination = keys =>
            {
                if (keys.Length == 2 && keys[0] == InputHelper.VK_CONTROL && keys[1] == InputHelper.VK_L)
                {
                    ctrlLCount++;
                }

                if (keys.Length == 1 && keys[0] == InputHelper.VK_RETURN)
                {
                    enterCount++;
                }
            };
            plugin.TrySetFocusedElementText = _ => false;
            plugin.Sleep = _ => { };

            var result = plugin.ExecuteSmartInput("alert('hi');");

            result.Success.Should().BeFalse();
            result.Severity.Should().Be(PluginErrorSeverity.Recoverable);
            result.Message.Should().Contain("重试");
            ctrlLCount.Should().Be(1);
            enterCount.Should().Be(0);
        }

        [Fact]
        public void ExecuteSmartInput_ShouldExecuteBookmarklet_WhenUiaInjectionSucceeds()
        {
            var plugin = new BookmarkletRunnerPlugin();
            int enterCount = 0;
            string? injectedPayload = null;

            plugin.SendKeyCombination = keys =>
            {
                if (keys.Length == 1 && keys[0] == InputHelper.VK_RETURN)
                {
                    enterCount++;
                }
            };
            plugin.TrySetFocusedElementText = text =>
            {
                injectedPayload = text;
                return true;
            };
            plugin.Sleep = _ => { };

            var result = plugin.ExecuteSmartInput("alert('hi');");

            result.Success.Should().BeTrue();
            injectedPayload.Should().Be("javascript:alert('hi');");
            enterCount.Should().Be(1);
        }

        [Fact]
        public async Task ExecuteAsync_ShouldReturnRecoverableErrorWithoutEnter_WhenUiaInjectionFails()
        {
            var windowService = new Mock<IWindowService>();
            var services = new Mock<IServiceProvider>();
            services.Setup(x => x.GetService(typeof(IWindowService))).Returns(windowService.Object);
            services.Setup(x => x.GetService(typeof(ILogger<BookmarkletRunnerPlugin>)))
                .Returns(NullLogger<BookmarkletRunnerPlugin>.Instance);

            var plugin = new BookmarkletRunnerPlugin
            {
                ReadScriptFile = _ => "alert('hi');",
                ResolveTargetBrowserWindow = (_, _) => new IntPtr(123),
                IsBrowserWindowMinimized = _ => false,
                FocusBrowserWindow = _ => true,
                DelayAsync = _ => Task.CompletedTask,
                Sleep = _ => { },
                TrySetFocusedElementText = _ => false
            };

            int enterCount = 0;
            plugin.SendKeyCombination = keys =>
            {
                if (keys.Length == 1 && keys[0] == InputHelper.VK_RETURN)
                {
                    enterCount++;
                }
            };

            plugin.Initialize(services.Object);

            var result = await plugin.ExecuteAsync(
                "run",
                new Dictionary<string, string> { ["scriptPath"] = @"C:\temp\bookmarklet.js" },
                PulsarContextFactory.CreateTestContext(targetWindowHandle: new IntPtr(10), targetProcessName: "chrome"));

            result.Success.Should().BeFalse();
            result.Severity.Should().Be(PluginErrorSeverity.Recoverable);
            result.Message.Should().Contain("浏览器地址栏");
            enterCount.Should().Be(0);
            windowService.Verify(x => x.HideMainWindow(), Times.Once);
        }
    }
}
