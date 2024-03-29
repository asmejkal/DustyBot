﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using DustyBot.Core.Async;
using DustyBot.Core.Collections;
using DustyBot.Core.Formatting;
using DustyBot.Database.Mongo.Collections;
using DustyBot.Database.Mongo.Models;
using DustyBot.Database.Services;
using DustyBot.Framework.Commands;
using DustyBot.Framework.Communication;
using DustyBot.Framework.Exceptions;
using DustyBot.Framework.Logging;
using DustyBot.Framework.Modules.Attributes;
using DustyBot.Framework.Reflection;
using DustyBot.Framework.Utility;
using DustyBot.Service.Definitions;
using DustyBot.Service.Helpers;
using Microsoft.Extensions.Logging;

namespace DustyBot.Service.Modules
{
    [Module("Starboard", "Repost the best messages as voted by users into specified channels.")]
    internal sealed class StarboardModule : IDisposable
    {
        private static readonly Regex LinkOnlyMessageRegex = new Regex(@"^\s*(?:(http[s]?:\/\/[^\s]+)\s*)+$", RegexOptions.Compiled);
        private static readonly Regex InstagramThumbnailRegex = new Regex(@"https?:\/\/(?:www.)?instagram.com\/p\/\w+\/media", RegexOptions.Compiled);
        private readonly BaseSocketClient _client;
        private readonly ICommunicator _communicator;
        private readonly ISettingsService _settings;
        private readonly ILogger<StarboardModule> _logger;
        private readonly IUserFetcher _userFetcher;
        private readonly IUrlShortener _urlShortener;
        private readonly IFrameworkReflector _frameworkReflector;
        private readonly HelpBuilder _helpBuilder;
        private readonly KeyedSemaphoreSlim<(ulong GuildId, int BoardId)> _processingMutex = new KeyedSemaphoreSlim<(ulong GuildId, int BoardId)>(1);

        public StarboardModule(
            BaseSocketClient client, 
            ICommunicator communicator, 
            ISettingsService settings, 
            ILogger<StarboardModule> logger, 
            IUserFetcher userFetcher, 
            IUrlShortener urlShortener, 
            IFrameworkReflector frameworkReflector,
            HelpBuilder helpBuilder)
        {
            _client = client;
            _communicator = communicator;
            _settings = settings;
            _logger = logger;
            _userFetcher = userFetcher;
            _urlShortener = urlShortener;
            _frameworkReflector = frameworkReflector;
            _helpBuilder = helpBuilder;

            _client.ReactionAdded += HandleReactionAdded;
            _client.ReactionRemoved += HandleReactionRemoved;
            _client.MessageDeleted += HandleMessageDeleted;
        }

        [Command("starboard", "help", "Shows help for this module.", CommandFlags.Hidden)]
        [Alias("starboard")]
        [IgnoreParameters]
        public async Task Help(ICommand command)
        {
            await command.Reply(_helpBuilder.GetModuleHelpEmbed(_frameworkReflector.GetModuleInfo(GetType()).Name, command.Prefix));
        }

        [Command("starboard", "add", "Sets up a new starboard.")]
        [Permissions(GuildPermission.Administrator)]
        [Parameter("Channel", ParameterType.TextChannel, ParameterFlags.Remainder, "a channel that will receive the starred messages")]
        [Comment("The bot will repost messages that were reacted to with a chosen emoji (:star: by default) to this channel. \n\nA user can't star their own messages unless you allow it with `starboard set rule`.")]
        public async Task AddStarboard(ICommand command)
        {
            if (!(await command.Guild.GetCurrentUserAsync()).GetPermissions(command["Channel"].AsTextChannel).SendMessages)
            {
                await command.ReplyError($"The bot can't send messages in this channel. Please set the correct guild or channel permissions.");
                return;
            }

            var id = await _settings.Modify(command.GuildId, (StarboardSettings s) =>
            {
                s.Starboards.Add(new Starboard() { Id = s.NextId, Channel = command["Channel"].AsTextChannel.Id, Style = StarboardStyle.Embed });
                return s.NextId++;
            });

            await command.ReplySuccess($"Starboard `{id}` has been enabled in channel {command["Channel"].AsTextChannel.Mention}. Use the `starboard set ...` commands to customize it.");
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

            var id = await _settings.Modify(command.GuildId, (StarboardSettings s) =>
            {
                var board = s.Starboards.FirstOrDefault(x => x.Id == (int)command["StarboardID"]);
                if (board == null)
                    throw new IncorrectParametersCommandException("No starboard found with this ID. Use `starboard list` to see all active starboards and their IDs.", false);

                board.Style = style;
                return board.Id;
            });

            await command.ReplySuccess($"Starboard `{id}` will now use the `{command["Style"]}` style.");
        }

