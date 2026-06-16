// [Path]: Pulsar/Pulsar/Helpers/Tutorial/TutorialMarker.cs

using System.Windows;

namespace Pulsar.Features.Tutorial.Helpers
{
    /// <summary>
    /// Attached Property 用于在 XAML 中标记教程目标元素
    /// 使用方式: local:TutorialMarker.Id="AddSlotButton"
    /// </summary>
    public static class TutorialMarker
    {
        private static readonly DependencyProperty LoadedHandlerProperty =
            DependencyProperty.RegisterAttached(
                "LoadedHandler",
                typeof(RoutedEventHandler),
                typeof(TutorialMarker),
                new PropertyMetadata(null));

        private static readonly DependencyProperty UnloadedHandlerProperty =
            DependencyProperty.RegisterAttached(
                "UnloadedHandler",
                typeof(RoutedEventHandler),
                typeof(TutorialMarker),
                new PropertyMetadata(null));

        /// <summary>
        /// 教程目标元素的唯一标识符
        /// </summary>
        public static readonly DependencyProperty IdProperty =
            DependencyProperty.RegisterAttached(
                "Id",
                typeof(string),
                typeof(TutorialMarker),
                new PropertyMetadata(null, OnIdChanged));

        /// <summary>
        /// 获取元素的教程标记 ID
        /// </summary>
        public static string? GetId(DependencyObject obj)
        {
            return (string?)obj.GetValue(IdProperty);
        }

        /// <summary>
        /// 设置元素的教程标记 ID
        /// </summary>
        public static void SetId(DependencyObject obj, string? value)
        {
            obj.SetValue(IdProperty, value);
        }

        /// <summary>
        /// 当 Id 属性变化时，自动注册到 TutorialTargetRegistry
        /// </summary>
        private static void OnIdChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is FrameworkElement element)
            {
                var oldId = e.OldValue as string;
                var newId = e.NewValue as string;

                // 取消注册旧 ID
                if (!string.IsNullOrEmpty(oldId))
                {
                    TutorialTargetRegistry.Unregister(oldId);
                }

                // Detach any previously attached handlers to avoid event leaks.
                var previousLoaded = element.GetValue(LoadedHandlerProperty) as RoutedEventHandler;
                if (previousLoaded != null)
                {
                    element.Loaded -= previousLoaded;
                    element.ClearValue(LoadedHandlerProperty);
                }

                var previousUnloaded = element.GetValue(UnloadedHandlerProperty) as RoutedEventHandler;
                if (previousUnloaded != null)
                {
                    element.Unloaded -= previousUnloaded;
                    element.ClearValue(UnloadedHandlerProperty);
                }

                // 注册新 ID
                if (!string.IsNullOrEmpty(newId))
                {
                    RoutedEventHandler loadedHandler = (s, args) =>
                    {
                        try
                        {
                            TutorialTargetRegistry.Register(newId, element);
                        }
                        catch
                        {
                            // Ignore registration failures; tutorial system should never crash the app.
                        }
                    };

                    RoutedEventHandler unloadedHandler = (s, args) =>
                    {
                        TutorialTargetRegistry.Unregister(newId);
                    };

                    element.SetValue(LoadedHandlerProperty, loadedHandler);
                    element.SetValue(UnloadedHandlerProperty, unloadedHandler);

                    // 等待元素加载后再注册
                    if (element.IsLoaded)
                    {
                        loadedHandler(element, new RoutedEventArgs());
                    }
                    else
                    {
                        element.Loaded += loadedHandler;
                    }

                    // 当元素卸载时取消注册
                    element.Unloaded += unloadedHandler;
                }
            }
        }
    }
}
