// [Path]: Pulsar/Pulsar/Services/FuzzySearch/Strategies/ExactMatchStrategy.cs
using Pulsar.Services.Interfaces;
using System;

namespace Pulsar.Services.FuzzySearch.Strategies
{
    /// <summary>
    /// 精确匹配策略 - 完全匹配或子串匹配
    /// </summary>
    public class ExactMatchStrategy<T> : ISearchStrategy<T> where T : class
    {
        public string Name => "Exact";
        public int Weight => 100;

        public int Match(string query, T item, Func<T, string> textExtractor)
        {
            var text = textExtractor(item).ToLower();
            
            // 完全匹配
            if (text == query)
                return 100;
            
            // 子串匹配
            if (text.Contains(query))
                return 90;
            
            return 0;
        }
    }
}
