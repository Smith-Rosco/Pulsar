using System;
using System.Collections.Generic;

namespace Pulsar.Core.Localization
{
    public interface ILocalizationService
    {
        string CurrentLanguage { get; }
        IReadOnlyList<string> SupportedLanguages { get; }

        string GetString(string key);
        string this[string key] { get; }

        void SetLanguage(string cultureName);

        event EventHandler<string> LanguageChanged;
    }
}
