using Discord.Rest;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace DustyBot.Framework.Utility
{
    public class UserFetcher : IUserFetcher
    {
        private DiscordRestClient RestClient { get; }

        public UserFetcher(DiscordRestClient restClient)
        {
            RestClient = restClient ?? throw new ArgumentNullException(nameof(restClient));
        }

        public Task<RestUser> FetchUserAsync(ulong id) => RestClient.GetUserAsync(id);

        public Task<RestGuildUser> FetchGuildUserAsync(ulong guildId, ulong id) => RestClient.GetGuildUserAsync(guildId, id);
    }
}
