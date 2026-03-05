// [Path]: Pulsar/Pulsar/Services/FuzzySearch/FuzzySearchOptions.cs
namespace Pulsar.Services.FuzzySearch
{
    /// <summary>
    /// 模糊搜索配置选项
    /// </summary>
    public class FuzzySearchOptions
    {
        /// <summary>
        /// 最低分数阈值（低于此分数的结果将被过滤）
        /// </summary>
        public int MinScore { get; set; } = 30;

        /// <summary>
        /// 最大返回结果数量
        /// </summary>
        public int MaxResults { get; set; } = 100;

        /// <summary>
        /// 启用索引系统（提升前缀/首字母缩写查询性能）
        /// </summary>
        public bool EnableIndexing { get; set; } = true;

        /// <summary>
        /// 启用缓存系统（缓存最近查询结果）
        /// </summary>
        public bool EnableCaching { get; set; } = true;

        /// <summary>
        /// 缓存大小（LRU 缓存容量）
        /// </summary>
        public int CacheSize { get; set; } = 100;

        /// <summary>
        /// 渐进式搜索配置
        /// </summary>
        public ProgressiveSearchConfig Progressive { get; set; } = new();

        /// <summary>
        /// Levenshtein 最大编辑距离
        /// </summary>
        public int MaxLevenshteinDistance { get; set; } = 2;
    }

    /// <summary>
    /// 渐进式搜索配置
    /// </summary>
    public class ProgressiveSearchConfig
    {
        /// <summary>
        /// 短查询长度阈值（<= 此长度只使用快速策略）
        /// </summary>
        public int ShortQueryLength { get; set; } = 2;

        /// <summary>
        /// 中查询长度阈值（<= 此长度使用中速策略）
        /// </summary>
        public int MediumQueryLength { get; set; } = 4;

        /// <summary>
        /// 启用 Levenshtein 策略（仅长查询）
        /// </summary>
        public bool EnableLevenshtein { get; set; } = true;

        /// <summary>
        /// Levenshtein 最大候选数量（只对 Top N 候选计算编辑距离）
        /// </summary>
        public int LevenshteinMaxCandidates { get; set; } = 100;
    }
}
