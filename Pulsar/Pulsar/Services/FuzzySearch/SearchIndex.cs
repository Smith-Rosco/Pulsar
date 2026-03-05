// [Path]: Pulsar/Pulsar/Services/FuzzySearch/SearchIndex.cs
using System;
using System.Collections.Generic;
using System.Linq;

namespace Pulsar.Services.FuzzySearch
{
    /// <summary>
    /// 搜索索引 - 提供快速前缀和首字母缩写查询
    /// </summary>
    /// <typeparam name="T">索引项类型</typeparam>
    public class SearchIndex<T> where T : class
    {
        // 前缀索引: "set" -> [Settings, Setup, ...]
        private readonly Dictionary<string, List<T>> _prefixIndex = new();

        // 首字母缩写索引: "wc" -> [Window Close, ...]
        private readonly Dictionary<string, List<T>> _acronymIndex = new();

        // 分词索引: "close" -> [Window Close, ...]
        private readonly Dictionary<string, List<T>> _tokenIndex = new();

        /// <summary>
        /// 构建索引
        /// </summary>
        public void Build(IEnumerable<T> items, Func<T, string> textExtractor)
        {
            _prefixIndex.Clear();
            _acronymIndex.Clear();
            _tokenIndex.Clear();

            foreach (var item in items)
            {
                var text = textExtractor(item).ToLower();
                
                // 1. 构建前缀索引
                BuildPrefixIndex(text, item);

                // 2. 构建首字母缩写索引
                BuildAcronymIndex(text, item);

                // 3. 构建分词索引
                BuildTokenIndex(text, item);
            }
        }

        /// <summary>
        /// 前缀查询
        /// </summary>
        public List<T> SearchByPrefix(string prefix)
        {
            prefix = prefix.ToLower();
            return _prefixIndex.TryGetValue(prefix, out var results) 
                ? results 
                : new List<T>();
        }

        /// <summary>
        /// 首字母缩写查询
        /// </summary>
        public List<T> SearchByAcronym(string acronym)
        {
            acronym = acronym.ToLower();
            return _acronymIndex.TryGetValue(acronym, out var results) 
                ? results 
                : new List<T>();
        }

        /// <summary>
        /// 分词查询
        /// </summary>
        public List<T> SearchByToken(string token)
        {
            token = token.ToLower();
            return _tokenIndex.TryGetValue(token, out var results) 
                ? results 
                : new List<T>();
        }

        /// <summary>
        /// 清空索引
        /// </summary>
        public void Clear()
        {
            _prefixIndex.Clear();
            _acronymIndex.Clear();
            _tokenIndex.Clear();
        }

        // === 私有方法 ===

        private void BuildPrefixIndex(string text, T item)
        {
            // 为每个前缀建立索引（最多 20 个字符）
            int maxLength = Math.Min(text.Length, 20);
            for (int i = 1; i <= maxLength; i++)
            {
                var prefix = text.Substring(0, i);
                if (!_prefixIndex.ContainsKey(prefix))
                {
                    _prefixIndex[prefix] = new List<T>();
                }
                _prefixIndex[prefix].Add(item);
            }
        }

        private void BuildAcronymIndex(string text, T item)
        {
            // 提取首字母缩写
            // "Window Close" -> "wc"
            // "Add / Plus" -> "ap"
            var words = text.Split(new[] { ' ', '/', '-', '_' }, StringSplitOptions.RemoveEmptyEntries);
            if (words.Length <= 1) return;

            var acronym = string.Concat(words.Select(w => w[0]));
            
            // 为首字母缩写的每个前缀建立索引
            for (int i = 1; i <= acronym.Length; i++)
            {
                var prefix = acronym.Substring(0, i);
                if (!_acronymIndex.ContainsKey(prefix))
                {
                    _acronymIndex[prefix] = new List<T>();
                }
                _acronymIndex[prefix].Add(item);
            }
        }

        private void BuildTokenIndex(string text, T item)
        {
            // 分词索引
            var tokens = text.Split(new[] { ' ', '/', '-', '_' }, StringSplitOptions.RemoveEmptyEntries);
            
            foreach (var token in tokens)
            {
                var lowerToken = token.ToLower();
                
                // 为每个分词的前缀建立索引
                int maxLength = Math.Min(lowerToken.Length, 15);
                for (int i = 1; i <= maxLength; i++)
                {
                    var prefix = lowerToken.Substring(0, i);
                    if (!_tokenIndex.ContainsKey(prefix))
                    {
                        _tokenIndex[prefix] = new List<T>();
                    }
                    if (!_tokenIndex[prefix].Contains(item))
                    {
                        _tokenIndex[prefix].Add(item);
                    }
                }
            }
        }
    }
}
