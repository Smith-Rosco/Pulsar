using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;
using Pulsar.Models.Settings;
using Pulsar.Services;
using Pulsar.Services.Interfaces;

namespace Pulsar.ViewModels.Settings
{
    public partial class SettingsShellViewModel : ObservableObject
    {
        private readonly SettingsPageCatalog _pageCatalog;
        private readonly ILocalUiPreferencesService _localUiPreferencesService;
        private readonly ISettingsNavigationGuard _navigationGuard;
        private readonly ILogger<SettingsShellViewModel> _logger;

        [ObservableProperty]
        private string _currentPageId;

        public SettingsShellViewModel(
            SettingsPageCatalog pageCatalog,
            ILocalUiPreferencesService localUiPreferencesService,
            ISettingsNavigationGuard navigationGuard,
            ILogger<SettingsShellViewModel> logger)
        {
            _pageCatalog = pageCatalog;
            _localUiPreferencesService = localUiPreferencesService;
            _navigationGuard = navigationGuard;
            _logger = logger;
            _currentPageId = ResolveInitialPageId();
        }

        public IReadOnlyList<SettingsPageRegistration> Pages => _pageCatalog.Pages;

        public string CurrentLegacyViewName => _pageCatalog.GetLegacyViewName(CurrentPageId);

        public bool TryResolvePageIdFromLegacyViewName(string? legacyViewName, out string pageId)
        {
            return _pageCatalog.TryResolvePageIdFromLegacyViewName(legacyViewName, out pageId);
        }

        public async Task<bool> NavigateAsync(string? pageId, bool userInitiated)
        {
            if (!_pageCatalog.TryGetRegistration(pageId, out var registration))
            {
                _logger.LogWarning("[SettingsShellViewModel] Rejected unknown settings page '{PageId}'", pageId);
                return false;
            }

            if (string.Equals(CurrentPageId, registration.Id, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (userInitiated && !await _navigationGuard.CanNavigateAwayAsync(registration.Id, isWindowClosing: false))
            {
                return false;
            }

            CurrentPageId = registration.Id;
            _localUiPreferencesService.SetLastOpenedSettingsPageId(registration.Id);
            return true;
        }

        public Task<bool> CanCloseAsync()
        {
            return _navigationGuard.CanNavigateAwayAsync(null, isWindowClosing: true);
        }

        private string ResolveInitialPageId()
        {
            var preferredPageId = _localUiPreferencesService.GetLastOpenedSettingsPageId();
            if (_pageCatalog.TryGetRegistration(preferredPageId, out var registration))
            {
                return registration.Id;
            }

            return _pageCatalog.DefaultPageId;
        }
    }
}
