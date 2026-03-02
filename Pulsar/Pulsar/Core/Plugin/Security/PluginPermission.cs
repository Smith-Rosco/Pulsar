// [Path]: Pulsar/Pulsar/Core/Plugin/Security/PluginPermission.cs

using System;

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
                PluginPermission.ReadWindowInfo => "读取窗口信息",
                PluginPermission.ReadProcessPath => "读取进程路径",
                PluginPermission.ShowNotification => "显示通知",
                PluginPermission.ReadClipboard => "读取剪贴板",
                PluginPermission.WriteClipboard => "写入剪贴板",
                PluginPermission.ReadSelectedText => "读取选中文本",
                PluginPermission.ReadProcessWindows => "读取进程窗口列表",
                PluginPermission.SimulateKeyboard => "模拟键盘输入",
                PluginPermission.SimulateMouse => "模拟鼠标输入",
                PluginPermission.StartProcess => "启动外部进程",
                PluginPermission.KillProcess => "终止进程",
                PluginPermission.ReadFileSystem => "读取文件系统",
                PluginPermission.WriteFileSystem => "写入文件系统",
                PluginPermission.AccessRegistry => "访问注册表",
                PluginPermission.AccessEnvironment => "访问环境变量",
                PluginPermission.AccessCredentials => "访问凭据",
                PluginPermission.ModifyCredentials => "修改凭据",
                PluginPermission.AccessNetwork => "访问网络",
                PluginPermission.ExecuteNativeCode => "执行原生代码",
                PluginPermission.LoadAssembly => "加载程序集",
                PluginPermission.ModifyConfiguration => "修改配置",
                PluginPermission.RegisterHotkey => "注册热键",
                PluginPermission.ModifyPluginRegistry => "修改插件注册表",
                PluginPermission.AccessAllServices => "访问所有服务",
                PluginPermission.BypassPermissionCheck => "绕过权限检查",
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
                PluginPermission.ReadWindowInfo => "允许插件读取当前窗口的基础信息（进程名、PID、窗口句柄）",
                PluginPermission.ReadProcessPath => "允许插件读取目标进程的完整路径",
                PluginPermission.ShowNotification => "允许插件显示系统通知消息",
                PluginPermission.ReadClipboard => "允许插件读取剪贴板内容",
                PluginPermission.WriteClipboard => "允许插件修改剪贴板内容",
                PluginPermission.ReadSelectedText => "允许插件读取当前选中的文本",
                PluginPermission.ReadProcessWindows => "允许插件获取目标进程的所有窗口列表",
                PluginPermission.SimulateKeyboard => "允许插件模拟键盘输入",
                PluginPermission.SimulateMouse => "允许插件模拟鼠标操作",
                PluginPermission.StartProcess => "允许插件启动外部程序",
                PluginPermission.KillProcess => "允许插件终止进程（危险操作）",
                PluginPermission.ReadFileSystem => "允许插件读取文件系统",
                PluginPermission.WriteFileSystem => "允许插件写入文件系统（危险操作）",
                PluginPermission.AccessRegistry => "允许插件访问 Windows 注册表（危险操作）",
                PluginPermission.AccessEnvironment => "允许插件访问环境变量",
                PluginPermission.AccessCredentials => "允许插件读取 PKI 凭据管理器中的密码（敏感操作）",
                PluginPermission.ModifyCredentials => "允许插件添加/删除凭据（敏感操作）",
                PluginPermission.AccessNetwork => "允许插件发起网络请求",
                PluginPermission.ExecuteNativeCode => "允许插件执行原生代码（危险操作）",
                PluginPermission.LoadAssembly => "允许插件动态加载外部程序集（危险操作）",
                PluginPermission.ModifyConfiguration => "允许插件修改 Pulsar 配置",
                PluginPermission.RegisterHotkey => "允许插件注册全局热键（系统级操作）",
                PluginPermission.ModifyPluginRegistry => "允许插件修改插件注册表（系统级操作）",
                PluginPermission.AccessAllServices => "允许插件访问所有依赖注入服务（系统级操作）",
                PluginPermission.BypassPermissionCheck => "允许插件绕过权限检查（仅核心插件）",
                _ => "未知权限"
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
