using Discord;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.IO;
using System.Globalization;
using Newtonsoft.Json.Linq;
using DustyBot.Framework.Modules;
using DustyBot.Framework.Commands;
using DustyBot.Framework.Communication;
using DustyBot.Framework.Settings;
using DustyBot.Framework.Utility;
using DustyBot.Settings;

namespace DustyBot.Modules
{
    [Module("Administration", "Helps with server admin tasks.")]
    class AdministrationModule : Module
    {
        public ICommunicator Communicator { get; private set; }
        public ISettingsProvider Settings { get; private set; }

        public AdministrationModule(ICommunicator communicator, ISettingsProvider settings)
        {
            Communicator = communicator;
            Settings = settings;
        }
        
        [Command("role", "giveall", "Assigns a role to everyone."), RunAsync]
        [Parameters(ParameterType.Role)]
        [Permissions(GuildPermission.ManageRoles), BotPermissions(GuildPermission.ManageRoles)]
        [Usage("{p}role giveall `RoleNameOrID`\n\nMay take a while to complete.")]
        public async Task AssignToAll(ICommand command)
        {
            var failed = 0;
            await Task.Run(async () =>
            {
                var waitMsg = await command.Reply(Communicator, $"This may take a while...").ConfigureAwait(false);

                var users = await command.Guild.GetUsersAsync().ConfigureAwait(false);
                foreach (var user in users)
                {
                    try
                    {
                        await user.AddRoleAsync(command[0].AsRole).ConfigureAwait(false);
                    }
                    catch (Exception)
                    {
                        failed++;
                    }
                }

                await waitMsg.First().DeleteAsync().ConfigureAwait(false);
            });

            await command.ReplySuccess(Communicator, $"Role has been assigned to all users" + (failed > 0 ? $" ({failed} failed)." : ".")).ConfigureAwait(false);
        }

        [Command("role", "notin", "Checks for users who are missing a specified role.")]
        [Parameters(ParameterType.Role)]
        [Permissions(GuildPermission.ManageRoles), BotPermissions(GuildPermission.ManageRoles)]
        [Usage("{p}role notin `RoleNameOrID`")]
        public async Task NotInRole(ICommand command)
        {
            string result = "";
            await Task.Run(async () =>
            {
                var users = await command.Guild.GetUsersAsync();
                foreach (var user in users)
                {
                    if (user.RoleIds.Contains(command[0].AsRole.Id))
                        continue;

                    result += user.Username + ", ";
                }
            }).ConfigureAwait(false);

            if (result.Length > 2)
                result = result.Substring(0, result.Length - 2);

            if (string.IsNullOrEmpty(result))
                result = "Everyone has this role.";

            await command.Reply(Communicator, result).ConfigureAwait(false);
        }

        [Command("say", "Sends a specified message."), RunAsync]
        [Parameters(ParameterType.TextChannel)]
        [Permissions(GuildPermission.ManageMessages)]
        [Usage("{p}say `TargetChannel` `Message...`\n\n● `TargetChannel` - a channel that will receive the message\n● `Message...` - remainder; the message to be sent (you may also include one attachment)")]
        public async Task Say(ICommand command)
        {
            if (command.Message.Attachments.Count <= 0)
            {
                if (string.IsNullOrEmpty(command.Remainder.After(1)))
                    throw new Framework.Exceptions.IncorrectParametersCommandException("Specify a message or an attachment.");

                await command[0].AsTextChannel.SendMessageAsync(command.Remainder.After(1));
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

                    await command[0].AsTextChannel.SendFileAsync(memStream, attachment.Filename, command.Remainder.After(1));
                }
            }

            if (command[0].AsTextChannel.Id != command.Message.Channel.Id)
                await command.ReplySuccess(Communicator, "Message sent.").ConfigureAwait(false);
        }

        [Command("edit", "Edits a message sent by the say command."), RunAsync]
        [Parameters(ParameterType.ULong, ParameterType.String)]
        [Permissions(GuildPermission.ManageMessages)]
        [Usage("{p}edit `MessageId` `Message...`\n\n● `MessageID` - a message previously sent by the `say` command\n● `Message...` - remainder; the message to be sent (attachments cannot be edited)")]
        public async Task Edit(ICommand command)
        {
            var messageLoc = await command.Guild.GetMessageAsync((ulong)command[0]);
            var message = messageLoc?.Item1 as IUserMessage;
            if (message == null)
            {
                await command.ReplyError(Communicator, "Couldn't find the specified message.").ConfigureAwait(false);
                return;
            }

            if (message.Author.Id != (await command.Guild.GetCurrentUserAsync()).Id)
            {
                await command.ReplyError(Communicator, "Cannot edit messages that were not sent by the bot.").ConfigureAwait(false);
                return;
            }
            
            await message.ModifyAsync(x => x.Content = (string)command.Remainder.After(1));
            
            await command.ReplySuccess(Communicator, "Message edited.").ConfigureAwait(false);
        }

        [Command("dump", "settings", "Dumps all settings for a server."), RunAsync]
        [OwnerOnly]
        [Usage("{p}dump settings `[ServerId]`")]
        public async Task DumpSettings(ICommand command)
        {
            var channel = await command.Message.Author.GetOrCreateDMChannelAsync();
            var result = await Settings.DumpSettings((ulong?)command[0] ?? command.GuildId);
            await Communicator.CommandReply(channel, result, x => $"```{x}```", 6).ConfigureAwait(false);
        }
    }
}
