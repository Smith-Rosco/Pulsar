// [Path]: Pulsar/Pulsar/Core/Plugin/Security/PluginPermission.cs

using System;
using Pulsar.Core.Localization;

namespace Pulsar.Core.Plugin.Security
{
    /// <summary>
    /// 插件权限枚举 - 定义插件可以请求的所有权限
    /// 使用 Flags 特性支持权限组合
    /// </summary>
    [Flags]
    public enum PluginPermission : long
    {
        /// <summary>
        /// 无权限
        /// </summary>
        None = 0,

        // === 基础权限 (Basic Tier) ===

        /// <summary>
        /// 读取目标窗口基础信息 (进程名、PID、窗口句柄)
        /// </summary>
        ReadWindowInfo = 1L << 0,

        /// <summary>
        /// 读取目标进程路径
        /// </summary>
        ReadProcessPath = 1L << 1,

        /// <summary>
        /// 显示通知消息
        /// </summary>
        ShowNotification = 1L << 2,

        // === 标准权限 (Standard Tier) ===

        /// <summary>
        /// 读取剪贴板内容
        /// </summary>
        ReadClipboard = 1L << 10,

        /// <summary>
        /// 写入剪贴板内容
        /// </summary>
        WriteClipboard = 1L << 11,

        /// <summary>
        /// 读取选中的文本
        /// </summary>
        ReadSelectedText = 1L << 12,

        /// <summary>
        /// 获取目标进程的所有窗口列表
        /// </summary>
        ReadProcessWindows = 1L << 13,

        /// <summary>
        /// 模拟键盘输入
        /// </summary>
        SimulateKeyboard = 1L << 14,

        /// <summary>
        /// 模拟鼠标输入
        /// </summary>
        SimulateMouse = 1L << 15,

        // === 高级权限 (Advanced Tier) ===

        /// <summary>
        /// 启动外部进程
        /// </summary>
        StartProcess = 1L << 20,

        /// <summary>
        /// 终止外部进程
        /// </summary>
        KillProcess = 1L << 21,

        /// <summary>
        /// 读取文件系统
        /// </summary>
        ReadFileSystem = 1L << 22,

        /// <summary>
        /// 写入文件系统
        /// </summary>
        WriteFileSystem = 1L << 23,

        /// <summary>
        /// 访问注册表
        /// </summary>
        AccessRegistry = 1L << 24,

        /// <summary>
        /// 访问环境变量
        /// </summary>
        AccessEnvironment = 1L << 25,

        // === 敏感权限 (Sensitive Tier) ===

        /// <summary>
        /// 访问 PKI 凭据管理器 (读取密码)
        /// </summary>
        AccessCredentials = 1L << 30,

        /// <summary>
        /// 修改 PKI 凭据 (添加/删除密码)
        /// </summary>
        ModifyCredentials = 1L << 31,

        /// <summary>
        /// 访问网络 (HTTP 请求)
        /// </summary>
        AccessNetwork = 1L << 32,

        /// <summary>
        /// 执行原生代码 (P/Invoke, DllImport)
        /// </summary>
        ExecuteNativeCode = 1L << 33,

        /// <summary>
        /// 加载外部程序集
        /// </summary>
        LoadAssembly = 1L << 34,

        /// <summary>
        /// 修改 Pulsar 配置
        /// </summary>
        ModifyConfiguration = 1L << 35,

        // === 系统权限 (System Tier - 仅核心插件) ===

        /// <summary>
        /// 注册全局热键
        /// </summary>
        RegisterHotkey = 1L << 40,

        /// <summary>
        /// 修改插件注册表
        /// </summary>
        ModifyPluginRegistry = 1L << 41,

        /// <summary>
        /// 访问所有服务 (完全 DI 容器访问)
        /// </summary>
        AccessAllServices = 1L << 42,

        /// <summary>
        /// 绕过权限检查 (仅核心插件)
        /// </summary>
        BypassPermissionCheck = 1L << 43,
    }

    /// <summary>
    /// 权限组合 - 预定义的权限集合
    /// </summary>
    public static class PermissionSets
    {
        /// <summary>
        /// 基础权限集 - 适用于简单插件
        /// </summary>
        public static PluginPermission Basic =>
            PluginPermission.ReadWindowInfo |
            PluginPermission.ReadProcessPath |
            PluginPermission.ShowNotification;

