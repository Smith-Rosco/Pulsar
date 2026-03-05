// [Path]: Pulsar/Pulsar/Services/FuzzySearch/Strategies/TokenMatchStrategy.cs
using Pulsar.Services.Interfaces;
using System;
using System.Linq;

namespace Pulsar.Services.FuzzySearch.Strategies
{
    /// <summary>
    /// 分词匹配策略 - 多关键词匹配
    /// </summary>
    public class TokenMatchStrategy<T> : ISearchStrategy<T> where T : class
    {
        public string Name => "Token";
        public int Weight => 50;

        public int Match(string query, T item, Func<T, string> textExtractor)
        {
            var text = textExtractor(item).ToLower();
            
            // 分割查询词
            var queryTokens = query.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (queryTokens.Length <= 1)
                return 0; // 单词查询不使用此策略
            
            // 分割目标文本
            var textTokens = text.Split(new[] { ' ', '/', '-', '_' }, StringSplitOptions.RemoveEmptyEntries);
            
            // 计算匹配的 token 数量
            int matchedCount = 0;
            foreach (var queryToken in queryTokens)
            {
                if (textTokens.Any(t => t.Contains(queryToken)))
                {
                    matchedCount++;
                }
            }
            
            // 全部匹配
            if (matchedCount == queryTokens.Length)
                return 50;
            
            // 部分匹配
            if (matchedCount > 0)
            {
                return 30 + (matchedCount * 10 / queryTokens.Length);
            }
            
            return 0;
        }
    }
}