        [Command("starboard", "set", "emoji", "Sets one or more custom emoji for a starboard.")]
        [Alias("starboard", "set", "emojis", true), Alias("starboard", "set", "emotes", true)]
        [Alias("starboard", "emoji", true), Alias("starboard", "emojis", true), Alias("starboard", "emotes", true)]
        [Permissions(GuildPermission.Administrator)]
        [Parameter("StarboardID", ParameterType.Int, "ID of the starboard, use `starboard list` to see all active starboards and their IDs")]
        [Parameter("Emojis", ParameterType.String, ParameterFlags.Repeatable, "one or more emojis that will be used to star messages instead of the default :star: emoji; the first emoji will be the main one")]
        public async Task SetEmojis(ICommand command)
        {
            var id = await _settings.Modify(command.GuildId, (StarboardSettings s) =>
            {
                var board = s.Starboards.FirstOrDefault(x => x.Id == (int)command["StarboardID"]);
                if (board == null)
                    throw new IncorrectParametersCommandException("No starboard found with this ID. Use `starboard list` to see all active starboards and their IDs.", false);

                board.Emojis = new List<string>(command["Emojis"].Repeats.Select(x => x.AsString));
                return board.Id;
            });

            await command.ReplySuccess($"Starboard `{id}` will now look for {(command["Emojis"].Repeats.Count == 1 ? "the " : "")}{command["Emojis"].Repeats.Select(x => x.AsString).WordJoin()} emoji{(command["Emojis"].Repeats.Count > 1 ? "s" : "")}.");
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
                var id = await _settings.Modify(command.GuildId, (StarboardSettings s) =>
                {
                    var board = s.Starboards.FirstOrDefault(x => x.Id == (int)command["StarboardID"]);
                    if (board == null)
                        throw new IncorrectParametersCommandException("No starboard found with this ID. Use `starboard list` to see all active starboards and their IDs.", false);

                    board.Threshold = (uint)command["Threshold"];
                    return board.Id;
                });

                await command.ReplySuccess($"Starboard `{id}` will now require a minimum of `{(uint)command["Threshold"]}` reactions.");
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
            var id = await _settings.Modify(command.GuildId, (StarboardSettings s) =>
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
                await command.ReplySuccess($"Starboard `{id}` will now only repost messages from {command["Channels"].Repeats.Select(x => x.AsTextChannel.Mention).WordJoin()}.");
            else
                await command.ReplySuccess($"Starboard `{id}` will now repost messages from all channels.");
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
            var explanation = await _settings.Modify(command.GuildId, (StarboardSettings s) =>
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
                {
                    throw new IncorrectParametersCommandException("Unknown rule.");
                }
            });

            await command.ReplySuccess(explanation);
        }

