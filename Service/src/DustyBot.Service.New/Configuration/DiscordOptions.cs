using System.Collections.Generic;
using System.Linq;

namespace DustyBot.Service.Configuration
{
    public class DiscordOptions
    {
        public string? Token { get; set; }
        public string? ShardsList { get; set; }
        public int? TotalShards { get; set; }

        public IReadOnlyCollection<int>? Shards => ShardsList?.Split(',').Select(y => int.Parse(y.Trim())).ToList();
    }
}
