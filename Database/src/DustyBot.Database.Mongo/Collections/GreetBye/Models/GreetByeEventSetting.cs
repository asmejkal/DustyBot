using System;

namespace DustyBot.Database.Mongo.Collections.GreetBye.Models
{
    public class GreetByeEventSetting
    {
        public ulong ChannelId { get; set; }
        public string? Text { get; set; }
        public GreetByeEmbed? Embed { get; set; }

        public GreetByeEventSetting() 
        {
        }

        public GreetByeEventSetting(ulong channelId, string text)
        {
            if (string.IsNullOrEmpty(text))
                throw new ArgumentException(nameof(text));

            ChannelId = channelId;
            Text = text;
        }

        public GreetByeEventSetting(ulong channelId, GreetByeEmbed embed)
        {
            ChannelId = channelId;
            Embed = embed ?? throw new ArgumentNullException(nameof(embed));
        }
    }
}
