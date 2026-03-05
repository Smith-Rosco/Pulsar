// [Path]: Pulsar/Pulsar/Services/FuzzySearch/Strategies/PrefixMatchStrategy.cs
using Pulsar.Services.Interfaces;
using System;
using System.Linq;

namespace Pulsar.Services.FuzzySearch.Strategies
{
    /// <summary>
    /// 前缀匹配策略 - 匹配单词开头
    /// </summary>
    public class PrefixMatchStrategy<T> : ISearchStrategy<T> where T : class
    {
        public string Name => "Prefix";
        public int Weight => 80;

        public int Match(string query, T item, Func<T, string> textExtractor)
        {
            var text = textExtractor(item).ToLower();
            
            // 整体前缀匹配
            if (text.StartsWith(query))
                return 80;
            
            // 单词前缀匹配 (例如: "clo" 匹配 "Window Close")
            var words = text.Split(new[] { ' ', '/', '-', '_' }, StringSplitOptions.RemoveEmptyEntries);
            if (words.Any(w => w.StartsWith(query)))
                return 75;
            
            return 0;
        }
    }
}
