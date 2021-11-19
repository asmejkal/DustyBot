using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using DustyBot.Core.Parsing;
using DustyBot.Database.Mongo.Collections;
using DustyBot.Database.Services;
using DustyBot.Framework.Commands;
using DustyBot.Framework.Communication;
using DustyBot.Framework.Modules.Attributes;
using DustyBot.Framework.Reflection;
using DustyBot.Framework.Utility;
using DustyBot.Service.Helpers;
using Microsoft.Extensions.Logging;

namespace DustyBot.Service.Modules
{
    [Module("Mod", "Helps with server administration.")]
    internal sealed class AdministrationModule
    {
        private static readonly IReadOnlyList<string> SampleUsernames = new[]
        {
            "StreetTrader34",
            "Cue__True54",
            "ParvidensInfused21",
            "Thus Guideline20",
            "Gliddery+Pratincola95",
            "FlirdsDazzling69",
            "Recruit Wavy21",
            "TongueQuail64",
            "DanoneAll89",
            "KarstenMewish16",
            "Ratsbane Flair",
            "StudSkall",
            "ReachSarcastic",
            "KlubboFinnvik",
            "Worm Blow",
            "Lindved2Astern",
            "ButterburSkir",
            "Savings@Lewy",
            "DilkLarboard!"
        };

        private readonly ICommunicator _communicator;
        private readonly ISettingsService _settings;
        private readonly BaseSocketClient _client;
        private readonly IUserFetcher _userFetcher;
        private readonly IFrameworkReflector _frameworkReflector;
        private readonly HelpBuilder _helpBuilder;

        public AdministrationModule(
            ICommunicator communicator, 
            ISettingsService settings, 
            BaseSocketClient client, 
            IUserFetcher userFetcher, 
            IFrameworkReflector frameworkReflector,
            HelpBuilder helpBuilder)
        {
            _communicator = communicator;
            _settings = settings;
            _client = client;
            _userFetcher = userFetcher;
            _frameworkReflector = frameworkReflector;
            _helpBuilder = helpBuilder;
        }

        [Command("administration", "help", "Shows help for this module.", CommandFlags.Hidden)]
        [Alias("admin", "help")]
        [IgnoreParameters]
        public async Task Help(ICommand command)
        {
            await command.Reply(_helpBuilder.GetModuleHelpEmbed(_frameworkReflector.GetModuleInfo(GetType()).Name, command.Prefix));
        }

        [Command("say", "Sends a text message.")]
        [Permissions(GuildPermission.ManageMessages)]
        [Parameter("TargetChannel", ParameterType.TextChannel, "a channel that will receive the message")]
        [Parameter("Message", ParameterType.String, ParameterFlags.Remainder | ParameterFlags.Optional, "the message to be sent (you may also include one attachment)")]
        public async Task Say(ICommand command)
        {
            var message = command["Message"].AsString ?? "";
            var channel = command["TargetChannel"].AsTextChannel;

            // This is a mods-only command, but to prevent permission escalation, check
            // if there's any non-mentionable role and if the sender has a mention everyone perm
            var nonMentionableRoles = command.Message.MentionedRoleIds.Where(x => !command.Guild.GetRole(x)?.IsMentionable ?? false).ToList();
            var replaceRoleMentions = (message.ContainsEveryonePings() || nonMentionableRoles.Any()) &&
                !((IGuildUser)command.Author).GetPermissions(channel).MentionEveryone;

            if (replaceRoleMentions)
            {
                message = DiscordHelpers.ReplaceRoleMentions(message, nonMentionableRoles, command.Guild)
                    .Sanitise(allowRoleMentions: true);
            }

            if (command.Message.Attachments.Count <= 0)
            {
                if (string.IsNullOrEmpty(command["Message"]))
                    throw new Framework.Exceptions.IncorrectParametersCommandException("Specify a message or an attachment.");

                await channel.SendMessageAsync(message);
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

                    await channel.SendFileAsync(memStream, attachment.Filename, message);
                }
            }

            if (command["TargetChannel"].AsTextChannel.Id != command.Message.Channel.Id)
                await command.ReplySuccess("Message sent!" + (replaceRoleMentions ? " To mention roles, @here, or @everyone you must have the Mention Everyone permission." : ""));
        }

