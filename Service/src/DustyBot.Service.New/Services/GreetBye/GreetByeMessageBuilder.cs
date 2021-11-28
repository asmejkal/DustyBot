using System;
using Disqord;
using Disqord.Gateway;
using DustyBot.Database.Mongo.Collections.GreetBye.Models;

namespace DustyBot.Service.Services.GreetBye
{
    internal class GreetByeMessageBuilder : IGreetByeMessageBuilder
    {
        public string BuildText(string template, IUser user, IGatewayGuild guild) =>
            ReplacePlaceholders(template, user, guild);

        public LocalEmbed BuildEmbed(GreetByeEmbed template, IUser user, IGatewayGuild guild)
        {
            var embed = new LocalEmbed()
                .WithTitle(ReplacePlaceholders(template.Title, user, guild))
                .WithDescription(ReplacePlaceholders(template.Body, user, guild))
                .WithThumbnailUrl(user is IMember member ? member.GetGuildAvatarUrl(size: 512) : user.GetAvatarUrl(size: 512));

            if (template.Color.HasValue)
                embed.WithColor(Math.Min((int)template.Color.Value, 0xfffffe)); // 0xfffffff is a special code for blank

            if (template.Image != default)
                embed.WithImageUrl(template.Image.AbsoluteUri);

            if (!string.IsNullOrEmpty(template.Footer))
                embed.WithFooter(ReplacePlaceholders(template.Footer, user, guild));

            return embed;
        }

        private static string ReplacePlaceholders(string template, IUser user, IGatewayGuild guild)
        {
            return template
                .Replace(GreetByeMessagePlaceholders.Mention, user.Mention)
                .Replace(GreetByeMessagePlaceholders.Name, user.Name)
                .Replace(GreetByeMessagePlaceholders.FullName, user.Name + "#" + user.Discriminator)
                .Replace(GreetByeMessagePlaceholders.Id, user.Id.ToString())
                .Replace(GreetByeMessagePlaceholders.Server, guild.Name)
                .Replace(GreetByeMessagePlaceholders.MemberCount, guild.MemberCount.ToString());
        }
    }
}
