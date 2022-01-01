using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DustyBot.Core.Collections
{
    public static class DictionaryExtensions
    {
        public static TValue GetOrAdd<TKey, TValue>(this IDictionary<TKey, TValue> dict, TKey key, Func<TValue> factory)
        {
            if (!dict.TryGetValue(key, out var val))
                dict.Add(key, val = factory());

            return val;
        }

        public static TValue GetOrAdd<TKey, TValue>(this IDictionary<TKey, TValue> dict, TKey key)
            where TValue : new()
        {
            if (!dict.TryGetValue(key, out var val))
                dict.Add(key, val = new());

            return val;
        }

        public static async ValueTask<TValue> GetOrAddAsync<TKey, TValue>(this IDictionary<TKey, TValue> dict, TKey key, Func<ValueTask<TValue>> factory)
        {
            if (!dict.TryGetValue(key, out var val))
                dict.Add(key, val = await factory());

            return val;
        }

        public static ICollection<KeyValuePair<TKey, TValue>> RemoveAll<TKey, TValue>(this IDictionary<TKey, TValue> dic, Func<TKey, TValue, bool> predicate)
        {
            var items = dic.Where(x => predicate(x.Key, x.Value)).ToList();
            foreach (var item in items)
            {
                dic.Remove(item.Key);
            }

            return items;
        }
    }
}