        [Command("say", "embed", "Sends an embed message.")]
        [Permissions(GuildPermission.ManageMessages)]
        [Parameter("TargetChannel", ParameterType.TextChannel, "a channel that will receive the message")]
        [Parameter("EmbedDescription", ParameterType.String, ParameterFlags.Remainder, "description of the embed, see below for info")]
        [Comment("Example of the embed description:```Title: your title\n\nAuthor: author name (a sub-title)\n\nAuthor Link: link to make the author name clickable\n\nAuthor Icon: image link to show a small icon next to author\n\nImage: image link to show an image at the bottom\n\nThumbnail: image link to show a small image at the top right\n\nColor: hex code of a color, e.g. #91e687\n\nDescription: description text\nwhich can be on multiple lines\n\nFooter: your footer text\n\nFooter Icon: image link to show a small icon in the footer\n\nField (title of a field): text of a field\nwhich can also be on multiple lines\n\nField (another field's title): another field's text\n\nInline Field (title of an inlined field): text of an inlined field```There can be multiple fields.\nEverything except for description is optional.")]
        public async Task SayEmbed(ICommand command)
        {
            var specification = (string)command["EmbedDescription"];
            var channel = command["TargetChannel"].AsTextChannel;

            if (!TryBuildEmbedFromSpecification(specification, out var embed, out var error))
            {
                await command.ReplyError(error);
                return;
            }

            await channel.SendMessageAsync(embed: embed.Build());

            if (channel.Id != command.Channel.Id)
                await command.ReplySuccess("Message sent!");
        }

        [Command("read", "Reads the content and formatting of a specified message.")]
        [Permissions(GuildPermission.ManageMessages)]
        [Parameter("MessageId", ParameterType.GuildUserMessage, ParameterFlags.Remainder, "ID or message link")]
        public async Task Read(ICommand command)
        {
            var message = await command["MessageId"].AsGuildUserMessage;
            var permissions = ((IGuildUser)command.Author).GetPermissions((IGuildChannel)message.Channel);
            if (!permissions.ViewChannel || !permissions.ReadMessageHistory)
            {
                await command.ReplyError("You don't have a permission to read the message history in this channel.");
                return;
            }

            using (var stream = new MemoryStream())
            using (var writer = new StreamWriter(stream))
            {
                writer.Write(message.Content);
                writer.Flush();
                stream.Position = 0;

                await command.Channel.SendFileAsync(stream, $"Message-{command.Guild.Name}-{message.Id}.txt");
            }
        }

        [Command("read", "embed", "Reads the description of an embed.")]
        [Permissions(GuildPermission.ManageMessages)]
        [Parameter("MessageId", ParameterType.GuildUserMessage, ParameterFlags.Remainder, "ID or message link")]
        public async Task ReadEmbed(ICommand command)
        {
            var message = await command["MessageId"].AsGuildUserMessage;
            var permissions = ((IGuildUser)command.Author).GetPermissions((IGuildChannel)message.Channel);
            if (!permissions.ViewChannel || !permissions.ReadMessageHistory)
            {
                await command.ReplyError("You don't have a permission to read the message history in this channel.");
                return;
            }

            var embed = message.Embeds.FirstOrDefault();
            if (embed == null)
            {
                await command.ReplyError("This message does not contain an embed.");
                return;
            }

            var description = new StringBuilder();
            if (!string.IsNullOrEmpty(embed.Title))
                description.AppendLine($"Title: {embed.Title}");

            if (embed.Author != null && !string.IsNullOrEmpty(embed.Author.Value.Name))
            {
                description.AppendLine($"Author: {embed.Author.Value.Name}");
                
                if (!string.IsNullOrEmpty(embed.Author.Value.Url))
                    description.AppendLine($"Author Link: {embed.Author.Value.Url}");

                if (!string.IsNullOrEmpty(embed.Author.Value.IconUrl))
                    description.AppendLine($"Author Icon: {embed.Author.Value.IconUrl}");
            }

            if (!string.IsNullOrEmpty(embed.Image?.Url))
                description.AppendLine($"Image: {embed.Image.Value.Url}");

            if (!string.IsNullOrEmpty(embed.Thumbnail?.Url))
                description.AppendLine($"Thumbnail: {embed.Thumbnail.Value.Url}");

            if (embed.Color.HasValue)
                description.AppendLine($"Color: {embed.Color.Value}");

            description.AppendLine($"Description: {embed.Description}");

            if (embed.Footer != null && !string.IsNullOrEmpty(embed.Footer.Value.Text))
            {
                description.AppendLine($"Footer: {embed.Footer.Value.Text}");

                if (!string.IsNullOrEmpty(embed.Footer.Value.IconUrl))
                    description.AppendLine($"Footer Icon: {embed.Footer.Value.IconUrl}");
            }

            foreach (var field in embed.Fields)
            {
                if (field.Inline)
                    description.AppendLine($"Inline Field ({field.Name}): {field.Value}");
                else
                    description.AppendLine($"Field ({field.Name}): {field.Value}");
            }

            using (var stream = new MemoryStream())
            using (var writer = new StreamWriter(stream))
            {
                var result = description.ToString();
                writer.Write(result);
                writer.Flush();
                stream.Position = 0;

                await command.Channel.SendFileAsync(stream, $"Embed-{command.Guild.Name}-{message.Id}.txt", text: result);
            }
        }

