using System;
using System.Collections.Generic;
using System.Linq;
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

        /// <summary>
        /// Puts as many lines in a chunk as possible. If a line is longer than the chunk size, it gets split.
        /// </summary>
        /// <param name="text">text to chunkify</param>
        /// <param name="chunkSize">max chunk size</param>
        /// <returns>chunks</returns>
        public static IEnumerable<StringBuilder> ChunkifyByLines(this string text, int chunkSize)
        {
            var messages = new List<StringBuilder>();
            var lines = text.Split('\n');
            foreach (var line in lines)
            {
                if (line.Length > chunkSize)
                {
                    foreach (var chunk in line.Chunkify(chunkSize))
                        messages.Add(new StringBuilder(chunk));
                }
                else
                {
                    if (messages.Count <= 0)
                        messages.Add(new StringBuilder());

                    if (messages.Last().Length <= 0)
                        messages.Last().Append(line);
                    else if (messages.Last().Length + line.Length <= chunkSize)
                        messages.Last().Append("\n" + line);
                    else
                        messages.Add(new StringBuilder(line));
                }
            }

            return messages;
        }
    }
}
