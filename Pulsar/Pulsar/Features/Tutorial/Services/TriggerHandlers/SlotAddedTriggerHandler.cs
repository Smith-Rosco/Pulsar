// [Path]: Pulsar/Pulsar/Services/Tutorial/TriggerHandlers/SlotAddedTriggerHandler.cs

using System;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Pulsar.Features.Tutorial.Models;
using Pulsar.Models;
using Pulsar.Services.Interfaces;

namespace Pulsar.Features.Tutorial.Services.TriggerHandlers
{
    /// <summary>
    /// Slot 添加触发器处理器
    /// 监听配置更新事件，检测是否添加了符合条件的 Slot
    /// </summary>
    public class SlotAddedTriggerHandler : ITriggerHandler
    {
        private readonly IConfigService _configService;
        private readonly ILogger<SlotAddedTriggerHandler>? _logger;
        private Action? _onTriggered;
        private Action? _configUpdatedHandler;
        private string? _criteriaJson;

        public SlotAddedTriggerHandler(IConfigService configService, ILogger<SlotAddedTriggerHandler>? logger = null)
        {
            _configService = configService;
            _logger = logger;
        }

        public void Setup(TutorialTrigger trigger, Action onTriggered)
        {
            _onTriggered = onTriggered;
            _criteriaJson = trigger.TargetValue;

            _logger?.LogInformation(
                "[SlotAddedTriggerHandler] Setup trigger with criteria: {Criteria}",
                _criteriaJson);

            _configUpdatedHandler = () =>
            {
                var config = _configService.Current;

                _logger?.LogDebug(
                    "[SlotAddedTriggerHandler] ConfigUpdated event fired, checking criteria: {Criteria}",
                    _criteriaJson);

                if (SlotMatchesCriteria(config, _criteriaJson))
                {
                    _logger?.LogInformation(
                        "[SlotAddedTriggerHandler] Slot matching criteria found, triggering next step");
                    
                    _onTriggered?.Invoke();
                }
                else
                {
                    _logger?.LogDebug(
                        "[SlotAddedTriggerHandler] No matching slot found yet");
                }
            };

            _configService.ConfigUpdated += _configUpdatedHandler;
            
            // [Fix] 立即检查一次，以防 Slot 在触发器设置之前就已经存在
            var currentConfig = _configService.Current;
            if (SlotMatchesCriteria(currentConfig, _criteriaJson))
            {
                _logger?.LogInformation(
                    "[SlotAddedTriggerHandler] Matching slot already exists, triggering immediately");
                
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
        /// 检查配置中是否存在符合条件的 Slot
        /// </summary>
        /// <param name="config">当前配置</param>
        /// <param name="criteriaJson">JSON 格式的匹配条件</param>
        /// <returns>是否匹配</returns>
        private bool SlotMatchesCriteria(ProfilesConfig config, string? criteriaJson)
        {
            if (string.IsNullOrWhiteSpace(criteriaJson))
            {
                _logger?.LogWarning("[SlotAddedTriggerHandler] Criteria JSON is null or empty");
                return false;
            }

            try
            {
                // 解析匹配条件
                var criteria = JsonSerializer.Deserialize<SlotCriteria>(criteriaJson, new JsonSerializerOptions 
                { 
                    PropertyNameCaseInsensitive = true 
                });
                
                if (criteria == null)
                {
                    _logger?.LogWarning("[SlotAddedTriggerHandler] Failed to deserialize criteria JSON");
                    return false;
                }

                _logger?.LogDebug(
                    "[SlotAddedTriggerHandler] Checking criteria - PluginId: {PluginId}, Profile: {Profile}, Mode: {Mode}",
                    criteria.PluginId,
                    criteria.Profile,
                    criteria.Mode);

                // 遍历所有 Profile 的所有 Slot
                foreach (var profile in config.Profiles)
                {
                    // 检查 CommandMode slots
                    if (CheckSlots(profile.Value.CommandMode, profile.Key, "CommandMode", criteria))
                    {
                        _logger?.LogInformation(
                            "[SlotAddedTriggerHandler] Found matching slot in {Profile} CommandMode",
                            profile.Key);
                        return true;
                    }

                    // 检查 SwitchMode slots
                    if (CheckSlots(profile.Value.SwitchMode, profile.Key, "SwitchMode", criteria))
                    {
                        _logger?.LogInformation(
                            "[SlotAddedTriggerHandler] Found matching slot in {Profile} SwitchMode",
                            profile.Key);
                        return true;
                    }
                }

                return false;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "[SlotAddedTriggerHandler] Error checking slot criteria");
                return false;
            }
        }

        /// <summary>
        /// 检查 Slot 列表是否匹配条件
        /// </summary>
        private bool CheckSlots(System.Collections.Generic.List<PluginSlot> slots, 
            string profileKey, string mode, SlotCriteria criteria)
        {
            foreach (var slot in slots)
            {
                bool matches = true;

                // 检查 PluginId
                if (!string.IsNullOrEmpty(criteria.PluginId) && 
                    !string.Equals(slot.PluginId, criteria.PluginId, StringComparison.OrdinalIgnoreCase))
                {
                    matches = false;
                }

                // 检查 Profile (Context) - 使用不区分大小写比较
                if (!string.IsNullOrEmpty(criteria.Profile) && 
                    !string.Equals(profileKey, criteria.Profile, StringComparison.OrdinalIgnoreCase))
                {
                    matches = false;
                }

                // 检查 Mode
                if (!string.IsNullOrEmpty(criteria.Mode) && 
                    !string.Equals(mode, criteria.Mode, StringComparison.OrdinalIgnoreCase))
                {
                    matches = false;
                }

                // 检查 Parameters 中的特定键值对
                if (criteria.Parameters != null)
                {
                    foreach (var kvp in criteria.Parameters)
                    {
                        if (!slot.Args.TryGetValue(kvp.Key, out var value) || 
                            !string.Equals(value, kvp.Value, StringComparison.OrdinalIgnoreCase))
                        {
                            matches = false;
                            break;
                        }
                    }
                }

                if (matches)
                {
                    _logger?.LogDebug(
                        "[SlotAddedTriggerHandler] Slot matched - PluginId: {PluginId}, Profile: {Profile}, Mode: {Mode}",
                        slot.PluginId,
                        profileKey,
                        mode);
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Slot 匹配条件
        /// </summary>
        private class SlotCriteria
        {
            public string? PluginId { get; set; }
            public string? Profile { get; set; }
            public string? Mode { get; set; }
            public System.Collections.Generic.Dictionary<string, string>? Parameters { get; set; }
        }
    }
}
