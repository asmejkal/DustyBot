using Discord;
using System.Threading.Tasks;
using DustyBot.Framework.Modules;
using DustyBot.Framework.Commands;
using DustyBot.Framework.Communication;
using DustyBot.Framework.Logging;
using DustyBot.Definitions;
using System.Linq;
using System;
using Discord.WebSocket;
using DustyBot.Framework.Utility;
using DustyBot.Database.Services;

namespace DustyBot.Modules
{
    [Module("Info", "Shows various info about users and servers.")]
    class InfoModule : Module
    {
        public ICommunicator Communicator { get; }
        public ISettingsService Settings { get; }
        public ILogger Logger { get; }

        public InfoModule(ICommunicator communicator, ISettingsService settings, ILogger logger)
        {
            Communicator = communicator;
            Settings = settings;
            Logger = logger;
        }

        [Command("avatar", "Shows and links a big version of a user's avatar.")]
        [Alias("av")]
        [Parameter("User", ParameterType.GuildUser, ParameterFlags.Optional | ParameterFlags.Remainder, "the user; shows your avatar if omitted")]
        public async Task Avatar(ICommand command)
        {
            var user = command["User"].HasValue ? command["User"].AsGuildUser : command.Author;
            var avatar = user.GetAvatarUrl(size: 2048) ?? user.GetDefaultAvatarUrl();
            var embed = new EmbedBuilder()
                .WithTitle($"{user.Username}#{user.Discriminator}")
                .WithUrl(avatar)
                .WithImageUrl(avatar);

            await command.Channel.SendMessageAsync(embed: embed.Build());
        }

        [Command("user", "Shows information about a user.")]
        [Alias("uinfo"), Alias("userinfo")]
        [Parameter("User", ParameterType.GuildUser, ParameterFlags.Optional | ParameterFlags.Remainder, "the user; shows your info if omitted")]
        public async Task User(ICommand command)
        {
            var user = command["User"].HasValue ? command["User"].AsGuildUser : (IGuildUser)command.Author;
            var avatar = user.GetAvatarUrl(size: 2048) ?? user.GetDefaultAvatarUrl();
            var embed = new EmbedBuilder()
                .WithTitle((string.IsNullOrEmpty(user.Nickname) ? "" : $"{user.Nickname} – ") + $"{user.Username}#{user.Discriminator}")
                .WithUrl(avatar)
                .WithThumbnailUrl(avatar)
                .WithDescription(user.Status.ToString())
                .WithFooter($"#{user.Id} • times in UTC");

            if (user.JoinedAt.HasValue)
                embed.AddField(x => x.WithName("Joined the server on").WithValue($"{user.JoinedAt?.ToUniversalTime().ToString("f", GlobalDefinitions.Culture)} ({Math.Floor((DateTimeOffset.Now - user.JoinedAt.Value).TotalDays)} days ago)"));

            // User RoleIds aren't ordered properly, so we have to go through guild roles
            var roles = command.Guild.Roles.Where(x => x.Id != command.Guild.EveryoneRole.Id && user.RoleIds.Contains(x.Id))
                .OrderByDescending(x => x.Position)
                .Select(y => $"<@&{y.Id}>");

            embed.AddField(x => x.WithName("Account created on").WithValue($"{user.CreatedAt.ToUniversalTime().ToString("f", GlobalDefinitions.Culture)} ({Math.Floor((DateTimeOffset.Now - user.CreatedAt).TotalDays)} days ago)"));
            embed.AddField(x => x.WithName("Roles").WithValue(string.Join(" ", roles)));

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
            
            embed.AddField(x => x.WithName("Created on").WithValue($"{guild.CreatedAt.ToUniversalTime().ToString("f", GlobalDefinitions.Culture)} ({Math.Floor((DateTimeOffset.Now - guild.CreatedAt).TotalDays)} days ago)"));
            embed.AddField(x => x.WithName("Owner").WithValue($"{guild.Owner.Username}#{guild.Owner.Discriminator}"));

            embed.AddField(x => x.WithName("Members").WithValue($"{guild.MemberCount} ({guild.Users.Where(y => y.Status != UserStatus.Offline && y.Status != UserStatus.Invisible).Count()} online)").WithIsInline(true));
            embed.AddField(x => x.WithName("Channels").WithValue($"{guild.TextChannels.Count()} text, {guild.VoiceChannels.Count()} voice").WithIsInline(true));
            embed.AddField(x => x.WithName("Emotes").WithValue($"{guild.Emotes.Where(y => !y.Animated).Count()} static, {guild.Emotes.Where(y => y.Animated).Count()} animated").WithIsInline(true));

            await command.Channel.SendMessageAsync(embed: embed.Build());
        }
    }
}
