using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Pulsar.Models.Enums;
using Pulsar.Services.Interfaces;
using Pulsar.ViewModels;
using Pulsar.ViewModels.Base;
using Pulsar.Views.Dialogs;

namespace Pulsar.Services
{
    public class DialogService : IDialogService
    {
        private readonly IServiceProvider _serviceProvider;

        public DialogService(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public async Task<Pulsar.Models.Enums.DialogResult> ShowMessageAsync(string title, string message, DialogType type = DialogType.Info, DialogButtons buttons = DialogButtons.Ok)
        {
            return await RunOnUi(() =>
            {
                var vm = new DialogHostViewModel
                {
                    Title = title,
                    Content = message // Simple string content
                };
                vm.ConfigureButtons(buttons);

                return ShowDialogInternal(vm);
            });
        }

        public async Task<Pulsar.Models.Enums.DialogResult> ShowCustomAsync<TViewModel>(string title, TViewModel content, DialogButtons buttons = DialogButtons.OkCancel)
        {
            return await RunOnUi(() =>
            {
                var vm = new DialogHostViewModel
                {
                    Title = title,
                    Content = content
                };
                
                if (content is IDialogViewModel dialogVm)
                {
                    dialogVm.RequestClose = (result) => vm.CloseCommand.Execute(result);
                    vm.IsScrollable = dialogVm.IsScrollable;
                }

                vm.ConfigureButtons(buttons);

                return ShowDialogInternal(vm);
            });
        }

        public Task<string?> ShowInputAsync(string title, string message, string defaultValue = "")
        {
            // Placeholder for now
            return Task.FromResult<string?>(null);
        }

        public async Task<Pulsar.Models.Enums.DialogResult> ShowConfirmationAsync(string title, string message, string confirmText = "Confirm", string cancelText = "Cancel")
        {
            return await RunOnUi(() =>
            {
                var vm = new DialogHostViewModel
                {
                    Title = title,
                    Content = message,
                    IsPrimaryButtonVisible = true,
                    PrimaryButtonText = confirmText,
                    IsSecondaryButtonVisible = true,
                    SecondaryButtonText = cancelText
                };

                return ShowDialogInternal(vm);
            });
        }

        private Pulsar.Models.Enums.DialogResult ShowDialogInternal(DialogHostViewModel viewModel)
        {
            // Create window via DI to inject dependencies like IThemeService
            var window = ActivatorUtilities.CreateInstance<DialogHostWindow>(_serviceProvider);
            
            window.DataContext = viewModel;

            // Handle Close Request from VM
            Pulsar.Models.Enums.DialogResult result = Pulsar.Models.Enums.DialogResult.None;
            viewModel.RequestClose = (r) =>
            {
                result = r;
                window.Close();
            };

            // Owner
            if (System.Windows.Application.Current.MainWindow != window && System.Windows.Application.Current.MainWindow.IsVisible)
            {
                window.Owner = System.Windows.Application.Current.MainWindow;
            }

            window.ShowDialog();

            return result;
        }

        private async Task<T> RunOnUi<T>(Func<T> action)
        {
            if (System.Windows.Application.Current.Dispatcher.CheckAccess())
            {
                return action();
            }
            else
            {
                return await System.Windows.Application.Current.Dispatcher.InvokeAsync(action);
            }
        }
    }
}
