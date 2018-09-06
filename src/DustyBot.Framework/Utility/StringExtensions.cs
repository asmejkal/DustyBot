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
                    else if (messages.Last().Length + line.Length < chunkSize)
                        messages.Last().Append("\n" + line);
                    else
                        messages.Add(new StringBuilder(line));
                }
            }

            return messages;
        }
        
        public struct Token
        {
            public int Begin { get; set; }
            public int End { get; set; }
            public string Value { get; set; }
        }

        public static IEnumerable<Token> Tokenize(this string value, params char[] textQualifiers)
        {
            if (string.IsNullOrEmpty(value))
                yield break;

            char prevChar = '\0', nextChar = '\0', currentChar = '\0';
            bool inString = false;
            StringBuilder token = new StringBuilder();
            int begin = -1;
            for (int i = 0; i < value.Length; i++)
            {
                currentChar = value[i];
                prevChar = i > 0 ? prevChar = value[i - 1] : '\0';
                nextChar = i + 1 < value.Length ? value[i + 1] : '\0';

                if (textQualifiers.Contains(currentChar) && (prevChar == '\0' || char.IsWhiteSpace(prevChar)) && !inString)
                {
                    inString = true;
                    if (begin < 0)
                        begin = i;

                    continue;
                }

                if (textQualifiers.Contains(currentChar) && (nextChar == '\0' || char.IsWhiteSpace(nextChar)) && inString && prevChar != '\\')
                {
                    inString = false;
                    continue;
                }

                if (char.IsWhiteSpace(currentChar) && !inString)
                {
                    if (token.Length > 0)
                    {
                        yield return new Token { Begin = begin, End = i, Value = token.ToString() };
                        token = token.Remove(0, token.Length);
                    }

                    begin = -1;
                    continue;
                }

                if (begin < 0)
                    begin = i;

                token = token.Append(currentChar);
            }

            if (token.Length > 0)
                yield return new Token { Begin = begin, End = value.Length, Value = token.ToString() };
        }
    }
}
