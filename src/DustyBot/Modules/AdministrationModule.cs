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
        
        [Command("role", "giveall", "Assigns a role to everyone.")]
        [Parameters(ParameterType.String)]
        [Permissions(GuildPermission.ManageRoles), BotPermissions(GuildPermission.ManageRoles)]
        [Usage("{p}role giveall RoleName\n\nMay take a while to complete.")]
        public async Task AssignToAll(ICommand command)
        {
            var role = command.Guild.Roles.FirstOrDefault(x => x.Name == (string)command.GetParameter(0));
            if (role == null)
            {
                await command.ReplyError(Communicator, "Role not found.").ConfigureAwait(false);
                return;
            }
            
            await Task.Run(async () =>
            {
                var waitMsg = await command.Reply(Communicator, $"This may take a while...").ConfigureAwait(false);

                var users = await command.Guild.GetUsersAsync().ConfigureAwait(false);
                foreach (var user in users)
                {
                    try
                    {
                        await user.AddRoleAsync(role).ConfigureAwait(false);
                    }
                    catch (Exception)
                    {

                    }
                }

                await waitMsg.First().DeleteAsync().ConfigureAwait(false);
            });

            await command.ReplySuccess(Communicator, $"Role has been assigned to all users.").ConfigureAwait(false);
        }

        [Command("role", "notin", "Checks for users who are missing a specified role.")]
        [Parameters(ParameterType.String)]
        [Permissions(GuildPermission.ManageRoles), BotPermissions(GuildPermission.ManageRoles)]
        [Usage("{p}role notin RoleName")]
        public async Task NotInRole(ICommand command)
        {
            var role = command.Guild.Roles.FirstOrDefault(x => x.Name == (string)command.GetParameter(0));
            if (role == null)
            {
                await command.ReplyError(Communicator, "Role not found.").ConfigureAwait(false);
                return;
            }

            string result = "";
            await Task.Run(async () =>
            {
                var users = await command.Guild.GetUsersAsync();
                foreach (var user in users)
                {
                    if (user.RoleIds.Contains(role.Id))
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
        [Parameters(ParameterType.String)]
        [Permissions(GuildPermission.ManageMessages)]
        [Usage("{p}say TargetChannelMention Message...\n\n• *TargetChannelMention* - a channel that will receive the message\n• *Message* - the message to be sent; you may also include one attachment.")]
        public async Task Say(ICommand command)
        {
            if (command.Message.MentionedChannelIds.Count < 1)
                throw new Framework.Exceptions.IncorrectParametersCommandException("Missing target channel.");

            var channel = await command.Guild.GetTextChannelAsync(command.Message.MentionedChannelIds.First());
            if (channel == null)
                throw new Framework.Exceptions.IncorrectParametersCommandException("Cannot find this channel.");

            var text = new string(command.Body.SkipWhile(c => !char.IsWhiteSpace(c)).ToArray()).Trim();
            if (command.Message.Attachments.Count <= 0)
            {
                if (string.IsNullOrWhiteSpace(text))
                    throw new Framework.Exceptions.IncorrectParametersCommandException("Specify a message or an attachment.");

                await channel.SendMessageAsync(text);
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

                    await channel.SendFileAsync(memStream, attachment.Filename, text);
                }
            }

            if (channel.Id != command.Message.Channel.Id)
                await command.ReplySuccess(Communicator, "Message sent.").ConfigureAwait(false);
        }

        [Command("dump", "settings", "Dumps all settings for a server. Bot owner only."), RunAsync]
        [OwnerOnly, Hidden]
        [Usage("{p}dump settings [ServerId]")]
        public async Task DumpSettings(ICommand command)
        {
            var channel = await command.Message.Author.GetOrCreateDMChannelAsync();
            var result = await Settings.DumpSettings((ulong?)command.GetParameter(0) ?? command.GuildId);
            await Communicator.CommandReply(channel, result, x => $"```{x}```", 6).ConfigureAwait(false);
        }
    }
}
