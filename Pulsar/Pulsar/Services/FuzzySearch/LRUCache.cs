// [Path]: Pulsar/Pulsar/Services/FuzzySearch/LRUCache.cs
using System.Collections.Generic;

namespace Pulsar.Services.FuzzySearch
{
    /// <summary>
    /// LRU (Least Recently Used) 缓存实现
    /// </summary>
    public class LRUCache<TKey, TValue> where TKey : notnull
    {
        private readonly int _capacity;
        private readonly Dictionary<TKey, LinkedListNode<CacheItem>> _cache;
        private readonly LinkedList<CacheItem> _lruList;

        public LRUCache(int capacity)
        {
            _capacity = capacity;
            _cache = new Dictionary<TKey, LinkedListNode<CacheItem>>(capacity);
            _lruList = new LinkedList<CacheItem>();
        }

        public bool TryGet(TKey key, out TValue? value)
        {
            if (_cache.TryGetValue(key, out var node))
            {
                // 移动到链表头部（最近使用）
                _lruList.Remove(node);
                _lruList.AddFirst(node);
                
                value = node.Value.Value;
                return true;
            }

            value = default;
            return false;
        }

        public void Set(TKey key, TValue value)
        {
            if (_cache.TryGetValue(key, out var existingNode))
            {
                // 更新现有项
                _lruList.Remove(existingNode);
                existingNode.Value.Value = value;
                _lruList.AddFirst(existingNode);
            }
            else
            {
                // 添加新项
                if (_cache.Count >= _capacity)
                {
                    // 移除最久未使用的项
                    var lruNode = _lruList.Last;
                    if (lruNode != null)
                    {
                        _lruList.RemoveLast();
                        _cache.Remove(lruNode.Value.Key);
                    }
                }

                var newItem = new CacheItem { Key = key, Value = value };
                var newNode = new LinkedListNode<CacheItem>(newItem);
                _lruList.AddFirst(newNode);
                _cache[key] = newNode;
            }
        }

        public void Clear()
        {
            _cache.Clear();
            _lruList.Clear();
        }

        public int Count => _cache.Count;

        private class CacheItem
        {
            public TKey Key { get; set; } = default!;
            public TValue Value { get; set; } = default!;
        }
    }
}
