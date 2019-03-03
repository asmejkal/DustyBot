using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DustyBot.Framework.Utility
{
    public static class CollectionExtensions
    {
        public static void ForEach<T>(this IEnumerable<T> source, Action<T> action)
        {
            foreach (T item in source)
                action(item);
        }

        public static async Task ForEachAsync<T>(this IEnumerable<T> source, Func<T, Task> action)
        {
            foreach (T item in source)
                await action(item);
        }

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

        public static void RemoveAll<TKey, TValue>(this IDictionary<TKey, TValue> dic, Func<TKey, TValue, bool> predicate)
        {
            var keys = dic.Keys.Where(k => predicate(k, dic[k])).ToList();
            foreach (var key in keys)
            {
                dic.Remove(key);
            }
        }

        public static string WordJoin<T>(this IEnumerable<T> values, string separator = ", ", string lastSeparator = " and ")
        {
            var it = values.GetEnumerator();
            bool isFirst = true;
            bool hasRemainingItems;
            T item = default(T);
            string result = null;

            do
            {
                hasRemainingItems = it.MoveNext();
                if (hasRemainingItems)
                {
                    if (!isFirst)
                    {
                        if (result == null)
                            result = item.ToString();
                        else
                            result += separator + item.ToString();
                    }

                    item = it.Current;
                    isFirst = false;
                }
            } while (hasRemainingItems);

            if (result == null)
                result = item != null ? item.ToString() : string.Empty;
            else
                result += lastSeparator + item.ToString();

            return result;
        }

        public static string WordJoinQuoted<T>(this IEnumerable<T> values, string quote = "`", string separator = ", ", string lastSeparator = " and ")
        {
            var result = new StringBuilder(values.WordJoin(quote + separator + quote, quote + lastSeparator + quote));
            return result.Length > 0 ? (quote + result + quote) : string.Empty;
        }

        public static IEnumerable<T> SkipLast<T>(this IEnumerable<T> source, int n = 1)
        {
            var it = source.GetEnumerator();
            bool hasRemainingItems = false;
            var cache = new Queue<T>(n + 1);

            do
            {
                if (hasRemainingItems = it.MoveNext())
                {
                    cache.Enqueue(it.Current);
                    if (cache.Count > n)
                        yield return cache.Dequeue();
                }
            } while (hasRemainingItems);
        }
    }
}
