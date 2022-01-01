using System;

namespace DustyBot.Core.Collections
{
    public static class SpanExtensions
    {
        public static int IndexOf<TSource>(this ReadOnlySpan<TSource> source, Func<TSource, bool> predicate, int defaultIndex = -1)
        {
            for (var i = 0; i < source.Length; ++i)
            {
                if (predicate(source[i]))
                    return i;
            }

            return defaultIndex;
        }

        public static int LastIndexOf<TSource>(this ReadOnlySpan<TSource> source, Func<TSource, bool> predicate, int defaultIndex = -1)
        {
            for (var i = source.Length - 1; i >= 0; --i)
            {
                if (predicate(source[i]))
                    return i;
            }

            return defaultIndex;
        }
    }
}
