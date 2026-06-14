using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pulsar.Core.Localization;
using Pulsar.Models;
using Pulsar.Services.Interfaces;
using Pulsar.Services.Tutorial;
using Pulsar.ViewModels.Base;
using DialogResult = Pulsar.Models.Enums.DialogResult;

namespace Pulsar.ViewModels.Dialogs
{
    public partial class FirstLaunchSetupWizardViewModel : ObservableObject, IWizardDialogViewModel
    {
        public partial class UsageProfileOption : ObservableObject
        {
            public required OnboardingUsageProfile Value { get; init; }

            public required string Title { get; init; }

            public required string Description { get; init; }

            public required string SlotDescription { get; init; }

            [ObservableProperty]
            private bool _isSelected;
        }

        public partial class AppOption : ObservableObject
        {
            public required string Id { get; init; }

            public required string DisplayName { get; init; }

            public required string Subtitle { get; init; }

            public required string IconKey { get; init; }

            public required OnboardingAppSelection App { get; init; }

            [ObservableProperty]
            private bool _isSelected;
        }

        private readonly IOnboardingTemplateService _templateService;
        private readonly IConfigService _configService;
        private readonly IOnboardingStateService _onboardingStateService;
        private readonly ILocalizationService _loc;

        [ObservableProperty]
        private string _errorMessage = string.Empty;

