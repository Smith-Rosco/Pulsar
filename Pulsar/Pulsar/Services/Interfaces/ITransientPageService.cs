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
        /// 关闭（回收）临时页：存在未保存修改时先走守卫确认流程（保存/放弃/取消），
        /// 用户取消则保持打开并返回 false。
        /// </summary>
        Task<bool> CloseTransientPageAsync(string typeId);

        /// <summary>
        /// 导航离开钩子：来源页为干净（无未保存修改）的临时页时自动回收；
        /// 脏临时页保留（不阻塞导航，关窗时统一提示）。
        /// </summary>
        void NotifyNavigatedAwayFrom(string? sourcePageId);
    }
}
