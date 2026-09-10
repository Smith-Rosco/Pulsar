using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pulsar.Core.Localization;
using Pulsar.Core.Plugin.Metadata;
using Pulsar.Models;
using Pulsar.Plugins.Core.SecretFill.Models;
using Pulsar.Services.Interfaces;
using Pulsar.Services;
using Pulsar.ViewModels.Base;
using Pulsar.ViewModels.Settings;
using DialogResult = Pulsar.Models.Enums.DialogResult;

namespace Pulsar.ViewModels.Dialogs
{
    public partial class SlotEditorViewModel : ObservableObject, IWizardDialogViewModel
    {
        private readonly IReadOnlyList<SlotTypeCard> _allTypeCards;
        private readonly Func<string, PluginSlot> _createSlotDraft;
        private readonly Action<PluginSlot, string?> _setAction;
        private readonly Func<SlotParameterEditorField, Task> _pickParameterValueAsync;
        private readonly Func<PluginSlot, Task> _pickIconAsync;
        private readonly Func<PluginSlot, Task> _pickColorAsync;
        private readonly Func<string, SecretDisplayMetadata?>? _secretDisplayResolver;
        private readonly ILocalizationService _loc;
        private readonly IPluginMetadataRegistry? _metadataRegistry;
        private readonly Func<PluginSlot, Task>? _commitInPlaceAsync;

        /// <summary>
        /// 嵌入模式（unify-slot-editor-transient-pages 3.1/3.3）：VM 承载在 transient
        /// tab 而非模态对话框中。Save 不再 RequestClose，改为回调宿主提交（提交后
        /// 宿主负责草稿 tab → 实体 tab 的重注册）。
        /// </summary>
        public bool IsEmbedded => _commitInPlaceAsync != null;

        private static readonly ObservableCollection<SlotActionOption> _emptyActions = new();
        private static readonly ObservableCollection<SlotParameterEditorField> _emptyFields = new();
        private static readonly ObservableCollection<string> _emptyTokens = new();

        private bool _isApplyingSuggestions;
        private bool _shouldShowFieldValidation;
        private int _validationRequestId;
        private string _validationFocusTarget = string.Empty;
        private SlotParameterEditorField? _validationFocusField;
        private string _lastSuggestedLabel = string.Empty;
        private string _lastSuggestedIcon = string.Empty;
        private string _lastSuggestedColor = string.Empty;

        // ---- Primary cards (curated intent grid) ----

        public IReadOnlyList<SlotTypeCard> PrimaryCards => _allTypeCards.Where(c => c.IsPrimary).ToList();

        // ---- Search / browse ----

        [ObservableProperty]
        private string _searchText = string.Empty;

        [ObservableProperty]
        private bool _isBrowseExpanded;

