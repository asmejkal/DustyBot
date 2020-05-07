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
using DustyBot.Framework.Exceptions;
using DustyBot.Framework.LiteDB.Utility;
using DustyBot.Settings;
using DustyBot.Helpers;
using Discord.WebSocket;
using System.Text.RegularExpressions;
using System.Threading;

namespace DustyBot.Modules
{
    [Module("Starboard", "Reposts the best messages as voted by users into specified channels.")]
    class StarboardModule : Module
    {
        public ICommunicator Communicator { get; private set; }
        public ISettingsProvider Settings { get; private set; }
        public ILogger Logger { get; private set; }
        BotConfig Config { get; }

        AsyncMutexCollection<Tuple<ulong, int>> _processingMutexes = new AsyncMutexCollection<Tuple<ulong, int>>();

        public StarboardModule(ICommunicator communicator, ISettingsProvider settings, ILogger logger, BotConfig config)
        {
            Communicator = communicator;
            Settings = settings;
            Logger = logger;
            Config = config;
        }

        [Command("starboard", "help", "Shows help for this module.", CommandFlags.Hidden)]
        [Alias("starboard")]
        [IgnoreParameters]
        public async Task Help(ICommand command)
        {
            await command.Channel.SendMessageAsync(embed: await HelpBuilder.GetModuleHelpEmbed(this, Settings));
        }

        [Command("starboard", "add", "Sets up a new starboard.")]
        [Permissions(GuildPermission.Administrator)]
        [Parameter("Channel", ParameterType.TextChannel, ParameterFlags.Remainder, "a channel that will receive the starred messages")]
        [Comment("The bot will repost messages that were reacted to with a chosen emoji (:star: by default) to this channel. You can modify the minimum number of required reactions. You can have multiple starboards with different emojis or scoped to different channels.")]
        public async Task AddStarboard(ICommand command)
        {
            if (!(await command.Guild.GetCurrentUserAsync()).GetPermissions(command["Channel"].AsTextChannel).SendMessages)
            {
                await command.ReplyError(Communicator, $"The bot can't send messages in this channel. Please set the correct guild or channel permissions.");
                return;
            }

            var id = await Settings.Modify(command.GuildId, (StarboardSettings s) =>
            {
                s.Starboards.Add(new Starboard() { Id = s.NextId, Channel = command["Channel"].AsTextChannel.Id });
                return s.NextId++;
            }).ConfigureAwait(false);

            await command.ReplySuccess(Communicator, $"Starboard `{id}` has been enabled in channel {command["Channel"].AsTextChannel.Mention}.").ConfigureAwait(false);
        }

