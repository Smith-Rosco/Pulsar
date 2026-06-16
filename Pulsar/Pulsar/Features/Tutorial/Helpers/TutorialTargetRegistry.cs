// [Path]: Pulsar/Pulsar/Helpers/Tutorial/TutorialTargetRegistry.cs

using System;
using System.Collections.Generic;
using System.Windows;

namespace Pulsar.Features.Tutorial.Helpers
{
    /// <summary>
    /// 教程目标元素注册表
    /// 用于在运行时查找标记的 UI 元素并获取其屏幕坐标
    /// </summary>
    public static class TutorialTargetRegistry
    {
        private static readonly Dictionary<string, WeakReference<FrameworkElement>> _registry = new();
        private static readonly object _lock = new object();

        /// <summary>
        /// 注册一个教程目标元素
        /// </summary>
        /// <param name="id">元素的唯一标识符</param>
        /// <param name="element">UI 元素</param>
        public static void Register(string id, FrameworkElement element)
        {
            if (string.IsNullOrEmpty(id))
            {
                throw new ArgumentException("ID cannot be null or empty", nameof(id));
            }

            if (element == null)
            {
                throw new ArgumentNullException(nameof(element));
            }

            lock (_lock)
            {
                _registry[id] = new WeakReference<FrameworkElement>(element);
            }
        }

        /// <summary>
        /// 取消注册一个教程目标元素
        /// </summary>
        /// <param name="id">元素的唯一标识符</param>
        public static void Unregister(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return;
            }

            lock (_lock)
            {
                _registry.Remove(id);
            }
        }

        /// <summary>
        /// 根据 ID 查找元素
        /// </summary>
        /// <param name="id">元素的唯一标识符</param>
        /// <returns>找到的元素，如果不存在或已被回收则返回 null</returns>
        public static FrameworkElement? FindElement(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return null;
            }

            lock (_lock)
            {
                if (_registry.TryGetValue(id, out var weakRef))
                {
                    if (weakRef.TryGetTarget(out var element))
                    {
                        return element;
                    }
                    else
                    {
                        // 元素已被回收，清理注册表
                        _registry.Remove(id);
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// 获取元素的屏幕坐标边界
        /// </summary>
        /// <param name="id">元素的唯一标识符</param>
        /// <returns>元素的屏幕坐标矩形，如果元素不存在则返回 null</returns>
        public static Rect? GetElementBounds(string id)
        {
            var element = FindElement(id);
            if (element == null)
            {
                return null;
            }

            try
            {
                // 获取元素相对于屏幕的位置
                var point = element.PointToScreen(new System.Windows.Point(0, 0));
                return new Rect(point.X, point.Y, element.ActualWidth, element.ActualHeight);
            }
            catch
            {
                // 如果元素不在可视树中，可能会抛出异常
                return null;
            }
        }

        /// <summary>
        /// 清空注册表（用于测试或重置）
        /// </summary>
        public static void Clear()
        {
            lock (_lock)
            {
                _registry.Clear();
            }
        }

        /// <summary>
        /// 获取所有已注册的元素 ID
        /// </summary>
        public static IEnumerable<string> GetRegisteredIds()
        {
            lock (_lock)
            {
                return new List<string>(_registry.Keys);
            }
        }
    }
}
