using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Disqord;
using Disqord.Bot.Commands;
using Disqord.Gateway;
using Disqord.Rest;
using DustyBot.Core.Formatting;
using DustyBot.Framework.Commands.Attributes;
using DustyBot.Framework.Entities;
using DustyBot.Framework.Modules;
using Qmmands;
using Qmmands.Text;

namespace DustyBot.Service.Modules
{
    [Name("Info"), Description("Find various information about users and servers.")]
    public class InfoModule : DustyGuildModuleBase
    {
        public enum AvatarType
        {
            Global,
            Server
        }

        private const Markdown.TimestampFormat TimestampFormat = Markdown.TimestampFormat.LongDateTime;

        [TextCommand("avatar", "av"), Description("Shows a big version of a user's avatar.")]
        public IDiscordCommandResult ShowAvatar(
            [Description("use `global` to display the user's global profile avatar")]
            [Default(AvatarType.Server)]
            AvatarType? type,
            [Description("a user ID or a mention; shows your own avatar if not specified")]
            [Remainder]
            IMember? user)
        {
            user ??= GuildContext.Author;
            var avatar = type switch
            {
                AvatarType.Server => user.GetGuildAvatarUrl(CdnAssetFormat.Automatic, size: 2048),
                AvatarType.Global => user.GetAvatarUrl(CdnAssetFormat.Automatic, size: 2048),
                _ => throw new ArgumentOutOfRangeException()
            };

            var embed = new LocalEmbed()
                .WithTitle($"{user.Name}")
                .WithUrl(avatar)
                .WithImageUrl(avatar);

            return Result(embed);
        }

        [TextCommand("banner"), Description("Shows a big version of a user's banner.")]
        public async Task<IDiscordCommandResult> ShowBannerAsync(
            [Description("a user ID or a mention; shows your own banner if not specified")]
            [Remainder]
            IRestUser? user)
        {
            if (user == null)
                user = await Bot.FetchUserAsync(Context.Author.Id);

            var banner = user.GetBannerUrl(CdnAssetFormat.Automatic, size: 4096);
            if (string.IsNullOrEmpty(banner))
                return Result("User has no profile banner.");

            var embed = new LocalEmbed()
                .WithTitle($"{user.Name}")
                .WithUrl(banner)
                .WithImageUrl(banner);

            return Result(embed);
        }

        [TextCommand("user", "uinfo", "userinfo"), Description("Shows information about a server member.")]
        public IDiscordCommandResult ShowMemberInfo(
            [Description("a user ID or a mention; shows your own info if not specified")]
            [Remainder]
            IMember? user)
        {
            user ??= GuildContext.Author;
            var avatar = user.GetAvatarUrl(CdnAssetFormat.Automatic, size: 2048);
            var embed = new LocalEmbed()
                .WithTitle(user.Nick.FormatNonEmpty("{0} – ") + $"{user.Name}")
                .WithUrl(avatar)
                .WithThumbnailUrl(avatar)
                .WithFooter($"#{user.Id}");

            var now = DateTimeOffset.Now;
            if (user.JoinedAt.HasValue)
            {
                embed.AddField("Joined this server on", 
                    $"{Markdown.Timestamp(user.JoinedAt.Value, TimestampFormat)} _({Math.Floor((now - user.JoinedAt.Value).TotalDays)} days ago)_");
            }

            if (user.Id == Bot.GetGuild(GuildContext.GuildId)!.OwnerId)
                embed.WithDescription("Owner");
            else
                embed.WithDescription("Member");

            var roles = user.GetRoles().Where(x => x.Key != Bot.GetGuild(GuildContext.GuildId)!.GetEveryoneRoleId());
            var rolesBuilder = new StringBuilder();
            foreach (var item in roles.OrderByDescending(x => x.Value.Position).Select(x => Mention.Role(x.Value)))
            {
                if (!rolesBuilder.TryAppendLimited(item + " ", Discord.Limits.Message.Embed.Field.MaxValueLength))
                    break;
            }

            embed.AddField("Account created on", 
                $"{Markdown.Timestamp(user.CreatedAt(), TimestampFormat)} _({Math.Floor((now - user.CreatedAt()).TotalDays)} days ago)_");

            embed.AddField("Roles", rolesBuilder.Length > 0 ? rolesBuilder.ToString() : "None");

            return Result(embed);
        }

        [TextCommand("server", "sinfo", "serverinfo"), Description("Shows information about the server.")]
        public async Task<IDiscordCommandResult> ShowServerInfo()
        {
            var guild = Bot.GetGuild(GuildContext.GuildId)!;
            var iconUrl = guild.GetIconUrl(CdnAssetFormat.Automatic, size: 2048);
            var embed = new LocalEmbed()
                .WithTitle(guild.Name)
                .WithUrl(iconUrl)
                .WithThumbnailUrl(iconUrl)
                .WithFooter($"#{guild.Id}");

            var owner = await guild.GetOrFetchMemberAsync(guild.OwnerId, cancellationToken: Bot.StoppingToken);
            embed.AddField("Created on", 
                $"{Markdown.Timestamp(guild.CreatedAt(), TimestampFormat)} _({Math.Floor((DateTimeOffset.Now - guild.CreatedAt()).TotalDays)} days ago)_");

            embed.AddField("Owner", $"{owner.Name}");

            embed.AddField("Members", $"{guild.MemberCount}", true);
            embed.AddField("Channels", 
                $"{guild.GetChannels(ChannelType.Text).Count()} text, {guild.GetChannels(ChannelType.Voice).Count()} voice", 
                true);

            embed.AddField("Emotes", 
                $"{guild.Emojis.Where(x => !x.Value.IsAnimated).Count()} static, {guild.Emojis.Where(x => x.Value.IsAnimated).Count()} animated",
                true);

            return Result(embed);
        }

        [VerbCommand("server", "banner"), Description("Shows the server banner.")]
        public IDiscordCommandResult ShowServerBanner()
        {
            var banner = Bot.GetGuild(GuildContext.GuildId)!.GetBannerUrl(CdnAssetFormat.Automatic, size: 4096);
            if (string.IsNullOrEmpty(banner))
                return Result("Server has no banner.");

            var embed = new LocalEmbed()
                .WithTitle($"{Bot.GetGuild(GuildContext.GuildId)!.Name}")
                .WithUrl(banner)
                .WithImageUrl(banner);

            return Result(embed);
        }
    }
}
