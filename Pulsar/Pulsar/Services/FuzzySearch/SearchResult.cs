// [Path]: Pulsar/Pulsar/Services/FuzzySearch/SearchResult.cs
namespace Pulsar.Services.FuzzySearch
{
    /// <summary>
    /// 搜索结果包装类
    /// </summary>
    /// <typeparam name="T">结果项类型</typeparam>
    public class SearchResult<T> where T : class
    {
        /// <summary>
        /// 匹配项
        /// </summary>
        public required T Item { get; init; }

        /// <summary>
        /// 综合评分（0-100）
        /// </summary>
        public int Score { get; init; }

        /// <summary>
        /// 匹配策略名称（用于调试）
        /// </summary>
        public string MatchedBy { get; init; } = string.Empty;
    }
}
