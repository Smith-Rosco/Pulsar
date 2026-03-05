// [Path]: Pulsar/Pulsar/Services/FuzzySearch/FuzzySearchService.cs
using Microsoft.Extensions.Logging;
using Pulsar.Services.FuzzySearch.Strategies;
using Pulsar.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Pulsar.Services.FuzzySearch
{
    /// <summary>
    /// 模糊搜索服务实现 - 支持索引、渐进式搜索
    /// </summary>
    public class FuzzySearchService<T> : IFuzzySearchService<T> where T : class
    {
        private readonly ILogger<FuzzySearchService<T>> _logger;
        private readonly List<ISearchStrategy<T>> _strategies;
        private readonly SearchIndex<T>? _index;
        private readonly FuzzySearchOptions _defaultOptions;

        public FuzzySearchService(ILogger<FuzzySearchService<T>> logger)
        {
            _logger = logger;
            _defaultOptions = new FuzzySearchOptions();
            
            // 初始化策略（按权重降序）
            _strategies = new List<ISearchStrategy<T>>
            {
                new ExactMatchStrategy<T>(),
                new PrefixMatchStrategy<T>(),
                new TokenMatchStrategy<T>(),
                new LevenshteinStrategy<T>(_defaultOptions.MaxLevenshteinDistance)
            };

            // 初始化索引
            if (_defaultOptions.EnableIndexing)
            {
                _index = new SearchIndex<T>();
            }
        }

        public void BuildIndex(IEnumerable<T> items, Func<T, string> textExtractor)
        {
            if (_index == null)
            {
                _logger.LogWarning("Indexing is disabled, skipping BuildIndex");
                return;
            }

            try
            {
                _index.Build(items, textExtractor);
                _logger.LogInformation("Search index built successfully for {Count} items", items.Count());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to build search index");
            }
        }

        public List<SearchResult<T>> Search(
            string query, 
            IEnumerable<T> items, 
            Func<T, string> textExtractor,
            FuzzySearchOptions? options = null)
        {
            options ??= _defaultOptions;

            if (string.IsNullOrWhiteSpace(query))
            {
                return items.Select(item => new SearchResult<T>
                {
                    Item = item,
                    Score = 100,
                    MatchedBy = "All"
                }).ToList();
            }

            var queryLower = query.ToLower().Trim();
            var results = new Dictionary<T, (int Score, string MatchedBy)>();

            // 渐进式策略选择
            var activeStrategies = SelectStrategies(queryLower, options);

            // 候选集合（用于优化 Levenshtein）
            IEnumerable<T> candidates = items;

            // 如果启用索引，先通过索引快速筛选候选集
            if (_index != null && options.EnableIndexing)
            {
                candidates = GetCandidatesFromIndex(queryLower, items);
                
                // 如果索引返回空结果，回退到全量搜索
                if (!candidates.Any())
                {
                    candidates = items;
                }
            }

            // 执行策略匹配
            foreach (var strategy in activeStrategies)
            {
                // Levenshtein 策略特殊处理：只对 Top N 候选计算
                IEnumerable<T> strategyTargets = candidates;
                
                if (strategy is LevenshteinStrategy<T> && 
                    options.Progressive.EnableLevenshtein &&
                    results.Any())
                {
                    // 只对已有分数的 Top N 候选计算编辑距离
                    strategyTargets = results
                        .OrderByDescending(r => r.Value.Score)
                        .Take(options.Progressive.LevenshteinMaxCandidates)
                        .Select(r => r.Key);
                }

                foreach (var item in strategyTargets)
                {
                    int score = strategy.Match(queryLower, item, textExtractor);
                    
                    if (score > 0)
                    {
                        if (results.ContainsKey(item))
                        {
                            // 取最高分
                            if (score > results[item].Score)
                            {
                                results[item] = (score, strategy.Name);
                            }
                        }
                        else
                        {
                            results[item] = (score, strategy.Name);
                        }
                    }
                }
            }

            // 过滤、排序、限制结果数量
            var finalResults = results
                .Where(r => r.Value.Score >= options.MinScore)
                .OrderByDescending(r => r.Value.Score)
                .ThenBy(r => textExtractor(r.Key))
                .Take(options.MaxResults)
                .Select(r => new SearchResult<T>
                {
                    Item = r.Key,
                    Score = r.Value.Score,
                    MatchedBy = r.Value.MatchedBy
                })
                .ToList();

            _logger.LogDebug("Search for '{Query}' returned {Count} results", query, finalResults.Count);
            return finalResults;
        }

        public void ClearCache()
        {
            _index?.Clear();
            _logger.LogInformation("Search cache cleared");
        }

        // === 私有方法 ===

        /// <summary>
        /// 渐进式策略选择
        /// </summary>
        private List<ISearchStrategy<T>> SelectStrategies(string query, FuzzySearchOptions options)
        {
            var selected = new List<ISearchStrategy<T>>();

            // 短查询（1-2 字符）：只用快速策略
            if (query.Length <= options.Progressive.ShortQueryLength)
            {
                selected.Add(_strategies.First(s => s is ExactMatchStrategy<T>));
                selected.Add(_strategies.First(s => s is PrefixMatchStrategy<T>));
            }
            // 中查询（3-4 字符）：添加分词策略
            else if (query.Length <= options.Progressive.MediumQueryLength)
            {
                selected.Add(_strategies.First(s => s is ExactMatchStrategy<T>));
                selected.Add(_strategies.First(s => s is PrefixMatchStrategy<T>));
                selected.Add(_strategies.First(s => s is TokenMatchStrategy<T>));
            }
            // 长查询（5+ 字符）：全部策略
            else
            {
                selected.AddRange(_strategies);
                
                // 如果禁用 Levenshtein，移除该策略
                if (!options.Progressive.EnableLevenshtein)
                {
                    selected.RemoveAll(s => s is LevenshteinStrategy<T>);
                }
            }

            return selected;
        }

        /// <summary>
        /// 从索引获取候选集
        /// </summary>
        private IEnumerable<T> GetCandidatesFromIndex(string query, IEnumerable<T> fallback)
        {
            if (_index == null)
                return fallback;

            var candidates = new HashSet<T>();

            // 前缀索引查询
            var prefixResults = _index.SearchByPrefix(query);
            foreach (var item in prefixResults)
                candidates.Add(item);

            // 首字母缩写索引查询
            var acronymResults = _index.SearchByAcronym(query);
            foreach (var item in acronymResults)
                candidates.Add(item);

            // 分词索引查询
            var tokenResults = _index.SearchByToken(query);
            foreach (var item in tokenResults)
                candidates.Add(item);

            return candidates.Any() ? candidates : fallback;
        }
    }
}
