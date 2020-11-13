using Discord;
using System.Collections.Generic;

namespace DustyBot.Framework.Communication
{
    public class PageCollection : List<Page>
    {
        public Page Last => Count < 1 ? null : this[Count - 1];
        public bool IsEmpty => Count <= 0;

        public void Add(string content) => Add(new Page() { Content = content });
        public void Add(EmbedBuilder embed) => Add(new Page() { Embed = embed });
    }
}
