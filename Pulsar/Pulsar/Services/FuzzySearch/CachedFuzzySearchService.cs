// [Path]: Pulsar/Pulsar/Services/FuzzySearch/CachedFuzzySearchService.cs
using Microsoft.Extensions.Logging;
using Pulsar.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Pulsar.Services.FuzzySearch
{
    /// <summary>
    /// 带缓存的模糊搜索服务装饰器
    /// </summary>
    public class CachedFuzzySearchService<T> : IFuzzySearchService<T> where T : class
    {
        private readonly IFuzzySearchService<T> _innerService;
        private readonly ILogger<CachedFuzzySearchService<T>> _logger;
        private readonly LRUCache<string, List<SearchResult<T>>> _cache;
        private readonly bool _enableCache;

        public CachedFuzzySearchService(
            IFuzzySearchService<T> innerService,
            ILogger<CachedFuzzySearchService<T>> logger,
            FuzzySearchOptions? options = null)
        {
            _innerService = innerService;
            _logger = logger;
            
            options ??= new FuzzySearchOptions();
            _enableCache = options.EnableCaching;
            _cache = new LRUCache<string, List<SearchResult<T>>>(options.CacheSize);
        }

        public List<SearchResult<T>> Search(
            string query, 
            IEnumerable<T> items, 
            Func<T, string> textExtractor,
            FuzzySearchOptions? options = null)
        {
            if (!_enableCache || string.IsNullOrWhiteSpace(query))
            {
                return _innerService.Search(query, items, textExtractor, options);
            }

            var cacheKey = query.ToLower().Trim();

            // 检查缓存
            if (_cache.TryGet(cacheKey, out var cachedResults) && cachedResults != null)
            {
                _logger.LogDebug("Cache hit for query: '{Query}'", query);
                return cachedResults;
            }

            // 渐进式缓存优化：检查前缀缓存
            if (cacheKey.Length > 1)
            {
                var prefix = cacheKey.Substring(0, cacheKey.Length - 1);
                
                if (_cache.TryGet(prefix, out var prefixResults) && prefixResults != null)
                {
                    _logger.LogDebug("Prefix cache hit for query: '{Query}' (prefix: '{Prefix}')", query, prefix);
                    
                    // 在前缀结果基础上过滤
                    var filteredResults = FilterResults(prefixResults, cacheKey, textExtractor);
                    _cache.Set(cacheKey, filteredResults);
                    return filteredResults;
                }
            }

            // 缓存未命中，执行搜索
            _logger.LogDebug("Cache miss for query: '{Query}'", query);
            var results = _innerService.Search(query, items, textExtractor, options);
            
            // 存入缓存
            _cache.Set(cacheKey, results);
            
            return results;
        }

        public void BuildIndex(IEnumerable<T> items, Func<T, string> textExtractor)
        {
            _innerService.BuildIndex(items, textExtractor);
            
            // 索引重建后清空缓存
            ClearCache();
        }

        public void ClearCache()
        {
            _cache.Clear();
            _innerService.ClearCache();
            _logger.LogInformation("Cache cleared");
        }

        /// <summary>
        /// 基于前缀结果过滤（渐进式优化）
        /// </summary>
        private List<SearchResult<T>> FilterResults(
            List<SearchResult<T>> prefixResults, 
            string query,
            Func<T, string> textExtractor)
        {
            var queryLower = query.ToLower();
            
            return prefixResults
                .Where(r =>
                {
                    var text = textExtractor(r.Item).ToLower();
                    return text.Contains(queryLower) || 
                           text.Split(' ', '/', '-', '_').Any(w => w.StartsWith(queryLower));
                })
                .ToList();
        }
    }
}
