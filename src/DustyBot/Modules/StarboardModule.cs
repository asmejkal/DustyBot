using Discord;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Globalization;
using DustyBot.Framework.Modules;
using DustyBot.Framework.Commands;
using DustyBot.Framework.Communication;
using DustyBot.Framework.Utility;
using DustyBot.Framework.Logging;
using DustyBot.Framework.Exceptions;
using DustyBot.Settings;
using DustyBot.Helpers;
using Discord.WebSocket;
using System.Text.RegularExpressions;
using DustyBot.Database.Services;
using DustyBot.Core.Collections;
using DustyBot.Core.Formatting;
using DustyBot.Core.Async;
using DustyBot.Definitions;
using System.Net;

namespace DustyBot.Modules
{
    [Module("Starboard", "Reposts the best messages as voted by users into specified channels.")]
    class StarboardModule : Module
    {
        private static readonly Regex LinkOnlyMessageRegex = new Regex(@"^\s*(?:(http[s]?:\/\/[^\s]+)\s*)+$", RegexOptions.Compiled);
        private static readonly Regex InstagramThumbnailRegex = new Regex(@"https?:\/\/(?:www.)?instagram.com\/p\/\w+\/media", RegexOptions.Compiled);

        private ICommunicator Communicator { get; }
        private ISettingsService Settings { get; }
        private ILogger Logger { get; }
        private IUserFetcher UserFetcher { get; }
        private IUrlShortener UrlShortener { get; }

        AsyncMutexCollection<Tuple<ulong, int>> _processingMutexes = new AsyncMutexCollection<Tuple<ulong, int>>();

        public StarboardModule(ICommunicator communicator, ISettingsService settings, ILogger logger, IUserFetcher userFetcher, IUrlShortener urlShortener)
        {
            Communicator = communicator;
            Settings = settings;
            Logger = logger;
            UserFetcher = userFetcher;
            UrlShortener = urlShortener;
        }

        [Command("starboard", "help", "Shows help for this module.", CommandFlags.Hidden)]
        [Alias("starboard")]
        [IgnoreParameters]
        public async Task Help(ICommand command)
        {
            await command.Channel.SendMessageAsync(embed: HelpBuilder.GetModuleHelpEmbed(this, command.Prefix));
        }

        [Command("starboard", "add", "Sets up a new starboard.")]
        [Permissions(GuildPermission.Administrator)]
        [Parameter("Channel", ParameterType.TextChannel, ParameterFlags.Remainder, "a channel that will receive the starred messages")]
        [Comment("The bot will repost messages that were reacted to with a chosen emoji (:star: by default) to this channel. \n\nA user can't star their own messages unless you allow it with `starboard set rule`.")]
        public async Task AddStarboard(ICommand command)
        {
            if (!(await command.Guild.GetCurrentUserAsync()).GetPermissions(command["Channel"].AsTextChannel).SendMessages)
            {
                await command.ReplyError(Communicator, $"The bot can't send messages in this channel. Please set the correct guild or channel permissions.");
                return;
            }

            var id = await Settings.Modify(command.GuildId, (StarboardSettings s) =>
            {
                s.Starboards.Add(new Starboard() { Id = s.NextId, Channel = command["Channel"].AsTextChannel.Id, Style = StarboardStyle.Embed });
                return s.NextId++;
            });

            await command.ReplySuccess(Communicator, $"Starboard `{id}` has been enabled in channel {command["Channel"].AsTextChannel.Mention}. Use the `starboard set ...` commands to customize it.");
        }

        [Command("starboard", "set", "style", "Sets the style for the starboard (embed or text).")]
        [Alias("starboard", "style", true)]
        [Permissions(GuildPermission.Administrator)]
        [Parameter("StarboardID", ParameterType.Int, "ID of the starboard, use `starboard list` to see all active starboards and their IDs")]
        [Parameter("Style", ParameterType.String, "use `embed` or `text`")]
        [Comment("`embed` - compact + allows you to jump to the original message \n`text` - better for media (multiple images, playable videos, etc.)")]
        public async Task SetStyle(ICommand command)
        {
            if (!Enum.TryParse<StarboardStyle>(command["Style"], true, out var style) || !Enum.IsDefined(typeof(StarboardStyle), style))
                throw new IncorrectParametersCommandException("Unknown style.");

            var id = await Settings.Modify(command.GuildId, (StarboardSettings s) =>
            {
                var board = s.Starboards.FirstOrDefault(x => x.Id == (int)command["StarboardID"]);
                if (board == null)
                    throw new IncorrectParametersCommandException("No starboard found with this ID. Use `starboard list` to see all active starboards and their IDs.", false);

                board.Style = style;
                return board.Id;
            });

            await command.ReplySuccess(Communicator, $"Starboard `{id}` will now use the `{command["Style"]}` style.");
        }

