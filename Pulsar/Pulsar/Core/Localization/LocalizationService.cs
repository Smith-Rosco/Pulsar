using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Resources;
using System.Threading;
using Microsoft.Extensions.Logging;

namespace Pulsar.Core.Localization
{
    public class LocalizationService : ILocalizationService
    {
        private readonly ILogger<LocalizationService> _logger;
        private readonly ResourceManager _resourceManager;
        private string _currentLanguage = "en";

        private static readonly IReadOnlyList<string> _supportedLanguages = new List<string> { "en", "zh-CN" }.AsReadOnly();

        public string CurrentLanguage => _currentLanguage;
        public IReadOnlyList<string> SupportedLanguages => _supportedLanguages;

        public event EventHandler<string>? LanguageChanged;

        public LocalizationService(ILogger<LocalizationService> logger)
        {
            _logger = logger;
            _resourceManager = new ResourceManager("Pulsar.Resources.Strings", Assembly.GetExecutingAssembly());
        }

        public string GetString(string key)
        {
            try
            {
                var culture = CultureInfo.GetCultureInfo(_currentLanguage);
                var value = _resourceManager.GetString(key, culture);

                if (!string.IsNullOrEmpty(value))
                {
                    return value;
                }

                var fallback = _resourceManager.GetString(key, CultureInfo.GetCultureInfo("en"));
                if (!string.IsNullOrEmpty(fallback))
                {
                    return fallback;
                }

                return key;
            }
            catch (CultureNotFoundException)
            {
                _logger.LogWarning("Culture '{Culture}' not found, falling back to English", _currentLanguage);

                try
                {
                    var fallback = _resourceManager.GetString(key, CultureInfo.GetCultureInfo("en"));
                    if (!string.IsNullOrEmpty(fallback))
                    {
                        return fallback;
                    }
                }
                catch
                {
                }

                return key;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to resolve localized string for key '{Key}'", key);
                return key;
            }
        }

        public string this[string key] => GetString(key);

        public void SetLanguage(string cultureName)
        {
            if (string.IsNullOrEmpty(cultureName))
            {
                _logger.LogWarning("Empty culture name provided, falling back to English");
                cultureName = "en";
            }

            if (_currentLanguage.Equals(cultureName, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            try
            {
                var culture = CultureInfo.GetCultureInfo(cultureName);
                _currentLanguage = cultureName;
                Thread.CurrentThread.CurrentUICulture = culture;
                CultureInfo.DefaultThreadCurrentUICulture = culture;

                _logger.LogInformation("Language switched to {Language}", cultureName);
                LanguageChanged?.Invoke(this, cultureName);
            }
            catch (CultureNotFoundException)
            {
                _logger.LogWarning("Invalid culture '{Culture}', falling back to English", cultureName);
                _currentLanguage = "en";
                Thread.CurrentThread.CurrentUICulture = CultureInfo.GetCultureInfo("en");
                CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.GetCultureInfo("en");
                LanguageChanged?.Invoke(this, "en");
            }
        }
    }
}