        /// <summary>
        /// 标准权限集 - 适用于大多数插件
        /// </summary>
        public static PluginPermission Standard =>
            Basic |
            PluginPermission.ReadClipboard |
            PluginPermission.WriteClipboard |
            PluginPermission.ReadSelectedText |
            PluginPermission.ReadProcessWindows |
            PluginPermission.SimulateKeyboard;

        /// <summary>
        /// 高级权限集 - 适用于系统集成插件
        /// </summary>
        public static PluginPermission Advanced =>
            Standard |
            PluginPermission.StartProcess |
            PluginPermission.ReadFileSystem |
            PluginPermission.WriteFileSystem |
            PluginPermission.AccessEnvironment;

        /// <summary>
        /// 完全权限集 - 适用于受信任的插件
        /// </summary>
        public static PluginPermission Full =>
            Advanced |
            PluginPermission.KillProcess |
            PluginPermission.AccessRegistry |
            PluginPermission.AccessCredentials |
            PluginPermission.AccessNetwork |
            PluginPermission.ExecuteNativeCode |
            PluginPermission.LoadAssembly;

        /// <summary>
        /// 系统权限集 - 仅核心插件
        /// </summary>
        public static PluginPermission System =>
            Full |
            PluginPermission.RegisterHotkey |
            PluginPermission.ModifyPluginRegistry |
            PluginPermission.AccessAllServices |
            PluginPermission.BypassPermissionCheck;
    }

    /// <summary>
    /// 权限扩展方法
    /// </summary>
    public static class PluginPermissionExtensions
    {
        private static ILocalizationService? Loc
        {
            get
            {
                try
                {
                    if (System.Windows.Application.Current is App app)
                        return app.Services.GetService(typeof(ILocalizationService)) as ILocalizationService;
                    return null;
                }
                catch { return null; }
            }
        }

        /// <summary>
        /// 检查是否拥有指定权限
        /// </summary>
        public static bool HasPermission(this PluginPermission granted, PluginPermission required)
        {
            return (granted & required) == required;
        }

        /// <summary>
        /// 获取权限的友好名称
        /// </summary>
        public static string GetDisplayName(this PluginPermission permission)
        {
            return permission switch
            {
                PluginPermission.ReadWindowInfo => Loc?["PluginPermission.ReadWindowInfo"] ?? "Read Window Information",
                PluginPermission.ReadProcessPath => Loc?["PluginPermission.ReadProcessPath"] ?? "Read Process Path",
                PluginPermission.ShowNotification => Loc?["PluginPermission.ShowNotification"] ?? "Show Notification",
                PluginPermission.ReadClipboard => Loc?["PluginPermission.ReadClipboard"] ?? "Read Clipboard",
                PluginPermission.WriteClipboard => Loc?["PluginPermission.WriteClipboard"] ?? "Write Clipboard",
                PluginPermission.ReadSelectedText => Loc?["PluginPermission.ReadSelectedText"] ?? "Read Selected Text",
                PluginPermission.ReadProcessWindows => Loc?["PluginPermission.ReadProcessWindows"] ?? "Read Process Windows",
                PluginPermission.SimulateKeyboard => Loc?["PluginPermission.SimulateKeyboard"] ?? "Simulate Keyboard",
                PluginPermission.SimulateMouse => Loc?["PluginPermission.SimulateMouse"] ?? "Simulate Mouse",
                PluginPermission.StartProcess => Loc?["PluginPermission.StartProcess"] ?? "Start Process",
                PluginPermission.KillProcess => Loc?["PluginPermission.KillProcess"] ?? "Kill Process",
                PluginPermission.ReadFileSystem => Loc?["PluginPermission.ReadFileSystem"] ?? "Read File System",
                PluginPermission.WriteFileSystem => Loc?["PluginPermission.WriteFileSystem"] ?? "Write File System",
                PluginPermission.AccessRegistry => Loc?["PluginPermission.AccessRegistry"] ?? "Access Registry",
                PluginPermission.AccessEnvironment => Loc?["PluginPermission.AccessEnvironment"] ?? "Access Environment",
                PluginPermission.AccessCredentials => Loc?["PluginPermission.AccessCredentials"] ?? "Access Credentials",
                PluginPermission.ModifyCredentials => Loc?["PluginPermission.ModifyCredentials"] ?? "Modify Credentials",
                PluginPermission.AccessNetwork => Loc?["PluginPermission.AccessNetwork"] ?? "Access Network",
                PluginPermission.ExecuteNativeCode => Loc?["PluginPermission.ExecuteNativeCode"] ?? "Execute Native Code",
                PluginPermission.LoadAssembly => Loc?["PluginPermission.LoadAssembly"] ?? "Load Assembly",
                PluginPermission.ModifyConfiguration => Loc?["PluginPermission.ModifyConfiguration"] ?? "Modify Configuration",
                PluginPermission.RegisterHotkey => Loc?["PluginPermission.RegisterHotkey"] ?? "Register Hotkey",
                PluginPermission.ModifyPluginRegistry => Loc?["PluginPermission.ModifyPluginRegistry"] ?? "Modify Plugin Registry",
                PluginPermission.AccessAllServices => Loc?["PluginPermission.AccessAllServices"] ?? "Access All Services",
                PluginPermission.BypassPermissionCheck => Loc?["PluginPermission.BypassPermissionCheck"] ?? "Bypass Permission Check",
                _ => permission.ToString()
            };
        }

