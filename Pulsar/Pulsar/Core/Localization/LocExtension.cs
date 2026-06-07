using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Data;
using System.Windows.Markup;
using Microsoft.Extensions.DependencyInjection;

namespace Pulsar.Core.Localization
{
    public class LocaleExtension : MarkupExtension
    {
        private static readonly HashSet<LocaleExtension> _activeExtensions = new();

        private WeakReference<DependencyObject>? _targetObject;
        private DependencyProperty? _targetProperty;
        private string? _key;

        public string Key { get; set; } = string.Empty;

        public LocaleExtension()
        {
        }

        public LocaleExtension(string key)
        {
            Key = key;
        }

        public override object? ProvideValue(IServiceProvider serviceProvider)
        {
            if (string.IsNullOrEmpty(Key))
            {
                return Key;
            }

            var service = GetLocalizationService();
            if (service == null)
            {
                return Key;
            }

            if (serviceProvider.GetService(typeof(IProvideValueTarget)) is IProvideValueTarget target)
            {
                _targetObject = new WeakReference<DependencyObject>((DependencyObject)target.TargetObject);
                _targetProperty = target.TargetProperty as DependencyProperty;
                _key = Key;

                lock (_activeExtensions)
                {
                    _activeExtensions.Add(this);
                }

                SubscribeToLanguageChanged(service);
            }

            return service.GetString(Key);
        }

        private static ILocalizationService? GetLocalizationService()
        {
            try
            {
                return ((App)Application.Current).Services.GetService<ILocalizationService>();
            }
            catch
            {
                return null;
            }
        }

        private void SubscribeToLanguageChanged(ILocalizationService service)
        {
            service.LanguageChanged += OnLanguageChanged;
        }

        private void OnLanguageChanged(object? sender, string e)
        {
            if (_targetObject == null || _targetProperty == null || _key == null)
            {
                return;
            }

            if (!_targetObject.TryGetTarget(out var target))
            {
                Unsubscribe();
                return;
            }

            var service = GetLocalizationService();
            if (service == null)
            {
                return;
            }

            Application.Current.Dispatcher.Invoke(() =>
            {
                if (_targetObject.TryGetTarget(out var currentTarget))
                {
                    currentTarget.SetValue(_targetProperty, service.GetString(_key));
                }
            });
        }

        private void Unsubscribe()
        {
            var service = GetLocalizationService();
            if (service != null)
            {
                service.LanguageChanged -= OnLanguageChanged;
            }

            lock (_activeExtensions)
            {
                _activeExtensions.Remove(this);
            }
        }

        public static void RefreshAllActive()
        {
            var service = GetLocalizationService();
            if (service == null)
            {
                return;
            }

            lock (_activeExtensions)
            {
                foreach (var ext in _activeExtensions)
                {
                    ext.OnLanguageChanged(null, service.CurrentLanguage);
                }
            }
        }
    }
}
