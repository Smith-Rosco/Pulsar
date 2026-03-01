using Pulsar.Models;
using System.Collections.Generic;
using System.Linq;

namespace Pulsar.Services.Interfaces
{
    /// <summary>
    /// 插件推荐类型
    /// </summary>
    public enum RecommendationType
    {
        DisableUnusedPlugin,      // 禁用未使用的插件
        CheckPluginErrors,        // 检查插件错误
        OptimizePerformance       // 性能优化建议
    }

    /// <summary>
    /// 插件推荐项
    /// </summary>
    public class PluginRecommendation
    {
        public RecommendationType Type { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string PluginId { get; set; } = string.Empty;
        public string PluginName { get; set; } = string.Empty;
        public string ActionLabel { get; set; } = string.Empty;
        public string Icon { get; set; } = "💡";
        public string Severity { get; set; } = "Info"; // Info, Warning, Error
    }

    /// <summary>
    /// 插件推荐引擎接口
    /// </summary>
    public interface IPluginRecommendationEngine
    {
        /// <summary>
        /// 获取所有推荐
        /// </summary>
        List<PluginRecommendation> GetRecommendations();

        /// <summary>
        /// 获取指定插件的推荐
        /// </summary>
        List<PluginRecommendation> GetRecommendationsForPlugin(string pluginId);
    }
}
