using Discord;

namespace DustyBot.Framework.Communication
{
    public class Page
    {
        public string Content { get; set; } = string.Empty;
        public EmbedBuilder Embed { get; set; }
    }
}