        [Command("starboard", "list", "Lists all active starboards.")]
        public async Task ListStarboards(ICommand command)
        {
            var settings = await _settings.Read<StarboardSettings>(command.GuildId, false);
            if (settings == null || settings.Starboards.Count <= 0)
            {
                await command.Reply("No starboards have been set up on this server. Use `starboard add` to create a starboard.");
                return;
            }

            var result = new StringBuilder();
            foreach (var s in settings.Starboards)
                result.AppendLine($"ID: `{s.Id}` Channel: <#{s.Channel}> Style: `{s.Style}` Emojis: {string.Join(" ", s.Emojis)} Threshold: `{s.Threshold}` Linked channels: {(s.ChannelsWhitelist.Count > 0 ? string.Join(" ", s.ChannelsWhitelist.Select(x => "<#" + x + ">")) : "`all`")}");

            await command.Reply(result.ToString());
        }

        [Command("starboard", "remove", "Disables a starboard.")]
        [Permissions(GuildPermission.Administrator)]
        [Parameter("StarboardID", ParameterType.Int, "ID of the starboard, use `starboard list` to see all active starboards and their IDs")]
        [Comment("The bot will not delete the starboard channel, but the reaction data will be lost.")]
        public async Task RemoveStarboard(ICommand command)
        {
            var r = await _settings.Modify(command.GuildId, (StarboardSettings s) =>
            {
                return s.Starboards.RemoveAll(x => x.Id == (int)command["StarboardID"]);
            });

            if (r > 0)
                await command.ReplySuccess($"Starboard `{command["StarboardID"]}` has been disabled.");
            else
                throw new IncorrectParametersCommandException("No starboard found with this ID. Use `starboard list` to see all active starboards and their IDs.", false);
        }

        [Command("starboard", "ranking", "Shows which users have received the most stars.")]
        public async Task StarboardRanking(ICommand command)
        {
            var settings = await _settings.Read<StarboardSettings>(command.GuildId);

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
                var user = await command.Guild.GetUserAsync(dataPair.Key) ?? await _userFetcher.FetchGuildUserAsync(command.Guild.Id, dataPair.Key);
                if (user == null)
                    continue;

                result.AppendLine($"`{dataPair.Value}x ⭐` {user.Username}{(!string.IsNullOrEmpty(user.Nickname) ? " ~ " + user.Nickname : "")}");

                if (++count == 15)
                    break;
            }

            if (count <= 0)
            {
                await command.Reply("There are no users with starred messages on this server.");
                return;
            }

            var embed = new EmbedBuilder()
                .WithTitle("Top posters")
                .WithDescription(result.ToString());

            await command.Reply(embed.Build());
        }

        [Command("starboard", "top", "Shows the top ranked messages.")]
        public async Task StarboardTop(ICommand command)
        {
            var settings = await _settings.Read<StarboardSettings>(command.GuildId);

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
                await command.Reply("There are no starred messages on this server.");
                return;
            }

            await command.Reply(pages);
        }

        [Command("starboard", "remove", "message", "Removes one of your own messages from starboard.")]
        [Parameter("Message", ParameterType.GuildSelfMessage, "ID or link to the starboard message (the repost in starboard, not the original message)")]
        [Comment("You can only remove your own messages. To get a message link, right click the message and select `Copy Message Link`.")]
        public async Task RemoveStarboardMessage(ICommand command)
        {
            var message = await command["Message"].AsGuildSelfMessage;
            var found = await _settings.Modify(command.GuildId, (StarboardSettings x) =>
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
                await command.Reply($"Removed your starred message `{message.Id}` from starboard in channel <#{message.Channel.Id}>.");
            }
            else
            {
                await command.Reply($"Can't find message `{message.Id}` in any starboard on this server. Please provide a valid message from a starboard channel (not the original message).");
            }
        }

        public void Dispose()
        {
            _client.ReactionAdded -= HandleReactionAdded;
            _client.ReactionRemoved -= HandleReactionRemoved;
            _client.MessageDeleted -= HandleMessageDeleted;
        }

        private Task HandleReactionAdded(Cacheable<IUserMessage, ulong> cachedMessage, ISocketMessageChannel channel, SocketReaction reaction)
        {
            TaskHelper.FireForget(async () =>
            {
                try
                {
                    if (reaction.UserId == _client.CurrentUser.Id)
                        return;

                    if (!(channel is ITextChannel textChannel))
                        return;

                    var settings = await _settings.Read<StarboardSettings>(textChannel.GuildId, false);
                    if (settings == null || settings.Starboards.Count <= 0)
                        return;
                    
                    foreach (var board in settings.Starboards)
                    {
                        TaskHelper.FireForget(async () =>
                        {
                            // Process only one reaction at a time for each starboard, so we don't have to deal with race conditions
                            using (await _processingMutex.ClaimAsync((textChannel.GuildId, board.Id)))
                            {
                                try
                                {
                                    await ProcessNewStar(board, textChannel, cachedMessage, reaction);
                                }
                                catch (Exception ex)
                                {
                                    _logger.WithScope(reaction).LogError(ex, "Failed to process new star in board {StarboardId}", board.Id);
                                }
                            }
                        });
                    }
                }
                catch (Exception ex)
                {
                    _logger.WithScope(reaction).LogError(ex, "Failed to process a potential new starboard reaction");
                }
            });

            return Task.CompletedTask;
        }

