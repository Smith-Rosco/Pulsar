using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pulsar.ViewModels.Base;
using DialogResult = Pulsar.Models.Enums.DialogResult;

namespace Pulsar.ViewModels.Dialogs
{
    /// <summary>
    /// ViewModel for the Add Slot type-picker dialog.
    /// Presents available plugin types as selectable cards.
    /// </summary>
    public partial class AddSlotViewModel : ObservableObject, IDialogViewModel
    {
        public record PluginTypeOption(string PluginId, string Icon, string DisplayName, string Description);

        public List<PluginTypeOption> PluginTypes { get; } = new()
        {
            new("com.pulsar.winswitcher",  "\uE8A7", "Window Switcher",  "Switch to or launch applications"),
            new("com.pulsar.command",      "\uE756", "Command",           "Run an executable or shell script"),
            new("com.pulsar.bookmarklet",  "\uE8A4", "Bookmarklet",       "Run JavaScript in the browser"),
            new("com.pulsar.vbarunner",    "\uE8F4", "VBA Runner",        "Run VBA scripts in Excel / WPS"),
            new("com.pulsar.pki",          "\uE72E", "Secret (PKI)",      "Auto-fill encrypted credentials"),
            new("com.pulsar.system",       "\uE713", "System",            "Internal Pulsar commands"),
        };

        [ObservableProperty]
        private PluginTypeOption? _selectedType;

        public string? SelectedPluginId => SelectedType?.PluginId;

        public Action<DialogResult>? RequestClose { get; set; }
        public bool IsScrollable => false;

        public Task<bool> CanCloseAsync(DialogResult result)
        {
            if (result == DialogResult.Confirmed && SelectedType == null)
                return Task.FromResult(false);
            return Task.FromResult(true);
        }

        [RelayCommand]
        private void SelectAndConfirm(PluginTypeOption option)
        {
            SelectedType = option;
            RequestClose?.Invoke(DialogResult.Confirmed);
        }
    }
}
