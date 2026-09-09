using System.Threading.Tasks;

namespace Pulsar.Services.Interfaces
{
    /// <summary>
    /// 设置窗口临时页（动态标签页）协调器（openspec 2026-09-08-dynamic-settings-tabs）。
    /// 页面注册语义由 <see cref="SettingsPageCatalog"/> 承载；本服务负责打开
    /// （注册 + 导航激活）、关闭（含脏状态确认），以及"导航离开即回收"的判定。
    /// </summary>
    public interface ITransientPageService
    {
        /// <summary>该类型的临时页当前是否已打开（已注册到目录）。</summary>
        bool IsOpen(string typeId);

        /// <summary>
        /// 打开并激活临时页：未打开时先注册到目录（侧边栏出现新条目）再导航；
        /// 已打开时直接激活既有实例（按类型单例，不新增条目）。
        /// </summary>
        Task<bool> OpenTransientPageAsync(string typeId);

        /// <summary>
        /// 打开实体级临时页（模板 + 实体双参，openspec unify-slot-editor-transient-pages D1/D2）：
        /// 按 "<c>templateId:entityId</c>" 组合 id 克隆模板注册后注册并导航；组合 id 即注册 id，
        /// 同一实体的重复触发激活既有 tab（VS Code preview-tab 语义）。目录/回收/单例判定
        /// 全部按组合 id 原样工作。
        /// </summary>
        /// <param name="templateId">模板定义 id（须已 RegisterDefinition 登记）。</param>
        /// <param name="entityId">实体标识（如 "Global:1"），与模板 id 拼装为组合注册 id。</param>
        /// <param name="titleOverride">tab 标题覆盖（已本地化的实体标题，如 slot 标签 + 上下文）；空则用模板标题。</param>
        Task<bool> OpenTransientPageAsync(string templateId, string entityId, string? titleOverride = null);

        /// <summary>
        /// 关闭（回收）临时页：存在未保存修改时先走守卫确认流程（保存/放弃/取消），
        /// 用户取消则保持打开并返回 false。
        /// </summary>
        Task<bool> CloseTransientPageAsync(string typeId);

        /// <summary>
        /// 强制回收临时页（不走守卫确认）。仅供程序化生命周期使用：实体转换
        /// （draft → 已落盘实体）或实体已消失（unify D7）时，页面未保存状态随
        /// 之废弃，不应对一个已不存在的页面弹出 save/discard/cancel。
        /// </summary>
        /// <param name="pageId">组合注册 id（如 "slot-editor:Global:1"）。</param>
        Task<bool> DiscardTransientPageAsync(string pageId);

        /// <summary>
        /// 导航离开钩子：来源页为干净（无未保存修改）的临时页时自动回收；
        /// 脏临时页保留（不阻塞导航，关窗时统一提示）。
        /// </summary>
        void NotifyNavigatedAwayFrom(string? sourcePageId);
    }
}
