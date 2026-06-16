using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pulsar.Core.Localization;
using Pulsar.Models;
using Pulsar.Features.Tutorial.Models;
using Pulsar.Services.Interfaces;
using Pulsar.Features.Tutorial.Services;
using Pulsar.ViewModels.Base;
using DialogResult = Pulsar.Models.Enums.DialogResult;

namespace Pulsar.ViewModels.Dialogs
{
    public partial class FirstLaunchSetupWizardViewModel : ObservableObject, IWizardDialogViewModel
    {
        private readonly IOnboardingTemplateService _templateService;
        private readonly IConfigService _configService;
        private readonly IOnboardingStateService _onboardingStateService;
        private readonly ILocalizationService _loc;
        private readonly TutorialScenarioRegistry _scenarioRegistry;

        public FirstLaunchSetupWizardViewModel(
            IOnboardingTemplateService templateService,
            IConfigService configService,
            IOnboardingStateService onboardingStateService,
            ILocalizationService localizationService)
            : this(templateService, configService, onboardingStateService, localizationService,
                  new TutorialScenarioRegistry())
        {
        }

        public FirstLaunchSetupWizardViewModel(
            IOnboardingTemplateService templateService,
            IConfigService configService,
            IOnboardingStateService onboardingStateService,
            ILocalizationService localizationService,
            TutorialScenarioRegistry scenarioRegistry)
        {
            _templateService = templateService;
            _configService = configService;
            _onboardingStateService = onboardingStateService;
            _loc = localizationService;
            _scenarioRegistry = scenarioRegistry;

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

            _selectedLanguage = SupportedLanguages.FirstOrDefault(l => l.Code == "zh-CN") ?? SupportedLanguages.FirstOrDefault();
            if (_selectedLanguage != null)
                _loc.SetLanguage("zh-CN");
        }

        public ObservableCollection<LanguageDisplayModel> SupportedLanguages { get; }

        [ObservableProperty]
        private LanguageDisplayModel? _selectedLanguage;

        partial void OnSelectedLanguageChanged(LanguageDisplayModel? value)
        {
            if (value == null) return;
            if (string.Equals(_loc.CurrentLanguage, value.Code, System.StringComparison.OrdinalIgnoreCase))
                return;

            _loc.SetLanguage(value.Code);
            OnPropertyChanged(nameof(SetupTitle));
            OnPropertyChanged(nameof(Description));
            OnPropertyChanged(nameof(FeatureIntro));
            OnPropertyChanged(nameof(FeatureSwitchModeTitle));
            OnPropertyChanged(nameof(FeatureSwitchModeDesc));
            OnPropertyChanged(nameof(FeatureCommandModeTitle));
            OnPropertyChanged(nameof(FeatureCommandModeDesc));
            OnPropertyChanged(nameof(SetupFooter));
            OnPropertyChanged(nameof(PrimaryButtonText));
            OnPropertyChanged(nameof(SecondaryButtonText));
            OnPropertyChanged(nameof(LanguageLabel));
        }

        private static readonly IReadOnlyList<OnboardingAppSelection> DefaultApps = new List<OnboardingAppSelection>
        {
            new() { Id = "explorer", DisplayName = "File Explorer", ProcessName = "explorer", LaunchPath = "explorer.exe", IconKey = "\uE8B7" }
        };

        public string LanguageLabel => _loc["Settings.General.Language"];

        public string SetupTitle => _loc["FirstLaunch.SetupTitle"];

        public string Description => _loc["FirstLaunch.SetupDescription"];

        public string FeatureIntro => _loc["FirstLaunch.FeatureIntro"];

        public string FeatureSwitchModeTitle => _loc["FirstLaunch.Feature.SwitchModeTitle"];

        public string FeatureSwitchModeDesc => _loc["FirstLaunch.Feature.SwitchModeDesc"];

        public string FeatureCommandModeTitle => _loc["FirstLaunch.Feature.CommandModeTitle"];

        public string FeatureCommandModeDesc => _loc["FirstLaunch.Feature.CommandModeDesc"];

        public string SetupFooter => _loc["FirstLaunch.SetupFooter"];

        public string PrimaryButtonText => _loc["FirstLaunch.CreateConfig"];

        public string SecondaryButtonText => _loc["FirstLaunch.Skip"];

        public bool IsPrimaryButtonVisible => true;

        public bool IsSecondaryButtonVisible => true;

        public ICommand PrimaryCommand => FinishCommand;

        public ICommand SecondaryCommand => SkipCommand;

        public Action<DialogResult>? RequestClose { get; set; }

        [RelayCommand]
        private async Task Finish()
        {
            var scenario = _scenarioRegistry.Default;
            var apps = BuildScenarioApps(scenario);
            var config = _templateService.BuildInitialConfig(scenario, apps);
            config.Settings.SelectedTutorialScenarioId = scenario.Id;

            if (SelectedLanguage != null)
                config.Settings.Language = SelectedLanguage.Code;

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
            if (result == DialogResult.None)
            {
                await _onboardingStateService.MarkOnboardingSkippedAsync();
                _configService.ScheduleSmartDetection();
            }

            return true;
        }

        private IReadOnlyList<OnboardingAppSelection> BuildScenarioApps(TutorialScenario scenario)
        {
            var allApps = new List<OnboardingAppSelection>(DefaultApps);
            var available = _templateService.GetAvailableApps();

            foreach (var appId in scenario.RequiredAppIds)
            {
                if (allApps.Any(a => string.Equals(a.Id, appId, StringComparison.OrdinalIgnoreCase)))
                    continue;

                var match = available.FirstOrDefault(a =>
                    string.Equals(a.Id, appId, StringComparison.OrdinalIgnoreCase));
                if (match != null)
                    allApps.Add(match);
            }

            return allApps;
        }
    }
}
