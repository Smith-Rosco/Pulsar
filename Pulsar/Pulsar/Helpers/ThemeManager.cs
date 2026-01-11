using System;
using System.Windows;
using Pulsar.Models;

namespace Pulsar.Helpers
{
    public static class ThemeManager
    {
        public static void ApplyTheme(Window window, AppTheme theme)
        {
            if (window == null) return;

            // 根据枚举决定加载哪个文件
            string themePath = theme == AppTheme.Light
                ? "pack://application:,,,/Pulsar;component/Themes/Theme.Light.xaml"
                : "pack://application:,,,/Pulsar;component/Themes/Theme.Dark.xaml";

            // 1. 移除旧的主题字典 (防止重复)
            // 我们通过 Source 路径特征来识别主题字典
            for (int i = window.Resources.MergedDictionaries.Count - 1; i >= 0; i--)
            {
                var dict = window.Resources.MergedDictionaries[i];
                if (dict.Source != null && dict.Source.ToString().Contains("/Themes/Theme."))
                {
                    window.Resources.MergedDictionaries.RemoveAt(i);
                }
            }

            // 2. 添加新字典
            var newDict = new ResourceDictionary { Source = new Uri(themePath, UriKind.Absolute) };
            window.Resources.MergedDictionaries.Add(newDict);
        }
    }
}