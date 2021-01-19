using Discord;
using System.Threading.Tasks;
using DustyBot.Framework.Commands;
using DustyBot.Definitions;
using System.Linq;
using System;
using Discord.WebSocket;
using DustyBot.Framework.Utility;
using System.Text;
using DustyBot.Framework.Modules.Attributes;

namespace DustyBot.Modules
{
    [Module("Info", "Find various info about users and servers.")]
    internal sealed class InfoModule
    {
        private readonly IUserFetcher _userFetcher;

        public InfoModule(IUserFetcher userFetcher)
        {
            _userFetcher = userFetcher;
        }

        [Command("avatar", "Shows a big version of a user's avatar.")]
        [Alias("av")]
        [Parameter("User", ParameterType.GuildUser, ParameterFlags.Optional | ParameterFlags.Remainder, "the user; shows your avatar if omitted")]
        public async Task Avatar(ICommand command)
        {
            var user = command["User"].HasValue ? await command["User"].AsGuildUser : command.Author;
            var avatar = user.GetAvatarUrl(size: 2048) ?? user.GetDefaultAvatarUrl();
            var embed = new EmbedBuilder()
                .WithTitle($"{user.Username}#{user.Discriminator}")
                .WithUrl(avatar)
                .WithImageUrl(avatar);

            await command.Reply(embed.Build());
        }

        [Command("user", "Shows information about a user.")]
        [Alias("uinfo"), Alias("userinfo")]
        [Parameter("User", ParameterType.GuildUser, ParameterFlags.Optional | ParameterFlags.Remainder, "the user; shows your info if omitted")]
        public async Task User(ICommand command)
        {
            var user = command["User"].HasValue ? await command["User"].AsGuildUser : (IGuildUser)command.Author;
            var avatar = user.GetAvatarUrl(size: 2048) ?? user.GetDefaultAvatarUrl();
            var embed = new EmbedBuilder()
                .WithTitle((string.IsNullOrEmpty(user.Nickname) ? "" : $"{user.Nickname} – ") + $"{user.Username}#{user.Discriminator}")
                .WithUrl(avatar)
                .WithThumbnailUrl(avatar)
                .WithFooter($"#{user.Id} • times in UTC");

            if (user.JoinedAt.HasValue)
                embed.AddField(x => x.WithName("Joined this server on").WithValue($"{user.JoinedAt?.ToUniversalTime().ToString("f", GlobalDefinitions.Culture)} ({Math.Floor((DateTimeOffset.Now - user.JoinedAt.Value).TotalDays)} days ago)"));

            var roles = command.Guild.Roles.Where(x => x.Id != command.Guild.EveryoneRole.Id && user.RoleIds.Contains(x.Id)).ToList();

            if (user.Id == command.Guild.OwnerId)
                embed.WithDescription("Owner");
            else
                embed.WithDescription("Member");

            var rolesBuilder = new StringBuilder();
            foreach (var item in roles.OrderByDescending(x => x.Position).Select(y => $"<@&{y.Id}> "))
            {
                if (rolesBuilder.Length + item.Length > DiscordHelpers.MaxEmbedFieldLength)
                    break;

                rolesBuilder.Append(item);
            }

            embed.AddField(x => x.WithName("Account created on").WithValue($"{user.CreatedAt.ToUniversalTime().ToString("f", GlobalDefinitions.Culture)} ({Math.Floor((DateTimeOffset.Now - user.CreatedAt).TotalDays)} days ago)"));
            embed.AddField(x => x.WithName("Roles").WithValue(rolesBuilder.Length > 0 ? rolesBuilder.ToString() : "None"));

            await command.Reply(embed.Build());
        }

        [Command("server", "Shows information about the server.")]
        [Alias("sinfo"), Alias("serverinfo")]
        public async Task Server(ICommand command)
        {
            var guild = (SocketGuild)command.Guild;
            var embed = new EmbedBuilder()
                .WithTitle(guild.Name)
                .WithUrl(guild.GetAnimatedIconUrl() ?? guild.IconUrl)
                .WithThumbnailUrl(guild.GetAnimatedIconUrl() ?? guild.IconUrl)
                .WithFooter($"#{guild.Id} • times in UTC");

            var owner = (IGuildUser)guild.Owner ?? await _userFetcher.FetchGuildUserAsync(guild.Id, guild.OwnerId);
            embed.AddField(x => x.WithName("Created on").WithValue($"{guild.CreatedAt.ToUniversalTime().ToString("f", GlobalDefinitions.Culture)} ({Math.Floor((DateTimeOffset.Now - guild.CreatedAt).TotalDays)} days ago)"));
            embed.AddField(x => x.WithName("Owner").WithValue($"{owner.Username}#{owner.Discriminator}"));

            embed.AddField(x => x.WithName("Members").WithValue($"{guild.MemberCount}").WithIsInline(true));
            embed.AddField(x => x.WithName("Channels").WithValue($"{guild.TextChannels.Count()} text, {guild.VoiceChannels.Count()} voice").WithIsInline(true));
            embed.AddField(x => x.WithName("Emotes").WithValue($"{guild.Emotes.Where(y => !y.Animated).Count()} static, {guild.Emotes.Where(y => y.Animated).Count()} animated").WithIsInline(true));

            await command.Reply(embed.Build());
        }
    }
}
