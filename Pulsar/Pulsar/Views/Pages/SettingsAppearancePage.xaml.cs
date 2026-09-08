using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Extensions.DependencyInjection;
using Pulsar.Core.Localization;
using Pulsar.Services.Interfaces;
using Pulsar.ViewModels;

namespace Pulsar.Views.Pages
{
    /// <summary>
    /// 主题预设选项（色块网格项）。色值移植自 <see cref="Pulsar.Core.Rendering.RadialThemePresetCatalog"/>
    /// 的 accent hex；System/Dark/Light 用中性代表色。仅作静态示意，不做实时渲染预览。
    /// </summary>
    public sealed class ThemePresetOption : INotifyPropertyChanged
    {
        private readonly ILocalizationService _loc;

        public ThemePresetOption(string tag, string titleKey, Brush swatch, ILocalizationService loc)
        {
            Tag = tag;
            _titleKey = titleKey;
            Swatch = swatch;
            _loc = loc;
        }

        public string Tag { get; }

        private readonly string _titleKey;

        public string DisplayName => _loc.GetString(_titleKey);

        public Brush Swatch { get; }

        public event PropertyChangedEventHandler? PropertyChanged;

        internal void RefreshLocalization() =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DisplayName)));
    }

    /// <summary>
    /// 外观常驻页（openspec 2026-09-08-dynamic-settings-tabs P1）：主题 / 渲染器风格 /
    /// 主题预设色块网格。绑定目标为 <see cref="SettingsViewModel"/>（与迁移前一致）。
    /// </summary>
    public partial class SettingsAppearancePage : Page
    {
        private readonly ILocalizationService _localizationService;

        public ObservableCollection<ThemePresetOption> ThemePresetOptions { get; } = new();

        public SettingsAppearancePage()
            : this(
                App.Current.Services.GetRequiredService<SettingsViewModel>(),
                App.Current.Services.GetRequiredService<IThemeService>(),
                App.Current.Services.GetRequiredService<ILocalizationService>())
        {
        }

        public SettingsAppearancePage(SettingsViewModel viewModel, IThemeService themeService, ILocalizationService localizationService)
        {
            InitializeComponent();
            _localizationService = localizationService;
            DataContext = viewModel;
            themeService.ApplyTheme(this, themeService.CurrentTheme);

            BuildPresetOptions();
            _localizationService.LanguageChanged += OnLanguageChanged;
        }

        private void BuildPresetOptions()
        {
            ThemePresetOptions.Clear();

            foreach (var option in new[]
            {
                Build("System", "Settings.Appearance.ThemePreset.System", SystemSwatch()),
                Build("Dark", "Settings.Appearance.ThemePreset.Dark", Solid("#2B2B2B")),
                Build("Light", "Settings.Appearance.ThemePreset.Light", Solid("#F3F3F3")),
                // 预设 accent 色：RadialThemePresetCatalog highlightBgHex。
                Build("MatchaForest", "Settings.Appearance.ThemePreset.MatchaForest", Solid("#10B981")),
                Build("GlacialIce", "Settings.Appearance.ThemePreset.GlacialIce", Solid("#0284C7")),
                Build("MorandiMuted", "Settings.Appearance.ThemePreset.MorandiMuted", Solid("#78716C"))
            })
            {
                ThemePresetOptions.Add(option);
            }
        }

        private static ThemePresetOption Build(string tag, string titleKey, Brush swatch) =>
            new(tag, titleKey, swatch, App.Current.Services.GetRequiredService<ILocalizationService>());

        private static Brush Solid(string hex)
        {
            var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
            brush.Freeze();
            return brush;
        }

        private static Brush SystemSwatch()
        {
            var brush = new LinearGradientBrush
            {
                StartPoint = new System.Windows.Point(0, 0),
                EndPoint = new System.Windows.Point(1, 1),
                GradientStops =
                {
                    new GradientStop((Color)ColorConverter.ConvertFromString("#1F1F1F"), 0),
                    new GradientStop((Color)ColorConverter.ConvertFromString("#F3F3F3"), 1)
                }
            };
            brush.Freeze();
            return brush;
        }

        private void OnLanguageChanged(object? sender, string e)
        {
            foreach (var option in ThemePresetOptions)
            {
                option.RefreshLocalization();
            }
        }
    }
}
