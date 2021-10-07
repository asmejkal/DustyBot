using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using DustyBot.Database.Services;
using DustyBot.Definitions;
using DustyBot.Framework.Commands;
using DustyBot.Framework.Communication;
using DustyBot.Framework.Logging;
using DustyBot.Framework.Modules;
using DustyBot.Framework.Utility;
using DustyBot.Settings;
using Newtonsoft.Json.Linq;

namespace DustyBot.Modules
{
    [Module("Info", "Shows various info about users and servers.")]
    class InfoModule : Module
    {
        private ICommunicator Communicator { get; }
        private ISettingsService Settings { get; }
        private ILogger Logger { get; }
        private IUserFetcher UserFetcher { get; }

        public InfoModule(ICommunicator communicator, ISettingsService settings, ILogger logger, IUserFetcher userFetcher)
        {
            Communicator = communicator;
            Settings = settings;
            Logger = logger;
            UserFetcher = userFetcher;
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

            await command.Channel.SendMessageAsync(embed: embed.Build());
        }

        [Command("banner", "Shows a big version of a user's profile banner.")]
        [Parameter("User", ParameterType.GuildUser, ParameterFlags.Optional | ParameterFlags.Remainder, "the user; shows your banner if omitted")]
        public async Task Banner(ICommand command)
        {
            var user = command["User"].HasValue ? await command["User"].AsGuildUser : command.Author;
            var config = await Settings.ReadGlobal<BotConfig>();

            var request = WebRequest.CreateHttp($"{DiscordConfig.APIUrl}users/{user.Id}");
            request.Headers.Add("Authorization", $"Bot {config.BotToken}");

            using var response = await request.GetResponseAsync();
            using var reader = new StreamReader(response.GetResponseStream());

            var content = await reader.ReadToEndAsync();
            var json = JObject.Parse(content);

            var bannerId = (string)json["banner"];
            if (string.IsNullOrEmpty(bannerId))
            {
                await command.Reply(Communicator, "User has no profile banner.");
                return;
            }

            var extension = bannerId.StartsWith("a_") ? "gif" : "png";
            var url = $"{DiscordConfig.CDNUrl}banners/{user.Id}/{bannerId}.{extension}?size=4096";

            var embed = new EmbedBuilder()
                    .WithTitle($"{user.Username}#{user.Discriminator}")
                    .WithUrl(url)
                    .WithImageUrl(url);

            await command.Channel.SendMessageAsync(embed: embed.Build());
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

            await command.Channel.SendMessageAsync(embed: embed.Build());
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

            var owner = (IGuildUser)guild.Owner ?? await UserFetcher.FetchGuildUserAsync(guild.Id, guild.OwnerId);
            embed.AddField(x => x.WithName("Created on").WithValue($"{guild.CreatedAt.ToUniversalTime().ToString("f", GlobalDefinitions.Culture)} ({Math.Floor((DateTimeOffset.Now - guild.CreatedAt).TotalDays)} days ago)"));
            embed.AddField(x => x.WithName("Owner").WithValue($"{owner.Username}#{owner.Discriminator}"));

            embed.AddField(x => x.WithName("Members").WithValue($"{guild.MemberCount}").WithIsInline(true));
            embed.AddField(x => x.WithName("Channels").WithValue($"{guild.TextChannels.Count()} text, {guild.VoiceChannels.Count()} voice").WithIsInline(true));
            embed.AddField(x => x.WithName("Emotes").WithValue($"{guild.Emotes.Where(y => !y.Animated).Count()} static, {guild.Emotes.Where(y => y.Animated).Count()} animated").WithIsInline(true));

            await command.Channel.SendMessageAsync(embed: embed.Build());
        }
    }
}