        [Command("edit", "Edits a message sent by the say command.")]
        [Permissions(GuildPermission.ManageMessages)]
        [Parameter("MessageId", ParameterType.GuildSelfMessage, "a message previously sent by the `say` command")]
        [Parameter("Message", ParameterType.String, ParameterFlags.Remainder, "the message to send")]
        public async Task Edit(ICommand command)
        {
            var message = await command[0].AsGuildSelfMessage;
            await message.ModifyAsync(x => x.Content = command["Message"].AsString);
            await command.ReplySuccess("Message edited.");
        }

        [Command("edit", "embed", "Edits an embed message.")]
        [Permissions(GuildPermission.ManageMessages)]
        [Parameter("MessageId", ParameterType.GuildSelfMessage, "an ID or link to the edited message")]
        [Parameter("EmbedDescription", ParameterType.String, ParameterFlags.Remainder, "description of the embed, see below for info")]
        [Comment("Example of the embed description:```Title: your title\n\nAuthor: author name (a sub-title)\n\nAuthor Link: link to make the author name clickable\n\nAuthor Icon: image link to show a small icon next to author\n\nImage: image link to show an image at the bottom\n\nThumbnail: image link to show a small image at the top right\n\nColor: hex code of a color, e.g. #91e687\n\nDescription: description text\nwhich can be on multiple lines\n\nFooter: your footer text\n\nFooter Icon: image link to show a small icon in the footer\n\nField (title of a field): text of a field\nwhich can also be on multiple lines\n\nField (another field's title): another field's text\n\nInline Field (title of an inlined field): text of an inlined field```There can be multiple fields.\nEverything except for description is optional.")]
        public async Task EditEmbed(ICommand command)
        {
            var specification = (string)command["EmbedDescription"];
            if (!TryBuildEmbedFromSpecification(specification, out var embed, out var error))
            {
                await command.ReplyError(error);
                return;
            }

            var message = await command["MessageId"].AsGuildSelfMessage;
            await message.ModifyAsync(x => x.Embed = embed.Build());

            await command.ReplySuccess("Embed edited!");
        }

        [Command("mute", "Mutes a server member.")]
        [Permissions(GuildPermission.ManageRoles), BotPermissions(GuildPermission.ManageRoles)]
        [Parameter("User", ParameterType.GuildUser, "a server member to be muted (mention or ID)")]
        [Parameter("Reason", ParameterType.String, ParameterFlags.Remainder | ParameterFlags.Optional, "reason")]
        public async Task Mute(ICommand command)
        {
            var user = await command["User"].AsGuildUser;
            var permissionFails = await AdministrationHelpers.Mute(user, command["Reason"], _settings);
            var reply = $"User **{user.Username}#{user.DiscriminatorValue}** has been muted.";
            var fails = permissionFails.Count();
            if (fails > 0)
                reply += $"\nℹ Couldn't mute in {fails} channel{(fails > 1 ? "s" : "")} because the bot doesn't have permission to access {(fails > 1 ? "them" : "it")}.";
            
            await command.ReplySuccess(reply);
        }

        [Command("unmute", "Unmutes a server member.")]
        [Permissions(GuildPermission.ManageRoles), BotPermissions(GuildPermission.ManageRoles)]
        [Parameter("User", ParameterType.GuildUser, "a server member to be unmuted (mention or ID)")]
        public async Task Unmute(ICommand command)
        {
            var user = await command["User"].AsGuildUser;
            await AdministrationHelpers.Unmute(user); 
            await command.ReplySuccess($"User **{user.Username}#{user.DiscriminatorValue}** has been unmuted.");
        }

