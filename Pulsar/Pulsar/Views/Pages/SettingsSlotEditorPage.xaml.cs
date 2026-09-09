using System.Windows.Controls;
using Pulsar.Services;
using Pulsar.Services.Interfaces;
using Pulsar.ViewModels.Dialogs;

namespace Pulsar.Views.Pages
{
    /// <summary>
    /// Slot 编辑器临时页（openspec unify-slot-editor-transient-pages P1）。
    /// 每个打开的 slot 一个组合 id 实例（<c>slot-editor:&lt;contextKey&gt;:&lt;slotNo&gt;</c>），
    /// VM 组合既有 <see cref="SlotEditorViewModel"/>（D3：组合优先，不提取核心），
    /// 直接编辑 live <c>PluginSlot</c>；无独立保存按钮，统一走设置窗口保存。
    /// </summary>
    public partial class SettingsSlotEditorPage : Page
    {
        public SettingsSlotEditorPage(SlotEditorViewModel viewModel, IThemeService themeService)
        {
            InitializeComponent();
            DataContext = viewModel;
            // AGENTS.md 不变量：Page 加载会替换 Resources 字典，ApplyTheme 必须在
            // InitializeComponent 之后调用。
            themeService.ApplyTheme(this, themeService.CurrentTheme);
        }
    }
}
