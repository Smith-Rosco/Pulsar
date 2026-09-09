using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;
using Pulsar.Core.Messages;
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
        private readonly SettingsEntityPageStore _entityPages;
        private readonly ILogger<SettingsTransientPageService> _logger;
        private readonly Dictionary<string, SettingsPageRegistration> _definitions =
            new(StringComparer.OrdinalIgnoreCase);

        public SettingsTransientPageService(
            SettingsPageCatalog pageCatalog,
            SettingsShellViewModel shell,
            ISettingsNavigationGuard navigationGuard,
            SettingsEntityPageStore entityPages,
            ILogger<SettingsTransientPageService> logger)
        {
            _pageCatalog = pageCatalog;
            _shell = shell;
            _navigationGuard = navigationGuard;
            _entityPages = entityPages;
            _logger = logger;

            // 生命周期联动（unify-slot-editor-transient-pages D7）：slot / profile 删除时
            // 同步回收其编辑 tab。WeakReferenceMessenger 持弱引用，实例回收后注册自动失效。
            WeakReferenceMessenger.Default.Register<SlotRemovedMessage>(this,
                (recipient, message) => ((SettingsTransientPageService)recipient).CloseSlotEditorPage(
                    $"{SettingsPageIds.SlotEditor}:{message.ContextKey}:{message.SlotNo}"));
            WeakReferenceMessenger.Default.Register<ProfileRemovedMessage>(this,
                (recipient, message) => ((SettingsTransientPageService)recipient).CloseSlotEditorPagesForContext(message.ContextKey));
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

        /// <inheritdoc />
        public async Task<bool> OpenTransientPageAsync(string templateId, string entityId, string? titleOverride = null)
        {
            if (string.IsNullOrWhiteSpace(templateId))
            {
                throw new ArgumentException("Template id must not be empty.", nameof(templateId));
            }

            if (string.IsNullOrWhiteSpace(entityId))
            {
                throw new ArgumentException("Entity id must not be empty.", nameof(entityId));
            }

            if (!_definitions.TryGetValue(templateId, out var template))
            {
                _logger.LogWarning("[TransientPages] Unknown transient page template '{TemplateId}'", templateId);
                return false;
            }

            // 组合 id 即注册 id（D1）：克隆模板注册（实体标题覆盖）后按组合 id 走既有
            // 单例注册/激活路径，catalog 对"模板"零感知。
            var compositeId = $"{templateId}:{entityId}";
            if (!IsOpen(compositeId))
            {
                _pageCatalog.RegisterTransient(template.CloneAsTransientEntity(compositeId, titleOverride));
            }

            return await _shell.NavigateAsync(compositeId, userInitiated: true);
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

            var closed = _pageCatalog.UnregisterTransient(typeId);
            if (closed)
            {
                _entityPages.Remove(typeId);
            }

            return closed;
        }

        /// <inheritdoc />
        public Task<bool> DiscardTransientPageAsync(string pageId)
        {
            // 复用实体消失路径的强制回收（不走守卫）：调用方保证页面状态可废弃
            // （draft 已提交为实体 / 实体已删除），不该再对它弹 save/discard/cancel。
            CloseSlotEditorPage(pageId);
            return Task.FromResult(true);
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

            if (_pageCatalog.UnregisterTransient(sourcePageId))
            {
                _entityPages.Remove(sourcePageId);
            }
        }

        // ---- 生命周期联动（D7）：实体消失 → 编辑 tab 同步回收 ----
        //
        // 直接 Unregister（不走守卫确认）：实体已从配置删除，tab 是悬空引用，
        // 用户在删除确认时已做过一次决策；编辑 tab 的未保存状态随实体一并废弃。

        private void CloseSlotEditorPage(string compositeId)
        {
            if (!IsOpen(compositeId))
            {
                return;
            }

            if (_pageCatalog.UnregisterTransient(compositeId))
            {
                _entityPages.Remove(compositeId);
                _logger.LogDebug("[TransientPages] Recycled entity page '{PageId}' (entity removed)", compositeId);
            }
        }

        private void CloseSlotEditorPagesForContext(string contextKey)
        {
            if (string.IsNullOrEmpty(contextKey))
            {
                return;
            }

            // 前缀含结尾冒号，避免 "Global" 误伤 "GlobalX"。
            var prefix = $"{SettingsPageIds.SlotEditor}:{contextKey}:";
            foreach (var page in _pageCatalog.Pages.ToList())
            {
                if (page.IsTransient && page.Id.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    CloseSlotEditorPage(page.Id);
                }
            }
        }
    }
}
