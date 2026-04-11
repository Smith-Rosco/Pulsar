using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
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
            IOnboardingStateService onboardingStateService)
        {
            _templateService = templateService;
            _configService = configService;
            _onboardingStateService = onboardingStateService;

            UsageProfiles = new ObservableCollection<UsageProfileOption>
            {
                new() { Value = OnboardingUsageProfile.GeneralProductivity, Title = "通用效率", Description = "从日常应用开始，配以简单的命令示例。" },
                new() { Value = OnboardingUsageProfile.DeveloperWorkflow, Title = "开发者工作流", Description = "偏好编辑器和终端导向的默认配置。" },
                new() { Value = OnboardingUsageProfile.BrowserAndDocs, Title = "浏览器与文档", Description = "专注于浏览、笔记和快速查阅任务。" }
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

        public string Title => "设置 Pulsar";

        public string Description => "选择您计划如何使用 Pulsar，然后选择几个启动应用。生成的配置位可以在设置中随时修改。";

        public string SelectionHint => "至少选择一个应用。最多六个应用将作为 Switch Mode 启动配置位。";

        public string UsageProfileLabel => "使用场景";

        public string StarterAppsLabel => "启动应用";

        public string SelectedLabel => "已选择";

        public string SelectedAppCountLabel => "已选择应用: ";

        public int SelectedAppCount => CommonApps.Count(app => app.IsSelected);

        public bool CanFinish => SelectedProfile != null && SelectedAppCount > 0;

        public string PrimaryButtonText => "创建初始配置";

        public string SecondaryButtonText => "暂时跳过";

        public string FooterDescription => "生成的默认值将在全局配置文件中创建标准的可编辑配置位，涵盖 Switch Mode 和 Command Mode。";

        public string ErrorChooseProfile => "请先选择一个使用场景。";

        public string ErrorSelectApp => "请至少选择一个启动应用以生成默认配置。";

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
