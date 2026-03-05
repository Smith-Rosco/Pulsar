// [Path]: Pulsar/Pulsar/Services/Interfaces/IFuzzySearchService.cs
using Pulsar.Services.FuzzySearch;
using System;
using System.Collections.Generic;

namespace Pulsar.Services.Interfaces
{
    /// <summary>
    /// 模糊搜索服务接口
    /// </summary>
    /// <typeparam name="T">搜索目标类型</typeparam>
    public interface IFuzzySearchService<T> where T : class
    {
        /// <summary>
        /// 执行模糊搜索
        /// </summary>
        /// <param name="query">搜索查询</param>
        /// <param name="items">待搜索项集合</param>
        /// <param name="textExtractor">文本提取器（从 T 提取搜索文本）</param>
        /// <param name="options">搜索选项（可选）</param>
        /// <returns>按相关度排序的搜索结果</returns>
        List<SearchResult<T>> Search(
            string query, 
            IEnumerable<T> items, 
            Func<T, string> textExtractor,
            FuzzySearchOptions? options = null);

        /// <summary>
        /// 构建索引（可选，用于性能优化）
        /// </summary>
        /// <param name="items">待索引项集合</param>
        /// <param name="textExtractor">文本提取器</param>
        void BuildIndex(IEnumerable<T> items, Func<T, string> textExtractor);

        /// <summary>
        /// 清除缓存
        /// </summary>
        void ClearCache();
    }
}
