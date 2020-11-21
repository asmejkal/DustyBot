using Discord;
using System.Threading.Tasks;
using DustyBot.Framework.Modules;
using DustyBot.Framework.Commands;
using DustyBot.Framework.Communication;
using DustyBot.Framework.Logging;
using System.Linq;
using System;
using Discord.WebSocket;
using System.Net;
using System.IO;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using DustyBot.Framework.Exceptions;
using DustyBot.Helpers;
using DustyBot.Settings;
using System.IO.Compression;
using System.Text.RegularExpressions;
using System.Collections.Concurrent;
using DustyBot.Definitions;
using DustyBot.Database.Services;
using DustyBot.Core.Async;
using Newtonsoft.Json;

namespace DustyBot.Modules
{
    [Module("Instagram", "Helps with Instagram post previews.")]
    class InstagramModule : Module
    {
        private const string PostRegexString = @"http[s]:\/\/(?:www\.)?instagram\.com\/(?:p|tv)\/([^/?#>\s]+)";
        private const string QuotedPostRegexString = @"<http[s]:\/\/(?:www\.)?instagram\.com\/(?:p|tv)\/([^/?#>\s]+)[^\s]*>";
        private static readonly Regex PostRegex = new Regex(PostRegexString, RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex QuotedPostRegex = new Regex(QuotedPostRegexString, RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private const int LinkPerPostLimit = 8;
        private static readonly TimeSpan PreviewDeleteWindow = TimeSpan.FromSeconds(30);

        private ICommunicator Communicator { get; }
        private ISettingsService Settings { get; }
        private ILogger Logger { get; }
        private BotConfig Config { get; }
        private IUrlShortener UrlShortener { get; }

        private ConcurrentDictionary<ulong, (ulong AuthorId, IEnumerable<IUserMessage> Messages)> Previews = 
            new ConcurrentDictionary<ulong, (ulong AuthorId, IEnumerable<IUserMessage> Messages)>();

        public InstagramModule(ICommunicator communicator, ISettingsService settings, ILogger logger, BotConfig config, IUrlShortener urlShortener)
        {
            Communicator = communicator;
            Settings = settings;
            Logger = logger;
            Config = config;
            UrlShortener = urlShortener;
        }

        [Command("ig", "Shows a preview of one or more Instagram posts.")]
        [Alias("instagram")]
        [Parameter("Style", "^(none|embed|text)$", ParameterType.String, ParameterFlags.Optional, "use `embed` or `text` (displays text with all images)")]
        [Parameter("Url", ParameterType.String, ParameterFlags.Remainder, "one or more links (max 8)")]
        [Comment("You can set the default style with `ig set style`.\n\nGive the bot Manage Messages permission to also delete the original embeds (or wrap your links in `< >` braces).")]
        public async Task Instagram(ICommand command)
        {
            var style = InstagramPreviewStyle.None;
            if (command["Style"].HasValue)
            {
                if (!Enum.TryParse(command["Style"], true, out style) || !Enum.IsDefined(typeof(InstagramPreviewStyle), style))
                    throw new IncorrectParametersCommandException("Unknown style.");
            }
            else
            {
                var settings = await Settings.ReadUser<UserMediaSettings>(command.Author.Id, false);
                if (settings != null && settings.InstagramPreviewStyle != InstagramPreviewStyle.None)
                    style = settings.InstagramPreviewStyle;
            }

            var matches = PostRegex.Matches(command["Url"]);
            if (!matches.Any())
                throw new IncorrectParametersCommandException("Not a valid post link.");

            var unquoted = matches.Count > QuotedPostRegex.Matches(command["Url"]).Count;
            Task suppressionTask = null;
            if (unquoted && (await command.Guild.GetCurrentUserAsync()).GetPermissions((ITextChannel)command.Channel).ManageMessages)
                suppressionTask = TrySuppressEmbed(command.Message);

            foreach (var id in matches.Select(x => x.Groups[1].Value).Distinct().Take(LinkPerPostLimit))
            {
                try
                {
                    await PostPreview(id, command.Channel, style, command.Author.Id);
                }
                catch (Exception ex)
                {
                    await Logger.Log(new LogMessage(LogSeverity.Info, "Instagram", $"Failed to create preview for post {id} and user {command.Author.Username} ({command.Author.Id}) on {command.Guild.Name}", ex));

                    try
                    {
                        await command.ReplyError(Communicator, "Failed to create preview.");
                    }
                    catch (Exception)
                    {
                    }
                }
            }

            if (suppressionTask != null)
                await suppressionTask;
        }

        [Command("ig", "toggle", "auto", "Automatically create previews for Instagram links you post.", CommandFlags.DirectMessageAllow)]
        [Alias("instagram", "toggle", "auto")]
        [Comment("Use again to disable. Max number of links per post is 8.\n\nGive the bot Manage Messages permission to also delete the original embeds (or wrap your links in `< >` braces).")]
        public async Task ToggleInstagramAuto(ICommand command)
        {
            var current = await Settings.ModifyUser<UserMediaSettings, bool>(command.Author.Id, x => x.InstagramAutoPreviews = !x.InstagramAutoPreviews);
            await command.ReplySuccess(Communicator, current ? "Previews will now be created automatically for your posts on all servers!" : "Previews will no longer be created automatically for your posts (unless it's toggled on by server admins).");
        }

        [Command("ig", "set", "style", "Sets your personal preferred style of Instagram previews.", CommandFlags.DirectMessageAllow)]
        [Alias("instagram", "set", "style")]
        [Parameter("Style", ParameterType.String, "use `embed` or `text`")]
        [Comment("The style you set will become your default style for the `ig` command. \n\nThe `embed` style looks like the usual Discord embeds. The `text` style displays all images and videos.")]
        public async Task SetInstagramStyle(ICommand command)
        {
            if (!Enum.TryParse<InstagramPreviewStyle>(command["Style"], true, out var style) || !Enum.IsDefined(typeof(InstagramPreviewStyle), style))
                throw new IncorrectParametersCommandException("Unknown style.");

            await Settings.ModifyUser<UserMediaSettings>(command.Author.Id, x => x.InstagramPreviewStyle = style);
            await command.ReplySuccess(Communicator, $"Your personal preferred style for Instagram previews is now `{command["Style"]}`!");
        }

        [Command("ig", "toggle", "server", "auto", "Automatically create previews for Instagram links posted on your server.", CommandFlags.DirectMessageAllow)]
        [Alias("instagram", "toggle", "server", "auto")]
        [Permissions(GuildPermission.Administrator)]
        [Comment("Use again to disable. Max number of links per post is 8.\n\nGive the bot Manage Messages permission to also delete the original embeds (or wrap your links in `< >` braces).")]
        public async Task ToggleInstagramServerAuto(ICommand command)
        {
            var current = await Settings.Modify<MediaSettings, bool>(command.GuildId, x => x.InstagramAutoPreviews = !x.InstagramAutoPreviews);
            await command.ReplySuccess(Communicator, current ? "Previews will now be created automatically for Instagram posts from everyone on this server! \nTo enable automatic previews only for you personally, use `ig toggle auto`." : "Previews will no longer be created automatically for Instagram posts. \nAnyone can still toggle automatic previews only for themselves with `ig toggle auto` or use the `ig` command.");
        }

        [Command("ig", "set", "server", "style", "Sets the default style of automatic previews on your server (can be overriden by users' personal settings).", CommandFlags.DirectMessageAllow)]
        [Alias("instagram", "set", "server", "style")]
        [Permissions(GuildPermission.Administrator)]
        [Parameter("Style", ParameterType.String, "use `embed` or `text`")]
        [Comment("The style can be overriden by users' personal settings. \n\nThe `embed` style looks like the usual Discord embeds. The `text` style displays all images and videos.")]
        public async Task SetInstagramServerStyle(ICommand command)
        {
            if (!Enum.TryParse<InstagramPreviewStyle>(command["Style"], true, out var style) || !Enum.IsDefined(typeof(InstagramPreviewStyle), style))
                throw new IncorrectParametersCommandException("Unknown style.");

            await Settings.Modify<MediaSettings>(command.GuildId, x => x.InstagramPreviewStyle = style);
            await command.ReplySuccess(Communicator, $"The default style for automatic Instagram previews on this server is now `{command["Style"]}`! This can be overriden by users' personal settings.");
        }

        public override Task OnMessageReceived(SocketMessage message)
        {
            TaskHelper.FireForget(async () =>
            {
                try
                {
                    var channel = message.Channel as ITextChannel;
                    if (channel == null)
                        return;

                    var user = message.Author as IGuildUser;
                    if (user == null)
                        return;

                    if (user.IsBot)
                        return;

                    var userSettings = await Settings.ReadUser<UserMediaSettings>(user.Id, false);
                    InstagramPreviewStyle style;
                    if (userSettings == null || !userSettings.InstagramAutoPreviews)
                    {
                        var serverSettings = await Settings.Read<MediaSettings>(channel.Guild.Id, false);
                        if (serverSettings == null || !serverSettings.InstagramAutoPreviews)
                            return;

                        if (userSettings == null || userSettings.InstagramPreviewStyle == InstagramPreviewStyle.None)
                            style = serverSettings.InstagramPreviewStyle;
                        else
                            style = userSettings.InstagramPreviewStyle;
                    }
                    else
                        style = userSettings.InstagramPreviewStyle;

                    var matches = PostRegex.Matches(message.Content);
                    if (!matches.Any())
                        return;

                    var botSettings = await Settings.Read<BotSettings>(channel.GuildId, createIfNeeded: false);
                    var commandMatch = SocketCommand.FindLongestMatch(message.Content, botSettings?.CommandPrefix ?? Config.DefaultCommandPrefix, new[] { HandledCommands.Single(x => x.PrimaryUsage.InvokeUsage == "ig") });
                    if (commandMatch != null)
                        return; // Don't create duplicate previews from the ig command

                    var unquoted = matches.Count > QuotedPostRegex.Matches(message.Content).Count;

                    var ids = matches.Select(x => x.Groups[1].Value).Distinct().Take(LinkPerPostLimit).ToList();
                    await Logger.Log(new LogMessage(LogSeverity.Info, "Instagram", $"Creating previews of {ids.Count} posts{(unquoted ? " [unquoted]" : "")} ({string.Join(" ", ids)}) for user {message.Author.Username} ({message.Author.Id}) on {channel.Guild.Name}"));

                    Task suppressionTask = null;
                    if (unquoted)
                    {
                        suppressionTask = TrySuppressEmbed((IUserMessage)message);
                    }

                    foreach (var id in ids)
                    {
                        try
                        {
                            await PostPreview(id, message.Channel, style, user.Id);
                        }
                        catch (Exception ex)
                        {
                            await Logger.Log(new LogMessage(LogSeverity.Info, "Instagram", $"Failed to create preview for post {id} and user {message.Author.Username} ({message.Author.Id}) on {channel.Guild.Name}", ex));
                        }
                    }

                    if (suppressionTask != null)
                        await suppressionTask;
                }
                catch (Exception ex)
                {
                    await Logger.Log(new LogMessage(LogSeverity.Error, "Instagram", "Failed to process message", ex));
                }
            });

            return Task.CompletedTask;
        }

        public override Task OnReactionAdded(Cacheable<IUserMessage, ulong> message, ISocketMessageChannel channel, SocketReaction reaction)
        {
            TaskHelper.FireForget(async () =>
            {
                try
                {
                    if (!reaction.User.IsSpecified || reaction.User.Value.IsBot)
                        return;

                    if (reaction.Emote.Name != EmoteConstants.ClickToRemove.Name)
                        return;

                    if (!Previews.TryGetValue(message.Id, out var context))
                        return;

                    if (reaction.User.Value.Id != context.AuthorId)
                        return;

                    try
                    {
                        await Logger.Log(new LogMessage(LogSeverity.Info, "Instagram", $"Deleting post preview for user {reaction.User.Value.Username} ({reaction.User.Value.Id})."));
                        foreach (var m in context.Messages)
                            await m.DeleteAsync();
                    }
                    catch (NullReferenceException)
                    {
                        return; // Message deleted
                    }
                    finally
                    {
                        Previews.TryRemove(message.Id, out _);
                    }
                }
                catch (Exception ex)
                {
                    await Logger.Log(new LogMessage(LogSeverity.Error, "Instagram", "Failed to process preview delete reaction.", ex));
                }
            });

            return Task.CompletedTask;
        }

        private async Task PostPreview(string id, IMessageChannel channel, InstagramPreviewStyle style, ulong authorId)
        {
            if (style == InstagramPreviewStyle.None)
                style = InstagramPreviewStyle.Embed;

            var request = WebRequest.CreateHttp($"https://www.instagram.com/graphql/query/?query_hash=505f2f2dfcfce5b99cb7ac4155cbf299&variables=%7B%22shortcode%22%3A%22{id}%22%2C%22child_comment_count%22%3A3%2C%22fetch_comment_count%22%3A40%2C%22parent_comment_count%22%3A24%2C%22has_threaded_comments%22%3Atrue%7D");
            request.Accept = "*/*";
            request.UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/83.0.4103.106 Safari/537.36";
            request.Referer = "https://www.instagram.com/p/{id}/";
            request.Headers.Add("Accept-Encoding", "gzip, deflate, br");

            if (!string.IsNullOrEmpty(Config.RotatingProxyUrl))
            {
                var proxyUrl = new UriBuilder(Config.RotatingProxyUrl);
                var proxy = new WebProxy(proxyUrl.Uri);
                if (!string.IsNullOrEmpty(proxyUrl.UserName) && !string.IsNullOrEmpty(proxyUrl.Password))
                    proxy.Credentials = new NetworkCredential(proxyUrl.UserName, proxyUrl.Password);

                request.Proxy = proxy;
            }

            using (var response = (HttpWebResponse)await request.GetResponseAsync())
            using (var gzipStream = new GZipStream(response.GetResponseStream(), CompressionMode.Decompress))
            using (var reader = new StreamReader(gzipStream))
            {
                var json = await reader.ReadToEndAsync();
                var root = JObject.Parse(json);
                var mediaRoot = root["data"]?["shortcode_media"];

                if (mediaRoot == null)
                    throw new CommandException("Failed to retrieve data.");

                IEnumerable<JToken> mediaTokens;
                if (mediaRoot["edge_sidecar_to_children"]?["edges"] is JArray subMedia)
                    mediaTokens = subMedia.Select(x => x["node"]).Where(x => x != null);
                else
                    mediaTokens = new[] { mediaRoot };

                var media = mediaTokens.Select(x => new
                {
                    Url = (string)((bool)x["is_video"] ? x["video_url"] : x["display_url"]),
                    IsVideo = (bool)x["is_video"],
                    Thumbnail = (string)x["display_url"]
                });

                var postUrl = $"https://instagram.com/p/{id}/";
                var username = (string)mediaRoot["owner"]?["username"];
                var fullname = (string)mediaRoot["owner"]?["full_name"];
                var caption = (string)mediaRoot["edge_media_to_caption"]?["edges"]?.FirstOrDefault()?["node"]?["text"];
                var displayName = !string.IsNullOrWhiteSpace(fullname) ? fullname : username;

                var sent = new List<IUserMessage>();
                if (style == InstagramPreviewStyle.Embed)
                {
                    var thumbnail = (string)mediaTokens.FirstOrDefault()?["display_url"];
                    var avatar = (string)mediaRoot["owner"]?["profile_pic_url"];
                    var timestamp = DateTimeOffset.FromUnixTimeSeconds((int)mediaRoot["taken_at_timestamp"]);

                    var embed = PrintHelpers.BuildMediaEmbed(
                        displayName, 
                        await Task.WhenAll(media.Take(PrintHelpers.EmbedMediaCutoff).Select(x => UrlShortener.ShortenAsync(x.Url))), 
                        url: postUrl, 
                        caption: caption, 
                        thumbnail: media.Any() ? new PrintHelpers.Thumbnail(media.First().Thumbnail, media.First().IsVideo, media.First().Url) : null,
                        footer: $"@{username}", 
                        timestamp: timestamp, 
                        iconUrl: avatar);

                    sent.Add(await channel.SendMessageAsync(embed: embed.Build()));
                }
                else
                {
                    var mediaUrls = new List<string>();
                    foreach (var item in media)
                        mediaUrls.Add(item.IsVideo ? item.Url : await UrlShortener.ShortenAsync(item.Url));

                    var messages = PrintHelpers.BuildMediaText(
                        $"<:ig:725481240245043220> **{displayName}**",
                        mediaUrls,
                        url: postUrl, 
                        caption: caption);

                    foreach (var m in messages)
                        sent.AddRange(await Communicator.SendMessage(channel, m));
                }

                Previews.TryAdd(sent.Last().Id, (authorId, sent));
                try
                {
                    await sent.Last().AddReactionAsync(EmoteConstants.ClickToRemove);
                }
                catch (Discord.Net.HttpException ex) when (ex.HttpCode == HttpStatusCode.Forbidden)
                {
                    await Logger.Log(new LogMessage(LogSeverity.Info, "Instagram", $"Missing permissions to add reaction for post id {id} on {(channel as IGuildChannel)?.Guild.Name}", ex));
                }

                TaskHelper.FireForget(async () =>
                {
                    try
                    {
                        await Task.Delay(PreviewDeleteWindow);
                        if (Previews.TryRemove(sent.Last().Id, out _))
                            await sent.Last().RemoveReactionAsync(EmoteConstants.ClickToRemove, sent.Last().Author);
                    }
                    catch (Discord.Net.HttpException dex) when (dex.HttpCode == HttpStatusCode.NotFound)
                    {
                        // Message probably deleted manually
                    }
                    catch (Exception ex)
                    {
                        await Logger.Log(new LogMessage(LogSeverity.Error, "Instagram", "Failed to remove delete reaction from message", ex));
                    }
                });
            }
        }

        private async Task TrySuppressEmbed(IUserMessage message)
        {
            try
            {
                var channel = (ITextChannel)message.Channel;
                for (int i = 0; i < 12; ++i)
                {
                    if (message.Embeds.Any() && message.Embeds.All(x => x.Title == "Login • Instagram" || x.Footer?.Text == "Instagram"))
                    {
                        if ((await channel.Guild.GetCurrentUserAsync()).GetPermissions(channel).ManageMessages)
                        {
                            await Logger.Log(new LogMessage(LogSeverity.Info, "Instagram", $"Deleting default embed with attempt {i + 1} for {message.Author.Username} ({message.Author.Id}) on {(message.Channel as IGuildChannel)?.Guild.Name}"));
                            await message.ModifySuppressionAsync(true);
                        }
                        else
                        {
                            await Logger.Log(new LogMessage(LogSeverity.Info, "Instagram", $"Missing permissions to delete default embed for {message.Author.Username} ({message.Author.Id}) on {(message.Channel as IGuildChannel)?.Guild.Name}"));
                        }
                        
                        return;
                    }

                    await Task.Delay(500); // Discord can take a while with the embed
                }
            }
            catch (Discord.Net.HttpException ex) when (ex.HttpCode == HttpStatusCode.Forbidden)
            {
                await Logger.Log(new LogMessage(LogSeverity.Info, "Instagram", $"Missing permissions to delete default embed for {message.Author.Username} ({message.Author.Id}) on {(message.Channel as IGuildChannel)?.Guild.Name}", ex));
            }
            catch (Exception ex)
            {
                await Logger.Log(new LogMessage(LogSeverity.Error, "Instagram", $"Failed to delete default embed for {message.Author.Username} ({message.Author.Id}) on {(message.Channel as IGuildChannel)?.Guild.Name}", ex));
            }
        }
    }
}
