using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pulsar.Core.Localization;
using Pulsar.Core.Plugin;
using Pulsar.Helpers;
using Pulsar.Models;
using Pulsar.Models.Enums;
using Pulsar.Services.Interfaces;
using Pulsar.ViewModels.Base;

namespace Pulsar.ViewModels.Dialogs
{
    /// <summary>
    /// 插件使用详情（分析页「下钻」）对话框 VM。
    ///
    /// 数据全部来自分析页已构建的内存快照（<see cref="AnalyticsItem"/>）——
    /// 无新查询、无副作用。插件静态元数据（版本/作者/描述/图标）经
    /// <see cref="IPluginRegistry.GetDescriptor"/> 补全；单插件推荐经
    /// <see cref="IPluginRecommendationEngine.GetRecommendationsForPlugin"/>。
    /// </summary>
    public partial class PluginAnalyticsDetailViewModel : ObservableObject, IDialogViewModel
    {
        private readonly IPluginRegistry? _pluginRegistry;
        private readonly ILocalizationService? _loc;

        public Action<DialogResult>? RequestClose { get; set; }

        // ---- 头部：插件身份 ----

        public string PluginId { get; }
        public string DisplayName { get; }

        [ObservableProperty]
        private string _iconGlyph = string.Empty;

        [ObservableProperty]
        private string _versionText = string.Empty;

        [ObservableProperty]
        private string _authorText = string.Empty;

        [ObservableProperty]
        private string _description = string.Empty;

        [ObservableProperty]
        private string _tierText = string.Empty;

        [ObservableProperty]
        private bool _hasMetadata;

        // ---- 汇总度量 ----

        public string TotalFormatted { get; }
        public string SuccessRateFormatted { get; }
        public string DurationFormatted { get; }
        public string LastUsedFormatted { get; }
        public string SuccessRateColor { get; }

        // ---- 趋势（7 天）----

        public IReadOnlyList<DailyTrendItem> TrendData { get; }

        [ObservableProperty]
        private string _trendSummary = string.Empty;

        public bool HasTrend => TrendData.Count > 0;

        // ---- 槽位分布 ----

        public ObservableCollection<SlotUsageRow> SlotUsage { get; } = new();

        public bool HasSlotUsage => SlotUsage.Count > 0;

        // ---- 模式分布 ----

        [ObservableProperty]
        private int _taskModeCount;

        [ObservableProperty]
        private int _actionModeCount;

        [ObservableProperty]
        private string _modeSummary = string.Empty;

        public bool HasModeSplit => TaskModeCount > 0 || ActionModeCount > 0;

        // ---- 单插件推荐 ----

        public ObservableCollection<PluginRecommendation> Recommendations { get; } = new();

        public bool HasRecommendations => Recommendations.Count > 0;

        public PluginAnalyticsDetailViewModel(
            AnalyticsItem item,
            ILocalizationService? localization = null,
            IPluginRegistry? pluginRegistry = null,
            IPluginRecommendationEngine? recommendationEngine = null)
        {
            _pluginRegistry = pluginRegistry;
            _loc = localization;

            PluginId = item.PluginId;
            DisplayName = item.DisplayName;

            TotalFormatted = item.TotalFormatted;
            SuccessRateFormatted = $"{item.SuccessRate:F1}%";
            DurationFormatted = item.DurationFormatted;
            LastUsedFormatted = item.LastUsedFormatted;
            SuccessRateColor = item.SuccessRateColor;

            TrendData = item.TrendData.ToList();
            TrendSummary = BuildTrendSummary(item.TrendData, _loc);

            foreach (var kv in item.SlotUsage.OrderBy(k => k.Key))
            {
                SlotUsage.Add(new SlotUsageRow(kv.Key, kv.Value, item.TotalExecutions));
            }

            TaskModeCount = item.TaskModeCount;
            ActionModeCount = item.ActionModeCount;
            ModeSummary = item.ModeSummary;

            LoadMetadata();
            LoadRecommendations(recommendationEngine);
        }

        private void LoadMetadata()
        {
            var descriptor = _pluginRegistry?.GetDescriptor(PluginId);
            if (descriptor == null)
            {
                HasMetadata = false;
                return;
            }

            IconGlyph = IconHelper.GetGlyph(descriptor.Icon);
            VersionText = descriptor.Version;
            AuthorText = descriptor.Author;
            Description = descriptor.Description;
            TierText = descriptor.Tier.ToString();
            HasMetadata = true;
        }

        private void LoadRecommendations(IPluginRecommendationEngine? engine)
        {
            if (engine == null)
            {
                return;
            }

            foreach (var rec in engine.GetRecommendationsForPlugin(PluginId))
            {
                Recommendations.Add(rec);
            }
        }

        /// <summary>
        /// 7 天趋势一句话摘要：执行次数合计。
        /// 数据不足（全 0）时返回空串，由 UI 隐藏该行。
        /// </summary>
        private static string BuildTrendSummary(IReadOnlyList<DailyTrendItem> trend, ILocalizationService? loc)
        {
            if (trend.Count == 0)
            {
                return string.Empty;
            }

            var total = trend.Sum(t => t.Count);
            if (total == 0)
            {
                return string.Empty;
            }

            // 本地化：模板含 {0} 占位（如 EN "Executions in last 7 days: {0}" / ZH "近 7 天执行次数：{0}"）。
            var template = loc?["Dialog.PluginAnalyticsDetail.TrendSummary"] ?? "Executions in last 7 days: {0}";
            return string.Format(template, total);
        }

        [RelayCommand]
        private void Close()
        {
            RequestClose?.Invoke(DialogResult.Cancelled);
        }

        public Task<bool> CanCloseAsync(DialogResult result) => Task.FromResult(true);
    }

    /// <summary>槽位分布的单行（槽位号 / 次数 / 占比）。</summary>
    public sealed class SlotUsageRow
    {
        public int SlotIndex { get; }
        public int Count { get; }
        public string CountFormatted { get; }
        public string ShareFormatted { get; }

        public SlotUsageRow(int slotIndex, int count, int total)
        {
            SlotIndex = slotIndex;
            Count = count;
            CountFormatted = count.ToString();
            ShareFormatted = total > 0
                ? $"{count * 100.0 / total:F0}%"
                : "0%";
        }
    }
}
