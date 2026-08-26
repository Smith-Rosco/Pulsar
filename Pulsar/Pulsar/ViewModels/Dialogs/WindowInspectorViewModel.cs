using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Pulsar.Core.Localization;
using Pulsar.Services;
using Pulsar.Services.Interfaces;
using Pulsar.Services.WindowSwitching;
using Pulsar.ViewModels.Base;
using DialogResult = Pulsar.Models.Enums.DialogResult;

namespace Pulsar.ViewModels.Dialogs
{
    /// <summary>
    /// Window Inspector：列出全部顶层窗口的"可切换"判定报告（含原因），支持
    /// 定位（闪烁）与一键排除（生成最具体的身份规则并运行时 + 持久化双写）。
    /// 只依赖 <see cref="IWindowService"/> 与 <see cref="IConfigService"/>，可 Mock 单测。
    /// </summary>
    public partial class WindowInspectorViewModel : ObservableObject, IDialogViewModel
    {
        public const string WinSwitcherPluginId = "com.pulsar.winswitcher";

        private readonly IWindowService _windowService;
        private readonly IConfigService _configService;
        private readonly ILocalizationService? _loc;
        private readonly ILogger<WindowInspectorViewModel>? _logger;
        private readonly Func<IReadOnlyList<WindowEligibilityRule>, Task> _persistRules;
        private List<WindowEligibilityRule> _rules = new();

        [ObservableProperty]
        private ObservableCollection<WindowInspectorRow> _rows = new();

        [ObservableProperty]
        private bool _isLoading;

        [ObservableProperty]
        private string _ruleSummary = string.Empty;

        [ObservableProperty]
        private string _statusMessage = string.Empty;

        public Action<DialogResult>? RequestClose { get; set; }

        public bool IsScrollable => true;

        public Task<bool> CanCloseAsync(DialogResult result) => Task.FromResult(true);

        public WindowInspectorViewModel(
            IWindowService windowService,
            IConfigService configService,
            ILocalizationService? loc = null,
            ILogger<WindowInspectorViewModel>? logger = null,
            Func<IReadOnlyList<WindowEligibilityRule>, Task>? persistRules = null)
        {
            _windowService = windowService;
            _configService = configService;
            _loc = loc;
            _logger = logger;
            _persistRules = persistRules ?? PersistRulesAsync;
        }

        /// <summary>对话框打开前调用：加载当前规则 + 首次枚举。</summary>
        public async Task InitializeAsync()
        {
            _rules = _windowService.GetEligibilityRules().ToList();
            UpdateRuleSummary();
            await RefreshAsync();
        }

        [RelayCommand]
        private async Task RefreshAsync()
        {
            IsLoading = true;
            try
            {
                var report = await _windowService.GetWindowEligibilityReportAsync();
                Rows = new ObservableCollection<WindowInspectorRow>(
                    report.Select(r => new WindowInspectorRow(r, _loc)));
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand]
        private void Flash(WindowInspectorRow? row)
        {
            if (row == null)
            {
                return;
            }

            _windowService.FlashWindow(row.Report.Hwnd);
        }

        [RelayCommand]
        private async Task ExcludeAsync(WindowInspectorRow? row)
        {
            if (row == null)
            {
                return;
            }

            var rule = BuildExcludeRule(row.Report);
            if (rule == null)
            {
                return;
            }

            if (!_rules.Any(existing => AreSameRule(existing, rule)))
            {
                _rules.Add(rule);
            }

            // 运行时立即生效（本次会话）+ 持久化到 WinSwitcher 的 ExcludeRules 设置（下次启动/保存同步）。
            _windowService.UpdateEligibilityRules(_rules);
            await _persistRules(_rules);

            StatusMessage = string.Format(
                _loc?["Inspector.ExcludedFormat"] ?? "Excluded '{0}' ({1})",
                string.IsNullOrWhiteSpace(row.Report.Title) ? row.Report.ProcessName : row.Report.Title,
                DescribeRule(rule));
            UpdateRuleSummary();
            await RefreshAsync();
        }

        [RelayCommand]
        private void Close()
        {
            RequestClose?.Invoke(DialogResult.Confirmed);
        }

        /// <summary>默认持久化：写入 WinSwitcher 插件配置的 ExcludeRules（走 ConfigEditSession 单写者通道）。</summary>
        private async Task PersistRulesAsync(IReadOnlyList<WindowEligibilityRule> rules)
        {
            var json = WindowEligibilityRuleSerializer.Serialize(rules);
            try
            {
                await ConfigEditSession.RunAsync(_configService, session =>
                    session.UpdatePluginProfile(WinSwitcherPluginId, profile => profile.Config["ExcludeRules"] = json));
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "[WindowInspector] Failed to persist ExcludeRules");
                StatusMessage = _loc?["Inspector.PersistFailed"] ?? "Rule is active for this session, but saving to disk failed.";
            }
        }

