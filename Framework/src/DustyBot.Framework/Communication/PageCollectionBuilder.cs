using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Discord;
using DustyBot.Core.Formatting;

namespace DustyBot.Framework.Communication
{
    public class PageCollectionBuilder
    {
        private List<string> Lines { get; } = new List<string>();

        public PageCollectionBuilder()
        {
        }

        public PageCollectionBuilder(IEnumerable<string> lines)
        {
            Lines = lines.ToList();
        }

        public void AppendLine(string text) => Lines.Add(text);

        public PageCollection BuildEmbedCollection(Func<EmbedBuilder> embedFactory, int maxLinesPerPage = int.MaxValue)
        {
            var pages = new PageCollection();
            var description = new StringBuilder();
            var pageLines = 0;
            foreach (var line in Lines)
            {
                if (++pageLines > maxLinesPerPage || !description.TryAppendLineLimited(line, EmbedBuilder.MaxDescriptionLength))
                {
                    var embed = embedFactory().WithDescription(description.ToString());
                    pages.Add(embed);
                    description.Clear();
                    pageLines = 1;
                    description.Append(line.Truncate(EmbedBuilder.MaxDescriptionLength - 1) + '\n');
                }
            }

            if (description.Length > 0)
            {
                var embed = embedFactory().WithDescription(description.ToString());
                pages.Add(embed);
            }

            return pages;
        }
    }
}
