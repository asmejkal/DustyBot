using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using DustyBot.Database.Services;
using DustyBot.Framework.Commands;
using DustyBot.Framework.Communication;
using DustyBot.Framework.Logging;
using DustyBot.Framework.Modules;
using DustyBot.Framework.Utility;
using DustyBot.Helpers;

namespace DustyBot.Modules
{
    [Module("Mod", "Helps with server administration.")]
    class AdministrationModule : Module
    {
        private ICommunicator Communicator { get; }
        private ISettingsService Settings { get; }
        private ILogger Logger { get; }
        private BaseSocketClient Client { get; }
        private IUserFetcher UserFetcher { get; }

        public AdministrationModule(ICommunicator communicator, ISettingsService settings, ILogger logger, BaseSocketClient client, IUserFetcher userFetcher)
        {
            Communicator = communicator;
            Settings = settings;
            Logger = logger;
            Client = client;
            UserFetcher = userFetcher;
        }

        [Command("administration", "help", "Shows help for this module.", CommandFlags.Hidden)]
        [Alias("admin", "help")]
        [IgnoreParameters]
        public async Task Help(ICommand command)
        {
            await command.Channel.SendMessageAsync(embed: HelpBuilder.GetModuleHelpEmbed(this, command.Prefix));
        }

        [Command("say", "Sends a specified message.")]
        [Permissions(GuildPermission.ManageMessages)]
        [Parameter("TargetChannel", ParameterType.TextChannel, "a channel that will receive the message")]
        [Parameter("Message", ParameterType.String, ParameterFlags.Remainder | ParameterFlags.Optional, "the message to be sent (you may also include one attachment)")]
        public async Task Say(ICommand command)
        {
            var message = command["Message"].AsString ?? "";
            var channel = command["TargetChannel"].AsTextChannel;

            // This is a mods-only command, but to prevent permission creep, check
            // if there's any non-mentionable role and if the sender has a mention everyone perm
            var nonMentionableRoles = command.Message.MentionedRoleIds.Where(x => !command.Guild.GetRole(x)?.IsMentionable ?? false).ToList();
            var replaceRoleMentions = (message.ContainsEveryonePings() || nonMentionableRoles.Any()) && 
                !((IGuildUser)command.Author).GetPermissions(channel).MentionEveryone;

            if (replaceRoleMentions)
            {
                message = DiscordHelpers.ReplaceRoleMentions(message, nonMentionableRoles, command.Guild)
                    .Sanitise(allowRoleMentions: true);
            }

            if (command.Message.Attachments.Count <= 0)
            {
                if (string.IsNullOrEmpty(command["Message"]))
                    throw new Framework.Exceptions.IncorrectParametersCommandException("Specify a message or an attachment.");

                await channel.SendMessageAsync(message);
            }
            else
            {
                var attachment = command.Message.Attachments.First();
                var request = WebRequest.CreateHttp(attachment.Url);
                using (var response = await request.GetResponseAsync())
                using (var stream = response.GetResponseStream())
                using (var memStream = new MemoryStream())
                {
                    await stream.CopyToAsync(memStream);
                    memStream.Position = 0;

                    await channel.SendFileAsync(memStream, attachment.Filename, message);
                }
            }

            if (command["TargetChannel"].AsTextChannel.Id != command.Message.Channel.Id)
                await command.ReplySuccess(Communicator, "Message sent." + (replaceRoleMentions ? " To mention roles, @here, or @everyone you must have the Mention Everyone permission." : ""));
        }

        [Command("edit", "Edits a message sent by the say command.")]
        [Permissions(GuildPermission.ManageMessages)]
        [Parameter("MessageId", ParameterType.GuildSelfMessage, "a message previously sent by the `say` command")]
        [Parameter("Message", ParameterType.String, ParameterFlags.Remainder, "the message to send")]
        public async Task Edit(ICommand command)
        {
            var message = await command[0].AsGuildSelfMessage;
            await message.ModifyAsync(x => x.Content = command["Message"].AsString);
            await command.ReplySuccess(Communicator, "Message edited.");
        }

        [Command("mute", "Mutes a server member.")]
        [Permissions(GuildPermission.ManageRoles), BotPermissions(GuildPermission.ManageRoles)]
        [Parameter("User", ParameterType.GuildUser, "a server member to be muted (mention or ID)")]
        [Parameter("Reason", ParameterType.String, ParameterFlags.Remainder | ParameterFlags.Optional, "reason")]
        public async Task Mute(ICommand command)
        {
            var user = await command["User"].AsGuildUser;
            var permissionFails = await AdministrationHelpers.Mute(user, command["Reason"], Settings);
            var reply = $"User **{user.Username}#{user.DiscriminatorValue}** has been muted.";
            var fails = permissionFails.Count();
            if (fails > 0)
                reply += $"\nℹ Couldn't mute in {fails} channel{(fails > 1 ? "s" : "")} because the bot doesn't have permission to access {(fails > 1 ? "them" : "it")}.";
            
            await command.ReplySuccess(Communicator, reply);
        }

        [Command("unmute", "Unmutes a server member.")]
        [Permissions(GuildPermission.ManageRoles), BotPermissions(GuildPermission.ManageRoles)]
        [Parameter("User", ParameterType.GuildUser, "a server member to be unmuted (mention or ID)")]
        public async Task Unmute(ICommand command)
        {
            var user = await command["User"].AsGuildUser;
            await AdministrationHelpers.Unmute(user); 
            await command.ReplySuccess(Communicator, $"User **{user.Username}#{user.DiscriminatorValue}** has been unmuted.");
        }