        private void UpdateRuleSummary()
        {
            RuleSummary = string.Format(
                _loc?["Inspector.RuleCountFormat"] ?? "{0} exclusion rule(s) active",
                _rules.Count);
        }

        /// <summary>
        /// 生成"最具体且安全"的排除规则：进程 + 类 + 标题正则同时匹配，只命中当前这一个窗口。
        /// 真实同进程窗口（标题不同）不会误伤。窗口必然有类名，因此总能生成类名规则；
        /// 标题缺失时退化为进程 + 类规则。
        /// </summary>
        private static WindowEligibilityRule? BuildExcludeRule(WindowEligibilityReport report)
        {
            var titlePattern = string.IsNullOrWhiteSpace(report.Title)
                ? null
                : "^" + Regex.Escape(report.Title) + "$";
            var windowClass = string.IsNullOrWhiteSpace(report.ClassName) ? null : report.ClassName;

            if (titlePattern == null && windowClass == null)
            {
                return null;
            }

            return new WindowEligibilityRule(
                Allow: false,
                ProcessName: string.IsNullOrWhiteSpace(report.ProcessName) ? null : report.ProcessName,
                WindowClass: windowClass,
                TitlePattern: titlePattern);
        }

        private static string DescribeRule(WindowEligibilityRule rule)
        {
            var parts = new List<string>();
            if (rule.ProcessName != null) parts.Add($"proc={rule.ProcessName}");
            if (rule.WindowClass != null) parts.Add($"class={rule.WindowClass}");
            if (rule.TitlePattern != null) parts.Add($"title={rule.TitlePattern}");
            return string.Join(", ", parts);
        }

        private static bool AreSameRule(WindowEligibilityRule a, WindowEligibilityRule b)
            => a.Allow == b.Allow
               && string.Equals(a.ProcessName, b.ProcessName, StringComparison.OrdinalIgnoreCase)
               && string.Equals(a.WindowClass, b.WindowClass, StringComparison.OrdinalIgnoreCase)
               && string.Equals(a.TitlePattern, b.TitlePattern, StringComparison.Ordinal);
    }

    /// <summary>Inspector 的一行展示模型（判定报告 + 本地化文本）。</summary>
    public sealed class WindowInspectorRow
    {
        public WindowEligibilityReport Report { get; }

        public string Title { get; }

        public string ProcessName { get; }

        public string ClassName { get; }

        public string HwndText { get; }

        public string RectText { get; }

        public string VerdictText { get; }

        public bool IsExcluded => !Report.Included;

        /// <summary>只有当前"可切换"的窗口才值得排除（已被硬规则/规则排除的无需再排除）。</summary>
        public bool CanExclude => Report.Included;

        public WindowInspectorRow(WindowEligibilityReport report, ILocalizationService? loc)
        {
            Report = report;
            Title = string.IsNullOrWhiteSpace(report.Title) ? "—" : report.Title;
            ProcessName = string.IsNullOrWhiteSpace(report.ProcessName) ? "—" : report.ProcessName;
            ClassName = string.IsNullOrWhiteSpace(report.ClassName) ? "—" : report.ClassName;
            HwndText = "0x" + report.Hwnd.ToInt64().ToString("X");
            RectText = report.Rect is { } r
                ? $"{r.Left},{r.Top} {r.Right - r.Left}×{r.Bottom - r.Top}"
                : "—";
            VerdictText = DescribeVerdict(report.Verdict, loc);
        }

        private static string DescribeVerdict(WindowEligibilityVerdict verdict, ILocalizationService? loc)
        {
            var key = verdict switch
            {
                WindowEligibilityVerdict.Eligible => "Inspector.Verdict.Eligible",
                WindowEligibilityVerdict.ExcludedSelf => "Inspector.Verdict.ExcludedSelf",
                WindowEligibilityVerdict.ExcludedHidden => "Inspector.Verdict.ExcludedHidden",
                WindowEligibilityVerdict.ExcludedCloaked => "Inspector.Verdict.ExcludedCloaked",
                WindowEligibilityVerdict.ExcludedToolWindow => "Inspector.Verdict.ExcludedToolWindow",
                WindowEligibilityVerdict.ExcludedOwned => "Inspector.Verdict.ExcludedOwned",
                WindowEligibilityVerdict.ExcludedOffScreen => "Inspector.Verdict.ExcludedOffScreen",
                WindowEligibilityVerdict.ExcludedBlacklistedClass => "Inspector.Verdict.ExcludedBlacklistedClass",
                WindowEligibilityVerdict.ExcludedBlacklistedProcess => "Inspector.Verdict.ExcludedBlacklistedProcess",
                WindowEligibilityVerdict.ExcludedByRule => "Inspector.Verdict.ExcludedByRule",
                _ => "Inspector.Verdict.Unknown"
            };
            return loc?[key] ?? verdict.ToString();
        }
    }
}
