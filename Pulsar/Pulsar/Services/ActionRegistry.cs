using System;
using System.Collections.Generic;
using Pulsar.Core.Interfaces;
using Pulsar.Models;

namespace Pulsar.Services
{
    /// <summary>
    /// 动作注册中心：负责维护 "哪种 Item 由哪个 Handler 处理" 的关系
    /// </summary>
    public class ActionRegistry
    {
        private readonly Dictionary<Type, IActionHandler> _handlers = new();

        /// <summary>
        /// 注册一个处理器
        /// </summary>
        /// <typeparam name="TItem">该处理器支持的数据模型类型</typeparam>
        public void Register<TItem>(IActionHandler handler) where TItem : GridItemBase
        {
            _handlers[typeof(TItem)] = handler;
        }

        /// <summary>
        /// 根据数据模型查找对应的处理器
        /// </summary>
        public IActionHandler? GetHandler(GridItemBase item)
        {
            if (item == null) return null;

            // 尝试直接获取
            if (_handlers.TryGetValue(item.GetType(), out var handler))
            {
                return handler;
            }

            // 如果没有直接匹配，可以考虑遍历基类 (暂不需要，保持简单)
            return null;
        }
    }
}