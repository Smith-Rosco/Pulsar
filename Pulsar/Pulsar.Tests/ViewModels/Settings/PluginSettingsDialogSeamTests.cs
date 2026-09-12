using System;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using FluentAssertions;
using Pulsar.Services.Interfaces;
using Pulsar.ViewModels.Dialogs;
using Pulsar.ViewModels.Settings;
using Xunit;

namespace Pulsar.Tests.ViewModels.Settings
{
    /// <summary>
    /// 插件设置对话框的接缝钉子：
    /// ① 服务依赖必须走构造注入，对话框 VM 不得从兄弟 VM（PluginViewModel）挖取；
    /// ② PluginViewModel 不得重新长出公开的 DialogService 出口；
    /// ③ async void 只允许 ValueChanged 事件薄壳一个，程序内调用点一律走
    ///    可 await 的 PersistSettingAsync。
    /// </summary>
    public class PluginSettingsDialogSeamTests
    {
        [Fact]
        public void DialogService_IsConstructorInjected()
        {
            var ctor = typeof(PluginSettingsDialogViewModel).GetConstructors().Single();
            ctor.GetParameters()
                .Select(p => p.ParameterType)
                .Should().Contain(typeof(IDialogService),
                    "对话框服务是对话框自己的依赖；从 PluginViewModel 挖取即跨接缝泄漏");
        }

        [Fact]
        public void PluginViewModel_DoesNotExposeDialogService()
        {
            typeof(PluginViewModel)
                .GetProperty("DialogService", BindingFlags.Public | BindingFlags.Instance)
                .Should().BeNull("公开的 DialogService 出口会诱导后续 VM 绕过构造注入");
        }

        [Fact]
        public void PluginViewModel_AsyncVoidOnlyForEventShell()
        {
            var asyncVoidMethods = typeof(PluginViewModel)
                .GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static)
                .Where(m => m.ReturnType == typeof(void) && m.GetCustomAttribute<AsyncStateMachineAttribute>() != null)
                .Select(m => m.Name)
                .ToList();

            asyncVoidMethods.Should().BeEquivalentTo(
                new[] { "OnSettingChanged" },
                "async void 只允许在 ValueChanged 事件边界存在（薄壳 + 整体 try/catch）；程序内调用点必须 await PersistSettingAsync");

            var shell = typeof(PluginViewModel).GetMethod(
                "OnSettingChanged", BindingFlags.NonPublic | BindingFlags.Instance);
            shell.Should().NotBeNull();
            shell!.IsPublic.Should().BeFalse("事件壳不应成为程序内调用入口");
        }
    }
}
