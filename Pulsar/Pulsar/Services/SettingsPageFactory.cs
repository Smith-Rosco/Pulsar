using System;
using System.Collections.Generic;
using System.Windows.Controls;
using Pulsar.Core.Localization;
using Pulsar.Services.Interfaces;
using Pulsar.ViewModels;
using Pulsar.ViewModels.Settings;
using Pulsar.Views.Pages;

namespace Pulsar.Services
{
    /// <summary>
    /// 设置页面构造器注册表（openspec 2026-09-08-dynamic-settings-tabs D2）：
    /// 由 switch 硬编码改为按 id 注册的字典，新增页面（含运行时注册的临时页）
    /// 只需注册一个构造委托，消除"目录 + 工厂"双点维护。
    /// </summary>
    public class SettingsPageFactory
    {
        private readonly PluginManagerViewModel _pluginManagerViewModel;
        private readonly ExternalPluginManagerViewModel _externalPluginManagerViewModel;
        private readonly SettingsAnalyticsPageViewModel _analyticsViewModel;
        private readonly AboutViewModel _aboutViewModel;
        private readonly IThemeService _themeService;
        private readonly ILocalizationService _localizationService;
        private readonly SettingsEntityPageStore _entityPages;
        private readonly Dictionary<string, Func<SettingsViewModel, Page>> _creators;

        public SettingsPageFactory(
            PluginManagerViewModel pluginManagerViewModel,
            ExternalPluginManagerViewModel externalPluginManagerViewModel,
            SettingsAnalyticsPageViewModel analyticsViewModel,
            AboutViewModel aboutViewModel,
            IThemeService themeService,
            ILocalizationService localizationService,
            SettingsEntityPageStore entityPageStore)
        {
            _pluginManagerViewModel = pluginManagerViewModel;
            _externalPluginManagerViewModel = externalPluginManagerViewModel;
            _analyticsViewModel = analyticsViewModel;
            _aboutViewModel = aboutViewModel;
            _themeService = themeService;
            _localizationService = localizationService;
            _entityPages = entityPageStore;

            _creators = new Dictionary<string, Func<SettingsViewModel, Page>>(StringComparer.OrdinalIgnoreCase)
            {
                [SettingsPageIds.General] = vm => new SettingsGeneralPage(vm, _themeService),
                [SettingsPageIds.Appearance] = vm => new SettingsAppearancePage(vm, _themeService, _localizationService),
                [SettingsPageIds.Slots] = vm => new SettingsSlotsPage(vm, _themeService),
                [SettingsPageIds.Plugins] = vm => new SettingsPluginsPage(_pluginManagerViewModel, _themeService, _externalPluginManagerViewModel),
                [SettingsPageIds.Analytics] = vm => new SettingsAnalyticsPage(_analyticsViewModel, _themeService),
                [SettingsPageIds.About] = vm => new SettingsAboutPage(_aboutViewModel, _themeService),
                [SettingsPageIds.Gesture] = vm => new SettingsGesturePage(vm, _themeService)
            };
        }

        /// <summary>运行时注册/覆盖页面构造器（临时页、插件贡献页用）。</summary>
        public void RegisterCreator(string pageId, Func<SettingsViewModel, Page> creator)
        {
            _creators[pageId] = creator;
        }

        public Page CreatePage(string pageId, SettingsViewModel settingsViewModel)
        {
            // 实体级临时页（unify-slot-editor-transient-pages D1/D2）：组合 id 的构造器
            // 由发起打开的 SettingsViewModel 闭包登记在单例 store 里（需要实体引用），
            // 优先于静态注册表解析。
            if (_entityPages.TryGet(pageId ?? string.Empty, out var entityCreator))
            {
                return entityCreator(settingsViewModel);
            }

            if (_creators.TryGetValue(pageId ?? string.Empty, out var creator))
            {
                return creator(settingsViewModel);
            }

            throw new InvalidOperationException($"Unknown settings page id '{pageId}'.");
        }
    }
}
