using System;
using System.Collections.Generic;
using System.Linq;
using Pulsar.Core.Localization;
using Pulsar.Models.Settings;
using Pulsar.Views.Pages;
using Wpf.Ui.Controls;

namespace Pulsar.Services
{
    public static class SettingsPageIds
    {
        public const string General = "General";
        public const string Appearance = "Appearance";
        public const string Gesture = "Gesture";
        public const string Slots = "Slots";
        public const string Plugins = "Plugins";
        public const string Analytics = "Analytics";
        public const string About = "About";
    }

    /// <summary>
    /// 设置导航分组标识。M2 叙事约定：办公工作台条目在前，系统/支持条目在后。
    /// </summary>
    public static class SettingsPageGroupIds
    {
        /// <summary>办公工作台（宏 / 网页脚本 / 安全填写的编辑与管理入口）。</summary>
        public const string Workbench = "Workbench";

        /// <summary>系统与支持（常规设置、统计、关于）。</summary>
        public const string System = "System";
    }

    public class SettingsPageCatalog
    {
        private readonly ILocalizationService _loc;
        private readonly List<SettingsPageRegistration> _pages;

        /// <summary>
        /// 临时页注册/注销事件（openspec 2026-09-08-dynamic-settings-tabs）。
        /// 设置窗口据此增删侧边栏导航项；目录本身不持有 UI 引用。
        /// </summary>
        public event Action<SettingsPageRegistration>? TransientPageRegistered;

        public event Action<string>? TransientPageUnregistered;

        public SettingsPageCatalog(ILocalizationService loc)
        {
            _loc = loc;

            // 注册顺序 = 导航展示顺序：工作台条目（槽位 = 宏/网页脚本/安全填写、插件）在前，
            // 常规设置与统计/关于等系统支持页靠后（见 openspec home-screen-entry-reorder）。
            _pages =
            [
                new SettingsPageRegistration(SettingsPageIds.Slots, "Settings.Slots.Title", "Slots", SymbolRegular.Grid24, typeof(SettingsSlotsPage), "SlotsNavigationItem", SettingsPageGroupIds.Workbench),
                new SettingsPageRegistration(SettingsPageIds.Plugins, "Settings.Plugins.Title", "Plugins", SymbolRegular.PuzzlePiece24, typeof(SettingsPluginsPage), groupId: SettingsPageGroupIds.Workbench),
                new SettingsPageRegistration(SettingsPageIds.General, "Settings.General.Title", "Settings", SymbolRegular.Settings24, typeof(SettingsGeneralPage), groupId: SettingsPageGroupIds.System),
                new SettingsPageRegistration(SettingsPageIds.Appearance, "SettingsPage.Appearance.Title", "Appearance", SymbolRegular.Color24, typeof(SettingsAppearancePage), groupId: SettingsPageGroupIds.System),
                new SettingsPageRegistration(SettingsPageIds.Analytics, "Settings.Analytics.Title", "Analytics", SymbolRegular.ArrowTrendingLines24, typeof(SettingsAnalyticsPage), groupId: SettingsPageGroupIds.System),
                new SettingsPageRegistration(SettingsPageIds.About, "Settings.About.Title", "About", SymbolRegular.Info24, typeof(SettingsAboutPage), groupId: SettingsPageGroupIds.System)
            ];
        }

        public IReadOnlyList<SettingsPageRegistration> Pages => _pages;

        public string DefaultPageId => _pages[0].Id;

        public bool TryGetRegistration(string? pageId, out SettingsPageRegistration registration)
        {
            registration = _pages.FirstOrDefault(page => string.Equals(page.Id, pageId, StringComparison.OrdinalIgnoreCase))!;
            return registration != null;
        }

        /// <summary>
        /// 指定页面是否为临时页（未注册 / 常驻页均返回 false）。
        /// </summary>
        public bool IsTransient(string? pageId)
        {
            return TryGetRegistration(pageId, out var registration) && registration.IsTransient;
        }

        /// <summary>
        /// 运行时注册临时页：插入到其语义分组末尾（分组连续，同组最后一个条目之后；
        /// 目录中无该组则追加到表尾）。重复注册（按类型单例）返回 false 且不重复触发事件。
        /// </summary>
        public bool RegisterTransient(SettingsPageRegistration registration)
        {
            if (!registration.IsTransient)
            {
                throw new ArgumentException("Only transient registrations can be registered at runtime.", nameof(registration));
            }

            if (TryGetRegistration(registration.Id, out _))
            {
                return false;
            }

            var insertIndex = _pages.Count;
            for (var i = _pages.Count - 1; i >= 0; i--)
            {
                if (string.Equals(_pages[i].GroupId, registration.GroupId, StringComparison.Ordinal))
                {
                    insertIndex = i + 1;
                    break;
                }
            }

            _pages.Insert(insertIndex, registration);
            TransientPageRegistered?.Invoke(registration);
            return true;
        }

        /// <summary>
        /// 注销临时页（回收）。未注册或非临时页返回 false。
        /// </summary>
        public bool UnregisterTransient(string pageId)
        {
            if (!TryGetRegistration(pageId, out var registration) || !registration.IsTransient)
            {
                return false;
            }

            _pages.Remove(registration);
            TransientPageUnregistered?.Invoke(registration.Id);
            return true;
        }

        public bool TryResolvePageIdFromLegacyViewName(string? legacyViewName, out string pageId)
        {
            var registration = _pages.FirstOrDefault(page => string.Equals(page.LegacyViewName, legacyViewName, StringComparison.OrdinalIgnoreCase));
            if (registration == null)
            {
                pageId = string.Empty;
                return false;
            }

            pageId = registration.Id;
            return true;
        }

        public string GetLegacyViewName(string? pageId)
        {
            return TryGetRegistration(pageId, out var registration)
                ? registration.LegacyViewName
                : _pages[0].LegacyViewName;
        }
    }
}