        [Command("starboard", "emojis", "Sets one or more custom emoji for a starboard.")]
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
            }).ConfigureAwait(false);

            await command.ReplySuccess(Communicator, $"Starboard `{id}` will now look for {(command["Emojis"].Repeats.Count == 1 ? "the " : "")}{command["Emojis"].Repeats.Select(x => x.AsString).WordJoin()} emoji{(command["Emojis"].Repeats.Count > 1 ? "s" : "")}.").ConfigureAwait(false);
        }

        [Command("starboard", "threshold", "Sets the minimum reactions for a starboard.")]
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
                }).ConfigureAwait(false);

                await command.ReplySuccess(Communicator, $"Starboard `{id}` will now require a minimum of `{(uint)command["Threshold"]}` reactions.").ConfigureAwait(false);
            }
            catch (ArgumentOutOfRangeException)
            {
                throw new IncorrectParametersCommandException($"Threshold is out of allowed range.", false);
            }
        }

        [Command("starboard", "channels", "Sets which channels belong to this starboard.")]
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
            }).ConfigureAwait(false);

            if (command["Channels"].HasValue)
                await command.ReplySuccess(Communicator, $"Starboard `{id}` will now only repost messages from {command["Channels"].Repeats.Select(x => x.AsTextChannel.Mention).WordJoin()}.").ConfigureAwait(false);
            else
                await command.ReplySuccess(Communicator, $"Starboard `{id}` will now repost messages from all channels.").ConfigureAwait(false);
        }

        [Command("starboard", "list", "Lists all active starboards.")]
        public async Task ListStarboards(ICommand command)
        {
            var settings = await Settings.Read<StarboardSettings>(command.GuildId, false).ConfigureAwait(false);
            if (settings == null || settings.Starboards.Count <= 0)
            {
                await command.Reply(Communicator, "No starboards have been set up on this server. Use `starboard add` to create a starboard.").ConfigureAwait(false);
                return;
            }

            var result = new StringBuilder();
            foreach (var s in settings.Starboards)
                result.AppendLine($"ID: `{s.Id}` Channel: <#{s.Channel}> Emojis: {string.Join(" ", s.Emojis)} Threshold: `{s.Threshold}` Linked channels: {(s.ChannelsWhitelist.Count > 0 ? string.Join(" ", s.ChannelsWhitelist.Select(x => "<#" + x + ">")) : "`all`")}");

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
            }).ConfigureAwait(false);

            if (r > 0)
                await command.ReplySuccess(Communicator, $"Starboard `{command["StarboardID"]}` has been disabled.").ConfigureAwait(false);
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
                        data[message.Value.Author] = value + message.Value.Starrers.Count;
                    else
                        data[message.Value.Author] = message.Value.Starrers.Count;
                }
            }

            var result = new StringBuilder();
            int count = 0;
            foreach (var dataPair in data.OrderByDescending(x => x.Value))
            {
                var user = await command.Guild.GetUserAsync(dataPair.Key);
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
                        data[key] = value + message.Value.Starrers.Count;
                    else
                        data[key] = message.Value.Starrers.Count;
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

                pages.Add(new Page() { Content = $"**[#{pages.Count + 1}]** " + message.Content });
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
                                await Logger.Log(new LogMessage(LogSeverity.Error, "Starboard", $"Failed to process new star in board {board.Id} on {textChannel.Guild.Name} for message {cachedMessage.Id}", ex));
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
                    await Logger.Log(new LogMessage(LogSeverity.Error, "Starboard", $"Failed to process a new reaction for message {cachedMessage.Id}", ex));
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

            if (message.Author.Id == reaction.UserId || message.Author.IsBot)
                return;

            if (string.IsNullOrEmpty(message.Content) && message.Attachments.Count <= 0)
                return;

            var entry = await Settings.Modify(channel.GuildId, (StarboardSettings s) =>
            {
                var b = s.Starboards.FirstOrDefault(x => x.Id == board.Id);
                if (b == null)
                    return null;

                var e = b.StarredMessages.GetOrCreate(message.Id);
                e.Author = message.Author.Id;
                e.Starrers.Add(reaction.UserId);
                
                return e;
            });

            if (entry == null)
                return;

            if (entry.Starrers.Count < board.Threshold)
                return;

            var starChannel = await channel.Guild.GetTextChannelAsync(board.Channel);
            if (starChannel == null)
                return;

            if (entry.StarboardMessage == default)
            {
                //Post new
                var attachments = await ProcessAttachments(message.Attachments);
                var built = await BuildStarMessage(message, channel, entry.Starrers.Count, board.Emojis.First(), false, attachments);
                var starMessage = await starChannel.SendMessageAsync(built);

                await Settings.Modify(channel.GuildId, (StarboardSettings s) =>
                {
                    var b = s.Starboards.FirstOrDefault(x => x.Id == board.Id);
                    if (b != null && b.StarredMessages.TryGetValue(message.Id, out var e))
                    {
                        e.StarboardMessage = starMessage.Id;
                        e.Attachments = attachments;
                    }                        
                });
            }
            else
            {
                //Update
                var starMessage = await starChannel.GetMessageAsync(entry.StarboardMessage) as IUserMessage;
                if (starMessage == null)
                    return; //Probably got deleted from starboard

                var built = await BuildStarMessage(message, channel, entry.Starrers.Count, board.Emojis.First(), true, entry.Attachments);
                await starMessage.ModifyAsync(x => x.Content = built);
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
                                await Logger.Log(new LogMessage(LogSeverity.Error, "Starboard", $"Failed to removed star in board {board.Id} on {textChannel.Guild.Name} for message {cachedMessage.Id}", ex));
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
                    await Logger.Log(new LogMessage(LogSeverity.Error, "Starboard", $"Failed to process a removed reaction for message {cachedMessage.Id}", ex));
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

            if (message.Author.Id == reaction.UserId || message.Author.IsBot)
                return;

            if (string.IsNullOrEmpty(message.Content) && message.Attachments.Count <= 0)
                return;

            var entry = await Settings.Modify(channel.GuildId, (StarboardSettings s) =>
            {
                var b = s.Starboards.FirstOrDefault(x => x.Id == board.Id);
                if (b == null)
                    return null;

                if (b.StarredMessages.TryGetValue(message.Id, out var e))
                {
                    e.Starrers.Remove(reaction.UserId);
                    return e;
                }
                else
                    return null;
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

            var built = await BuildStarMessage(message, channel, entry.Starrers.Count, board.Emojis.First(), true, entry.Attachments);
            await starMessage.ModifyAsync(x => x.Content = built);
        }

        async Task<List<string>> ProcessAttachments(IEnumerable<IAttachment> attachments)
        {
            var result = new List<string>();
            foreach (var a in attachments)
            {
                try
                {
                    if (a.Url.EndsWith(".jpg") || a.Url.EndsWith(".jpeg") || a.Url.EndsWith(".png"))
                        result.Add(await UrlShortener.ShortenUrl(a.Url, Config.ShortenerKey));
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

        static readonly Regex HttpUrlRegex = new Regex(@"^http[s]?://[^\s]+$", RegexOptions.Compiled);
        async Task<string> BuildStarMessage(IUserMessage message, ITextChannel channel, int starCount, string emote, bool editing, List<string> attachments)
        {
            var footer = $"\n{emote} {starCount} | `{message.CreatedAt.ToUniversalTime().ToString(@"MMM d yyyy HH:mm", new CultureInfo("en-US"))}` | {channel.Mention}";
            string attachmentsSection = string.Join("\n", attachments);

            string content = $"**@{message.Author.Username}:**\n";

            if (string.IsNullOrWhiteSpace(message.Content))
            {
                //No content, should have attachments
                if (attachments.Count > 0)
                    content += attachmentsSection;
            }
            else if (message.Embeds.Count > 0 && HttpUrlRegex.IsMatch(message.Content))
            {
                content += message.Content;
                if (attachments.Count > 0)
                    content += "\n" + attachmentsSection;
            }
            else
            {
                content += message.Content + "\n";
                if (attachments.Count > 0)
                    content += attachmentsSection + "\n";
            }

            content = await DiscordHelpers.ReplaceMentions(content, message.MentionedUserIds, message.MentionedRoleIds, channel.Guild);
            return DiscordHelpers.EscapeMentions(content + footer);
        }
    }
}
