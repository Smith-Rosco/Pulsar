using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pulsar.Core.Localization;
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

            UsageProfiles = new ObservableCollection<UsageProfileOption>
            {
                new() { Value = OnboardingUsageProfile.GeneralProductivity, Title = _loc["FirstLaunch.GeneralProductivity"], Description = _loc["FirstLaunch.GeneralProductivityDesc"] },
                new() { Value = OnboardingUsageProfile.DeveloperWorkflow, Title = _loc["FirstLaunch.DeveloperWorkflow"], Description = _loc["FirstLaunch.DeveloperWorkflowDesc"] },
                new() { Value = OnboardingUsageProfile.BrowserAndDocs, Title = _loc["FirstLaunch.BrowserDocs"], Description = _loc["FirstLaunch.BrowserDocsDesc"] }
            };

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
                        ClearError();
                    }
                };
            }

            SelectProfile(UsageProfiles[0]);
        }

        public ObservableCollection<UsageProfileOption> UsageProfiles { get; }

        public ObservableCollection<AppOption> CommonApps { get; }

        public string Title => _loc["FirstLaunch.SetupTitle"];

        public string Description => _loc["FirstLaunch.SetupDescription"];

        public string SelectionHint => _loc["FirstLaunch.SetupHint"];

        public string UsageProfileLabel => _loc["FirstLaunch.UsageScenario"];

        public string StarterAppsLabel => _loc["FirstLaunch.LaunchApps"];

        public string SelectedLabel => _loc["FirstLaunch.Selected"];

        public string SelectedAppCountLabel => _loc["FirstLaunch.SelectedApps"];

        public int SelectedAppCount => CommonApps.Count(app => app.IsSelected);

        public bool CanFinish => SelectedProfile != null && SelectedAppCount > 0;

        public string PrimaryButtonText => _loc["FirstLaunch.CreateConfig"];

        public string SecondaryButtonText => _loc["FirstLaunch.Skip"];

        public string FooterDescription => _loc["FirstLaunch.Footer"];

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

            await _configService.SaveAsync(config);
            await _onboardingStateService.MarkSetupCompletedAsync();
            RequestClose?.Invoke(DialogResult.Confirmed);
        }

        [RelayCommand]
        private async Task Skip()
        {
            await _onboardingStateService.MarkOnboardingSkippedAsync();
            RequestClose?.Invoke(DialogResult.Cancelled);
        }

        public Task<bool> CanCloseAsync(DialogResult result)
        {
            if (result == DialogResult.Confirmed)
            {
                return Task.FromResult(Validate());
            }

            return Task.FromResult(true);
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
