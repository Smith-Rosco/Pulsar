// [Path]: Pulsar/Pulsar/Core/Plugin/Security/PermissionInterceptor.cs

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Pulsar.Core.Plugin.Security
{
    /// <summary>
    /// 权限拦截器 - 负责权限检查和授权管理
    /// </summary>
    public class PermissionInterceptor
    {
        private readonly ILogger<PermissionInterceptor> _logger;
        private readonly ConcurrentDictionary<string, PluginPermission> _grantedPermissions;
        private readonly ConcurrentDictionary<string, PluginPermission> _requestedPermissions;
        private readonly ConcurrentDictionary<string, HashSet<PluginPermission>> _deniedPermissions;
        
        /// <summary>
        /// 权限请求事件 - 当插件请求新权限时触发
        /// </summary>
        public event EventHandler<PermissionRequestEventArgs>? PermissionRequested;

        public PermissionInterceptor(ILogger<PermissionInterceptor> logger)
        {
            _logger = logger;
            _grantedPermissions = new ConcurrentDictionary<string, PluginPermission>();
            _requestedPermissions = new ConcurrentDictionary<string, PluginPermission>();
            _deniedPermissions = new ConcurrentDictionary<string, HashSet<PluginPermission>>();
        }

        /// <summary>
        /// 注册插件的声明权限
        /// </summary>
        /// <param name="pluginId">插件 ID</param>
        /// <param name="permissions">声明的权限</param>
        public void RegisterPluginPermissions(string pluginId, PluginPermission permissions)
        {
            _requestedPermissions[pluginId] = permissions;
            _logger.LogInformation("[PermissionInterceptor] Plugin {PluginId} declared permissions: {Permissions}", 
                pluginId, permissions);
        }

        /// <summary>
        /// 授予插件权限
        /// </summary>
        /// <param name="pluginId">插件 ID</param>
        /// <param name="permissions">要授予的权限</param>
        public void GrantPermissions(string pluginId, PluginPermission permissions)
        {
            _grantedPermissions.AddOrUpdate(pluginId, permissions, (_, existing) => existing | permissions);
            _logger.LogInformation("[PermissionInterceptor] Granted permissions to {PluginId}: {Permissions}", 
                pluginId, permissions);
        }

        /// <summary>
        /// 撤销插件权限
        /// </summary>
        /// <param name="pluginId">插件 ID</param>
        /// <param name="permissions">要撤销的权限</param>
        public void RevokePermissions(string pluginId, PluginPermission permissions)
        {
            if (_grantedPermissions.TryGetValue(pluginId, out var existing))
            {
                var newPermissions = existing & ~permissions;
                _grantedPermissions[pluginId] = newPermissions;
                _logger.LogInformation("[PermissionInterceptor] Revoked permissions from {PluginId}: {Permissions}", 
                    pluginId, permissions);
            }
        }

        /// <summary>
        /// 拒绝插件权限（用户明确拒绝）
        /// </summary>
        /// <param name="pluginId">插件 ID</param>
        /// <param name="permission">被拒绝的权限</param>
        public void DenyPermission(string pluginId, PluginPermission permission)
        {
            var denied = _deniedPermissions.GetOrAdd(pluginId, _ => new HashSet<PluginPermission>());
            denied.Add(permission);
            _logger.LogWarning("[PermissionInterceptor] User denied permission {Permission} for plugin {PluginId}", 
                permission, pluginId);
        }

        /// <summary>
        /// 检查插件是否拥有指定权限
        /// </summary>
        /// <param name="pluginId">插件 ID</param>
        /// <param name="permission">要检查的权限</param>
        /// <returns>是否拥有权限</returns>
        public bool HasPermission(string pluginId, PluginPermission permission)
        {
            // 检查是否被明确拒绝
            if (_deniedPermissions.TryGetValue(pluginId, out var denied) && denied.Contains(permission))
            {
                return false;
            }

            // 检查是否已授予
            if (_grantedPermissions.TryGetValue(pluginId, out var granted))
            {
                return granted.HasPermission(permission);
            }

            return false;
        }

        /// <summary>
        /// 检查权限，如果没有则抛出异常
        /// </summary>
        /// <param name="pluginId">插件 ID</param>
        /// <param name="permission">要检查的权限</param>
        /// <param name="operation">操作描述（用于日志）</param>
        /// <exception cref="UnauthorizedAccessException">权限不足时抛出</exception>
        public void CheckPermission(string pluginId, PluginPermission permission, string operation)
        {
            if (!HasPermission(pluginId, permission))
            {
                var message = $"Plugin '{pluginId}' does not have permission '{permission}' for operation '{operation}'";
                _logger.LogError("[PermissionInterceptor] {Message}", message);
                throw new UnauthorizedAccessException(message);
            }
        }

        /// <summary>
        /// 异步请求权限（如果未授予，则触发 UI 请求）
        /// </summary>
        /// <param name="pluginId">插件 ID</param>
        /// <param name="permission">要请求的权限</param>
        /// <param name="reason">请求原因</param>
        /// <returns>是否授予权限</returns>
        public async Task<bool> RequestPermissionAsync(string pluginId, PluginPermission permission, string reason)
        {
            // 如果已经拥有权限，直接返回
            if (HasPermission(pluginId, permission))
            {
                return true;
            }

            // 如果被明确拒绝，直接返回 false
            if (_deniedPermissions.TryGetValue(pluginId, out var denied) && denied.Contains(permission))
            {
                _logger.LogWarning("[PermissionInterceptor] Permission {Permission} was previously denied for {PluginId}", 
                    permission, pluginId);
                return false;
            }

            // 触发权限请求事件
            var args = new PermissionRequestEventArgs(pluginId, permission, reason);
            PermissionRequested?.Invoke(this, args);

            // 等待用户响应
            await args.ResponseTask;

            if (args.IsGranted)
            {
                GrantPermissions(pluginId, permission);
                
                // 如果用户选择记住，则保存到配置
                if (args.RememberChoice)
                {
                    // TODO: 保存到 Profiles.json
                    _logger.LogInformation("[PermissionInterceptor] User chose to remember permission grant for {PluginId}", pluginId);
                }
            }
            else
            {
                DenyPermission(pluginId, permission);
                
                if (args.RememberChoice)
                {
                    // TODO: 保存拒绝记录到配置
                    _logger.LogInformation("[PermissionInterceptor] User chose to remember permission denial for {PluginId}", pluginId);
                }
            }

            return args.IsGranted;
        }

        /// <summary>
        /// 获取插件的所有已授予权限
        /// </summary>
        /// <param name="pluginId">插件 ID</param>
        /// <returns>已授予的权限</returns>
        public PluginPermission GetGrantedPermissions(string pluginId)
        {
            return _grantedPermissions.TryGetValue(pluginId, out var permissions) 
                ? permissions 
                : PluginPermission.None;
        }

        /// <summary>
        /// 获取插件声明的权限
        /// </summary>
        /// <param name="pluginId">插件 ID</param>
        /// <returns>声明的权限</returns>
        public PluginPermission GetRequestedPermissions(string pluginId)
        {
            return _requestedPermissions.TryGetValue(pluginId, out var permissions) 
                ? permissions 
                : PluginPermission.None;
        }

        /// <summary>
        /// 获取插件被拒绝的权限列表
        /// </summary>
        /// <param name="pluginId">插件 ID</param>
        /// <returns>被拒绝的权限列表</returns>
        public IEnumerable<PluginPermission> GetDeniedPermissions(string pluginId)
        {
            return _deniedPermissions.TryGetValue(pluginId, out var denied) 
                ? denied 
                : Enumerable.Empty<PluginPermission>();
        }

        /// <summary>
        /// 清除插件的所有权限记录
        /// </summary>
        /// <param name="pluginId">插件 ID</param>
        public void ClearPluginPermissions(string pluginId)
        {
            _grantedPermissions.TryRemove(pluginId, out _);
            _requestedPermissions.TryRemove(pluginId, out _);
            _deniedPermissions.TryRemove(pluginId, out _);
            _logger.LogInformation("[PermissionInterceptor] Cleared all permissions for plugin {PluginId}", pluginId);
        }

        /// <summary>
        /// 获取所有插件的权限摘要
        /// </summary>
        /// <returns>权限摘要字典</returns>
        public Dictionary<string, PermissionSummary> GetAllPermissionSummaries()
        {
            var summaries = new Dictionary<string, PermissionSummary>();

            foreach (var pluginId in _requestedPermissions.Keys.Union(_grantedPermissions.Keys))
            {
                summaries[pluginId] = new PermissionSummary
                {
                    PluginId = pluginId,
                    RequestedPermissions = GetRequestedPermissions(pluginId),
                    GrantedPermissions = GetGrantedPermissions(pluginId),
                    DeniedPermissions = GetDeniedPermissions(pluginId).ToList()
                };
            }

            return summaries;
        }
    }

    /// <summary>
    /// 权限请求事件参数
    /// </summary>
    public class PermissionRequestEventArgs : EventArgs
    {
        /// <summary>
        /// 插件 ID
        /// </summary>
        public string PluginId { get; }

        /// <summary>
        /// 请求的权限
        /// </summary>
        public PluginPermission Permission { get; }

        /// <summary>
        /// 请求原因
        /// </summary>
        public string Reason { get; }

        /// <summary>
        /// 是否授予权限
        /// </summary>
        public bool IsGranted { get; set; }

        /// <summary>
        /// 是否记住选择
        /// </summary>
        public bool RememberChoice { get; set; }

        /// <summary>
        /// 响应任务完成源
        /// </summary>
        private readonly TaskCompletionSource<bool> _responseSource;

        /// <summary>
        /// 响应任务
        /// </summary>
        public Task ResponseTask => _responseSource.Task;

        public PermissionRequestEventArgs(string pluginId, PluginPermission permission, string reason)
        {
            PluginId = pluginId;
            Permission = permission;
            Reason = reason;
            _responseSource = new TaskCompletionSource<bool>();
        }

        /// <summary>
        /// 完成权限请求
        /// </summary>
        /// <param name="granted">是否授予</param>
        /// <param name="remember">是否记住</param>
        public void Complete(bool granted, bool remember = false)
        {
            IsGranted = granted;
            RememberChoice = remember;
            _responseSource.TrySetResult(granted);
        }
    }

    /// <summary>
    /// 权限摘要
    /// </summary>
    public class PermissionSummary
    {
        /// <summary>
        /// 插件 ID
        /// </summary>
        public string PluginId { get; set; } = string.Empty;

        /// <summary>
        /// 请求的权限
        /// </summary>
        public PluginPermission RequestedPermissions { get; set; }

        /// <summary>
        /// 已授予的权限
        /// </summary>
        public PluginPermission GrantedPermissions { get; set; }

        /// <summary>
        /// 被拒绝的权限
        /// </summary>
        public List<PluginPermission> DeniedPermissions { get; set; } = new();
    }
}
