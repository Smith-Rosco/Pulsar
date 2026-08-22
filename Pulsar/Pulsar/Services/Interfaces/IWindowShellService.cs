namespace Pulsar.Services.Interfaces
{
    /// <summary>
    /// Host integration for the Pulsar shell (hiding the main window before
    /// input injection). Consumers that only need to clear the desktop should
    /// depend on this narrow interface instead of the full IWindowService.
    /// </summary>
    public interface IWindowShellService
    {
        /// <summary>
        /// 注册隐藏主窗口的操作委托
        /// </summary>
        void RegisterHideAction(System.Action hideAction);

        /// <summary>
        /// 强制隐藏主窗口 (用于 PKI 注入前的清场)
        /// </summary>
        void HideMainWindow();
    }
}