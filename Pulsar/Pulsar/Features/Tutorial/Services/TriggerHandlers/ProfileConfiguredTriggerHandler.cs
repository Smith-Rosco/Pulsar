// [Path]: Pulsar/Pulsar/Services/Tutorial/TriggerHandlers/ProfileConfiguredTriggerHandler.cs

using System;
using Microsoft.Extensions.Logging;
using Pulsar.Features.Tutorial.Models;
using Pulsar.Models;
using Pulsar.Services.Interfaces;

namespace Pulsar.Features.Tutorial.Services.TriggerHandlers
{
    /// <summary>
    /// Profile 配置触发器处理器
    /// 监听配置更新事件，检测是否为指定进程创建了 Profile
    /// </summary>
    public class ProfileConfiguredTriggerHandler : ITriggerHandler
    {
        private readonly IConfigService _configService;
        private readonly ILogger<ProfileConfiguredTriggerHandler>? _logger;
        private Action? _onTriggered;
        private Action? _configUpdatedHandler;
        private string? _targetProfileName;

        public ProfileConfiguredTriggerHandler(IConfigService configService, ILogger<ProfileConfiguredTriggerHandler>? logger = null)
        {
            _configService = configService;
            _logger = logger;
        }

        public void Setup(TutorialTrigger trigger, Action onTriggered)
        {
            _onTriggered = onTriggered;
            _targetProfileName = trigger.TargetValue;

            _logger?.LogInformation(
                "[ProfileConfiguredTriggerHandler] Setup trigger for profile: {ProfileName}",
                _targetProfileName);

            _configUpdatedHandler = () =>
            {
                var config = _configService.Current;

                _logger?.LogDebug(
                    "[ProfileConfiguredTriggerHandler] ConfigUpdated event fired, checking profile: {ProfileName}",
                    _targetProfileName);

                if (ProfileExists(config, _targetProfileName))
                {
                    _logger?.LogInformation(
                        "[ProfileConfiguredTriggerHandler] Profile '{ProfileName}' found, triggering next step",
                        _targetProfileName);
                    
                    _onTriggered?.Invoke();
                }
                else
                {
                    _logger?.LogDebug(
                        "[ProfileConfiguredTriggerHandler] Profile '{ProfileName}' not found yet",
                        _targetProfileName);
                }
            };

            _configService.ConfigUpdated += _configUpdatedHandler;
            
            // [Fix] 立即检查一次，以防 Profile 在触发器设置之前就已经存在
            var currentConfig = _configService.Current;
            if (ProfileExists(currentConfig, _targetProfileName))
            {
                _logger?.LogInformation(
                    "[ProfileConfiguredTriggerHandler] Profile '{ProfileName}' already exists, triggering immediately",
                    _targetProfileName);
                
                _onTriggered?.Invoke();
            }
        }

        public void Cleanup()
        {
            if (_configUpdatedHandler != null)
            {
                _configService.ConfigUpdated -= _configUpdatedHandler;
            }

            _onTriggered = null;
            _configUpdatedHandler = null;
        }

        /// <summary>
        /// 检查指定 Profile 是否存在
        /// [Fix] 简化逻辑：只要 Profile 存在就算完成，不需要检查是否有 Slot
        /// 这样用户创建 Profile 后就能立即进入下一步，符合教程流程
        /// </summary>
        /// <param name="config">当前配置</param>
        /// <param name="profileName">Profile 名称（如 "notepad"）</param>
        /// <returns>Profile 是否存在</returns>
        private bool ProfileExists(ProfilesConfig config, string? profileName)
        {
            if (string.IsNullOrWhiteSpace(profileName))
            {
                _logger?.LogWarning("[ProfileConfiguredTriggerHandler] ProfileName is null or empty");
                return false;
            }

            // 只检查 Profile 是否存在（字典使用 OrdinalIgnoreCase，所以大小写不敏感）
            bool exists = config.Profiles.ContainsKey(profileName);
            
            _logger?.LogDebug(
                "[ProfileConfiguredTriggerHandler] Profile '{ProfileName}' exists: {Exists}",
                profileName,
                exists);

            return exists;
        }
    }
}
