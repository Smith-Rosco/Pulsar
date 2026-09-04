using System.Collections.Generic;
using Pulsar.Core.Plugin.Metadata;

namespace Pulsar.Models
{
    /// <summary>
    /// External Plugin 生命周期操作到达的阶段。
    /// 部分成功不回滚：结果携带"到达哪个阶段"，UI 据此给出准确文案。
    /// </summary>
    public enum ExternalPluginOpPhase
    {
        /// <summary>未开始 / 前置校验失败。</summary>
        None = 0,

        /// <summary>安装：插件文件已复制到安装目录。</summary>
        FilesInstalled = 1,

        /// <summary>安装：发现已刷新，descriptor 已进入运行时目录。</summary>
        Discovered = 2,

        /// <summary>安装：清单权限已持久化到 PluginProfile。</summary>
        PermissionsGranted = 3,

        /// <summary>安装/启用：插件已激活（OnEnableAsync 贡献已生效）。</summary>
        Activated = 4,

        /// <summary>卸载：插件已停用，ALC 已卸载（文件可删除）。</summary>
        Deactivated = 5,

        /// <summary>卸载：插件目录已删除。</summary>
        Uninstalled = 6
    }

    /// <summary>
    /// External Plugin 生命周期操作（安装 / 卸载 / 启用 / 授权）的结果。
    /// 部分成功时 <see cref="Success"/> 仍为 true，但 <see cref="Warning"/> 描述未完成部分；
    /// 硬失败时 <see cref="Success"/> 为 false，携带 <see cref="ErrorCode"/> 与 <see cref="Message"/>。
    /// </summary>
    public sealed record ExternalPluginOpResult(
        bool Success,
        string PluginId,
        ExternalPluginOpPhase Phase,
        string? ErrorCode = null,
        string? Message = null,
        string? Warning = null)
    {
        public static ExternalPluginOpResult Ok(string pluginId, ExternalPluginOpPhase phase, string? warning = null)
        {
            return new ExternalPluginOpResult(true, pluginId, phase, Warning: warning);
        }

        public static ExternalPluginOpResult Fail(string pluginId, ExternalPluginOpPhase phase, string errorCode, string message)
        {
            return new ExternalPluginOpResult(false, pluginId, phase, ErrorCode: errorCode, Message: message);
        }
    }

    /// <summary>
    /// 安装前置检查结果：返回清单与待审批权限，供 UI 弹确认框。
    /// 校验失败时 <see cref="Success"/> 为 false。
    /// </summary>
    public sealed record ExternalPluginInstallPreparation(
        bool Success,
        PluginManifest? Manifest = null,
        IReadOnlyList<string>? PendingPermissions = null,
        string? ErrorCode = null,
        string? ErrorMessage = null)
    {
        public static ExternalPluginInstallPreparation Ready(PluginManifest manifest, IReadOnlyList<string> pendingPermissions)
        {
            return new ExternalPluginInstallPreparation(true, manifest, pendingPermissions);
        }

        public static ExternalPluginInstallPreparation Invalid(string errorCode, string errorMessage)
        {
            return new ExternalPluginInstallPreparation(false, ErrorCode: errorCode, ErrorMessage: errorMessage);
        }
    }
}
