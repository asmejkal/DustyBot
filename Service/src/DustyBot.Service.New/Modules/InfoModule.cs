using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Disqord;
using Disqord.Gateway;
using Disqord.Rest;
using DustyBot.Core.Formatting;
using DustyBot.Framework.Commands.Attributes;
using DustyBot.Framework.Entities;
using DustyBot.Framework.Modules;
using Qmmands;

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

        [Command("avatar", "av"), Description("Shows a big version of a user's avatar.")]
        public CommandResult ShowAvatar(
            [Description("use `global` to display the user's global profile avatar")]
            [Default(AvatarType.Server)]
            AvatarType? type,
            [Description("a user ID or a mention; shows your own avatar if not specified")]
            [Remainder]
            IMember? user)
        {
            user ??= Context.Author;
            var avatar = type switch
            {
                AvatarType.Server => user.GetGuildAvatarUrl(CdnAssetFormat.Automatic, size: 2048),
                AvatarType.Global => user.GetAvatarUrl(CdnAssetFormat.Automatic, size: 2048),
                _ => throw new ArgumentOutOfRangeException()
            };

            var embed = new LocalEmbed()
                .WithTitle($"{user.Name}#{user.Discriminator}")
                .WithUrl(avatar)
                .WithImageUrl(avatar);

            return Result(embed);
        }

        [Command("banner"), Description("Shows a big version of a user's avatar.")]
        public async Task<CommandResult> ShowBannerAsync(
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
                .WithTitle($"{user.Name}#{user.Discriminator}")
                .WithUrl(banner)
                .WithImageUrl(banner);

            return Result(embed);
        }

        [Command("user", "uinfo", "userinfo"), Description("Shows information about a server member.")]
        public CommandResult ShowMemberInfo(
            [Description("a user ID or a mention; shows your own info if not specified")]
            [Remainder]
            IMember? user)
        {
            user ??= Context.Author;
            var avatar = user.GetAvatarUrl(CdnAssetFormat.Automatic, size: 2048);
            var embed = new LocalEmbed()
                .WithTitle(user.Nick.FormatNonEmpty("{0} – ") + $"{user.Name}#{user.Discriminator}")
                .WithUrl(avatar)
                .WithThumbnailUrl(avatar)
                .WithFooter($"#{user.Id}");

            var now = DateTimeOffset.Now;
            if (user.JoinedAt.HasValue)
            {
                embed.AddField("Joined this server on", 
                    $"{Markdown.Timestamp(user.JoinedAt.Value, TimestampFormat)} _({Math.Floor((now - user.JoinedAt.Value).TotalDays)} days ago)_");
            }

            if (user.Id == Context.Guild.OwnerId)
                embed.WithDescription("Owner");
            else
                embed.WithDescription("Member");

            var roles = user.GetRoles().Where(x => x.Key != Context.Guild.GetEveryoneRoleId());
            var rolesBuilder = new StringBuilder();
            foreach (var item in roles.OrderByDescending(x => x.Value.Position).Select(x => Mention.Role(x.Value)))
            {
                if (!rolesBuilder.TryAppendLimited(item + " ", LocalEmbedField.MaxFieldValueLength))
                    break;
            }

            embed.AddField("Account created on", 
                $"{Markdown.Timestamp(user.CreatedAt(), TimestampFormat)} _({Math.Floor((now - user.CreatedAt()).TotalDays)} days ago)_");

            embed.AddField("Roles", rolesBuilder.Length > 0 ? rolesBuilder.ToString() : "None");

            return Result(embed);
        }

        [Command("server", "sinfo", "serverinfo"), Description("Shows information about the server.")]
        public async Task<CommandResult> ShowServerInfo()
        {
            var guild = Context.Guild;
            var embed = new LocalEmbed()
                .WithTitle(guild.Name)
                .WithUrl(guild.GetIconUrl(CdnAssetFormat.Automatic, size: 2048))
                .WithThumbnailUrl(guild.GetIconUrl())
                .WithFooter($"#{guild.Id}");

            var owner = await guild.GetOrFetchMemberAsync(guild.OwnerId, cancellationToken: Bot.StoppingToken);
            embed.AddField("Created on", 
                $"{Markdown.Timestamp(guild.CreatedAt(), TimestampFormat)} _({Math.Floor((DateTimeOffset.Now - guild.CreatedAt()).TotalDays)} days ago)_");

            embed.AddField("Owner", $"{owner.Name}#{owner.Discriminator}");

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
        public CommandResult ShowServerBanner()
        {
            var banner = Context.Guild.GetBannerUrl(CdnAssetFormat.Automatic, size: 4096);
            if (string.IsNullOrEmpty(banner))
                return Result("Server has no banner.");

            var embed = new LocalEmbed()
                .WithTitle($"{Context.Guild.Name}")
                .WithUrl(banner)
                .WithImageUrl(banner);

            return Result(embed);
        }
    }
}