        [Command("starboard", "set", "emoji", "Sets one or more custom emoji for a starboard.")]
        [Alias("starboard", "set", "emojis", true), Alias("starboard", "set", "emotes", true)]
        [Alias("starboard", "emoji", true), Alias("starboard", "emojis", true), Alias("starboard", "emotes", true)]
        [Permissions(GuildPermission.Administrator)]
        [Parameter("StarboardID", ParameterType.Int, "ID of the starboard, use `starboard list` to see all active starboards and their IDs")]
        [Parameter("Emojis", ParameterType.String, ParameterFlags.Repeatable, "one or more emojis that will be used to star messages instead of the default :star: emoji; the first emoji will be the main one")]
        public async Task SetEmojis(ICommand command)
        {
            var id = await Settings.Modify(command.GuildId, (StarboardSettings s) =>
            {
                var board = s.Starboards.FirstOrDefault(x => x.Id == (int)command["StarboardID"]);
                if (board == null)
                    throw new IncorrectParametersCommandException("No starboard found with this ID. Use `starboard list` to see all active starboards and their IDs.", false);

                board.Emojis = new List<string>(command["Emojis"].Repeats.Select(x => x.AsString));
                return board.Id;
            });

            await command.ReplySuccess(Communicator, $"Starboard `{id}` will now look for {(command["Emojis"].Repeats.Count == 1 ? "the " : "")}{command["Emojis"].Repeats.Select(x => x.AsString).WordJoin()} emoji{(command["Emojis"].Repeats.Count > 1 ? "s" : "")}.");
        }

        [Command("starboard", "set", "threshold", "Sets the minimum reactions for a starboard.")]
        [Alias("starboard", "threshold", true)]
        [Permissions(GuildPermission.Administrator)]
        [Parameter("StarboardID", ParameterType.Int, "ID of the starboard, use `starboard list` to see all active starboards and their IDs")]
        [Parameter("Threshold", ParameterType.UInt, "sets how many reactions a message must have to get reposted in the starboard channel")]
        public async Task SetThreshold(ICommand command)
        {
            try
            {
                var id = await Settings.Modify(command.GuildId, (StarboardSettings s) =>
                {
                    var board = s.Starboards.FirstOrDefault(x => x.Id == (int)command["StarboardID"]);
                    if (board == null)
                        throw new IncorrectParametersCommandException("No starboard found with this ID. Use `starboard list` to see all active starboards and their IDs.", false);

                    board.Threshold = (uint)command["Threshold"];
                    return board.Id;
                });

                await command.ReplySuccess(Communicator, $"Starboard `{id}` will now require a minimum of `{(uint)command["Threshold"]}` reactions.");
            }
            catch (ArgumentOutOfRangeException)
            {
                throw new IncorrectParametersCommandException($"Threshold is out of allowed range.", false);
            }
        }

        [Command("starboard", "set", "channels", "Sets which channels belong to this starboard.")]
        [Alias("starboard", "channels", true)]
        [Permissions(GuildPermission.Administrator)]
        [Parameter("StarboardID", ParameterType.Int, "ID of the starboard, use `starboard list` to see all active starboards and their IDs")]
        [Parameter("Channels", ParameterType.TextChannel, ParameterFlags.Repeatable | ParameterFlags.Optional, "one or more channels")]
        [Comment("The bot will look only in these channels for starred messages (for this particular starboard). Omit the `Channels` parameter to accept all channels.")]
        public async Task SetChannelsWhitelist(ICommand command)
        {
            var id = await Settings.Modify(command.GuildId, (StarboardSettings s) =>
            {
                var board = s.Starboards.FirstOrDefault(x => x.Id == (int)command["StarboardID"]);
                if (board == null)
                    throw new IncorrectParametersCommandException("No starboard found with this ID. Use `starboard list` to see all active starboards and their IDs.", false);

                if (command["Channels"].HasValue)
                    board.ChannelsWhitelist = new HashSet<ulong>(command["Channels"].Repeats.Select(x => x.AsTextChannel.Id));
                else
                    board.ChannelsWhitelist.Clear();

                return board.Id;
            });

            if (command["Channels"].HasValue)
                await command.ReplySuccess(Communicator, $"Starboard `{id}` will now only repost messages from {command["Channels"].Repeats.Select(x => x.AsTextChannel.Mention).WordJoin()}.");
            else
                await command.ReplySuccess(Communicator, $"Starboard `{id}` will now repost messages from all channels.");
        }

