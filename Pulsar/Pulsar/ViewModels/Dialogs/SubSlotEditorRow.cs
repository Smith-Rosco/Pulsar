using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pulsar.Core.Plugin.Metadata;
using Pulsar.Models;
using Pulsar.Plugins.Core.Pki.Models;
using Pulsar.Services.Interfaces;

namespace Pulsar.ViewModels.Dialogs
{
    /// <summary>Lightweight option for the per-row plugin selector.</summary>
    public sealed record SubSlotPluginOption(string PluginId, string DisplayName);

    /// <summary>
    /// Editable wrapper over the immutable <see cref="SubSlotDescriptor"/>. The editor
    /// binds to this observable row (PluginId/Action/Label/IconKey/ColorHex plus a
    /// working <see cref="Args"/> dictionary) and materializes a fresh descriptor on
    /// save, so the descriptor record itself never mutates during typing.
    /// </summary>
    public partial class SubSlotEditorRow : ObservableObject, IDisposable
    {
        private readonly IPluginMetadataRegistry? _metadataRegistry;
        private readonly Func<SlotParameterEditorField, Task>? _pickParameterValueAsync;
        private readonly Func<string, SecretDisplayMetadata?>? _secretDisplayResolver;
        private readonly PluginSlot _backingSlot;

        private bool _isRebuilding;

        public SubSlotEditorRow(
            SubSlotDescriptor? descriptor,
            IPluginMetadataRegistry? metadataRegistry = null,
            Func<SlotParameterEditorField, Task>? pickParameterValueAsync = null,
            Func<string, SecretDisplayMetadata?>? secretDisplayResolver = null,
            IReadOnlyList<SubSlotPluginOption>? availablePlugins = null)
        {
            _metadataRegistry = metadataRegistry;
            _pickParameterValueAsync = pickParameterValueAsync;
            _secretDisplayResolver = secretDisplayResolver;
            AvailablePlugins = availablePlugins ?? new List<SubSlotPluginOption>();

            // Share the row's working Args dictionary with the backing slot so the
            // parameter-field machinery (indexer reads/writes) operates on the same
            // dictionary the descriptor will be materialized from on save.
            Args = descriptor?.Args ?? new Dictionary<string, string>();
            _backingSlot = new PluginSlot
            {
                PluginId = descriptor?.PluginId ?? string.Empty,
                Action = descriptor?.Action ?? string.Empty,
                Label = descriptor?.Label ?? string.Empty,
                IconKey = descriptor?.IconKey ?? string.Empty,
                Color = descriptor?.ColorHex ?? string.Empty,
                Args = Args
            };

            PluginId = descriptor?.PluginId ?? string.Empty;
            Action = descriptor?.Action ?? string.Empty;
            Label = descriptor?.Label ?? string.Empty;
            IconKey = descriptor?.IconKey ?? string.Empty;
            ColorHex = descriptor?.ColorHex ?? string.Empty;

            Rebuild();
        }

        // ---- Working state ----

        [ObservableProperty]
        private string _pluginId;

        [ObservableProperty]
        private string _action;

        // [UX fix 2026-09-09, unify-slot-editor-transient-pages D5] The action selector
        // binds SelectedItem to this VM-owned property instead of SelectedValue->Action.
        // Rebuild() clears and repopulates AvailableActions while a selection commit is in
        // flight; with SelectedValue TwoWay that reset pushed null back into Action and
        // swallowed the user's first pick (had to select twice). The VM-owned selected
        // item is synced explicitly at the end of Rebuild, and a null arriving from the
        // ComboBox while _isRebuilding is ignored.
        [ObservableProperty]
        private SlotActionOption? _selectedActionOption;

        [ObservableProperty]
        private string _label;

        [ObservableProperty]
        private string _iconKey;

        [ObservableProperty]
        private string _colorHex;

        /// <summary>
        /// [3.4 手风琴] 行展开状态；互斥（同时只展开一行）由 owner
        /// <see cref="SlotEditorViewModel"/> 在 PropertyChanged 里统一裁决。
        /// </summary>
        [ObservableProperty]
        private bool _isExpanded;

        /// <summary>折叠态摘要：动作名（如 "Run"）；空选择时为空串。</summary>
        public string SummaryActionText => ActionLabel;

        /// <summary>折叠态摘要：展开与否决定正文可见性（XAML BoolToVis 用）。</summary>
        public bool IsCollapsed => !IsExpanded;

        [RelayCommand]
        private void ToggleExpand() => IsExpanded = !IsExpanded;

        partial void OnIsExpandedChanged(bool value) => OnPropertyChanged(nameof(IsCollapsed));

        /// <summary>折叠态摘要：有名称显示名称，否则显示 resx 占位（Unnamed）。</summary>
        public bool HasLabel => !string.IsNullOrWhiteSpace(Label);

        public Dictionary<string, string> Args { get; }

        public IReadOnlyList<SubSlotPluginOption> AvailablePlugins { get; }

        /// <summary>Backing slot sharing <see cref="Args"/>; used by parameter pickers.</summary>
        public PluginSlot BackingSlot => _backingSlot;

        // ---- Metadata-derived projections ----

        public ObservableCollection<SlotActionOption> AvailableActions { get; } = new();

        public ObservableCollection<SlotParameterEditorField> RequiredParameters { get; } = new();

        public ObservableCollection<SlotParameterEditorField> OptionalParameters { get; } = new();

