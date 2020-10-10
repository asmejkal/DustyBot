using Discord;
using DustyBot.Core.Async;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace DustyBot.Framework.Utility
{
    public static class DiscordExtensions
    {
        public static void DeleteAfter(this IMessage msg, int seconds)
        {
            TaskHelper.FireForget(async () =>
            {
                await Task.Delay(seconds * 1000);
                await msg.DeleteAsync().ConfigureAwait(false);
            });
        }

        public static async Task<IMessage> GetMessageAsync(this IGuild guild, ulong id)
        {
            var user = await guild.GetCurrentUserAsync().ConfigureAwait(false);
            foreach (var c in await guild.GetChannelsAsync().ConfigureAwait(false))
            {
                if (c is ITextChannel textChannel)
                {
                    try
                    {
                        if (!user.GetPermissions(c).ReadMessageHistory)
                            continue;
                        
                        var message = await textChannel.GetMessageAsync(id).ConfigureAwait(false);
                        if (message != null)
                            return message;
                    }
                    catch (Discord.Net.HttpException ex) when (ex.DiscordCode == 50001)
                    {
                        //Missing access
                    }
                }
            }

            return null;
        }

        public static string GetFullName(this IEmote emote)
        {
            if (emote is Emote customEmote)
                return $"<:{customEmote.Name}:{customEmote.Id}>";
            else
                return emote.Name;
        }

        public static string GetLink(this IMessage message)
        {
            var channel = message.Channel as ITextChannel;
            if (channel == null)
                throw new InvalidOperationException("Message not in text channel.");

            return $"https://discordapp.com/channels/{channel.GuildId}/{channel.Id}/{message.Id}";
        }

        public static string GetAnimatedIconUrl(this IGuild guild) => 
            (guild.IconId?.StartsWith("a_") ?? false) ? System.IO.Path.ChangeExtension(guild.IconUrl, "gif") : null;

        public static string GetFullName(this IUser user) => $"{user.Username}#{user.Discriminator}";

        public static bool CanUserAssign(this IRole role, IGuildUser user)
        {
            var userMaxPosition = user.RoleIds.Select(x => role.Guild.GetRole(x)).Max(x => x?.Position ?? 0);
            return role.Position < userMaxPosition;
        }
    }
}