        /// <summary>
        /// 获取权限的描述
        /// </summary>
        public static string GetDescription(this PluginPermission permission)
        {
            return permission switch
            {
                PluginPermission.ReadWindowInfo => Loc?["PluginPermission.ReadWindowInfoDesc"] ?? "Allows plugin to read basic window information (process name, PID, window handle)",
                PluginPermission.ReadProcessPath => Loc?["PluginPermission.ReadProcessPathDesc"] ?? "Allows plugin to read the full path of the target process",
                PluginPermission.ShowNotification => Loc?["PluginPermission.ShowNotificationDesc"] ?? "Allows plugin to display system notification messages",
                PluginPermission.ReadClipboard => Loc?["PluginPermission.ReadClipboardDesc"] ?? "Allows plugin to read clipboard content",
                PluginPermission.WriteClipboard => Loc?["PluginPermission.WriteClipboardDesc"] ?? "Allows plugin to modify clipboard content",
                PluginPermission.ReadSelectedText => Loc?["PluginPermission.ReadSelectedTextDesc"] ?? "Allows plugin to read currently selected text",
                PluginPermission.ReadProcessWindows => Loc?["PluginPermission.ReadProcessWindowsDesc"] ?? "Allows plugin to get the list of all windows for the target process",
                PluginPermission.SimulateKeyboard => Loc?["PluginPermission.SimulateKeyboardDesc"] ?? "Allows plugin to simulate keyboard input",
                PluginPermission.SimulateMouse => Loc?["PluginPermission.SimulateMouseDesc"] ?? "Allows plugin to simulate mouse operations",
                PluginPermission.StartProcess => Loc?["PluginPermission.StartProcessDesc"] ?? "Allows plugin to start external programs",
                PluginPermission.KillProcess => Loc?["PluginPermission.KillProcessDesc"] ?? "Allows plugin to terminate processes (dangerous operation)",
                PluginPermission.ReadFileSystem => Loc?["PluginPermission.ReadFileSystemDesc"] ?? "Allows plugin to read the file system",
                PluginPermission.WriteFileSystem => Loc?["PluginPermission.WriteFileSystemDesc"] ?? "Allows plugin to write to the file system (dangerous operation)",
                PluginPermission.AccessRegistry => Loc?["PluginPermission.AccessRegistryDesc"] ?? "Allows plugin to access the Windows Registry (dangerous operation)",
                PluginPermission.AccessEnvironment => Loc?["PluginPermission.AccessEnvironmentDesc"] ?? "Allows plugin to access environment variables",
                PluginPermission.AccessCredentials => Loc?["PluginPermission.AccessCredentialsDesc"] ?? "Allows plugin to read passwords from the PKI credential manager (sensitive operation)",
                PluginPermission.ModifyCredentials => Loc?["PluginPermission.ModifyCredentialsDesc"] ?? "Allows plugin to add/delete credentials (sensitive operation)",
                PluginPermission.AccessNetwork => Loc?["PluginPermission.AccessNetworkDesc"] ?? "Allows plugin to initiate network requests",
                PluginPermission.ExecuteNativeCode => Loc?["PluginPermission.ExecuteNativeCodeDesc"] ?? "Allows plugin to execute native code (dangerous operation)",
                PluginPermission.LoadAssembly => Loc?["PluginPermission.LoadAssemblyDesc"] ?? "Allows plugin to dynamically load external assemblies (dangerous operation)",
                PluginPermission.ModifyConfiguration => Loc?["PluginPermission.ModifyConfigurationDesc"] ?? "Allows plugin to modify Pulsar configuration",
                PluginPermission.RegisterHotkey => Loc?["PluginPermission.RegisterHotkeyDesc"] ?? "Allows plugin to register global hotkeys (system-level operation)",
                PluginPermission.ModifyPluginRegistry => Loc?["PluginPermission.ModifyPluginRegistryDesc"] ?? "Allows plugin to modify the plugin registry (system-level operation)",
                PluginPermission.AccessAllServices => Loc?["PluginPermission.AccessAllServicesDesc"] ?? "Allows plugin to access all dependency injection services (system-level operation)",
                PluginPermission.BypassPermissionCheck => Loc?["PluginPermission.BypassPermissionCheckDesc"] ?? "Allows plugin to bypass permission checks (core plugins only)",
                _ => Loc?["PluginPermission.UnknownPermission"] ?? "Unknown Permission"
            };
        }