        [Command("starboard", "set", "rule", "Sets the rules for a starboard, e.g. to allow self-stars.")]
        [Alias("starboard", "set", "rules", true)]
        [Permissions(GuildPermission.Administrator)]
        [Parameter("StarboardID", ParameterType.Int, "ID of the starboard, use `starboard list` to see all active starboards and their IDs")]
        [Parameter("Rule", ParameterType.String, "the rule (see below)")]
        [Parameter("Setting", "^(yes|no)$", ParameterType.String, "the rule setting: `yes` or `no`")]
        [Comment("__Rules:__ \n`SelfStars` - allow users to star their own messages \n`KeepUnstarred` - don't delete reposts that fall below the star treshold \n`KeepDeleted` - don't delete reposts if the original message gets deleted")]
        [Example("2 SelfStars yes")]
        public async Task SetRule(ICommand command)
        {
            var rule = (string)command["Rule"];
            var value = string.Compare(command["Setting"], "yes", true, GlobalDefinitions.Culture) == 0;
            var explanation = await Settings.Modify(command.GuildId, (StarboardSettings s) =>
            {
                var board = s.Starboards.FirstOrDefault(x => x.Id == (int)command["StarboardID"]);
                if (board == null)
                    throw new IncorrectParametersCommandException("No starboard found with this ID. Use `starboard list` to see all active starboards and their IDs.", false);

                if (string.Compare(rule, "SelfStars", true, GlobalDefinitions.Culture) == 0)
                {
                    board.AllowSelfStars = value;
                    return $"Users can {(value ? "now" : "no longer")} star their own messages for starboard `{board.Id}`.";
                }
                else if (string.Compare(rule, "KeepUnstarred", true, GlobalDefinitions.Culture) == 0)
                {
                    board.KeepUnstarred = value;
                    return $"Messages that fall below the minimum star count of `{board.Threshold}` will {(value ? "now" : "no longer")} be kept for starboard `{board.Id}`.";
                }
                else if (string.Compare(rule, "KeepDeleted", true, GlobalDefinitions.Culture) == 0)
                {
                    board.KeepDeleted = value;
                    return $"Reposts in starboard `{board.Id}` will {(value ? "now" : "no longer")} be kept if the original message is deleted.";
                }
                else
                    throw new IncorrectParametersCommandException("Unknown rule.");
            });

            await command.ReplySuccess(Communicator, explanation);
        }

