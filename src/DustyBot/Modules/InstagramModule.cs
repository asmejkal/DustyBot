using Discord;
using System.Threading.Tasks;
using DustyBot.Framework.Modules;
using DustyBot.Framework.Commands;
using DustyBot.Framework.Communication;
using DustyBot.Framework.Settings;
using DustyBot.Framework.Logging;
using System.Linq;
using System;
using Discord.WebSocket;
using DustyBot.Framework.Utility;
using System.Net;
using System.IO;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using DustyBot.Framework.Exceptions;
using DustyBot.Helpers;
using DustyBot.Settings;
using System.IO.Compression;
using System.Text.RegularExpressions;

namespace DustyBot.Modules
{
    [Module("Instagram (beta)", "Helps with Instagram post previews.")]
    class InstagramModule : Module
    {
        private const string PostRegexString = @"http[s]:\/\/(?:www\.)?instagram\.com\/(?:p|tv)\/([^/?#>\s]+)";
        private static readonly Regex PostRegex = new Regex(PostRegexString, RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private const int LinkPerPostLimit = 8;

        public ICommunicator Communicator { get; private set; }
        public ISettingsProvider Settings { get; private set; }
        public ILogger Logger { get; private set; }
        public BotConfig Config { get; }

        public InstagramModule(ICommunicator communicator, ISettingsProvider settings, ILogger logger, BotConfig config)
        {
            Communicator = communicator;
            Settings = settings;
            Logger = logger;
            Config = config;
        }

        [Command("ig", "Shows a preview of one or more Instagram posts.")]
        [Alias("instagram")]
        [Parameter("Style", "^(none|embed|text)$", ParameterType.String, ParameterFlags.Optional, "use `embed` or `text` (displays text with all images)")]
        [Parameter("Url", ParameterType.String, ParameterFlags.Remainder, "one or more links (max 8)")]
        [Comment("You can set the default style with `ig set style`.")]
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
                return;

            foreach (var id in matches.Select(x => x.Groups[1].Value).Distinct().Take(LinkPerPostLimit))
            {
                try
                {
                    await PostPreview(id, command.Channel, style);
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
        }

        [Command("ig", "toggle", "auto", "Automatically create previews for Instagram links you post.", CommandFlags.DirectMessageAllow)]
        [Alias("instagram", "toggle", "auto")]
        [Comment("Use again to disable. Max number of links per post is 8.")]
        public async Task ToggleInstagramAuto(ICommand command)
        {
            var current = await Settings.ModifyUser<UserMediaSettings, bool>(command.Author.Id, x => x.InstagramAutoPreviews = !x.InstagramAutoPreviews);
            await command.ReplySuccess(Communicator, current ? "Previews will now be created automatically for your posts on all servers!" : "Previews will no longer be created automatically for your posts.");
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
        [Comment("Use again to disable. Max number of links per post is 8.")]
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

                    var commandMatch = SocketCommand.FindLongestMatch(message.Content, Config.CommandPrefix, new[] { HandledCommands.Single(x => x.PrimaryUsage.InvokeUsage == "ig") });
                    if (commandMatch != null)
                        return; // Don't create duplicate previews from the ig command

                    var ids = matches.Select(x => x.Groups[1].Value).Distinct().Take(LinkPerPostLimit).ToList();
                    await Logger.Log(new LogMessage(LogSeverity.Info, "Instagram", $"Creating previews of {ids.Count} posts ({string.Join(" ", ids)}) for user {message.Author.Username} ({message.Author.Id}) on {channel.Guild.Name}"));

                    foreach (var id in ids)
                    {
                        try
                        {
                            await PostPreview(id, message.Channel, style);
                        }
                        catch (Exception ex)
                        {
                            await Logger.Log(new LogMessage(LogSeverity.Info, "Instagram", $"Failed to create preview for post {id} and user {message.Author.Username} ({message.Author.Id}) on {channel.Guild.Name}", ex));
                        }
                    }
                }
                catch (Exception ex)
                {
                    await Logger.Log(new LogMessage(LogSeverity.Error, "Instagram", "Failed to process message", ex));
                }
            });

            return Task.CompletedTask;
        }

        private async Task PostPreview(string id, IMessageChannel channel, InstagramPreviewStyle style)
        {
            if (style == InstagramPreviewStyle.None)
                style = InstagramPreviewStyle.Embed;

            var request = WebRequest.CreateHttp($"https://www.instagram.com/graphql/query/?query_hash=505f2f2dfcfce5b99cb7ac4155cbf299&variables=%7B%22shortcode%22%3A%22{id}%22%2C%22child_comment_count%22%3A3%2C%22fetch_comment_count%22%3A40%2C%22parent_comment_count%22%3A24%2C%22has_threaded_comments%22%3Atrue%7D");
            request.Accept = "*/*";
            request.UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/83.0.4103.106 Safari/537.36";
            request.Referer = "https://www.instagram.com/p/{id}/";
            request.Headers.Add("Accept-Encoding", "gzip, deflate, br");

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

                var media = mediaTokens.Select(x => new PrintHelpers.Media((bool)x["is_video"], (string)((bool)x["is_video"] ? x["video_url"] : x["display_url"]), (string)x["display_url"]));

                var postUrl = $"https://instagram.com/p/{id}/";
                var username = (string)mediaRoot["owner"]?["username"];
                var fullname = (string)mediaRoot["owner"]?["full_name"];
                var caption = (string)mediaRoot["edge_media_to_caption"]?["edges"]?.FirstOrDefault()?["node"]?["text"];
                
                if (style == InstagramPreviewStyle.Embed)
                {
                    var avatar = (string)mediaRoot["owner"]?["profile_pic_url"];
                    var timestamp = DateTimeOffset.FromUnixTimeSeconds((int)mediaRoot["taken_at_timestamp"]);

                    var embed = await PrintHelpers.BuildMediaEmbed(fullname, media, postUrl, Config.ShortenerKey, caption, $"@{username}", timestamp, avatar);
                    await channel.SendMessageAsync(embed: embed.Build());
                }
                else
                {
                    var messages = await PrintHelpers.BuildMediaText($"<:ig:725481240245043220> **{fullname}**", media, Config.ShortenerKey, url: postUrl, caption: caption);
                    foreach (var message in messages)
                        await Communicator.SendMessage(channel, message);
                }
            }
        }
    }
}
