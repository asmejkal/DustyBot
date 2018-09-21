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

        [Command("server", "settings", "get", "Gets settings for a server.", CommandFlags.RunAsync | CommandFlags.OwnerOnly | CommandFlags.DirectMessageAllow)]
        [Parameter("ServerId", ParameterType.Id, ParameterFlags.Optional)]
        [Parameter("Module", ParameterType.String, "LiteDB collection name")]
        public async Task GetSettings(ICommand command)
        {
            var channel = await command.Message.Author.GetOrCreateDMChannelAsync();
            var result = await Settings.DumpSettings(command[0].AsId ?? command.GuildId, command["Module"]);
            await Communicator.CommandReply(channel, result, x => $"```{x}```", 6).ConfigureAwait(false);
        }

        
    }
}
