using System;
using System.Collections.Generic;
using System.Linq;

namespace DustyBot.Core.Collections
{
    public static class DictionaryExtensions
    {
        public static TValue GetOrCreate<TKey, TValue>(this IDictionary<TKey, TValue> dict, TKey key)
            where TValue : new()
        {
            TValue val;

            if (!dict.TryGetValue(key, out val))
            {
                val = new TValue();
                dict.Add(key, val);
            }

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