        private async Task ProcessNewStar(Starboard board, ITextChannel channel, Cacheable<IUserMessage, ulong> cachedMessage, SocketReaction reaction)
        {
            if (!board.Emojis.Contains(reaction.Emote.GetFullName()))
                return;

            if (board.ChannelsWhitelist.Count > 0 && !board.ChannelsWhitelist.Contains(channel.Id))
                return;

            var message = await cachedMessage.GetOrDownloadAsync();
            if (message == null)
                return;

            var logger = _logger.WithScope(message);

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
                    logger.LogInformation("Missing access to read reactions in channel");
                }
            }

            if (!board.AllowSelfStars)
                starrers.Remove(message.Author.Id);

            var entry = await _settings.Modify(channel.GuildId, (StarboardSettings s) =>
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
                // Post new
                if (!(await channel.Guild.GetCurrentUserAsync()).GetPermissions(channel).SendMessages)
                {
                    logger.LogInformation("Can't post starred message because of missing permissions in {TargetChannelName} ({TargetChannelId})", starChannel.Name, starChannel.Id);
                    return;
                }

                var attachments = await ProcessAttachments(message.Attachments);
                var built = await BuildStarMessage(message, channel, starrers.Count, board.Emojis.First(), attachments, board.Style);
                var starMessage = await _communicator.SendMessage(starChannel, built.Text, built.Embed);

                await _settings.Modify(channel.GuildId, (StarboardSettings s) =>
                {
                    var b = s.Starboards.FirstOrDefault(x => x.Id == board.Id);
                    if (b != null && b.StarredMessages.TryGetValue(message.Id, out var e))
                    {
                        e.StarboardMessage = starMessage.Single().Id;
                        e.Attachments = attachments;
                    }
                });

                logger.LogInformation("New starred message in board {StarboardId}", board.Id);
            }
            else
            {
                // Update
                var starMessage = await starChannel.GetMessageAsync(entry.StarboardMessage) as IUserMessage;
                if (starMessage == null)
                    return; // Probably got deleted from starboard

                var built = await BuildStarMessage(message, channel, starrers.Count, board.Emojis.First(), entry.Attachments, board.Style);
                await starMessage.ModifyAsync(x => 
                {
                    x.Content = built.Text;
                    x.Embed = built.Embed;
                });
            }
        }

        private Task HandleReactionRemoved(Cacheable<IUserMessage, ulong> cachedMessage, ISocketMessageChannel channel, SocketReaction reaction)
        {
            TaskHelper.FireForget(async () =>
            {
                try
                {
                    if (reaction.UserId == _client.CurrentUser.Id)
                        return;

                    if (!(channel is ITextChannel textChannel))
                        return;

                    var settings = await _settings.Read<StarboardSettings>(textChannel.GuildId, false);
                    if (settings == null || settings.Starboards.Count <= 0)
                        return;

                    foreach (var board in settings.Starboards)
                    {
                        TaskHelper.FireForget(async () =>
                        {
                            using (await _processingMutex.ClaimAsync((textChannel.GuildId, board.Id)))
                            {
                                try
                                {
                                    await ProcessRemovedStar(board, textChannel, cachedMessage, reaction);
                                }
                                catch (Exception ex)
                                {
                                    _logger.WithScope(reaction).LogError(ex, "Failed to process removed star in board {StarboardId}", board.Id);
                                }
                            }
                        });
                    }
                }
                catch (Exception ex)
                {
                    _logger.WithScope(reaction).LogError(ex, "Failed to process a potential removed starboard reaction");
                }
            });

            return Task.CompletedTask;
        }

        private async Task ProcessRemovedStar(Starboard board, ITextChannel channel, Cacheable<IUserMessage, ulong> cachedMessage, SocketReaction reaction)
        {
            if (!board.Emojis.Contains(reaction.Emote.GetFullName()))
                return;

            var message = await cachedMessage.GetOrDownloadAsync();
            if (message == null)
                return;

            var logger = _logger.WithScope(message);

            if ((!board.AllowSelfStars && message.Author.Id == reaction.UserId) || message.Author.IsBot)
                return;

            if (string.IsNullOrEmpty(message.Content) && message.Attachments.Count <= 0)
                return;

            var starrers = new HashSet<ulong>();
            foreach (var emoji in message.Reactions.Where(x => board.Emojis.Contains(x.Key.GetFullName())).Select(x => x.Key))
                starrers.UnionWith((await message.GetReactionUsersAsync(emoji, int.MaxValue).FlattenAsync()).Select(x => x.Id));

            if (!board.AllowSelfStars)
                starrers.Remove(message.Author.Id);

            var entry = await _settings.Modify(channel.GuildId, (StarboardSettings s) =>
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
                return; // Probably got deleted from starboard

            if (starrers.Count <= 0 && !board.KeepUnstarred)
            {
                try
                {
                    await starMessage.DeleteAsync();
                    logger.LogInformation("Removed unstarred message in board {StarboardId}", board.Id);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to delete unstarred message in board {StarboardId}", board.Id);
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
                        result.Add(await _urlShortener.ShortenAsync(a.Url));
                    else
                        result.Add(a.Url);
                }
                catch (Exception ex)
                {
                    result.Add(a.Url);
                    _logger.LogError(ex, "Failed to shorten URL {UrlString}", a.Url);
                }
            }

            return result;
        }

        private async Task<(string Text, Embed Embed)> BuildStarMessage(IUserMessage message, ITextChannel channel, int starCount, string emote, List<string> attachments, StarboardStyle style)
        {
            var author = await channel.Guild.GetUserAsync(message.Author.Id) ?? await _userFetcher.FetchGuildUserAsync(channel.GuildId, message.Author.Id);

            if (style == StarboardStyle.Embed)
            {
                var embed = message.Embeds.FirstOrDefault(x => x.Thumbnail.HasValue || x.Image.HasValue);
                var attachedImage = attachments.FirstOrDefault(x => _urlShortener.IsShortened(x));
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
                    footer: footer,
                    maxCaptionLength: int.MaxValue,
                    maxCaptionLines: int.MaxValue);

                var result = await DiscordHelpers.ReplaceMentions(messages.Single(), message.MentionedUserIds, message.MentionedRoleIds, channel.Guild);
                return (DiscordHelpers.EscapeMentions(result), null);
            }
        }

        private Task HandleMessageDeleted(Cacheable<IMessage, ulong> message, ISocketMessageChannel channel)
        {
            TaskHelper.FireForget(async () =>
            {
                try
                {
                    var textChannel = channel as ITextChannel;
                    if (textChannel == null)
                        return;

                    var guild = textChannel.Guild;

                    var settings = await _settings.Read<StarboardSettings>(guild.Id, false);
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
                                _logger.WithScope(channel, message.Id).LogInformation("Removed starboard message due to source deletion");
                            }
                            catch (Exception ex)
                            {
                                _logger.WithScope(channel, message.Id).LogError(ex, "Failed to remove starboard message with a deleted source");
                            }
                        }
                    }

                    if (boardIds.Any())
                    {
                        await _settings.Modify(guild.Id, (StarboardSettings s) =>
                        {
                            foreach (var board in s.Starboards.Where(x => boardIds.Contains(x.Id)))
                                board.StarredMessages.Remove(message.Id);
                        });
                    }
                }
                catch (Exception ex)
                {
                    _logger.WithScope(channel, message.Id).LogError(ex, "Failed to process deleted message");
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
                _logger.LogError(ex, "Failed to resolve instagram thumbnail {UrlString}", baseUrl);
            }

            return null;
        }
    }
}
