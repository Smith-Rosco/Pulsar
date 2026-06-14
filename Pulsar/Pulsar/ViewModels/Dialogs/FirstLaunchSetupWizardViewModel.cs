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
using Pulsar.Models.Tutorial;
using Pulsar.Services.Interfaces;
using Pulsar.Services.Tutorial;
using Pulsar.Services.Tutorial.Prerequisites;
using Pulsar.ViewModels.Base;
using DialogResult = Pulsar.Models.Enums.DialogResult;

namespace Pulsar.ViewModels.Dialogs
{
    public partial class FirstLaunchSetupWizardViewModel : ObservableObject, IWizardDialogViewModel
    {
        public partial class ScenarioOption : ObservableObject
        {
            public required TutorialScenario Scenario { get; init; }

            public required string Title { get; init; }

            public required string Description { get; init; }

            public required string SlotDescription { get; init; }

            public required string IconKey { get; init; }

            [ObservableProperty]
            private bool _isSelected;

            [ObservableProperty]
            private bool _prerequisitesLoading = true;

            [ObservableProperty]
            private string _prerequisiteSummary = string.Empty;
        }

        public partial class UsageProfileOption : ObservableObject
        {
            public required OnboardingUsageProfile Value { get; init; }

            public required string Title { get; init; }

            public required string Description { get; init; }

            public required string SlotDescription { get; init; }

            [ObservableProperty]
            private bool _isSelected;
        }

        private readonly IOnboardingTemplateService _templateService;
        private readonly IConfigService _configService;
        private readonly IOnboardingStateService _onboardingStateService;
        private readonly ILocalizationService _loc;
        private readonly TutorialScenarioRegistry _scenarioRegistry;

        [ObservableProperty]
        private string _errorMessage = string.Empty;

