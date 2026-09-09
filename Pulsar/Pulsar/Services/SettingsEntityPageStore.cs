using System;
using System.Collections.Generic;
using System.Windows.Controls;
using Pulsar.ViewModels;

namespace Pulsar.Services
{
    /// <summary>
    /// 实体级临时页的页面构造器注册表（openspec unify-slot-editor-transient-pages D1/D2）。
    /// 组合 id（如 <c>slot-editor:Global:1</c>）的页面构造需要闭合实体引用（live
    /// <c>PluginSlot</c>、委托 seam），由发起打开的 <c>SettingsViewModel</c> 在 Open 前
    /// 登记。<see cref="SettingsPageFactory"/>（每窗口 transient）在构造页面时先查本表。
    /// 单例生命周期使其同时可见于 transient 服务（单例，负责回收清理）与 factory；
    /// 设置窗口关闭时调用 <see cref="Clear"/>，防止跨窗口会话悬挂旧闭包。
    /// </summary>
    public class SettingsEntityPageStore
    {
        private readonly Dictionary<string, Func<SettingsViewModel, Page>> _creators =
            new(StringComparer.OrdinalIgnoreCase);

        public void Register(string pageId, Func<SettingsViewModel, Page> creator)
        {
            _creators[pageId] = creator;
        }

        public bool TryGet(string pageId, out Func<SettingsViewModel, Page> creator)
        {
            return _creators.TryGetValue(pageId, out creator!);
        }

        public bool Remove(string pageId)
        {
            return _creators.Remove(pageId);
        }

        public void Clear()
        {
            _creators.Clear();
        }
    }
}