        [Command("starboard", "list", "Lists all active starboards.")]
        public async Task ListStarboards(ICommand command)
        {
            var settings = await Settings.Read<StarboardSettings>(command.GuildId, false);
            if (settings == null || settings.Starboards.Count <= 0)
            {
                await command.Reply(Communicator, "No starboards have been set up on this server. Use `starboard add` to create a starboard.");
                return;
            }

            var result = new StringBuilder();
            foreach (var s in settings.Starboards)
                result.AppendLine($"ID: `{s.Id}` Channel: <#{s.Channel}> Style: `{(s.Style)}` Emojis: {string.Join(" ", s.Emojis)} Threshold: `{s.Threshold}` Linked channels: {(s.ChannelsWhitelist.Count > 0 ? string.Join(" ", s.ChannelsWhitelist.Select(x => "<#" + x + ">")) : "`all`")}");

            await command.Reply(Communicator, result.ToString());
        }

        [Command("starboard", "remove", "Disables a starboard.")]
        [Permissions(GuildPermission.Administrator)]
        [Parameter("StarboardID", ParameterType.Int, "ID of the starboard, use `starboard list` to see all active starboards and their IDs")]
        [Comment("The bot will not delete the starboard channel, but the reaction data will be lost.")]
        public async Task RemoveStarboard(ICommand command)
        {
            var r = await Settings.Modify(command.GuildId, (StarboardSettings s) =>
            {
                return s.Starboards.RemoveAll(x => x.Id == (int)command["StarboardID"]);
            });

            if (r > 0)
                await command.ReplySuccess(Communicator, $"Starboard `{command["StarboardID"]}` has been disabled.");
            else
                throw new IncorrectParametersCommandException("No starboard found with this ID. Use `starboard list` to see all active starboards and their IDs.", false);
        }

        [Command("starboard", "ranking", "Shows which users have received the most stars.")]
        public async Task StarboardRanking(ICommand command)
        {
            var settings = await Settings.Read<StarboardSettings>(command.GuildId);

            var data = new Dictionary<ulong, int>();
            foreach (var board in settings.Starboards)
            {
                foreach (var message in board.StarredMessages)
                {
                    if (data.TryGetValue(message.Value.Author, out var value))
                        data[message.Value.Author] = value + message.Value.StarCount;
                    else
                        data[message.Value.Author] = message.Value.StarCount;
                }
            }

            var result = new StringBuilder();
            int count = 0;
            foreach (var dataPair in data.OrderByDescending(x => x.Value))
            {
                var user = await command.Guild.GetUserAsync(dataPair.Key) ?? await UserFetcher.FetchGuildUserAsync(command.Guild.Id, dataPair.Key);
                if (user == null)
                    continue;

                result.AppendLine($"`{dataPair.Value}x ⭐` {user.Username}{(!string.IsNullOrEmpty(user.Nickname) ? " ~ " + user.Nickname : "")}");

                if (++count == 15)
                    break;
            }

            if (count <= 0)
            {
                await command.Reply(Communicator, "There are no users with starred messages on this server.");
                return;
            }

            var embed = new EmbedBuilder()
                .WithTitle("Top posters")
                .WithDescription(result.ToString());

            await command.Message.Channel.SendMessageAsync(string.Empty, embed: embed.Build());
        }

        [Command("starboard", "top", "Shows the top ranked messages.")]
        public async Task StarboardTop(ICommand command)
        {
            var settings = await Settings.Read<StarboardSettings>(command.GuildId);

            var data = new Dictionary<(ulong channel, ulong message), int>();
            foreach (var board in settings.Starboards)
            {
                foreach (var message in board.StarredMessages)
                {
                    if (message.Value.StarboardMessage == default)
                        continue;

                    var key = (board.Channel, message.Value.StarboardMessage);
                    if (data.TryGetValue(key, out var value))
                        data[key] = value + message.Value.StarCount;
                    else
                        data[key] = message.Value.StarCount;
                }
            }

            var pages = new PageCollection();
            foreach (var dataPair in data.OrderByDescending(x => x.Value))
            {
                var channel = await command.Guild.GetTextChannelAsync(dataPair.Key.channel);
                if (channel == null)
                    continue;

                var message = await channel.GetMessageAsync(dataPair.Key.message);
                if (message == null)
                    continue;

                pages.Add(new Page() 
                { 
                    Content = $"**[#{pages.Count + 1}]** " + message.Content, 
                    Embed = string.IsNullOrEmpty(message.Content) ? message.Embeds?.FirstOrDefault()?.ToEmbedBuilder() : null
                });

                if (pages.Count >= 20)
                    break;
            }

            if (pages.Count <= 0)
            {
                await command.Reply(Communicator, "There are no starred messages on this server.");
                return;
            }

            await command.Reply(Communicator, pages);
        }

        [Command("starboard", "remove", "message", "Removes one of your own messages from starboard.")]
        [Parameter("Message", ParameterType.GuildSelfMessage, "ID or link to the starboard message (the repost in starboard, not the original message)")]
        [Comment("You can only remove your own messages. To get a message link, right click the message and select `Copy Message Link`.")]
        public async Task RemoveStarboardMessage(ICommand command)
        {
            var message = await command["Message"].AsGuildSelfMessage;
            var found = await Settings.Modify(command.GuildId, (StarboardSettings x) =>
            {
                foreach (var b in x.Starboards)
                {
                    var id = b.StarredMessages.FirstOrDefault(x => x.Value.StarboardMessage == message.Id && x.Value.Author == command.Author.Id).Key;
                    if (id != default)
                    {
                        b.StarredMessages.Remove(id);
                        return true;
                    }
                }

                return false;
            });

            if (found)
            {
                await message.DeleteAsync();
                await command.Reply(Communicator, $"Removed your starred message `{message.Id}` from starboard in channel <#{message.Channel.Id}>.");
            }
            else
                await command.Reply(Communicator, $"Can't find message `{message.Id}` in any starboard on this server. Please provide a valid message from a starboard channel (not the original message).");
        }

        public override Task OnReactionAdded(Cacheable<IUserMessage, ulong> cachedMessage, ISocketMessageChannel channel, SocketReaction reaction)
        {
            TaskHelper.FireForget(async () =>
            {
                try
                {
                    if (!reaction.User.IsSpecified || reaction.User.Value.IsBot)
                        return;

                    if (!(channel is ITextChannel textChannel))
                        return;

                    var settings = await Settings.Read<StarboardSettings>(textChannel.GuildId, false);
                    if (settings == null || settings.Starboards.Count <= 0)
                        return;
                    
                    foreach (var board in settings.Starboards)
                    {
                        TaskHelper.FireForget(async () =>
                        {
                            //Process only one reaction at a time for each starboard, so we don't have to deal with race conditions
                            var mutex = await _processingMutexes.GetOrCreate(Tuple.Create(textChannel.GuildId, board.Id));

                            try
                            {
                                await mutex.WaitAsync();
                                await ProcessNewStar(board, textChannel, cachedMessage, reaction);
                            }
                            catch (Exception ex)
                            {
                                await Logger.Log(new LogMessage(LogSeverity.Error, "Starboard", $"Failed to process new star in board {board.Id} on {textChannel.Guild.Name} ({textChannel.Guild.Id}) for message {cachedMessage.Id}", ex));
                            }
                            finally
                            {
                                mutex.Release();
                            }
                        });
                    }
                }
                catch (Exception ex)
                {
                    await Logger.Log(new LogMessage(LogSeverity.Error, "Starboard", $"Failed to process a new reaction for message {cachedMessage.Id} on {(channel as ITextChannel)?.GuildId}", ex));
                }
            });

            return Task.CompletedTask;
        }

        public async Task ProcessNewStar(Starboard board, ITextChannel channel, Cacheable<IUserMessage, ulong> cachedMessage, SocketReaction reaction)
        {
            if (!board.Emojis.Contains(reaction.Emote.GetFullName()))
                return;

            if (board.ChannelsWhitelist.Count > 0 && !board.ChannelsWhitelist.Contains(channel.Id))
                return;

            var message = await cachedMessage.GetOrDownloadAsync();
            if (message == null)
                return;

            if ((!board.AllowSelfStars && message.Author.Id == reaction.UserId) || message.Author.IsBot)
                return;

            if (string.IsNullOrEmpty(message.Content) && message.Attachments.Count <= 0)
                return;

            var starrers = new HashSet<ulong>();
            foreach (var emoji in message.Reactions.Where(x => board.Emojis.Contains(x.Key.GetFullName())).Select(x => x.Key))
            {
                try
                {
                    starrers.UnionWith((await message.GetReactionUsersAsync(emoji, int.MaxValue).FlattenAsync()).Select(x => x.Id));
                }
                catch (Discord.Net.HttpException ex) when (ex.DiscordCode == 50001)
                {
                    await Logger.Log(new LogMessage(LogSeverity.Info, "Starboard", $"Missing access to read reactions in channel {channel.Id} for {message.Id} on {channel.Guild.Name} ({channel.Guild.Id})"));
                }
            }

            if (!board.AllowSelfStars)
                starrers.Remove(message.Author.Id);

            var entry = await Settings.Modify(channel.GuildId, (StarboardSettings s) =>
            {
                var b = s.Starboards.FirstOrDefault(x => x.Id == board.Id);
                if (b == null)
                    return null;

                var e = b.StarredMessages.GetOrCreate(message.Id);
                e.Author = message.Author.Id;
                e.StarCount = starrers.Count;
                
                return e;
            });

            if (entry == null)
                return;

            if (starrers.Count < board.Threshold)
                return;

            var starChannel = await channel.Guild.GetTextChannelAsync(board.Channel);
            if (starChannel == null)
                return;

            if (entry.StarboardMessage == default)
            {
                //Post new
                if (!(await channel.Guild.GetCurrentUserAsync()).GetPermissions(channel).SendMessages)
                {
                    await Logger.Log(new LogMessage(LogSeverity.Info, "Starboard", $"Can't post starred message because of missing permissions in #{channel.Name} ({channel.Id}) on {channel.Guild.Name} ({channel.Guild.Id})"));
                    return;
                }

                var attachments = await ProcessAttachments(message.Attachments);
                var built = await BuildStarMessage(message, channel, starrers.Count, board.Emojis.First(), attachments, board.Style);
                var starMessage = await starChannel.SendMessageAsync(built.Text, embed: built.Embed);

                await Settings.Modify(channel.GuildId, (StarboardSettings s) =>
                {
                    var b = s.Starboards.FirstOrDefault(x => x.Id == board.Id);
                    if (b != null && b.StarredMessages.TryGetValue(message.Id, out var e))
                    {
                        e.StarboardMessage = starMessage.Id;
                        e.Attachments = attachments;
                    }
                });

                await Logger.Log(new LogMessage(LogSeverity.Info, "Starboard", $"New starred message {message.Id} in board {board.Id} on {channel.Guild.Name} ({channel.GuildId})"));
            }
            else
            {
                //Update
                var starMessage = await starChannel.GetMessageAsync(entry.StarboardMessage) as IUserMessage;
                if (starMessage == null)
                    return; //Probably got deleted from starboard

                var built = await BuildStarMessage(message, channel, starrers.Count, board.Emojis.First(), entry.Attachments, board.Style);
                await starMessage.ModifyAsync(x => 
                {
                    x.Content = built.Text;
                    x.Embed = built.Embed;
                });
            }
        }

        public override Task OnReactionRemoved(Cacheable<IUserMessage, ulong> cachedMessage, ISocketMessageChannel channel, SocketReaction reaction)
        {
            TaskHelper.FireForget(async () =>
            {
                try
                {
                    if (reaction == null || !reaction.User.IsSpecified || reaction.User.Value == null || reaction.User.Value.IsBot)
                        return;

                    if (!(channel is ITextChannel textChannel))
                        return;

                    var settings = await Settings.Read<StarboardSettings>(textChannel.GuildId, false);
                    if (settings == null || settings.Starboards.Count <= 0)
                        return;

                    foreach (var board in settings.Starboards)
                    {
                        TaskHelper.FireForget(async () =>
                        {
                            //Process only one reaction at a time for each starboard, so we don't have to deal with race conditions
                            var mutex = await _processingMutexes.GetOrCreate(Tuple.Create(textChannel.GuildId, board.Id));

                            try
                            {
                                await mutex.WaitAsync();
                                await ProcessRemovedStar(board, textChannel, cachedMessage, reaction);
                            }
                            catch (Exception ex)
                            {
                                await Logger.Log(new LogMessage(LogSeverity.Error, "Starboard", $"Failed to process removed star in board {board.Id} on {textChannel.Guild.Name} ({textChannel.Guild.Id}) for message {cachedMessage.Id}", ex));
                            }
                            finally
                            {
                                mutex.Release();
                            }
                        });
                    }
                }
                catch (Exception ex)
                {
                    await Logger.Log(new LogMessage(LogSeverity.Error, "Starboard", $"Failed to process a removed reaction for message {cachedMessage.Id} on {(channel as ITextChannel)?.GuildId}", ex));
                }
            });

            return Task.CompletedTask;
        }

        public async Task ProcessRemovedStar(Starboard board, ITextChannel channel, Cacheable<IUserMessage, ulong> cachedMessage, SocketReaction reaction)
        {
            if (!board.Emojis.Contains(reaction.Emote.GetFullName()))
                return;

            var message = await cachedMessage.GetOrDownloadAsync();
            if (message == null)
                return;

            if ((!board.AllowSelfStars && message.Author.Id == reaction.UserId) || message.Author.IsBot)
                return;

            if (string.IsNullOrEmpty(message.Content) && message.Attachments.Count <= 0)
                return;

            var starrers = new HashSet<ulong>();
            foreach (var emoji in message.Reactions.Where(x => board.Emojis.Contains(x.Key.GetFullName())).Select(x => x.Key))
                starrers.UnionWith((await message.GetReactionUsersAsync(emoji, int.MaxValue).FlattenAsync()).Select(x => x.Id));

            if (!board.AllowSelfStars)
                starrers.Remove(message.Author.Id);

            var entry = await Settings.Modify(channel.GuildId, (StarboardSettings s) =>
            {
                var b = s.Starboards.FirstOrDefault(x => x.Id == board.Id);
                if (b == null)
                    return null;

                b.StarredMessages.TryGetValue(message.Id, out var e);

                if (starrers.Count <= 0 && !board.KeepUnstarred)
                    b.StarredMessages.Remove(message.Id);

                return e;
            });

            if (entry == null)
                return;

            if (entry.StarboardMessage == default)
                return;

            var starChannel = await channel.Guild.GetTextChannelAsync(board.Channel);
            if (starChannel == null)
                return;

            var starMessage = await starChannel.GetMessageAsync(entry.StarboardMessage) as IUserMessage;
            if (starMessage == null)
                return; //Probably got deleted from starboard

            if (starrers.Count <= 0 && !board.KeepUnstarred)
            {
                try
                {
                    await starMessage.DeleteAsync();
                    await Logger.Log(new LogMessage(LogSeverity.Info, "Starboard", $"Removed unstarred message {message.Id} in board {board.Id} on {channel.Guild.Name} ({channel.GuildId})"));
                }
                catch (Exception ex)
                {
                    await Logger.Log(new LogMessage(LogSeverity.Error, "Starboard", $"Failed to delete unstarred message {message.Id} on {channel.Guild} ({channel.GuildId})", ex));
                }
            }
            else
            {
                var built = await BuildStarMessage(message, channel, starrers.Count, board.Emojis.First(), entry.Attachments, board.Style);
                await starMessage.ModifyAsync(x =>
                {
                    x.Content = built.Text;
                    x.Embed = built.Embed;
                });
            }
        }

        private bool IsImageLink(string link) => link.EndsWith(".jpg") || link.EndsWith(".jpeg") || link.EndsWith(".png");

        private async Task<List<string>> ProcessAttachments(IEnumerable<IAttachment> attachments)
        {
            var result = new List<string>();
            foreach (var a in attachments)
            {
                try
                {
                    if (IsImageLink(a.Url))
                        result.Add(await UrlShortener.ShortenAsync(a.Url));
                    else
                        result.Add(a.Url);
                }
                catch (Exception ex)
                {
                    result.Add(a.Url);
                    await Logger.Log(new LogMessage(LogSeverity.Error, "Starboard", $"Failed to shorten URL {a.Url}", ex));
                }
            }

            return result;
        }

        async Task<(string Text, Embed Embed)> BuildStarMessage(IUserMessage message, ITextChannel channel, int starCount, string emote, List<string> attachments, StarboardStyle style)
        {
            var author = await channel.Guild.GetUserAsync(message.Author.Id) ?? await UserFetcher.FetchGuildUserAsync(channel.GuildId, message.Author.Id);

            if (style == StarboardStyle.Embed)
            {
                var embed = message.Embeds.FirstOrDefault(x => x.Thumbnail.HasValue || x.Image.HasValue);
                var attachedImage = attachments.FirstOrDefault(x => UrlShortener.IsShortened(x));
                PrintHelpers.Thumbnail thumbnail = null;
                if (attachedImage != null)
                {
                    thumbnail = new PrintHelpers.Thumbnail(attachedImage);
                }
                else if (embed != null)
                {
                    if (embed.Video.HasValue)
                        thumbnail = new PrintHelpers.Thumbnail(embed.Thumbnail?.Url, true, embed.Video.Value.Url);
                    else
                        thumbnail = new PrintHelpers.Thumbnail(embed.Thumbnail?.Url ?? embed.Image?.Url);
                }
                else if (attachments.Any())
                {
                    thumbnail = new PrintHelpers.Thumbnail(attachments.First());
                }

                if (thumbnail != null && InstagramThumbnailRegex.IsMatch(thumbnail.Url))
                    thumbnail.Url = await ResolveInstagramThumbnailAsync(thumbnail.Url) ?? thumbnail.Url;

                var result = PrintHelpers.BuildMediaEmbed(
                    $"{author.Username}{(string.IsNullOrEmpty(author.Nickname) ? "" : $" ~ {author.Nickname}")}",
                    attachments.Select(x => x),
                    caption: message.Content,
                    thumbnail: thumbnail,
                    captionFooter: $"[⤴️ Go to message]({message.GetLink()})",
                    footer: $"⭐ {starCount} in #{message.Channel.Name}",
                    timestamp: message.Timestamp,
                    iconUrl: message.Author.GetAvatarUrl(),
                    maxCaptionLength: int.MaxValue,
                    maxCaptionLines: int.MaxValue);

                return (null, result.Build());
            }
            else
            {
                var content = message.Content;
                var linkMatch = LinkOnlyMessageRegex.Match(message.Content);
                if (linkMatch.Success)
                {
                    content = null;
                    attachments.InsertRange(0, linkMatch.Groups[1].Captures.Select(x => x.Value));
                }

                var footer = $"{emote} {starCount} | `{message.CreatedAt.ToUniversalTime().ToString(@"MMM d yyyy HH:mm", new CultureInfo("en-US"))}` | {channel.Mention}";
                var messages = PrintHelpers.BuildMediaText(
                    $"**@{author.Username}{(string.IsNullOrEmpty(author.Nickname) ? "" : $" ~ {author.Nickname}")}:**",
                    attachments.Take(PrintHelpers.MediaPerTextMessage),
                    caption: content,
                    footer: footer);

                var result = await DiscordHelpers.ReplaceMentions(messages.First(), message.MentionedUserIds, message.MentionedRoleIds, channel.Guild);
                return (DiscordHelpers.EscapeMentions(result), null);
            }
        }

        public override Task OnMessageDeleted(Cacheable<IMessage, ulong> message, ISocketMessageChannel channel)
        {
            TaskHelper.FireForget(async () =>
            {
                try
                {
                    var textChannel = channel as ITextChannel;
                    if (textChannel == null)
                        return;

                    var guild = textChannel.Guild;

                    var settings = await Settings.Read<StarboardSettings>(guild.Id, false);
                    if (settings == null)
                        return;

                    var boardIds = new List<int>();
                    foreach (var board in settings.Starboards)
                    {
                        if (board.KeepDeleted)
                            continue;

                        if (!board.StarredMessages.TryGetValue(message.Id, out var starredMessage))
                            continue;

                        boardIds.Add(board.Id);
                        if (starredMessage.StarboardMessage != default)
                        {
                            var starChannel = await guild.GetTextChannelAsync(board.Channel);
                            if (starChannel == null)
                                continue;

                            var starMessage = await starChannel.GetMessageAsync(starredMessage.StarboardMessage) as IUserMessage;
                            if (starMessage == null)
                                continue;

                            try
                            {
                                await starMessage.DeleteAsync();
                                await Logger.Log(new LogMessage(LogSeverity.Info, "Starboard", $"Removed deleted starred message {message.Id} in board {board.Id} on {guild.Name} ({guild.Id})"));
                            }
                            catch (Exception ex)
                            {
                                await Logger.Log(new LogMessage(LogSeverity.Error, "Starboard", $"Failed to delete message {message.Id} on {guild.Id}", ex));
                            }
                        }
                    }

                    if (boardIds.Any())
                    {
                        await Settings.Modify(guild.Id, (StarboardSettings s) =>
                        {
                            foreach (var board in s.Starboards.Where(x => boardIds.Contains(x.Id)))
                                board.StarredMessages.Remove(message.Id);
                        });
                    }
                }
                catch (Exception ex)
                {
                    await Logger.Log(new LogMessage(LogSeverity.Error, "Starboard", "Failed to process deleted message", ex));
                }
            });

            return Task.CompletedTask;
        }

        private async Task<string> ResolveInstagramThumbnailAsync(string baseUrl)
        {
            try
            {
                var currentUrl = baseUrl;
                var redirectCodes = new[] { HttpStatusCode.MovedPermanently, HttpStatusCode.Redirect };
                for (int i = 0; i < 8; ++i)
                {
                    try
                    {
                        if (string.IsNullOrEmpty(currentUrl))
                            break;

                        var request = WebRequest.CreateHttp(currentUrl);
                        request.Method = "GET";
                        request.AllowAutoRedirect = false;

                        using (var response = await request.GetResponseAsync())
                        {
                            return currentUrl;
                        }
                    }
                    catch (WebException ex) when (ex.Response is HttpWebResponse r && redirectCodes.Contains(r.StatusCode))
                    {
                        currentUrl = r.Headers["Location"];
                    }
                }
            }
            catch (Exception ex)
            {
                await Logger.Log(new LogMessage(LogSeverity.Error, "Starboard", $"Failed to resolve instagram thumbnail {baseUrl}", ex));
            }

            return null;
        }
    }
}
