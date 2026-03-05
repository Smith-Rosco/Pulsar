// [Path]: Pulsar/Pulsar/Services/Interfaces/ISearchStrategy.cs
using System;

namespace Pulsar.Services.Interfaces
{
    /// <summary>
    /// 搜索策略接口 - 定义单一匹配算法
    /// </summary>
    /// <typeparam name="T">搜索目标类型</typeparam>
    public interface ISearchStrategy<T> where T : class
    {
        /// <summary>
        /// 策略名称（用于调试和日志）
        /// </summary>
        string Name { get; }

        /// <summary>
        /// 策略权重（0-100，权重越高优先级越高）
        /// </summary>
        int Weight { get; }

        /// <summary>
        /// 执行匹配
        /// </summary>
        /// <param name="query">搜索查询（已转小写）</param>
        /// <param name="item">待匹配项</param>
        /// <param name="textExtractor">文本提取器（从 T 提取搜索文本）</param>
        /// <returns>匹配结果（0-100 分，0 表示不匹配）</returns>
        int Match(string query, T item, Func<T, string> textExtractor);
    }
}
