// [Path]: Pulsar/Pulsar/Services/FuzzySearch/Strategies/LevenshteinStrategy.cs
using Pulsar.Services.Interfaces;
using System;

namespace Pulsar.Services.FuzzySearch.Strategies
{
    /// <summary>
    /// Levenshtein 编辑距离策略 - 容错拼写
    /// </summary>
    public class LevenshteinStrategy<T> : ISearchStrategy<T> where T : class
    {
        private readonly int _maxDistance;

        public string Name => "Levenshtein";
        public int Weight => 40;

        public LevenshteinStrategy(int maxDistance = 2)
        {
            _maxDistance = maxDistance;
        }

        public int Match(string query, T item, Func<T, string> textExtractor)
        {
            var text = textExtractor(item).ToLower();
            
            // 对整个文本计算编辑距离
            int distance = CalculateLevenshteinDistance(query, text);
            
            if (distance <= _maxDistance)
            {
                // 距离越小，分数越高
                return 40 - (distance * 10);
            }
            
            // 对每个单词计算编辑距离
            var words = text.Split(new[] { ' ', '/', '-', '_' }, StringSplitOptions.RemoveEmptyEntries);
            int minDistance = int.MaxValue;
            
            foreach (var word in words)
            {
                int wordDistance = CalculateLevenshteinDistance(query, word);
                if (wordDistance < minDistance)
                {
                    minDistance = wordDistance;
                }
            }
            
            if (minDistance <= _maxDistance)
            {
                return 35 - (minDistance * 10);
            }
            
            return 0;
        }

        /// <summary>
        /// 计算 Levenshtein 编辑距离
        /// </summary>
        private static int CalculateLevenshteinDistance(string source, string target)
        {
            if (string.IsNullOrEmpty(source))
                return target?.Length ?? 0;
            
            if (string.IsNullOrEmpty(target))
                return source.Length;
            
            int sourceLength = source.Length;
            int targetLength = target.Length;
            
            // 优化：如果长度差异过大，直接返回大值
            if (Math.Abs(sourceLength - targetLength) > 3)
                return Math.Abs(sourceLength - targetLength);
            
            var distance = new int[sourceLength + 1, targetLength + 1];
            
            // 初始化第一行和第一列
            for (int i = 0; i <= sourceLength; i++)
                distance[i, 0] = i;
            
            for (int j = 0; j <= targetLength; j++)
                distance[0, j] = j;
            
            // 动态规划计算编辑距离
            for (int i = 1; i <= sourceLength; i++)
            {
                for (int j = 1; j <= targetLength; j++)
                {
                    int cost = (source[i - 1] == target[j - 1]) ? 0 : 1;
                    
                    distance[i, j] = Math.Min(
                        Math.Min(
                            distance[i - 1, j] + 1,      // 删除
                            distance[i, j - 1] + 1),     // 插入
                        distance[i - 1, j - 1] + cost);  // 替换
                }
            }
            
            return distance[sourceLength, targetLength];
        }
    }
}
