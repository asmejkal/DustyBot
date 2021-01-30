using System.Linq;

namespace DustyBot.Service.Configuration
{
    public class DiscordOptions
    {
        public string BotToken { get; set; }
        public string ShardsList { get; set; }
        public int? TotalShards { get; set; }

        public int[] Shards => ShardsList?.Split(',').Select(y => int.Parse(y.Trim())).ToArray();
    }
}
