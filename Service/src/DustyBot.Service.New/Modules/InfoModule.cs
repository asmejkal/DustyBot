using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Disqord;
using Disqord.Gateway;
using Disqord.Rest;
using DustyBot.Framework.Attributes;
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
            [Description("the user; shows your own avatar if not specified")]
            [Remainder]
            IMember? user)
        {
            user ??= Context.Author;
            var avatar = type switch
            {
                AvatarType.Server => user.GetGuildAvatarUrl(size: 2048),
                AvatarType.Global => user.GetAvatarUrl(size: 2048),
                _ => throw new ArgumentOutOfRangeException()
            };

            var embed = new LocalEmbed()
                .WithTitle($"{user.Name}#{user.Discriminator}")
                .WithUrl(avatar)
                .WithImageUrl(avatar);

            return Reply(embed);
        }

        [Command("banner"), Description("Shows a big version of a user's avatar.")]
        public async Task<CommandResult> ShowBannerAsync(
            [Description("the user; shows your own banner if not specified")]
            [Remainder]
            IRestUser? user)
        {
            if (user == null)
                user = await Bot.FetchUserAsync(Context.Author.Id);

            var banner = user.GetBannerUrl(size: 4096);
            if (string.IsNullOrEmpty(banner))
                return Reply("User has no profile banner.");

            var embed = new LocalEmbed()
                .WithTitle($"{user.Name}#{user.Discriminator}")
                .WithUrl(banner)
                .WithImageUrl(banner);

            return Reply(embed);
        }

        [Command("user", "uinfo", "userinfo"), Description("Shows information about a server member.")]
        public CommandResult ShowMemberInfo(
            [Description("the user; shows your own info if not specified")]
            [Remainder]
            IMember? user)
        {
            user ??= Context.Author;
            var avatar = user.GetAvatarUrl(size: 2048);
            var embed = new LocalEmbed()
                .WithTitle((string.IsNullOrEmpty(user.Name) ? "" : $"{user.Nick} – ") + $"{user.Name}#{user.Discriminator}")
                .WithUrl(avatar)
                .WithThumbnailUrl(avatar)
                .WithFooter($"#{user.Id}");

            var now = DateTimeOffset.Now;
            if (user.JoinedAt.HasValue)
            {
                embed.AddField("Joined this server on", 
                    $"{Markdown.Timestamp(user.JoinedAt.Value, TimestampFormat)} ({Math.Floor((now - user.JoinedAt.Value).TotalDays)} days ago)");
            }

            if (user.Id == Context.Guild.OwnerId)
                embed.WithDescription("Owner");
            else
                embed.WithDescription("Member");

            var roles = user.GetRoles().Where(x => x.Key != Context.Guild.GetEveryoneRoleId());
            var rolesBuilder = new StringBuilder();
            foreach (var item in roles.OrderByDescending(x => x.Value.Position).Select(x => Mention.Role(x.Value)))
            {
                if (rolesBuilder.Length + item.Length > LocalEmbedField.MaxFieldValueLength)
                    break;

                rolesBuilder.Append(item);
            }

            embed.AddField("Account created on", 
                $"{Markdown.Timestamp(user.CreatedAt(), TimestampFormat)} ({Math.Floor((now - user.CreatedAt()).TotalDays)} days ago)");

            embed.AddField("Roles", rolesBuilder.Length > 0 ? rolesBuilder.ToString() : "None");

            return Reply(embed);
        }

        [Command("server", "sinfo", "serverinfo"), Description("Shows information about the server.")]
        public async Task<CommandResult> ShowServerInfo()
        {
            var guild = Context.Guild;
            var embed = new LocalEmbed()
                .WithTitle(guild.Name)
                .WithUrl(guild.GetIconUrl())
                .WithThumbnailUrl(guild.GetIconUrl())
                .WithFooter($"#{guild.Id}");

            var owner = await guild.GetOrFetchMemberAsync(guild.OwnerId, cancellationToken: Bot.StoppingToken);
            embed.AddField("Created on", 
                $"{Markdown.Timestamp(guild.CreatedAt(), TimestampFormat)} ({Math.Floor((DateTimeOffset.Now - guild.CreatedAt()).TotalDays)} days ago)");

            embed.AddField("Owner", $"{owner.Name}#{owner.Discriminator}");

            embed.AddField("Members", $"{guild.MemberCount}", true);
            embed.AddField("Channels", 
                $"{guild.GetChannels(ChannelType.Text).Count()} text, {guild.GetChannels(ChannelType.Voice).Count()} voice", 
                true);

            embed.AddField("Emotes", 
                $"{guild.Emojis.Where(x => !x.Value.IsAnimated).Count()} static, {guild.Emojis.Where(x => x.Value.IsAnimated).Count()} animated",
                true);

            return Reply(embed);
        }
    }
}