        [Command("ban", "Bans one or more users.")]
        [Permissions(GuildPermission.BanMembers), BotPermissions(GuildPermission.BanMembers)]
        [Parameter("Reason", ParameterType.String, ParameterFlags.Optional, "reason for the ban")]
        [Parameter("DeleteDays", ParameterType.UInt, ParameterFlags.Optional, "number of days of messages to delete (max 7)")]
        [Parameter("Users", ParameterType.MentionOrId, ParameterFlags.Repeatable, "up to 10 user mentions or IDs")]
        [Example("raiders 318911554194243585 318903497502228482")]
        [Example("troll 7 @Troll")]
        [Example("\"picture spam\" @Spammer")]
        public async Task Ban(ICommand command)
        {
            if (command["Users"].Repeats.Count > 10)
                throw new Framework.Exceptions.IncorrectParametersCommandException("The maximum number of bans per command is 10.", false);

            var banningUser = (IGuildUser)command.Author;
            var banningUserMaxRole = banningUser.GetHighestRolePosition();
            var result = new StringBuilder();
            var bans = new Dictionary<ulong, (Task Task, string User)>();
            foreach (var id in command["Users"].Repeats.Select(x => x.AsMentionOrId.Value))
            {
                string userName;
                var guildUser = await command.Guild.GetUserAsync(id) ?? await UserFetcher.FetchGuildUserAsync(command.GuildId, id);
                if (guildUser != null)
                {
                    userName = $"{guildUser.GetFullName()} ({guildUser.Id})";
                    if (!banningUser.IsOwner() && (banningUserMaxRole <= guildUser.GetHighestRolePosition() || guildUser.IsOwner()))
                    {
                        result.AppendLine($"{Communicator.FailureMarker} You don't have permission to ban user `{userName}` on this server.");
                        continue;
                    }
                }
                else
                {
                    var user = (IUser)Client.GetUser(id) ?? await UserFetcher.FetchUserAsync(id);
                    userName = user != null ? $"{user.GetFullName()} ({user.Id})" : id.ToString();
                }

                bans[id] = (command.Guild.AddBanAsync(id, Math.Min(command["DeleteDays"].AsInt ?? 0, 7), command["Reason"].HasValue ? command["Reason"].AsString : null), userName);
            }

            try
            {
                await Task.WhenAll(bans.Select(x => x.Value.Task));
            }
            catch (Exception)
            {
            }

            foreach (var ban in bans.Values)
            {
                if (ban.Task.Exception != null && ban.Task.Exception.InnerException is Discord.Net.HttpException ex && ex.HttpCode == HttpStatusCode.Forbidden)
                {
                    result.AppendLine($"{Communicator.FailureMarker} Missing permissions to ban user `{ban.User}`.");
                }
                else if (ban.Task.Exception != null)
                {
                    await Logger.Log(new LogMessage(LogSeverity.Error, "Admin", $"Failed to ban user {ban.User}, ex: {ban.Task.Exception}"));
                    result.AppendLine($"{Communicator.FailureMarker} Failed to ban user `{ban.User}`.");
                }
                else
                {
                    result.AppendLine($"{Communicator.SuccessMarker} User `{ban.User}` has been banned.");
                }
            }

            await command.Reply(Communicator, result.ToString());
        }

        [Command("roles", "Lists all roles on the server with their IDs.")]
        public async Task Roles(ICommand command)
        {
            var result = new StringBuilder();
            foreach (var role in command.Guild.Roles.OrderByDescending(x => x.Position))
                result.AppendLine($"Name: `{role.Name}` Id: `{role.Id}`");

            await command.Reply(Communicator, result.ToString());
        }

        [Command("moddm", "Send an anonymous direct message from a moderator to a server member.", CommandFlags.Hidden)]
        [Permissions(GuildPermission.Administrator)]
        [Parameter("User", ParameterType.GuildUser, "the user to be messaged")]
        [Parameter("Message", ParameterType.String, ParameterFlags.Remainder, "content of the direct message, see below for how the whole message will look like")]
        [Comment("● Only server administrators can use this command, and only server members can be messaged. \n● This command won't work if the user has disabled private messages in their privacy settings. \n● For security reasons, this command cannot be used on servers below 100 members.\n\n**The direct message will have the following format:**\n:envelope: Message from a moderator of `MAMAMOO` (`167744403455082496`):\n\nYou have been muted for breaking the rule 3.")]
        [Example("@User You have been muted for breaking the rule 3.")]
        public async Task ModDm(ICommand command)
        {
            const int userThreshold = 100;
            if (((SocketGuild)command.Guild).MemberCount < userThreshold)
            {
                await command.ReplyError(Communicator, $"For security reasons, this command cannot be used on servers below {userThreshold} members.");
                return;
            }

            var user = await command["User"].AsGuildUser;
            if (user.IsBot)
            {
                await command.ReplyError(Communicator, $"This is a bot.");
                return;
            }

            var channel = await user.GetOrCreateDMChannelAsync();

            try
            {
                await channel.SendMessageAsync($":envelope: Message from a moderator of `{command.Guild.Name}` (`{command.GuildId}`):\n\n" + command["Message"]);
                await command.ReplySuccess(Communicator, "The user has been messaged.");
            }
            catch (Discord.Net.HttpException ex) when (ex.DiscordCode == 50007)
            {
                await command.ReplyError(Communicator, $"Can't send direct messages to this user. They have likely disabled private messages in their privacy settings.");
            }
        }
    }
}