        public ObservableCollection<SlotParameterEditorField> AdvancedParameters { get; } = new();

        public string ActionLabel
        {
            get
            {
                var actionMeta = _metadataRegistry?.GetActionMetadata(PluginId, Action);
                if (actionMeta != null && !string.IsNullOrWhiteSpace(actionMeta.Label))
                {
                    return actionMeta.Label;
                }

                return string.IsNullOrWhiteSpace(Action) ? string.Empty : Action;
            }
        }

        public bool HasParameters =>
            RequiredParameters.Count > 0 || OptionalParameters.Count > 0 || AdvancedParameters.Count > 0;

        public bool HasInvalidSelection =>
            string.IsNullOrWhiteSpace(PluginId)
            || string.IsNullOrWhiteSpace(Action)
            || _metadataRegistry?.GetActionMetadata(PluginId, Action) == null;

        // ---- Field change handling ----

        partial void OnPluginIdChanged(string value)
        {
            _backingSlot.PluginId = value;
            Rebuild();
        }

        partial void OnActionChanged(string value)
        {
            _backingSlot.Action = value;
            Rebuild();
            OnPropertyChanged(nameof(SummaryActionText));
        }

        partial void OnSelectedActionOptionChanged(SlotActionOption? value)
        {
            if (_isRebuilding)
            {
                return;
            }

            Action = value?.Value ?? string.Empty;
        }

        partial void OnLabelChanged(string value)
        {
            _backingSlot.Label = value;
            OnPropertyChanged(nameof(HasLabel));
        }

        partial void OnIconKeyChanged(string value) => _backingSlot.IconKey = value;

        partial void OnColorHexChanged(string value) => _backingSlot.Color = value;

        // ---- Command bridge (Tag pattern) ----

        [RelayCommand]
        public async Task PickParameterValueAsync(SlotParameterEditorField field)
        {
            if (_pickParameterValueAsync == null || field == null)
            {
                return;
            }

            await _pickParameterValueAsync(field);

            // [Fix 2026-09-09, unify-slot-editor-transient-pages D5 aftermath] Parameter
            // pickers only see the backing PluginSlot: e.g. SettingsViewModel.PickProcess
            // writes the extracted app icon (cache path) and a suggested Label onto
            // _backingSlot. The row->backing sync is one-way, so read the results back
            // into the observables; otherwise ToDescriptor() materializes an empty
            // IconKey and the cascade submenu renders a blank icon.
            Label = _backingSlot.Label;
            IconKey = _backingSlot.IconKey;
            OnPropertyChanged(nameof(HasInvalidSelection));
        }

        // ---- Materialization ----

        public SubSlotDescriptor ToDescriptor()
        {
            return new SubSlotDescriptor(
                PluginId,
                Action,
                Args,
                Label,
                IconKey,
                ColorHex);
        }

        // ---- Rebuild / dispose ----

        private void Rebuild()
        {
            if (_isRebuilding)
            {
                return;
            }

            _isRebuilding = true;
            try
            {
                DisposeFields();

                var metadata = _metadataRegistry?.GetMetadata(PluginId);
                var actionMetadata = _metadataRegistry?.GetActionMetadata(PluginId, Action);

                if (metadata != null)
                {
                    foreach (var action in metadata.Actions)
                    {
                        AvailableActions.Add(new SlotActionOption
                        {
                            Value = action.Key,
                            Label = action.Value.Label ?? action.Key,
                            Description = action.Value.Description,
                            IsSelected = string.Equals(action.Key, Action, StringComparison.OrdinalIgnoreCase)
                        });
                    }
                }

                if (actionMetadata != null)
                {
                    foreach (var parameter in actionMetadata.GetParametersByGroup(SlotParameterGroup.Required))
                    {
                        RequiredParameters.Add(CreateField(parameter));
                    }

                    foreach (var parameter in actionMetadata.GetParametersByGroup(SlotParameterGroup.Optional))
                    {
                        OptionalParameters.Add(CreateField(parameter));
                    }

                    foreach (var parameter in actionMetadata.GetParametersByGroup(SlotParameterGroup.Advanced))
                    {
                        AdvancedParameters.Add(CreateField(parameter));
                    }
                }

                // Restore the selected-item projection for the current Action while the
                // rebuild guard is still active (setting it here cannot re-enter the
                // Action write-back, unlike a ComboBox-driven reset).
                SlotActionOption? selectedOption = null;
                foreach (var option in AvailableActions)
                {
                    if (string.Equals(option.Value, Action, StringComparison.OrdinalIgnoreCase))
                    {
                        selectedOption = option;
                        break;
                    }
                }

                SelectedActionOption = selectedOption;

                OnPropertyChanged(nameof(ActionLabel));
                OnPropertyChanged(nameof(HasParameters));
                OnPropertyChanged(nameof(HasInvalidSelection));
            }
            finally
            {
                _isRebuilding = false;
            }
        }

        private SlotParameterEditorField CreateField(SlotParameterMetadata parameter)
        {
            return new SlotParameterEditorField(_backingSlot, parameter, _secretDisplayResolver);
        }

        private void DisposeFields()
        {
            foreach (var field in RequiredParameters.Concat(OptionalParameters).Concat(AdvancedParameters))
            {
                field.Dispose();
            }

            RequiredParameters.Clear();
            OptionalParameters.Clear();
            AdvancedParameters.Clear();
            AvailableActions.Clear();
        }

        public void Dispose()
        {
            DisposeFields();
        }
    }
}
