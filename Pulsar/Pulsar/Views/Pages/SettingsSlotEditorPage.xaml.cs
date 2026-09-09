using System.Windows;
using System.Windows.Controls;
using Pulsar.Services;
using Pulsar.Services.Interfaces;
using Pulsar.ViewModels.Dialogs;

namespace Pulsar.Views.Pages
{
    /// <summary>
    /// Slot 编辑器临时页（openspec unify-slot-editor-transient-pages P1/P2）。
    /// 每个打开的 slot / 草稿一个组合 id 实例（<c>slot-editor:&lt;contextKey&gt;:&lt;slotNo&gt;</c>
    /// 或 <c>:draft</c>），VM 组合既有 <see cref="SlotEditorViewModel"/>（D3：组合优先，
    /// 不提取核心）。编辑模式直接编辑 live <c>PluginSlot</c>，无独立保存按钮，统一走
    /// 设置窗口保存；新建模式（Create）承载两步向导 + 嵌入式提交页脚（3.1/3.3）。
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

            // 按 EditorMode 装配内容面：Edit → 编辑面；Create → 两步向导（含类型选择）。
            EditorHost.Content = viewModel.IsCreateMode
                ? new Dialogs.Contents.AddSlotContent()
                : new Dialogs.Contents.SlotConfigurationDialogContent();
        }
    }
}