        [Command("ban", "Bans one or more users.")]
        [Permissions(GuildPermission.BanMembers), BotPermissions(GuildPermission.BanMembers)]
        [Parameter("Reason", ParameterType.String, ParameterFlags.Optional, "reason for the ban")]
        [Parameter("DeleteDays", ParameterType.UInt, ParameterFlags.Optional, "number of days of messages to delete (max 7)")]
        [Parameter("Users", ParameterType.MentionOrId, ParameterFlags.Repeatable, "up to 10 user mentions or IDs")]
        [Example("raiders 318911554194243585 318903497502228482")]
        [Example("troll 7 @Troll")]
        [Example("\"picture spam\" @Spammer")]
        public async Task Ban(ICommand command, ILogger logger)
        {
            if (command["Users"].Repeats.Count > 10)
                throw new Framework.Exceptions.IncorrectParametersCommandException("The maximum number of bans per command is 10.", false);

            var banningUser = (IGuildUser)command.Author;
            var banningUserTopRole = banningUser.GetTopRole()?.Position ?? 0;
            var result = new StringBuilder();
            var bans = new Dictionary<ulong, (Task Task, string User, ulong UserId)>();
            foreach (var id in command["Users"].Repeats.Select(x => x.AsMentionOrId.Value))
            {
                string userName;
                var guildUser = await command.Guild.GetUserAsync(id) ?? await _userFetcher.FetchGuildUserAsync(command.GuildId, id);
                if (guildUser != null)
                {
                    userName = $"{guildUser.GetFullName()} ({guildUser.Id})";
                    if (!banningUser.IsOwner() && (banningUserTopRole <= (guildUser.GetTopRole()?.Position ?? 0) || guildUser.IsOwner()))
                    {
                        result.AppendLine(_communicator.FormatFailure($"You don't have permission to ban user `{userName}` on this server."));
                        continue;
                    }
                }
                else
                {
                    var user = (IUser)_client.GetUser(id) ?? await _userFetcher.FetchUserAsync(id);
                    userName = user != null ? $"{user.GetFullName()} ({user.Id})" : id.ToString();
                }

                bans[id] = (command.Guild.AddBanAsync(id, Math.Min(command["DeleteDays"].AsInt ?? 0, 7), command["Reason"].HasValue ? command["Reason"].AsString : null), userName, id);
            }

            try
            {
                await Task.WhenAll(bans.Select(x => x.Value.Task));
            }
            catch (Exception)
            {
                // Handled below
            }

            foreach (var ban in bans.Values)
            {
                if (ban.Task.Exception != null && ban.Task.Exception.InnerException is Discord.Net.HttpException ex && ex.HttpCode == HttpStatusCode.Forbidden)
                {
                    result.AppendLine(_communicator.FormatFailure($"Missing permissions to ban user `{ban.User}`."));
                }
                else if (ban.Task.Exception != null)
                {
                    logger.LogError(ban.Task.Exception, "Failed to ban user {TargetUserId}", ban.UserId);
                    result.AppendLine(_communicator.FormatFailure($"Failed to ban user `{ban.User}`."));
                }
                else
                {
                    result.AppendLine(_communicator.FormatSuccess($"User `{ban.User}` has been banned."));
                }
            }

            await command.Reply(result.ToString());
        }

        [Command("autoban", "Prevent users matching the specified criteria from joining your server.")]
        [Permissions(GuildPermission.BanMembers), BotPermissions(GuildPermission.BanMembers)]
        [Parameter("LogChannel", ParameterType.TextChannel, "a channel that will receive autoban notifications")]
        [Parameter("NameRegex", ParameterType.String, "users with a name matching this regular expression will be automatically banned")]
        [Comment("For testing of regular expressions you can use https://regexr.com/. Expressions ignore upper and lower case.")]
        [Example("\"weird dude.*\"")]
        public async Task Autoban(ICommand command)
        {
            if (!(await command.Guild.GetCurrentUserAsync()).GetPermissions(command["LogChannel"].AsTextChannel).SendMessages)
            {
                await command.ReplyError("The bot can't send messages in this channel. Please set the correct guild or channel permissions.");
                return;
            }

            var stopwatch = Stopwatch.StartNew(); // Ideally we'd measure thread execution time, but that's not as simple in C#
            foreach (var item in SampleUsernames)
            {
                try
                {
                    Regex.IsMatch(item, command["NameRegex"], RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(200));
                }
                catch (RegexMatchTimeoutException)
                {
                    // Continue
                }

                if (stopwatch.ElapsedMilliseconds > SampleUsernames.Count * 30)
                {
                    await command.Reply("Your regular expression is too complex.");
                    return;
                }
            }
            
            await _settings.Modify<AdministrationSettings>(command.GuildId, x =>
            {
                x.AutobanUsernameRegex = command["NameRegex"];
                x.AutobanLogChannelId = command["LogChannel"].AsTextChannel.Id;
            });

            await command.ReplySuccess("All users matching the provided conditions will now be automatically banned without a greeting message.");
        }

