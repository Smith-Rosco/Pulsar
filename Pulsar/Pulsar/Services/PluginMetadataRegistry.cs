// [Path]: Pulsar/Pulsar/Services/PluginMetadataRegistry.cs

using Microsoft.Extensions.Logging;
using Pulsar.Core.Plugin.Metadata;
using Pulsar.Services.Interfaces;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace Pulsar.Services
{
    /// <summary>
    /// 插件元数据注册表 - 管理所有插件的元数据信息
    /// </summary>
    public class PluginMetadataRegistry : IPluginMetadataRegistry
    {
        private readonly ConcurrentDictionary<string, PluginMetadata> _metadata = new();
        private readonly ILogger<PluginMetadataRegistry> _logger;

        public PluginMetadataRegistry(ILogger<PluginMetadataRegistry> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// 注册插件元数据
        /// </summary>
        public void Register(PluginMetadata metadata)
        {
            if (string.IsNullOrWhiteSpace(metadata.Id))
            {
                _logger.LogWarning("[PluginMetadataRegistry] Cannot register metadata with empty ID");
                return;
            }

            if (_metadata.TryAdd(metadata.Id, metadata))
            {
                _logger.LogDebug("[PluginMetadataRegistry] Registered metadata for plugin: {PluginId}", metadata.Id);
            }
            else
            {
                _logger.LogWarning("[PluginMetadataRegistry] Metadata for plugin {PluginId} already registered, updating...", metadata.Id);
                _metadata[metadata.Id] = metadata;
            }
        }

        /// <summary>
        /// 获取指定插件的元数据
        /// </summary>
        public PluginMetadata? GetMetadata(string pluginId)
        {
            if (string.IsNullOrWhiteSpace(pluginId))
            {
                return null;
            }

            return _metadata.TryGetValue(pluginId, out var metadata) ? metadata : null;
        }

        /// <summary>
        /// 获取所有已注册的插件元数据
        /// </summary>
        public IReadOnlyCollection<PluginMetadata> GetAllMetadata()
        {
            return _metadata.Values.ToList().AsReadOnly();
        }

        /// <summary>
        /// 检查插件是否已注册元数据
        /// </summary>
        public bool HasMetadata(string pluginId)
        {
            return !string.IsNullOrWhiteSpace(pluginId) && _metadata.ContainsKey(pluginId);
        }

        /// <summary>
        /// 按分类获取插件元数据
        /// </summary>
        public IReadOnlyCollection<PluginMetadata> GetByCategory(string category)
        {
            if (string.IsNullOrWhiteSpace(category))
            {
                return Array.Empty<PluginMetadata>();
            }

            return _metadata.Values
                .Where(m => string.Equals(m.Display.Category, category, StringComparison.OrdinalIgnoreCase))
                .ToList()
                .AsReadOnly();
        }

        /// <summary>
        /// 获取所有分类
        /// </summary>
        public IReadOnlyCollection<string> GetAllCategories()
        {
            return _metadata.Values
                .Select(m => m.Display.Category)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(c => c)
                .ToList()
                .AsReadOnly();
        }

        public SlotActionMetadata? GetActionMetadata(string pluginId, string action)
        {
            if (string.IsNullOrWhiteSpace(pluginId) || string.IsNullOrWhiteSpace(action))
            {
                return null;
            }

            var metadata = GetMetadata(pluginId);
            if (metadata?.Actions == null)
            {
                return null;
            }

            if (metadata.Actions.TryGetValue(action, out var actionMetadata))
            {
                return actionMetadata;
            }

            return metadata.Actions.Values.FirstOrDefault(candidate =>
                candidate.Aliases.Any(alias => string.Equals(alias, action, StringComparison.OrdinalIgnoreCase)));
        }

        /// <summary>
        /// 获取按排序优先级排序的元数据
        /// </summary>
        public IReadOnlyCollection<PluginMetadata> GetSortedMetadata()
        {
            return _metadata.Values
                .OrderBy(m => m.UI.SortOrder)
                .ThenBy(m => m.Display.Name)
                .ToList()
                .AsReadOnly();
        }

        /// <summary>
        /// 获取特色插件元数据
        /// </summary>
        public IReadOnlyCollection<PluginMetadata> GetFeaturedMetadata()
        {
            return _metadata.Values
                .Where(m => m.UI.IsFeatured)
                .OrderBy(m => m.UI.SortOrder)
                .ToList()
                .AsReadOnly();
        }

        /// <summary>
        /// 清空所有元数据 (用于测试或重新加载)
        /// </summary>
        public void Clear()
        {
            _metadata.Clear();
            _logger.LogInformation("[PluginMetadataRegistry] Cleared all metadata");
        }
    }
}