        /// <summary>
        /// 获取权限的风险等级
        /// </summary>
        public static PermissionRiskLevel GetRiskLevel(this PluginPermission permission)
        {
            return permission switch
            {
                PluginPermission.ReadWindowInfo => PermissionRiskLevel.Low,
                PluginPermission.ReadProcessPath => PermissionRiskLevel.Low,
                PluginPermission.ShowNotification => PermissionRiskLevel.Low,
                PluginPermission.ReadClipboard => PermissionRiskLevel.Medium,
                PluginPermission.WriteClipboard => PermissionRiskLevel.Medium,
                PluginPermission.ReadSelectedText => PermissionRiskLevel.Medium,
                PluginPermission.ReadProcessWindows => PermissionRiskLevel.Medium,
                PluginPermission.SimulateKeyboard => PermissionRiskLevel.Medium,
                PluginPermission.SimulateMouse => PermissionRiskLevel.Medium,
                PluginPermission.StartProcess => PermissionRiskLevel.High,
                PluginPermission.ReadFileSystem => PermissionRiskLevel.Medium,
                PluginPermission.WriteFileSystem => PermissionRiskLevel.High,
                PluginPermission.AccessEnvironment => PermissionRiskLevel.Medium,
                PluginPermission.KillProcess => PermissionRiskLevel.Critical,
                PluginPermission.AccessRegistry => PermissionRiskLevel.Critical,
                PluginPermission.AccessCredentials => PermissionRiskLevel.Critical,
                PluginPermission.ModifyCredentials => PermissionRiskLevel.Critical,
                PluginPermission.AccessNetwork => PermissionRiskLevel.High,
                PluginPermission.ExecuteNativeCode => PermissionRiskLevel.Critical,
                PluginPermission.LoadAssembly => PermissionRiskLevel.Critical,
                PluginPermission.ModifyConfiguration => PermissionRiskLevel.High,
                PluginPermission.RegisterHotkey => PermissionRiskLevel.High,
                PluginPermission.ModifyPluginRegistry => PermissionRiskLevel.Critical,
                PluginPermission.AccessAllServices => PermissionRiskLevel.Critical,
                PluginPermission.BypassPermissionCheck => PermissionRiskLevel.Critical,
                _ => PermissionRiskLevel.Unknown
            };
        }
    }

    /// <summary>
    /// 权限风险等级
    /// </summary>
    public enum PermissionRiskLevel
    {
        /// <summary>
        /// 未知风险
        /// </summary>
        Unknown = 0,

        /// <summary>
        /// 低风险 - 基础信息读取
        /// </summary>
        Low = 1,

        /// <summary>
        /// 中等风险 - 用户数据访问
        /// </summary>
        Medium = 2,

        /// <summary>
        /// 高风险 - 系统操作
        /// </summary>
        High = 3,

        /// <summary>
        /// 严重风险 - 危险操作
        /// </summary>
        Critical = 4
    }
}
