using System;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Pulsar.Core.Localization;
using Wpf.Ui.Controls;

namespace Pulsar.Models.Settings
{
    public sealed class SettingsPageRegistration
    {
        private readonly string _titleKey;
        private readonly string? _titleOverride;

        public SettingsPageRegistration(
            string id,
            string titleKey,
            string legacyViewName,
            SymbolRegular icon,
            Type pageType,
            string? tutorialMarkerId = null,
            string? groupId = null,
            bool isTransient = false,
            string? titleOverride = null)
        {
            Id = id;
            _titleKey = titleKey;
            _titleOverride = titleOverride;
            LegacyViewName = legacyViewName;
            Icon = icon;
            PageType = pageType;
            TutorialMarkerId = tutorialMarkerId;
            GroupId = groupId;
            IsTransient = isTransient;
        }

        public string Id { get; }

        public string Title
        {
            get
            {
                // 实体级临时页（unify-slot-editor-transient-pages D1）：组合 id 的标题由
                // 发起方按实体组合（已本地化），不走 resx 键查找。
                if (!string.IsNullOrEmpty(_titleOverride))
                {
                    return _titleOverride;
                }

                try
                {
                    if (Application.Current is App app)
                    {
                        var loc = app.Services.GetService<ILocalizationService>();
                        if (loc != null)
                        {
                            return loc.GetString(_titleKey);
                        }
                    }
                }
                catch
                {
                }

                return _titleKey;
            }
        }

        public string LegacyViewName { get; }

        public SymbolRegular Icon { get; }

        public Type PageType { get; }

        public string? TutorialMarkerId { get; }

        /// <summary>
        /// 导航分组标识（如 Workbench / System）。目录中的连续同组条目归为一组，
        /// 分组信息仅用于呈现（组间分隔），不参与页面解析。
        /// </summary>
        public string? GroupId { get; }

        /// <summary>
        /// 临时页标记（openspec 2026-09-08-dynamic-settings-tabs）：按需注册到侧边栏
        /// （语义分组末尾、斜体标题 + 关闭钮），导航离开且无未保存修改时自动回收。
        /// </summary>
        public bool IsTransient { get; }

        /// <summary>
        /// 以本注册为模板克隆一条实体级临时页注册（unify-slot-editor-transient-pages D1）：
        /// 新 id = 组合 id，标题可被实体标题覆盖，其余元数据（图标/页面类型/分组）原样保留。
        /// </summary>
        public SettingsPageRegistration CloneAsTransientEntity(string id, string? titleOverride = null)
        {
            return new SettingsPageRegistration(
                id,
                _titleKey,
                LegacyViewName,
                Icon,
                PageType,
                TutorialMarkerId,
                GroupId,
                isTransient: true,
                titleOverride: titleOverride ?? _titleOverride);
        }
    }
}
