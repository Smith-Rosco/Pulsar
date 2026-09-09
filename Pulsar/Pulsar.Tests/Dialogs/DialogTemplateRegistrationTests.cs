using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using FluentAssertions;
using Pulsar.Models.Enums;
using Pulsar.Services;
using Pulsar.ViewModels.Base;
using Xunit;

namespace Pulsar.Tests.Dialogs
{
    /// <summary>
    /// Guards the implicit VM → DataTemplate mapping maintained by hand in
    /// <c>Themes/DialogTemplates.xaml</c>. A dialog VM added without a template
    /// would silently render its type name (ToString fallback) with no error,
    /// log or compile failure — these tests make that regression visible.
    /// </summary>
    public class DialogTemplateRegistrationTests
    {
        /// <summary>
        /// 不再经模态 DataTemplate 展示的对话框 VM 豁免清单：
        /// - SlotEditorViewModel：P3（unify-slot-editor-transient-pages 4.1）模态退役，
        ///   slot 编辑统一走 transient tab（SettingsSlotEditorPage 直接实例化内容控件）。
        /// </summary>
        private static readonly HashSet<Type> NonModalVmExemptions = new()
        {
            typeof(Pulsar.ViewModels.Dialogs.SlotEditorViewModel),
        };

        [Fact]
        public void DialogTemplates_ShouldRegisterEveryDialogViewModel()
        {
            var templates = (ResourceDictionary)Application.LoadComponent(
                new Uri("Pulsar;component/Themes/DialogTemplates.xaml", UriKind.Relative));

            var registered = templates.Values.OfType<DataTemplate>()
                .Select(t => t.DataType)
                .OfType<Type>()
                .ToHashSet();

            var dialogVmTypes = typeof(IDialogViewModel).Assembly.GetTypes()
                .Where(t => t.IsClass && !t.IsAbstract && typeof(IDialogViewModel).IsAssignableFrom(t))
                .Where(t => !NonModalVmExemptions.Contains(t))
                .ToList();

            dialogVmTypes.Should().NotBeEmpty("the dialog system must have at least one custom VM");
            foreach (var vmType in dialogVmTypes)
            {
                registered.Should().Contain(vmType,
                    $"dialog VM {vmType.Name} must be registered in Themes/DialogTemplates.xaml");
            }
        }

        [Fact]
        public void HasTemplate_ShouldDetectRegisteredAndMissingTemplates()
        {
            var resources = new ResourceDictionary();
            resources[new DataTemplateKey(typeof(StubDialogVm))] = new DataTemplate();

            DialogService.HasTemplate(resources, typeof(StubDialogVm)).Should().BeTrue();
            DialogService.HasTemplate(resources, typeof(OtherDialogVm)).Should().BeFalse();
            DialogService.HasTemplate(null, typeof(StubDialogVm)).Should().BeFalse();
        }

        private sealed class StubDialogVm : IDialogViewModel
        {
            public Task<bool> CanCloseAsync(DialogResult result) => Task.FromResult(true);
            public Action<DialogResult>? RequestClose { get; set; }
        }

        private sealed class OtherDialogVm : IDialogViewModel
        {
            public Task<bool> CanCloseAsync(DialogResult result) => Task.FromResult(true);
            public Action<DialogResult>? RequestClose { get; set; }
        }
    }
}
