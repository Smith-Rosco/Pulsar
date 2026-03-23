// [Path]: Pulsar/Pulsar/ViewModels/Dialogs/PermissionRequestViewModel.cs

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pulsar.Core.Plugin.Security;
using Pulsar.ViewModels.Base;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Pulsar.ViewModels.Dialogs
{
    /// <summary>
    /// 权限请求对话框 ViewModel
    /// </summary>
    public partial class PermissionRequestViewModel : ObservableObject, IDialogViewModel
    {
        [ObservableProperty]
        private string _pluginId = string.Empty;

        [ObservableProperty]
        private string _pluginName = string.Empty;

        [ObservableProperty]
        private PluginPermission _requestedPermission;

        [ObservableProperty]
        private string _permissionDisplayName = string.Empty;

        [ObservableProperty]
        private string _permissionDescription = string.Empty;

        [ObservableProperty]
        private string _reason = string.Empty;

        [ObservableProperty]
        private PermissionRiskLevel _riskLevel;

        [ObservableProperty]
        private string _riskLevelText = string.Empty;

        [ObservableProperty]
        private string _riskLevelColor = "#FFA500"; // Orange default

        [ObservableProperty]
        private bool _rememberChoice = false;

        [ObservableProperty]
        private bool _isGranted = false;

        /// <summary>
        /// 请求关闭事件
        /// </summary>
        public Action<Pulsar.Models.Enums.DialogResult>? RequestClose { get; set; }

        /// <summary>
        /// 是否可滚动
        /// </summary>

        public PermissionRequestViewModel()
        {
        }

        /// <summary>
        /// 初始化权限请求
        /// </summary>
        public void Initialize(string pluginId, string pluginName, PluginPermission permission, string reason)
        {
            PluginId = pluginId;
            PluginName = pluginName;
            RequestedPermission = permission;
            Reason = reason;

            PermissionDisplayName = permission.GetDisplayName();
            PermissionDescription = permission.GetDescription();
            RiskLevel = permission.GetRiskLevel();

            // 设置风险等级文本和颜色
            (RiskLevelText, RiskLevelColor) = RiskLevel switch
            {
                PermissionRiskLevel.Low => ("低风险", "#4CAF50"),      // Green
                PermissionRiskLevel.Medium => ("中等风险", "#FF9800"), // Orange
                PermissionRiskLevel.High => ("高风险", "#FF5722"),     // Deep Orange
                PermissionRiskLevel.Critical => ("严重风险", "#F44336"), // Red
                _ => ("未知风险", "#9E9E9E")                           // Grey
            };
        }

        /// <summary>
        /// 授予权限命令
        /// </summary>
        [RelayCommand]
        private void Grant()
        {
            IsGranted = true;
            RequestClose?.Invoke(Pulsar.Models.Enums.DialogResult.Yes);
        }

        /// <summary>
        /// 拒绝权限命令
        /// </summary>
        [RelayCommand]
        private void Deny()
        {
            IsGranted = false;
            RequestClose?.Invoke(Pulsar.Models.Enums.DialogResult.No);
        }

        /// <summary>
        /// 检查是否可以关闭
        /// </summary>
        public Task<bool> CanCloseAsync(Pulsar.Models.Enums.DialogResult result)
        {
            return Task.FromResult(true);
        }
    }
}
