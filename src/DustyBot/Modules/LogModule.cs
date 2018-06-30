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
using System.Text.RegularExpressions;

namespace DustyBot.Modules
{
    [Module("Log", "Provides logging of server events.")]
    class LogModule : Module
    {
        public ICommunicator Communicator { get; private set; }
        public ISettingsProvider Settings { get; private set; }

        public LogModule(ICommunicator communicator, ISettingsProvider settings)
        {
            Communicator = communicator;
            Settings = settings;
        }

        [Command("log", "names", "Sets or disables a channel for name change logging.")]
        [Permissions(GuildPermission.Administrator)]
        [Usage("{p}log namechanges ChannelMention\n\nUse without parameters to disable name change logging.")]
        public async Task LogNameChanges(ICommand command)
        {
            if (command.Message.MentionedChannelIds.Count <= 0)
            {
                await Settings.Modify(command.GuildId, (LogSettings s) =>
                {
                    s.EventNameChangedChannel = 0;
                }).ConfigureAwait(false);
                
                await command.ReplySuccess(Communicator, "Name change logging channel has been disabled.").ConfigureAwait(false);
            }
            else
            {
                await Settings.Modify(command.GuildId, (LogSettings s) =>
                {
                    s.EventNameChangedChannel = command.Message.MentionedChannelIds.First();
                }).ConfigureAwait(false);
                
                await command.ReplySuccess(Communicator, "Name change logging channel has been set.").ConfigureAwait(false);
            }
        }
        
        [Command("log", "messages", "Sets or disables a channel for logging of deleted messages.")]
        [Permissions(GuildPermission.Administrator)]
        [Usage("{p}log messages ChannelMention\n\nUse without parameters to disable name change logging.")]
        public async Task LogMessages(ICommand command)
        {
            if (command.Message.MentionedChannelIds.Count <= 0)
            {
                await Settings.Modify(command.GuildId, (LogSettings s) =>
                {
                    s.EventMessageDeletedChannel = 0;
                }).ConfigureAwait(false);
                
                await command.ReplySuccess(Communicator, "Deleted messages logging channel has been disabled.").ConfigureAwait(false);
            }
            else
            {
                await Settings.Modify(command.GuildId, (LogSettings s) =>
                {
                    s.EventMessageDeletedChannel = command.Message.MentionedChannelIds.First();
                }).ConfigureAwait(false);
                
                await command.ReplySuccess(Communicator, "Deleted messages logging channel has been set.").ConfigureAwait(false);
            }
        }

        [Command("log", "messagefilter", "Sets or disables a regex filter for deleted messages.")]
        [Permissions(GuildPermission.Administrator)]
        [Usage("{p}log messagefilter RegularExpression\n\nMessages that match this regular expression won't be logged. Use without parameters to disable. For testing of regular expressions you can use https://regexr.com/.")]
        public async Task SetMessagesFilter(ICommand command)
        {
            await Settings.Modify(command.GuildId, (LogSettings s) =>
            {
                s.EventMessageDeletedFilter = command.Body;
            }).ConfigureAwait(false);

            await command.ReplySuccess(Communicator, string.IsNullOrEmpty(command.Body) ? "Filtering of deleted messages has been disabled." : "A filter for logged deleted messages has been set.").ConfigureAwait(false);
        }

        [Command("log", "channelfilter", "Excludes channels from logging of deleted messages.")]
        [Permissions(GuildPermission.Administrator)]
        [Usage("{p}log channelfilter ChannelMentions\n\nYou may specify one or more channels. Use without parameters to disable.")]
        public async Task SetMessagesChannelFilter(ICommand command)
        {
            await Settings.Modify(command.GuildId, (LogSettings s) =>
            {
                s.EventMessageDeletedChannelFilter = new List<ulong>(command.Message.MentionedChannelIds);
            }).ConfigureAwait(false);

            await command.ReplySuccess(Communicator, "A channel filter for logging of deleted messages has been " + 
                (command.Message.MentionedChannelIds.Count > 0 ? "set." : "disabled.")).ConfigureAwait(false);
        }
        
        public override async Task OnUserUpdated(SocketUser before, SocketUser after)
        {
            try
            {
                if (before.Username == after.Username)
                    return;

                var guildUser = after as SocketGuildUser;
                if (guildUser == null)
                    return;

                var guild = guildUser.Guild;
                var settings = await Settings.Read<LogSettings>(guild.Id).ConfigureAwait(false);

                var eventChannelId = settings.EventNameChangedChannel;
                if (eventChannelId == 0)
                    return;

                var eventChannel = guild.Channels.First(x => x.Id == eventChannelId) as ISocketMessageChannel;
                if (eventChannel == null)
                    return;

                await eventChannel.SendMessageAsync("`" + before.Username + "` changed to `" + after.Username + "` (<@" + after.Id.ToString() + ">)").ConfigureAwait(false);
            }
            catch (Exception)
            {
                //Log
            }
        }

        public override Task OnMessageDeleted(Cacheable<IMessage, ulong> message, ISocketMessageChannel channel)
        {
            var _ = Task.Run(async () =>
            {
                try
                {
                    var userMessage = (message.HasValue ? message.Value : null) as IUserMessage;
                    if (userMessage == null)
                        return;

                    if (userMessage.Author.IsBot)
                        return;

                    var textChannel = channel as ITextChannel;
                    if (textChannel == null)
                        return;

                    var guild = textChannel.Guild as SocketGuild;
                    if (guild == null)
                        return;

                    var settings = await Settings.Read<LogSettings>(guild.Id).ConfigureAwait(false);

                    var eventChannelId = settings.EventMessageDeletedChannel;
                    if (eventChannelId == 0)
                        return;

                    var eventChannel = guild.Channels.First(x => x.Id == eventChannelId) as ISocketMessageChannel;
                    if (eventChannel == null)
                        return;

                    if (settings.EventMessageDeletedChannelFilter.Contains(channel.Id))
                        return;

                    var filter = settings.EventMessageDeletedFilter;
                    if (!String.IsNullOrWhiteSpace(filter) && Regex.IsMatch(userMessage.Content, filter))
                        return;

                    var embed = new EmbedBuilder()
                    .WithDescription($"**Message by {userMessage.Author.Mention} in {textChannel.Mention} was deleted:**\n" + userMessage.Content)
                    .WithFooter(fb => fb.WithText($"{userMessage.Timestamp.ToUniversalTime().ToString("dd.MM.yyyy H:mm:ss UTC")} (deleted on {DateTime.Now.ToUniversalTime().ToString("dd.MM.yyyy H:mm:ss UTC")})"));
                    if (userMessage.Attachments.Any())
                        embed.AddField(efb => efb.WithName("Attachments").WithValue(string.Join(", ", userMessage.Attachments.Select(a => a.Url))).WithIsInline(false));

                    await eventChannel.SendMessageAsync("", false, embed).ConfigureAwait(false);
                }
                catch (Exception)
                {
                    //Log
                }
            });

            return Task.CompletedTask;
        }
    }
}