        [Command("autoban", "disable", "Disable automatic bans.")]
        [Permissions(GuildPermission.BanMembers)]
        public async Task DisableAutoban(ICommand command)
        {
            await _settings.Modify<AdministrationSettings>(command.GuildId, x => x.AutobanUsernameRegex = default);
            await command.ReplySuccess("Automatic bans disabled.");
        }

        [Command("roles", "Lists all roles on the server with their IDs.")]
        public async Task Roles(ICommand command)
        {
            var result = new StringBuilder();
            foreach (var role in command.Guild.Roles.OrderByDescending(x => x.Position))
                result.AppendLine($"Name: `{role.Name}` Id: `{role.Id}`");

            await command.Reply(result.ToString());
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
            if (((SocketGuild)command.Guild).MemberCount < userThreshold)
            {
                await command.ReplyError($"For security reasons, this command cannot be used on servers below {userThreshold} members.");
                return;
            }

            var user = await command["User"].AsGuildUser;
            if (user.IsBot)
            {
                await command.ReplyError($"This is a bot.");
                return;
            }

            try
            {
                await user.SendMessageAsync($":envelope: Message from a moderator of `{command.Guild.Name}` (`{command.GuildId}`):\n\n" + command["Message"]);
                await command.ReplySuccess("The user has been messaged.");
            }
            catch (Discord.Net.HttpException ex) when (ex.DiscordCode == 50007)
            {
                await command.ReplyError($"Can't send direct messages to this user. They have likely disabled private messages in their privacy settings.");
            }
        }

        private static bool TryBuildEmbedFromSpecification(string specification, out EmbedBuilder embed, out string error)
        {
            embed = new EmbedBuilder();
            error = null;

            var uriValidator = new Func<string, string, bool>((name, value) => Uri.TryCreate(value, UriKind.Absolute, out _));
            var parts = new[]
            {
                new KeyValueSpecificationPart("title", true, false),
                new KeyValueSpecificationPart("author", true, false),
                new KeyValueSpecificationPart("author link", true, false, "author", validator: uriValidator),
                new KeyValueSpecificationPart("author icon", true, false, "author", validator: uriValidator),
                new KeyValueSpecificationPart("image", true, false, validator: uriValidator),
                new KeyValueSpecificationPart("color", true, false),
                new KeyValueSpecificationPart("thumbnail", true, false, validator: uriValidator),
                new KeyValueSpecificationPart("description", true, true),
                new KeyValueSpecificationPart("footer", true, false),
                new KeyValueSpecificationPart("footer icon", true, false, "footer", validator: uriValidator),
                new KeyValueSpecificationPart("field", false, false, isNameAccepted: true),
                new KeyValueSpecificationPart("inline field", false, false, isNameAccepted: true),
            };

            var parser = new KeyValueSpecificationParser(parts);
            var result = parser.Parse(specification);
            if (!result.Succeeded)
            {
                error = result.Error switch
                {
                    KeyValueSpecificationParser.ErrorType.ValidationFailed => $"The specified {result.ErrorPart.Token} is invalid.",
                    KeyValueSpecificationParser.ErrorType.DuplicatedUniquePart => $"There can only be one {result.ErrorPart.Token}.",
                    KeyValueSpecificationParser.ErrorType.MissingDependency => $"The {result.ErrorPart.DependsOn} must also be specifed with {result.ErrorPart.Token}.",
                    KeyValueSpecificationParser.ErrorType.RequiredPartMissing => $"The {result.ErrorPart.Token} is missing.",
                    _ => "Invalid input"
                };

                return false;
            }

            foreach (var (part, match) in result.Matches)
            {
                switch (part.Token)
                {
                    case "title": embed.WithTitle(match.Value); break;
                    case "author": (embed.Author ??= new EmbedAuthorBuilder()).WithName(match.Value); break;
                    case "author link": (embed.Author ??= new EmbedAuthorBuilder()).WithUrl(match.Value); break;
                    case "author icon": (embed.Author ??= new EmbedAuthorBuilder()).WithIconUrl(match.Value); break;
                    case "image": embed.WithImageUrl(match.Value); break;
                    case "color": embed.WithColor(HexColorParser.Parse(match.Value)); break;
                    case "thumbnail": embed.WithThumbnailUrl(match.Value); break;
                    case "description": embed.WithDescription(match.Value); break;
                    case "footer": (embed.Footer ??= new EmbedFooterBuilder()).WithText(match.Value); break;
                    case "footer icon": (embed.Footer ??= new EmbedFooterBuilder()).WithIconUrl(match.Value); break;
                    case "field": embed.AddField(match.Name, match.Value, false); break;
                    case "inline field": embed.AddField(match.Name, match.Value, true); break;
                }
            }

            return true;
        }
    }
}
