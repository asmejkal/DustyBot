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
using Discord.WebSocket;
using System.Threading;
using DustyBot.Framework.Logging;
using System.Collections;
using Newtonsoft.Json;
using DustyBot.Helpers;

namespace DustyBot.Modules
{
    [Module("Administration", "Helps with server admin tasks.")]
    class AdministrationModule : Module
    {
        public ICommunicator Communicator { get; private set; }
        public ISettingsProvider Settings { get; private set; }
        public ILogger Logger { get; private set; }

        public AdministrationModule(ICommunicator communicator, ISettingsProvider settings, ILogger logger)
        {
            Communicator = communicator;
            Settings = settings;
            Logger = logger;
        }

        [Command("administration", "help", "Shows help for this module.", CommandFlags.Hidden)]
        [Alias("admin", "help")]
        [IgnoreParameters]
        public async Task Help(ICommand command)
        {
            await command.Channel.SendMessageAsync(embed: (await HelpBuilder.GetModuleHelpEmbed(this, Settings)).Build());
        }

        [Command("say", "Sends a specified message.", CommandFlags.RunAsync)]
        [Permissions(GuildPermission.ManageMessages)]
        [Parameter("TargetChannel", ParameterType.TextChannel, "a channel that will receive the message")]
        [Parameter("Message", ParameterType.String, ParameterFlags.Remainder | ParameterFlags.Optional, "the message to be sent (you may also include one attachment)")]
        public async Task Say(ICommand command)
        {
            if (command.Message.Attachments.Count <= 0)
            {
                if (string.IsNullOrEmpty(command["Message"]))
                    throw new Framework.Exceptions.IncorrectParametersCommandException("Specify a message or an attachment.");

                await command[0].AsTextChannel.SendMessageAsync(command["Message"]);
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

                    await command[0].AsTextChannel.SendFileAsync(memStream, attachment.Filename, command["Message"]);
                }
            }

            if (command["TargetChannel"].AsTextChannel.Id != command.Message.Channel.Id)
                await command.ReplySuccess(Communicator, "Message sent.").ConfigureAwait(false);
        }

        [Command("edit", "Edits a message sent by the say command.", CommandFlags.RunAsync)]
        [Permissions(GuildPermission.ManageMessages)]
        [Parameter("MessageId", ParameterType.GuildSelfMessage, "a message previously sent by the `say` command")]
        [Parameter("Message", ParameterType.String, ParameterFlags.Remainder, "the message to send")]
        public async Task Edit(ICommand command)
        {
            var message = await command[0].AsGuildSelfMessage();
            await message.ModifyAsync(x => x.Content = command["Message"].AsString);
            await command.ReplySuccess(Communicator, "Message edited.").ConfigureAwait(false);
        }

        [Command("mute", "Mutes a server member.", CommandFlags.RunAsync)]
        [Permissions(GuildPermission.ManageRoles), BotPermissions(GuildPermission.ManageRoles)]
        [Parameter("User", ParameterType.GuildUser, "a server member to be muted")]
        [Parameter("Reason", ParameterType.String, ParameterFlags.Remainder | ParameterFlags.Optional, "reason")]
        public async Task Mute(ICommand command)
        {
            await AdministrationHelpers.Mute(command["User"].AsGuildUser, command["Reason"], Settings);
            await command.ReplySuccess(Communicator, $"User **{command["User"].AsGuildUser.Username}#{command["User"].AsGuildUser.DiscriminatorValue}** has been muted.").ConfigureAwait(false);
        }

        [Command("unmute", "Unmutes a server member.")]
        [Permissions(GuildPermission.ManageRoles), BotPermissions(GuildPermission.ManageRoles)]
        [Parameter("User", ParameterType.GuildUser, "a server member to be unmuted")]
        public async Task Unmute(ICommand command)
        {
            await AdministrationHelpers.Unmute(command["User"].AsGuildUser); 
            await command.ReplySuccess(Communicator, $"User **{command["User"].AsGuildUser.Username}#{command["User"].AsGuildUser.DiscriminatorValue}** has been unmuted.").ConfigureAwait(false);
        }

        [Command("roles", "Lists all roles on the server with ther IDs.")]
        public async Task Roles(ICommand command)
        {
            var result = new StringBuilder();
            foreach (var role in command.Guild.Roles)
                result.AppendLine($"Name: `{role.Name}` Id: `{role.Id}`");

            await command.Reply(Communicator, result.ToString());
        }

        [Command("emotes", "Exports all emotes on the server in specified format.")]
        [Alias("emoji")]
        [Permissions(GuildPermission.ManageEmojis)]
        [Parameter("Format", ParameterType.String, ParameterFlags.Optional, "output format – `json` (default), `cytube` or `text`")]
        [Parameter("Size", ParameterType.String, ParameterFlags.Optional, "emote size (e.g. `16`, `32`, `64`, `128`, `256`, `512` or `1024`)")]
        public async Task Emoji(ICommand command)
        {
            var format = command["Format"].HasValue ? command["Format"].AsString.ToLowerInvariant() : "json";

            var result = new StringBuilder();
            var sizeAppendix = command["Size"].HasValue ? $"?size={command["Size"]}" : "";
            string extension;
            if (format == "json" || format == "cytube")
            {
                var array = new JArray();
                foreach (var emote in command.Guild.Emotes)
                {
                    var o = new JObject();
                    o["name"] = $":{emote.Name}:";
                    o["image"] = emote.Url + sizeAppendix;
                    array.Add(o);
                }

                result.Append(JsonConvert.SerializeObject(array));
                extension = "json";
            }
            else if (format == "text")
            {
                foreach (var emote in command.Guild.Emotes)
                    result.AppendLine($":{emote.Name}: {emote.Url + sizeAppendix}");

                extension = "txt";
            }
            else
                throw new Framework.Exceptions.IncorrectParametersCommandException("Unknown format.");

            using (var stream = new MemoryStream(Encoding.Unicode.GetBytes(result.ToString())))
            {
                await command.Message.Channel.SendFileAsync(stream, $"output.{extension}");
            }
        }

        [Command("server", "settings", "get", "Gets settings for a server.", CommandFlags.RunAsync | CommandFlags.OwnerOnly | CommandFlags.DirectMessageAllow)]
        [Parameter("ServerId", ParameterType.Id, ParameterFlags.Optional)]
        [Parameter("Module", ParameterType.String, "LiteDB collection name")]
        public async Task GetSettings(ICommand command)
        {
            var channel = await command.Message.Author.GetOrCreateDMChannelAsync();
            var result = await Settings.DumpSettings(command[0].AsId ?? command.GuildId, command["Module"]);
            await Communicator.CommandReply(channel, result, x => $"```{x}```", 6).ConfigureAwait(false);
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
            var users = await command.Guild.GetUsersAsync();
            if (users.Count < userThreshold)
            {
                await command.ReplyError(Communicator, $"For security reasons, this command cannot be used on servers below {userThreshold} members.");
                return;
            }

            if (command["User"].AsGuildUser.IsBot)
            {
                await command.ReplyError(Communicator, $"This is a bot.");
                return;
            }

            var channel = await command["User"].AsGuildUser.GetOrCreateDMChannelAsync();

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
