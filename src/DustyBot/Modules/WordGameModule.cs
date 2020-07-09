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
using DustyBot.Framework.Logging;
using DustyBot.Framework.Config;
using DustyBot.Settings;
using DustyBot.Helpers;
using Discord.WebSocket;

namespace DustyBot.Modules
{
    //[Module("WordGameModule", "Train your vocabulary!", true)]
    //class ClassroomModule : Module
    //{
    //    public ICommunicator Communicator { get; }
    //    public ISettingsProvider Settings { get; }
    //    public ILogger Logger { get; }

    //    public ClassroomModule(ICommunicator communicator, ISettingsProvider settings, ILogger logger)
    //    {
    //        Communicator = communicator;
    //        Settings = settings;
    //        Logger = logger;
    //    }

    //    [Command("wordgame", "help", "Shows help for this module.", CommandFlags.Hidden)]
    //    [Alias("wg", "help")]
    //    [IgnoreParameters]
    //    public async Task Help(ICommand command)
    //    {
    //        await command.Channel.SendMessageAsync(embed: await HelpBuilder.GetModuleHelpEmbed(this, Settings));
    //    }

    //    [Command("wordgame", "Starts a new game.")]
    //    [Alias("wordgame", "start"), Alias("wg"), Alias("wg", "start")]
    //    [Parameter("Language", ParameterType.String, "the language, e.g. `kr` or `Korean`")]
    //    [Parameter("Dictionary", ParameterType.String, ParameterFlags.Remainder, "the words to test; use `wg dictionary list` to see the options")]
    //    public async Task Start(ICommand command)
    //    {
            

    //        await command.Channel.SendMessageAsync(embed: await HelpBuilder.GetModuleHelpEmbed(this, Settings));
    //    }

    //    [Command("wordgame", "dictionary", "add", "Adds a new dictionary.")]
    //    [Alias("wordgame", "dict", "add"), Alias("wg", "dictionary", "add"), Alias("wg", "dict", "add")]
    //    [Parameter("Language", ParameterType.String, "the language, e.g. `Korean`")]
    //    [Comment("Upload a dictionary file as an attachment. The format of the file should be as follows: ```english word: answer\nanother english word: answer, another answer\n...```")]
    //    public async Task AddDictionary(ICommand command)
    //    {


    //        await command.Channel.SendMessageAsync(embed: await HelpBuilder.GetModuleHelpEmbed(this, Settings));
    //    }

    //    public override Task OnMessageReceived(SocketMessage message)
    //    {
    //        TaskHelper.FireForget(async () =>
    //        {
    //            try
    //            {
    //                var channel = message.Channel as ITextChannel;
    //                if (channel == null)
    //                    return;

    //                var user = message.Author as IGuildUser;
    //                if (user == null)
    //                    return;

    //                if (user.IsBot)
    //                    return;

    //                var settings = await Settings.Read<ReactionsSettings>(channel.GuildId, false);
    //                if (settings == null)
    //                    return;

    //                var reaction = settings.GetRandom(message.Content);
    //                if (reaction == null)
    //                    return;

    //                await Logger.Log(new LogMessage(LogSeverity.Info, "Reactions", $"\"{message.Content}\" by {message.Author.Username} ({message.Author.Id}) on {channel.Guild.Name}"));

    //                await Communicator.SendMessage(channel, reaction);
    //            }
    //            catch (Exception ex)
    //            {
    //                await Logger.Log(new LogMessage(LogSeverity.Error, "Reactions", "Failed to process reaction", ex));
    //            }
    //        });

    //        return Task.CompletedTask;
    //    }
    //}
}
