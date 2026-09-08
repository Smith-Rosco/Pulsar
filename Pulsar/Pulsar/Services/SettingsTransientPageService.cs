using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Pulsar.Models.Settings;
using Pulsar.Services.Interfaces;
using Pulsar.ViewModels.Settings;

namespace Pulsar.Services
{
    /// <summary>
    /// 临时页协调器实现。依赖方向：service → catalog / shell / guard（无环；
    /// shell 不反向依赖本服务，"导航离开回收"由设置窗口在页面切换时调用
    /// <see cref="NotifyNavigatedAwayFrom"/> 触发）。
    /// </summary>
    public class SettingsTransientPageService : ITransientPageService
    {
        private readonly SettingsPageCatalog _pageCatalog;
        private readonly SettingsShellViewModel _shell;
        private readonly ISettingsNavigationGuard _navigationGuard;
        private readonly ILogger<SettingsTransientPageService> _logger;
        private readonly Dictionary<string, SettingsPageRegistration> _definitions =
            new(StringComparer.OrdinalIgnoreCase);

        public SettingsTransientPageService(
            SettingsPageCatalog pageCatalog,
            SettingsShellViewModel shell,
            ISettingsNavigationGuard navigationGuard,
            ILogger<SettingsTransientPageService> logger)
        {
            _pageCatalog = pageCatalog;
            _shell = shell;
            _navigationGuard = navigationGuard;
            _logger = logger;
        }

        /// <summary>
        /// 登记临时页类型定义（组合根调用）。登记不会改变导航——直到
        /// <see cref="OpenTransientPageAsync"/> 被触发才进入目录与侧边栏。
        /// </summary>
        public void RegisterDefinition(SettingsPageRegistration definition)
        {
            if (!definition.IsTransient)
            {
                throw new ArgumentException("Only transient registrations can be defined as transient page types.", nameof(definition));
            }

            _definitions[definition.Id] = definition;
        }

        public bool IsOpen(string typeId)
        {
            return _pageCatalog.TryGetRegistration(typeId, out var registration) && registration.IsTransient;
        }

        public async Task<bool> OpenTransientPageAsync(string typeId)
        {
            if (!_definitions.TryGetValue(typeId, out var definition))
            {
                _logger.LogWarning("[TransientPages] Unknown transient page type '{TypeId}'", typeId);
                return false;
            }

            if (!IsOpen(typeId))
            {
                // 已打开时 RegisterTransient 返回 false（按类型单例）；
                // 两种情况都落到"激活既有实例"的导航路径。
                _pageCatalog.RegisterTransient(definition);
            }

            return await _shell.NavigateAsync(definition.Id, userInitiated: true);
        }

        public async Task<bool> CloseTransientPageAsync(string typeId)
        {
            if (!IsOpen(typeId))
            {
                return false;
            }

            // 关闭脏页需确认：复用守卫的 save/discard/cancel 流程，用户取消则保持打开。
            if (_navigationGuard.HasUnsavedChanges)
            {
                var allowed = await _navigationGuard.CanNavigateAwayAsync(null, isWindowClosing: false);
                if (!allowed)
                {
                    return false;
                }
            }

            return _pageCatalog.UnregisterTransient(typeId);
        }

        public void NotifyNavigatedAwayFrom(string? sourcePageId)
        {
            if (string.IsNullOrEmpty(sourcePageId) || !IsOpen(sourcePageId))
            {
                return;
            }

            // 脏临时页保留（不回收、不阻塞导航）；干净页立即回收。
            if (_navigationGuard.HasUnsavedChanges)
            {
                _logger.LogDebug("[TransientPages] Keep dirty transient page '{PageId}' open", sourcePageId);
                return;
            }

            _pageCatalog.UnregisterTransient(sourcePageId);
        }
    }
}
