using System;
using System.Collections.Generic;
using System.Text;

namespace DustyBot.Framework.Utility
{
    public static class StringExtensions
    {
        public static IEnumerable<string> Chunkify(this string str, int maxChunkSize)
        {
            for (int i = 0; i < str.Length; i += maxChunkSize)
                yield return str.Substring(i, Math.Min(maxChunkSize, str.Length - i));
        }
    }
}