        [ObservableProperty]
        private bool _hasError;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(CanFinish))]
        [NotifyPropertyChangedFor(nameof(ConfigPreviewSummary))]
        private UsageProfileOption? _selectedProfile;

        public FirstLaunchSetupWizardViewModel(
            IOnboardingTemplateService templateService,
            IConfigService configService,
            IOnboardingStateService onboardingStateService,
            ILocalizationService localizationService)
        {
            _templateService = templateService;
            _configService = configService;
            _onboardingStateService = onboardingStateService;
            _loc = localizationService;

            SupportedLanguages = new ObservableCollection<LanguageDisplayModel>();
            foreach (var code in _loc.SupportedLanguages)
            {
                SupportedLanguages.Add(new LanguageDisplayModel
                {
                    Code = code,
                    DisplayName = code switch
                    {
                        "en" => "English",
                        "zh-CN" => "中文 (Chinese)",
                        _ => code
                    }
                });
            }

            var currentLang = _loc.CurrentLanguage;
            _selectedLanguage = SupportedLanguages.FirstOrDefault(l => l.Code == currentLang) ?? SupportedLanguages.FirstOrDefault();

            UsageProfiles = new ObservableCollection<UsageProfileOption>();
            BuildUsageProfiles();

            CommonApps = new ObservableCollection<AppOption>(
                _templateService.GetAvailableApps().Select(app => new AppOption
                {
                    Id = app.Id,
                    DisplayName = app.DisplayName,
                    Subtitle = app.ProcessName,
                    IconKey = app.IconKey,
                    App = app,
                    IsSelected = app.Id is "explorer" or "notepad"
                }));

            foreach (var app in CommonApps)
            {
                app.PropertyChanged += (_, args) =>
                {
                    if (args.PropertyName == nameof(AppOption.IsSelected))
                    {
                        OnPropertyChanged(nameof(SelectedAppCount));
                        OnPropertyChanged(nameof(CanFinish));
                        OnPropertyChanged(nameof(ConfigPreviewSummary));
                        ClearError();
                    }
                };
            }

            SelectProfile(UsageProfiles[0]);
        }

        public ObservableCollection<LanguageDisplayModel> SupportedLanguages { get; }

        [ObservableProperty]
        private LanguageDisplayModel? _selectedLanguage;

        partial void OnSelectedLanguageChanged(LanguageDisplayModel? value)
        {
            if (value == null) return;
            _loc.SetLanguage(value.Code);
            BuildUsageProfiles();
            RefreshLocalizedProperties();
        }

        private void BuildUsageProfiles()
        {
            UsageProfiles.Clear();
            UsageProfiles.Add(new()
            {
                Value = OnboardingUsageProfile.GeneralProductivity,
                Title = _loc["FirstLaunch.GeneralProductivity"],
                Description = _loc["FirstLaunch.GeneralProductivityDesc"],
                SlotDescription = _loc[OnboardingUsageProfile.GeneralProductivity.GetSlotDescriptionKey()]
            });
            UsageProfiles.Add(new()
            {
                Value = OnboardingUsageProfile.DeveloperWorkflow,
                Title = _loc["FirstLaunch.DeveloperWorkflow"],
                Description = _loc["FirstLaunch.DeveloperWorkflowDesc"],
                SlotDescription = _loc[OnboardingUsageProfile.DeveloperWorkflow.GetSlotDescriptionKey()]
            });
            UsageProfiles.Add(new()
            {
                Value = OnboardingUsageProfile.BrowserAndDocs,
                Title = _loc["FirstLaunch.BrowserDocs"],
                Description = _loc["FirstLaunch.BrowserDocsDesc"],
                SlotDescription = _loc[OnboardingUsageProfile.BrowserAndDocs.GetSlotDescriptionKey()]
            });
        }

        private void RefreshLocalizedProperties()
        {
            OnPropertyChanged(nameof(Title));
            OnPropertyChanged(nameof(Description));
            OnPropertyChanged(nameof(SelectionHint));
            OnPropertyChanged(nameof(UsageProfileLabel));
            OnPropertyChanged(nameof(StarterAppsLabel));
            OnPropertyChanged(nameof(SelectedLabel));
            OnPropertyChanged(nameof(SelectedAppCountLabel));
            OnPropertyChanged(nameof(PrimaryButtonText));
            OnPropertyChanged(nameof(SecondaryButtonText));
            OnPropertyChanged(nameof(FooterDescription));
            OnPropertyChanged(nameof(LanguageLabel));
            OnPropertyChanged(nameof(PreviewLabel));
            OnPropertyChanged(nameof(ConfigPreviewSummary));
        }

        public ObservableCollection<UsageProfileOption> UsageProfiles { get; }

        public ObservableCollection<AppOption> CommonApps { get; }

        public string Title => _loc["FirstLaunch.SetupTitle"];

        public string LanguageLabel => _loc["Settings.General.Language"];

        public string Description => _loc["FirstLaunch.SetupDescription"];

        public string SelectionHint => _loc["FirstLaunch.SetupHint"];

        public string UsageProfileLabel => _loc["FirstLaunch.UsageScenario"];

        public string StarterAppsLabel => _loc["FirstLaunch.LaunchApps"];

        public string SelectedLabel => _loc["FirstLaunch.Selected"];

        public string SelectedAppCountLabel => _loc["FirstLaunch.SelectedApps"];

        public int SelectedAppCount => CommonApps.Count(app => app.IsSelected);

        public bool CanFinish => SelectedProfile != null && SelectedAppCount > 0;

        public string ConfigPreviewSummary
        {
            get
            {
                if (SelectedProfile == null || SelectedAppCount == 0)
                    return string.Empty;

                var summary = _templateService.BuildPreviewSummary(new OnboardingTemplateRequest
                {
                    Profile = SelectedProfile.Value,
                    SelectedApps = CommonApps.Where(a => a.IsSelected).Select(a => a.App).ToList()
                });

                var commandLabel = _loc[summary.CommandSlotLabel];
                return string.Format(_loc["FirstLaunch.Preview.Format"] ?? "{0} Switch slots + {1} Command slot ({2})",
                    summary.SwitchSlotCount, summary.CommandSlotCount, commandLabel);
            }
        }

        public string PrimaryButtonText => _loc["FirstLaunch.CreateConfig"];

        public string SecondaryButtonText => _loc["FirstLaunch.Skip"];

        public string FooterDescription => _loc["FirstLaunch.Footer"];

        public string PreviewLabel => _loc["FirstLaunch.Preview"];

        public string ErrorChooseProfile => _loc["FirstLaunch.SelectScenarioError"];

        public string ErrorSelectApp => _loc["FirstLaunch.SelectAppError"];

        public bool IsPrimaryButtonVisible => true;

        public bool IsSecondaryButtonVisible => true;

        public ICommand PrimaryCommand => FinishCommand;

        public ICommand SecondaryCommand => SkipCommand;

        public ICommand SelectProfileCommand => new RelayCommand<UsageProfileOption>(SelectProfile);

        public Action<DialogResult>? RequestClose { get; set; }

    [RelayCommand]
    private async Task Finish()
    {
        if (!Validate())
        {
            return;
        }

        var config = _templateService.BuildInitialConfig(new OnboardingTemplateRequest
        {
            Profile = SelectedProfile!.Value,
            SelectedApps = CommonApps.Where(app => app.IsSelected).Select(app => app.App).ToList()
        });

        if (SelectedLanguage != null)
        {
            config.Settings.Language = SelectedLanguage.Code;
        }

        config.Settings.HasCompletedInitialDetection = true;

        await _configService.SaveAsync(config);
        await _onboardingStateService.MarkSetupCompletedAsync();
        RequestClose?.Invoke(DialogResult.Confirmed);
    }

        [RelayCommand]
        private async Task Skip()
        {
            await _onboardingStateService.MarkOnboardingSkippedAsync();
            _configService.ScheduleSmartDetection();
            RequestClose?.Invoke(DialogResult.Cancelled);
        }

        public async Task<bool> CanCloseAsync(DialogResult result)
        {
            if (result == DialogResult.Confirmed)
            {
                return Validate();
            }

            if (result == DialogResult.None)
            {
                await _onboardingStateService.MarkOnboardingSkippedAsync();
                _configService.ScheduleSmartDetection();
            }

            return true;
        }

        private void SelectProfile(UsageProfileOption? profile)
        {
            if (profile == null)
            {
                return;
            }

            foreach (var option in UsageProfiles)
            {
                option.IsSelected = ReferenceEquals(option, profile);
            }

            SelectedProfile = profile;
            ClearError();
        }

        private bool Validate()
        {
            if (SelectedProfile == null)
            {
                SetError(ErrorChooseProfile);
                return false;
            }

            if (SelectedAppCount == 0)
            {
                SetError(ErrorSelectApp);
                return false;
            }

            ClearError();
            return true;
        }

        private void SetError(string message)
        {
            ErrorMessage = message;
            HasError = true;
        }

        private void ClearError()
        {
            ErrorMessage = string.Empty;
            HasError = false;
        }
    }
}