        // ---- Picker phase ----

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsPickerPhase))]
        [NotifyPropertyChangedFor(nameof(IsConfigurationPhase))]
        private bool _isConfigurationActive;

        public bool IsPickerPhase => !IsConfigurationActive;

        public bool IsConfigurationPhase => IsConfigurationActive;

        // ---- Slot ----

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasSelectedSlot))]
        [NotifyPropertyChangedFor(nameof(CreatedSlot))]
        private PluginSlot _slot;

        public PluginSlot? CreatedSlot => Slot.PluginId.Length > 0 ? Slot : null;

        public bool HasSelectedSlot => Slot != null && !string.IsNullOrWhiteSpace(Slot.PluginId);

        // ---- Editor mode ----

        public SlotEditorMode EditorMode { get; }

        public bool IsCreateMode => EditorMode == SlotEditorMode.Create;

        public bool IsEditMode => EditorMode == SlotEditorMode.Edit;

        // ---- IWizardDialogViewModel ----

        [ObservableProperty]
        private string _primaryButtonText = string.Empty;

        [ObservableProperty]
        private string _secondaryButtonText = string.Empty;

        [ObservableProperty]
        private bool _isPrimaryButtonVisible = true;

        [ObservableProperty]
        private bool _isSecondaryButtonVisible = true;

        public ICommand PrimaryCommand => SaveCommand;

        /// <summary>
        /// 模态模式 = Cancel（关闭对话框）；嵌入式新建 tab = 返回类型选择相位
        /// （配置相位；选择相位由页头返回箭头承载，见 AddSlotContent）。
        /// </summary>
        public ICommand SecondaryCommand =>
            IsEmbedded && IsCreateMode && IsConfigurationActive ? GoBackToPickerCommand : CancelCommand;

        // ---- Action selector ----

        public ObservableCollection<SlotActionOption> AvailableActions => Slot?.AvailableActions ?? _emptyActions;

        public bool HasSingleAction => Slot?.AvailableActions.Count == 1;

        public bool HasMultipleActions => Slot?.AvailableActions.Count > 1;

        public bool UseSegmentedButtons => Slot?.AvailableActions.Count is > 1 and <= 4;

        public bool UseComboBox => Slot?.AvailableActions.Count > 4;

        // ---- Parameters ----

        public ObservableCollection<SlotParameterEditorField> RequiredParameters => Slot?.RequiredParameters ?? _emptyFields;

        public ObservableCollection<SlotParameterEditorField> OptionalParameters => Slot?.OptionalParameters ?? _emptyFields;

        public ObservableCollection<SlotParameterEditorField> AdvancedParameters => Slot?.AdvancedParameters ?? _emptyFields;

        public ObservableCollection<string> SummaryTokens => Slot?.SummaryTokens ?? _emptyTokens;

        public bool HasRequiredParameters => Slot?.HasRequiredParameters == true;

        public bool HasOptionalParameters => Slot?.HasOptionalParameters == true;

        public bool HasAdvancedParameters => Slot?.HasAdvancedParameters == true;

        public bool HasSummaryTokens => Slot?.HasSummaryTokens == true;

        public bool HasOptionalSettings => Slot != null
            && (HasOptionalParameters || HasAdvancedParameters || HasAppearanceOptions);

        public bool HasAppearanceOptions => Slot != null;

        // ---- Validation ----

        public bool HasActionValidationError => _shouldShowFieldValidation
            && Slot != null
            && string.IsNullOrWhiteSpace(Slot.Action);

        public string ActionValidationMessage => HasActionValidationError
            ? _loc["Dialog.AddSlot.SelectActionValidation"]
            : string.Empty;

        public bool HasBlockingIssue => !string.IsNullOrWhiteSpace(BlockingIssueText);

        public string BlockingIssueText => GetBlockingIssueText();

        public bool HasValidationSummary => HasBlockingIssue || Slot?.HasValidationSummary == true;

        public ValidationSeverity ValidationSeverity => HasBlockingIssue
            ? ValidationSeverity.Error
            : Slot?.ValidationSeverity ?? ValidationSeverity.None;

        public string ValidationSummary => HasBlockingIssue
            ? BlockingIssueText
            : Slot?.ValidationSummary ?? string.Empty;

        public int ValidationRequestId
        {
            get => _validationRequestId;
            private set => SetProperty(ref _validationRequestId, value);
        }

        public string ValidationFocusTarget
        {
            get => _validationFocusTarget;
            private set => SetProperty(ref _validationFocusTarget, value);
        }

        public SlotParameterEditorField? ValidationFocusField
        {
            get => _validationFocusField;
            private set => SetProperty(ref _validationFocusField, value);
        }

        // ---- Header ----

        public string HeaderText => IsConfigurationActive
            ? (EditorMode == SlotEditorMode.Edit
                ? string.Format(_loc["Dialog.SlotConfig.HeaderFormat"], Slot?.Slot ?? 0, Slot?.Label ?? "")
                : (Slot?.Slot > 0
                    ? string.Format(_loc["Dialog.AddSlot.CreateSlotFormat"], Slot.Slot)
                    : _loc["Dialog.AddSlot.CreateSlot"]))
            : _loc["Dialog.AddSlot.CreateSlot"];

        public string HeaderStatusText => !IsConfigurationActive
            ? string.Empty
            : HasBlockingIssue
                ? _loc["Dialog.AddSlot.NeedsSetup"]
                : ValidationSeverity == ValidationSeverity.Warning
                    ? _loc["Dialog.AddSlot.DraftProgress"]
                    : _loc["Dialog.AddSlot.ReadyToSave"];

        public bool HasCriticalValidationState => Slot != null && (HasBlockingIssue || ValidationSeverity == ValidationSeverity.Error);

        // ---- Preview / simplified header elements ----

        public string PreviewTitle => Slot?.Presentation.Title ?? _loc["Dialog.AddSlot.NewSlot"];

        public string PreviewHealthBadge => Slot?.Presentation.HealthBadgeText ?? (IsCreateMode ? _loc["Dialog.AddSlot.Draft"] : "Ready");

        public string PreviewHealthToneKey => Slot?.Presentation.HealthToneKey ?? "SlotHealthBrushReady";

        // ---- Card expander states ----

        public bool IsAppearanceExpanded => EditorMode == SlotEditorMode.Edit;

        public bool IsAdvancedExpanded => false;

        // ---- Appearance section ----

        public string AppearanceDisclosureTitle => _loc["Dialog.AddSlot.Appearance"];

        // ---- Sub-Actions section ----

        public ObservableCollection<SubSlotEditorRow> SubActions { get; } = new();

        public IReadOnlyList<SubSlotPluginOption> AvailablePlugins { get; }

        public bool HasSubActions => SubActions.Count > 0;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsFanLayout))]
        [NotifyPropertyChangedFor(nameof(IsRingLayout))]
        private SubMenuLayoutStyle _cascadeLayoutStyle = SubMenuLayoutStyle.Fan;

        public bool IsFanLayout
        {
            get => CascadeLayoutStyle == SubMenuLayoutStyle.Fan;
            set
            {
                if (value)
                {
                    CascadeLayoutStyle = SubMenuLayoutStyle.Fan;
                }
            }
        }

        public bool IsRingLayout
        {
            get => CascadeLayoutStyle == SubMenuLayoutStyle.Ring;
            set
            {
                if (value)
                {
                    CascadeLayoutStyle = SubMenuLayoutStyle.Ring;
                }
            }
        }

        public string SubActionsSectionTitle => _loc["Dialog.AddSlot.SubActions"];

        /// <summary>
        /// [ADR-024 D2] Fan renders at most <see cref="SubMenuLayoutEngine.FanMaxSlots"/>
        /// wings; a larger set silently falls back to Ring geometry. The engine cap is the
        /// single source of truth, so the editor can never drift from what is rendered.
        /// </summary>
        public int FanMaxSubActions => SubMenuLayoutEngine.FanMaxSlots;

        /// <summary>
        /// [ADR-024 D2] True when the configured slot would not actually render as a Fan.
        /// Historically this was invisible: with 8 slots/page a Fan slot carrying 4-8
        /// sub-actions was drawn as Ring and the user never learned why.
        /// </summary>
        public bool IsFanOverCap => IsFanLayout && SubActions.Count > FanMaxSubActions;

        public string FanOverCapWarning => string.Format(
            _loc["Dialog.AddSlot.SubActions.FanOverCap"],
            FanMaxSubActions);

        // ---- Constructor ----

        public SlotEditorViewModel(
            SlotEditorMode editorMode,
            IReadOnlyList<SlotTypeCard> allTypeCards,
            Func<string, PluginSlot> createSlotDraft,
            Action<PluginSlot, string?> setAction,
            Func<SlotParameterEditorField, Task> pickParameterValueAsync,
            Func<PluginSlot, Task> pickIconAsync,
            Func<PluginSlot, Task> pickColorAsync,
            ILocalizationService loc,
            PluginSlot? existingSlot = null,
            IPluginMetadataRegistry? metadataRegistry = null,
            Func<string, SecretDisplayMetadata?>? secretDisplayResolver = null,
            Func<PluginSlot, Task>? commitInPlaceAsync = null)
        {
            EditorMode = editorMode;
            _allTypeCards = allTypeCards;
            _createSlotDraft = createSlotDraft;
            _setAction = setAction;
            _pickParameterValueAsync = pickParameterValueAsync;
            _pickIconAsync = pickIconAsync;
            _pickColorAsync = pickColorAsync;
            _loc = loc;
            _metadataRegistry = metadataRegistry;
            _secretDisplayResolver = secretDisplayResolver;
            _commitInPlaceAsync = commitInPlaceAsync;
            AvailablePlugins = BuildAvailablePlugins();

            PrimaryButtonText = _loc["Dialog.AddSlot.SaveSlot"];
            SecondaryButtonText = _loc["Dialog.AddSlot.Cancel"];

            if (editorMode == SlotEditorMode.Edit && existingSlot != null)
            {
                Slot = existingSlot;
                _cascadeLayoutStyle = existingSlot.CascadeLayoutStyle ?? SubMenuLayoutStyle.Fan;
                _isConfigurationActive = true;
                HookSlotPropertyChanged();
                ReloadSubActionRows();
            }
            else
            {
                Slot = new PluginSlot { Slot = 0, PluginId = string.Empty };
                _isConfigurationActive = false;
            }

            // 嵌入式新建（tab）：选择类型前无槽可保存，隐藏主按钮（模态语义不变）。
            if (IsEmbedded && IsCreateMode)
            {
                IsPrimaryButtonVisible = IsConfigurationActive;
            }

            // [ADR-024 D2] The Fan over-cap warning tracks the live sub-action count,
            // so every mutation path (add / remove / clear / reload) refreshes it.
            SubActions.CollectionChanged += (_, _) =>
            {
                OnPropertyChanged(nameof(IsFanOverCap));
                OnPropertyChanged(nameof(FanOverCapWarning));
            };
        }

        // ---- IDialogViewModel ----

        public Action<DialogResult>? RequestClose { get; set; }

        public Task<bool> CanCloseAsync(DialogResult result)
        {
            if (result == DialogResult.Confirmed)
            {
                return Task.FromResult(Slot != null && !HasBlockingIssue);
            }
            return Task.FromResult(true);
        }

        // ---- Commands ----

        [RelayCommand]
        private void Cancel()
        {
            RequestClose?.Invoke(DialogResult.Cancelled);
        }

        [RelayCommand]
        private async Task Save()
        {
            _shouldShowFieldValidation = true;
            ValidateFieldStates();

            if (Slot == null || HasBlockingIssue)
            {
                NotifyAll();
                QueueValidationFocusRequest();
                return;
            }

            MaterializeSubActions();

            // 嵌入式（tab）：不关对话框，回调宿主提交（CommitCreatedSlot + tab 重注册）。
            if (_commitInPlaceAsync != null && CreatedSlot != null)
            {
                await _commitInPlaceAsync(CreatedSlot);
                return;
            }

            RequestClose?.Invoke(DialogResult.Confirmed);
        }

        [RelayCommand]
        private void SelectSlotType(SlotTypeCard card)
        {
            if (card == null)
                return;

            _shouldShowFieldValidation = false;
            ResetSuggestionState();

            Slot = _createSlotDraft(card.PluginId);

            if (!string.IsNullOrWhiteSpace(card.DefaultAction))
            {
                _setAction(Slot, card.DefaultAction);
            }

            ApplySuggestions();
            ReloadSubActionRows();
            IsConfigurationActive = true;
            HookSlotPropertyChanged();
            NotifyAll();
        }

        [RelayCommand]
        private void GoBackToPicker()
        {
            if (Slot != null)
            {
                UnhookSlotPropertyChanged();
            }

            _shouldShowFieldValidation = false;
            Slot = new PluginSlot { Slot = 0, PluginId = string.Empty };
            ResetSuggestionState();
            ClearSubActionRows();
            IsConfigurationActive = false;
            NotifyAll();
        }

        // ---- Sub-Action commands ----

        [RelayCommand]
        private void AddSubAction()
        {
            if (Slot == null)
            {
                return;
            }

            var row = HookRow(new SubSlotEditorRow(
                null,
                _metadataRegistry,
                PickSubActionParameterValueAsync,
                _secretDisplayResolver,
                AvailablePlugins));

            // 新行默认展开（3.4 手风琴）：这是用户主动添加，展开是期待的结果；
            // 互斥裁决同时收起旧行，焦点即落新行。【默认折叠】（2026-09-10 用户反馈）
            // 只适用于从已保存配置载入的行 —— 见 ReloadSubActionRows。
            row.IsExpanded = true;
            SubActions.Add(row);
            OnPropertyChanged(nameof(HasSubActions));
        }

        [RelayCommand]
        private void RemoveSubAction(SubSlotEditorRow row)
        {
            if (row == null || !SubActions.Remove(row))
            {
                return;
            }

            UnhookRow(row);
            row.Dispose();
            OnPropertyChanged(nameof(HasSubActions));
        }

        [RelayCommand]
        private void MoveSubActionUp(SubSlotEditorRow row)
        {
            if (row == null)
            {
                return;
            }

            var index = SubActions.IndexOf(row);
            if (index <= 0)
            {
                return;
            }

            SubActions.Move(index, index - 1);
        }

        [RelayCommand]
        private void MoveSubActionDown(SubSlotEditorRow row)
        {
            if (row == null)
            {
                return;
            }

            var index = SubActions.IndexOf(row);
            if (index < 0 || index >= SubActions.Count - 1)
            {
                return;
            }

            SubActions.Move(index, index + 1);
        }

        // ---- Public methods ----

        public void SetAction(string? action)
        {
            if (Slot == null || string.IsNullOrWhiteSpace(Slot.PluginId))
                return;

            _setAction(Slot, action);
            ApplySuggestions();
            if (_shouldShowFieldValidation)
            {
                ValidateFieldStates();
            }
            NotifyAll();
        }

        [RelayCommand]
        public async Task PickParameterValueAsync(SlotParameterEditorField field)
        {
            await _pickParameterValueAsync(field);
            ApplySuggestions();
            if (_shouldShowFieldValidation)
            {
                ValidateFieldStates();
            }
            NotifyAll();
        }

        public async Task PickIconAsync()
        {
            if (Slot == null)
                return;

            await _pickIconAsync(Slot);
            NotifyAll();
        }

        [RelayCommand]
        public async Task PickColorAsync()
        {
            if (Slot == null)
                return;

            await _pickColorAsync(Slot);
            _lastSuggestedColor = Slot.Color;
            NotifyAll();
        }

        // ---- Sub-Action helpers ----

        private IReadOnlyList<SubSlotPluginOption> BuildAvailablePlugins()
        {
            if (_metadataRegistry == null)
            {
                return Array.Empty<SubSlotPluginOption>();
            }

            var allMetadata = _metadataRegistry.GetAllMetadata();
            if (allMetadata == null)
            {
                return Array.Empty<SubSlotPluginOption>();
            }

            return allMetadata
                .Where(metadata => metadata.Actions.Count > 0)
                .OrderBy(metadata => metadata.Display.Name, StringComparer.OrdinalIgnoreCase)
                .Select(metadata => new SubSlotPluginOption(
                    metadata.Id,
                    BuiltInPluginDisplayModel.FromMetadata(metadata, _loc).DisplayName))
                .ToList();
        }

        private void ReloadSubActionRows()
        {
            ClearSubActionRows();

            if (Slot?.SubActions == null)
            {
                OnPropertyChanged(nameof(HasSubActions));
                return;
            }

            // [2026-09-10 用户反馈] 从已保存配置载入的行一律折叠：多数 slot 没有子动作，
        // 少数有的也不该一进门就占满一屏；展开是用户按需动作。
            foreach (var descriptor in Slot.SubActions)
            {
                var row = HookRow(new SubSlotEditorRow(
                    descriptor,
                    _metadataRegistry,
                    PickSubActionParameterValueAsync,
                    _secretDisplayResolver,
                    AvailablePlugins));

                row.IsExpanded = false;
                SubActions.Add(row);
            }

            OnPropertyChanged(nameof(HasSubActions));
        }

        private void ClearSubActionRows()
        {
            foreach (var row in SubActions)
            {
                UnhookRow(row);
                row.Dispose();
            }

            SubActions.Clear();
            OnPropertyChanged(nameof(HasSubActions));
        }

        /// <summary>
        /// 挂上行的展开互斥裁决（[3.4 手风琴]：任一行展开时收起其余行），并返回该行。
        /// 订阅随行的加入/移除成对挂钩，Dispose 前先解绑。
        /// 注意本方法不管初始展开态——折叠策略由调用点决定
        /// （<see cref="AddSubAction"/> 展开、<see cref="ReloadSubActionRows"/> 折叠）。
        /// </summary>
        private SubSlotEditorRow HookRow(SubSlotEditorRow row)
        {
            row.PropertyChanged += OnSubActionRowPropertyChanged;
            return row;
        }

        private void UnhookRow(SubSlotEditorRow row)
        {
            row.PropertyChanged -= OnSubActionRowPropertyChanged;
        }

        private void OnSubActionRowPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(SubSlotEditorRow.IsExpanded)
                || sender is not SubSlotEditorRow expandedRow
                || !expandedRow.IsExpanded)
            {
                return;
            }

            foreach (var row in SubActions)
            {
                if (!ReferenceEquals(row, expandedRow) && row.IsExpanded)
                {
                    row.IsExpanded = false;
                }
            }
        }

        private void MaterializeSubActions()
        {
            if (Slot == null)
            {
                return;
            }

            Slot.SubActions = SubActions.Count > 0
                ? SubActions.Select(row => row.ToDescriptor()).ToList()
                : null;

            // Persist only a non-default (Ring) choice so the optional key is
            // omitted for Fan, keeping legacy profiles byte-compatible.
            Slot.CascadeLayoutStyle = CascadeLayoutStyle == SubMenuLayoutStyle.Fan
                ? null
                : CascadeLayoutStyle;
        }

        private Task PickSubActionParameterValueAsync(SlotParameterEditorField field)
        {
            return _pickParameterValueAsync(field);
        }

        [RelayCommand]
        public async Task PickSubActionIconAsync(SubSlotEditorRow row)
        {
            if (row == null)
            {
                return;
            }

            await _pickIconAsync(row.BackingSlot);
            row.IconKey = row.BackingSlot.IconKey;
        }

        [RelayCommand]
        public async Task PickSubActionColorAsync(SubSlotEditorRow row)
        {
            if (row == null)
            {
                return;
            }

            await _pickColorAsync(row.BackingSlot);
            row.ColorHex = row.BackingSlot.Color;
        }

        // ---- Slot PropertyChanged handling ----

        private void HookSlotPropertyChanged()
        {
            if (Slot != null)
            {
                Slot.PropertyChanged += OnSlotPropertyChanged;
            }
        }

        private void UnhookSlotPropertyChanged()
        {
            if (Slot != null)
            {
                Slot.PropertyChanged -= OnSlotPropertyChanged;
            }
        }

        private void OnSlotPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (_isApplyingSuggestions)
                return;

            // Action changes arrive here too (applied via the action selector options);
            // suggestions (label/icon) and the visible fields depend on the action.
            ApplySuggestions();
            NotifyAll();
        }

        // ---- Notification (single unified refresh) ----

        private void NotifyAll()
        {
            SyncSelectedActionStates();
            ValidateFieldStates();

            // 嵌入式新建（tab）：主/次按钮随相位切换（选择相位无槽可保存/无源可返回）。
            if (IsEmbedded && IsCreateMode)
            {
                IsPrimaryButtonVisible = IsConfigurationActive;
            }
            OnPropertyChanged(nameof(SecondaryCommand));
            OnPropertyChanged(nameof(AvailableActions));
            OnPropertyChanged(nameof(RequiredParameters));
            OnPropertyChanged(nameof(OptionalParameters));
            OnPropertyChanged(nameof(AdvancedParameters));
            OnPropertyChanged(nameof(SummaryTokens));
            OnPropertyChanged(nameof(HasSingleAction));
            OnPropertyChanged(nameof(HasMultipleActions));
            OnPropertyChanged(nameof(UseSegmentedButtons));
            OnPropertyChanged(nameof(UseComboBox));
            OnPropertyChanged(nameof(HasRequiredParameters));
            OnPropertyChanged(nameof(HasOptionalParameters));
            OnPropertyChanged(nameof(HasAdvancedParameters));
            OnPropertyChanged(nameof(HasOptionalSettings));
            OnPropertyChanged(nameof(HasSummaryTokens));
            OnPropertyChanged(nameof(HasAppearanceOptions));
            OnPropertyChanged(nameof(HasSubActions));
            OnPropertyChanged(nameof(HeaderText));
            OnPropertyChanged(nameof(HeaderStatusText));
            OnPropertyChanged(nameof(HasActionValidationError));
            OnPropertyChanged(nameof(ActionValidationMessage));
            OnPropertyChanged(nameof(HasBlockingIssue));
            OnPropertyChanged(nameof(BlockingIssueText));
            OnPropertyChanged(nameof(HasValidationSummary));
            OnPropertyChanged(nameof(ValidationSeverity));
            OnPropertyChanged(nameof(ValidationSummary));
            OnPropertyChanged(nameof(HasCriticalValidationState));
            OnPropertyChanged(nameof(PreviewTitle));
            OnPropertyChanged(nameof(PreviewHealthBadge));
            OnPropertyChanged(nameof(PreviewHealthToneKey));
            OnPropertyChanged(nameof(HasSelectedSlot));
            OnPropertyChanged(nameof(CreatedSlot));
            OnPropertyChanged(nameof(IsAppearanceExpanded));
            OnPropertyChanged(nameof(AppearanceDisclosureTitle));
            OnPropertyChanged(nameof(PrimaryButtonText));
            OnPropertyChanged(nameof(SecondaryButtonText));
        }

        // ---- Validation ----

        private void SyncSelectedActionStates()
        {
            if (Slot == null)
                return;

            foreach (var option in Slot.AvailableActions)
            {
                option.IsSelected = string.Equals(option.Value, Slot.Action, StringComparison.OrdinalIgnoreCase);
            }
        }

        private string GetBlockingIssueText()
        {
            if (!IsConfigurationActive || Slot == null || string.IsNullOrWhiteSpace(Slot.PluginId))
                return _loc["Dialog.AddSlot.ChooseTypeBegin"];

            if (string.IsNullOrWhiteSpace(Slot.Action))
                return _loc["Dialog.AddSlot.SelectActionValidation"];

            var missingRequired = RequiredParameters
                .Where(parameter => parameter.IsRequired && !parameter.HasValue)
                .Select(parameter => parameter.Label)
                .ToList();

            if (missingRequired.Count > 0)
                return string.Format(_loc["Dialog.AddSlot.CompleteRequiredFormat"], string.Join(", ", missingRequired));

            return string.Empty;
        }

        private void ValidateFieldStates()
        {
            foreach (var field in RequiredParameters)
            {
                field.ValidationMessage = _shouldShowFieldValidation && !field.HasValue
                    ? string.Format(_loc["Dialog.AddSlot.FieldRequiredFormat"], field.Label)
                    : string.Empty;
            }

            foreach (var field in OptionalParameters)
            {
                field.ValidationMessage = string.Empty;
            }

            foreach (var field in AdvancedParameters)
            {
                field.ValidationMessage = string.Empty;
            }
        }

        private void QueueValidationFocusRequest()
        {
            if (HasActionValidationError)
            {
                ValidationFocusField = null;
                ValidationFocusTarget = "action";
                ValidationRequestId++;
                return;
            }

            var firstMissingField = RequiredParameters
                .Concat(OptionalParameters)
                .Concat(AdvancedParameters)
                .FirstOrDefault(field => field.IsRequired && !field.HasValue);

            ValidationFocusField = firstMissingField;
            ValidationFocusTarget = firstMissingField == null ? string.Empty : "field";
            ValidationRequestId++;
        }

        // ---- Suggestions (from SlotActionMetadata) ----

        private void ApplySuggestions()
        {
            if (Slot == null || string.IsNullOrWhiteSpace(Slot.PluginId))
                return;

            _isApplyingSuggestions = true;

            try
            {
                var actionMeta = _metadataRegistry?.GetActionMetadata(Slot.PluginId, Slot.Action);

                string suggestedLabel = BuildSuggestedLabel(Slot, actionMeta);
                if (string.IsNullOrWhiteSpace(Slot.Label) || string.Equals(Slot.Label, _lastSuggestedLabel, StringComparison.Ordinal))
                {
                    Slot.Label = suggestedLabel;
                }
                _lastSuggestedLabel = suggestedLabel;

                string suggestedIcon = BuildSuggestedIcon(Slot, actionMeta);
                if (string.IsNullOrWhiteSpace(Slot.IconKey) || string.Equals(Slot.IconKey, _lastSuggestedIcon, StringComparison.Ordinal))
                {
                    Slot.IconKey = suggestedIcon;
                }
                _lastSuggestedIcon = suggestedIcon;

                _lastSuggestedColor = BuildSuggestedColor(actionMeta);
            }
            finally
            {
                _isApplyingSuggestions = false;
            }
        }

        private string BuildSuggestedLabel(PluginSlot slot, SlotActionMetadata? actionMeta)
        {
            if (actionMeta != null && !string.IsNullOrWhiteSpace(actionMeta.SuggestedLabelTemplate))
            {
                var template = actionMeta.SuggestedLabelTemplate;
                if (template.Contains("{app}", StringComparison.OrdinalIgnoreCase)
                    && slot.Args.TryGetValue("app", out var appVal) && !string.IsNullOrWhiteSpace(appVal))
                {
                    return template.Replace("{app}", ToTitle(appVal), StringComparison.OrdinalIgnoreCase);
                }
                if (template.Contains("{path}", StringComparison.OrdinalIgnoreCase)
                    && slot.Args.TryGetValue("path", out var pathVal) && !string.IsNullOrWhiteSpace(pathVal))
                {
                    return template.Replace("{path}", ExtractName(pathVal, "Item"), StringComparison.OrdinalIgnoreCase);
                }
                if (template.Contains("{keys}", StringComparison.OrdinalIgnoreCase)
                    && slot.Args.TryGetValue("keys", out var keysVal) && !string.IsNullOrWhiteSpace(keysVal))
                {
                    return template.Replace("{keys}", keysVal, StringComparison.OrdinalIgnoreCase);
                }
                return template;
            }

            return !string.IsNullOrWhiteSpace(slot.ActionLabel)
                ? slot.ActionLabel
                : $"Slot {slot.Slot}";
        }

        private string BuildSuggestedIcon(PluginSlot slot, SlotActionMetadata? actionMeta)
        {
            if (actionMeta != null && !string.IsNullOrWhiteSpace(actionMeta.SuggestedIconKey))
                return actionMeta.SuggestedIconKey;

            var pluginMeta = _metadataRegistry?.GetMetadata(slot.PluginId);
            if (pluginMeta != null && !string.IsNullOrWhiteSpace(pluginMeta.Display.IconKey))
                return pluginMeta.Display.IconKey;

            return string.Empty;
        }

        private static string BuildSuggestedColor(SlotActionMetadata? actionMeta)
        {
            if (actionMeta != null && !string.IsNullOrWhiteSpace(actionMeta.SuggestedColorHex))
                return actionMeta.SuggestedColorHex;

            return string.Empty;
        }

        // ---- Utility methods ----

        private void ResetSuggestionState()
        {
            _lastSuggestedLabel = string.Empty;
            _lastSuggestedIcon = string.Empty;
            _lastSuggestedColor = string.Empty;
        }

        private static string ExtractName(string rawValue, string fallback)
        {
            if (string.IsNullOrWhiteSpace(rawValue))
                return fallback;

            if (Uri.TryCreate(rawValue, UriKind.Absolute, out var uri)
                && !string.IsNullOrWhiteSpace(uri.Host))
            {
                return ToTitle(uri.Host.Replace("www.", string.Empty, StringComparison.OrdinalIgnoreCase));
            }

            string expanded = Environment.ExpandEnvironmentVariables(rawValue.Trim());

            try
            {
                string? fileName = Path.GetFileNameWithoutExtension(expanded);
                if (!string.IsNullOrWhiteSpace(fileName))
                    return ToTitle(fileName);
            }
            catch
            {
            }

            return ToTitle(expanded);
        }

        private static string ToTitle(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            var separators = new[] { '-', '_', '.', '/' };
            string normalized = separators.Aggregate(value.Trim(), (current, sep) => current.Replace(sep, ' '));
            var words = normalized
                .Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(word => word.Length == 1
                    ? word.ToUpperInvariant()
                    : char.ToUpperInvariant(word[0]) + word[1..].ToLowerInvariant());

            return string.Join(" ", words);
        }

        // ---- Filtered cards for search / browse-all ----

        public IReadOnlyList<SlotTypeCard> FilteredCards
        {
            get
            {
                if (string.IsNullOrWhiteSpace(SearchText))
                    return _allTypeCards;

                return _allTypeCards
                    .Where(c => c.Title.Contains(SearchText, StringComparison.OrdinalIgnoreCase)
                        || c.Description.Contains(SearchText, StringComparison.OrdinalIgnoreCase)
                        || c.Category.Contains(SearchText, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }
        }

        public IReadOnlyList<IGrouping<string, SlotTypeCard>> BrowseAllCategories
        {
            get
            {
                var allCards = IsBrowseExpanded
                    ? _allTypeCards
                    : _allTypeCards.Where(c => c.IsPrimary);

                if (!string.IsNullOrWhiteSpace(SearchText))
                {
                    allCards = allCards
                        .Where(c => c.Title.Contains(SearchText, StringComparison.OrdinalIgnoreCase)
                            || c.Description.Contains(SearchText, StringComparison.OrdinalIgnoreCase))
                        .ToList();
                }

                return allCards
                    .GroupBy(c => c.Category, StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }
        }

        // ---- Update filters when search text changes ----

        partial void OnSearchTextChanged(string value)
        {
            OnPropertyChanged(nameof(FilteredCards));
            OnPropertyChanged(nameof(BrowseAllCategories));
        }

        partial void OnIsBrowseExpandedChanged(bool value)
        {
            OnPropertyChanged(nameof(BrowseAllCategories));
        }
    }
}