        [ObservableProperty]
        private bool _hasError;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(CanFinish))]
        private UsageProfileOption? _selectedProfile;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(CanFinish))]
        private ScenarioOption? _selectedScenario;

        [ObservableProperty]
        private bool _isUsingScenarios = true;

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

            UsageProfiles = new ObservableCollection<UsageProfileOption>();
            BuildUsageProfiles();

            Scenarios = new ObservableCollection<ScenarioOption>();
            BuildScenarios();

            // 默认选中第一个场景，保证 CanFinish 在启动时有合理值
            if (Scenarios.Count > 0)
            {
                SelectScenario(Scenarios[0]);
            }

            SelectProfile(UsageProfiles[0]);

            SelectScenarioCommand = new RelayCommand<ScenarioOption>(SelectScenario);
            SelectProfileCommand = new RelayCommand<UsageProfileOption>(SelectProfile);
        }

        public ObservableCollection<LanguageDisplayModel> SupportedLanguages { get; }

        [ObservableProperty]
        private LanguageDisplayModel? _selectedLanguage;

        partial void OnSelectedLanguageChanged(LanguageDisplayModel? value)
        {
            if (value == null) return;
            if (string.Equals(_loc.CurrentLanguage, value.Code, System.StringComparison.OrdinalIgnoreCase))
                return;

            var previousScenarioId = SelectedScenario?.Scenario.Id;

            _loc.SetLanguage(value.Code);
            PrerequisiteResults.Clear();
            BuildUsageProfiles();
            BuildScenarios();

            // 重新选中之前选择的场景（Scenarios 已被重建，旧引用已失效）
            if (previousScenarioId != null)
            {
                var option = Scenarios.FirstOrDefault(s => s.Scenario.Id == previousScenarioId);
                if (option != null)
                    SelectScenario(option);
            }

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

        private void BuildScenarios()
        {
            var availableApps = _templateService.GetAvailableApps();
            Scenarios.Clear();
            foreach (var scenario in _scenarioRegistry.All)
            {
                var iconKey = ExtractScenarioIcon(scenario, availableApps);
                var option = new ScenarioOption
                {
                    Scenario = scenario,
                    Title = _loc[scenario.TitleKey],
                    Description = _loc[scenario.DescriptionKey],
                    SlotDescription = _loc[scenario.SlotDescriptionKey],
                    IconKey = iconKey,
                    PrerequisitesLoading = true
                };
                Scenarios.Add(option);

                _ = RunPrerequisiteChecksAsync(option);
            }
        }

        private static string ExtractScenarioIcon(TutorialScenario scenario, IReadOnlyList<OnboardingAppSelection> availableApps)
        {
            foreach (var appId in scenario.RequiredAppIds)
            {
                var app = availableApps.FirstOrDefault(a =>
                    string.Equals(a.Id, appId, StringComparison.OrdinalIgnoreCase));
                if (app == null) continue;

                try
                {
                    var extracted = OnboardingTemplateService.ResolveIconKey(
                        app.ProcessName, app.LaunchPath, app.IconKey);
                    if (!string.IsNullOrEmpty(extracted) && extracted != app.IconKey)
                        return extracted;
                }
                catch
                {
                }
            }

            return scenario.Id switch
            {
                "notepad" => "\U0001F4C4",
                "excel" => "\U0001F4CA",
                "browser" => "\U0001F310",
                _ => "\U0001F4E6"
            };
        }

        private async Task RunPrerequisiteChecksAsync(ScenarioOption option)
        {
            try
            {
                var scenario = option.Scenario;
                if (scenario.PrerequisiteProvider == null)
                {
                    option.PrerequisitesLoading = false;
                    option.PrerequisiteSummary = string.Empty;
                    return;
                }

                var provider = Activator.CreateInstance(scenario.PrerequisiteProvider) as IPrerequisiteProvider;
                if (provider == null)
                {
                    option.PrerequisitesLoading = false;
                    return;
                }

                var results = await provider.CheckAllAsync();
                PrerequisiteResults[scenario.Id] = results;

                var statusParts = results.Select(r =>
                {
                    var icon = r.Status switch
                    {
                        PrerequisiteStatus.Met => "\u2705",
                        PrerequisiteStatus.NotMet when r.Severity == PrerequisiteSeverity.Required => "\U0001F6D1",
                        PrerequisiteStatus.NotMet => "\u26A0\uFE0F",
                        _ => "\u23F3"
                    };
                    var name = _loc[r.DisplayNameKey] ?? r.DisplayNameKey;
                    return $"{icon} {name}";
                });

                option.PrerequisiteSummary = string.Join("  ", statusParts);
                option.PrerequisitesLoading = false;
            }
            catch
            {
                option.PrerequisitesLoading = false;
            }
        }

        public ICommand SelectScenarioCommand { get; }

        private void SelectScenario(ScenarioOption? option)
        {
            if (option == null) return;

            foreach (var sc in Scenarios)
            {
                sc.IsSelected = ReferenceEquals(sc, option);
            }

            SelectedScenario = option;
            ClearError();
        }

        private void RefreshLocalizedProperties()
        {
            OnPropertyChanged(nameof(Description));
            OnPropertyChanged(nameof(UsageProfileLabel));
            OnPropertyChanged(nameof(PrimaryButtonText));
            OnPropertyChanged(nameof(SecondaryButtonText));
            OnPropertyChanged(nameof(LanguageLabel));
        }

        private static readonly IReadOnlyList<OnboardingAppSelection> DefaultApps = new List<OnboardingAppSelection>
        {
            new() { Id = "explorer", DisplayName = "File Explorer", ProcessName = "explorer", LaunchPath = "explorer.exe", IconKey = "\uE8B7" }
        };

        public ObservableCollection<UsageProfileOption> UsageProfiles { get; }

        public ObservableCollection<ScenarioOption> Scenarios { get; }

        public Dictionary<string, IReadOnlyList<PrerequisiteResult>> PrerequisiteResults { get; } = new();

        public string LanguageLabel => _loc["Settings.General.Language"];

        public string Description => _loc["FirstLaunch.SetupDescription"];

        public string UsageProfileLabel => _loc["FirstLaunch.UsageScenario"];

        public bool CanFinish => SelectedScenario != null || SelectedProfile != null;

        public string PrimaryButtonText => _loc["FirstLaunch.CreateConfig"];

        public string SecondaryButtonText => _loc["FirstLaunch.Skip"];

        public string ErrorChooseProfile => _loc["FirstLaunch.SelectScenarioError"];

        public bool IsPrimaryButtonVisible => true;

        public bool IsSecondaryButtonVisible => true;

        public ICommand PrimaryCommand => FinishCommand;

        public ICommand SecondaryCommand => SkipCommand;

        public ICommand SelectProfileCommand { get; }

        public Action<DialogResult>? RequestClose { get; set; }

    [RelayCommand]
    private async Task Finish()
    {
        if (!Validate())
        {
            return;
        }

        ProfilesConfig config;

        if (IsUsingScenarios && SelectedScenario != null)
        {
            var apps = BuildScenarioApps(SelectedScenario.Scenario);
            config = _templateService.BuildInitialConfig(
                SelectedScenario.Scenario, apps);

            // 存储选中的场景 ID，供后续教程使用
            config.Settings.SelectedTutorialScenarioId = SelectedScenario.Scenario.Id;
        }
        else
        {
            config = _templateService.BuildInitialConfig(new OnboardingTemplateRequest
            {
                Profile = SelectedProfile!.Value,
                SelectedApps = DefaultApps
            });
        }

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
            if (IsUsingScenarios)
            {
                if (SelectedScenario == null)
                {
                    SetError(_loc["FirstLaunch.SelectScenarioError"]);
                    return false;
                }

                ClearError();
                return true;
            }

            if (SelectedProfile == null)
            {
                SetError(ErrorChooseProfile);
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

        private IReadOnlyList<OnboardingAppSelection> BuildScenarioApps(TutorialScenario scenario)
        {
            var allApps = new List<OnboardingAppSelection>(DefaultApps);
            var available = _templateService.GetAvailableApps();

            foreach (var appId in scenario.RequiredAppIds)
            {
                if (allApps.Any(a => string.Equals(a.Id, appId, StringComparison.OrdinalIgnoreCase)))
                    continue;

                if (!IsRequiredAppAvailable(scenario, appId))
                    continue;

                var match = available.FirstOrDefault(a =>
                    string.Equals(a.Id, appId, StringComparison.OrdinalIgnoreCase));
                if (match != null)
                {
                    allApps.Add(match);
                }
            }

            return allApps;
        }

        private bool IsRequiredAppAvailable(TutorialScenario scenario, string appId)
        {
            if (!PrerequisiteResults.TryGetValue(scenario.Id, out var results))
                return true;

            var relevantKeys = appId.ToLowerInvariant() switch
            {
                "excel" => new[] { "ExcelExists" },
                "chrome" or "edge" => new[] { "BrowserExists" },
                _ => Array.Empty<string>()
            };

            foreach (var result in results)
            {
                if (relevantKeys.Contains(result.Id) &&
                    result.Severity == PrerequisiteSeverity.Required &&
                    result.Status == PrerequisiteStatus.NotMet)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
