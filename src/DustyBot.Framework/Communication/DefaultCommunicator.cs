using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using DustyBot.Framework.Utility;
using System.Threading;
using Discord.WebSocket;

namespace DustyBot.Framework.Communication
{
    public class DefaultCommunicator : Events.EventHandler, ICommunicator
    {
        class PaginatedMessageContext
        {
            public PaginatedMessageContext(PageCollection pages, ulong messageOwner = 0)
            {
                _pages = new List<Page>(pages);
                InvokerUserId = messageOwner;
            }

            int _currentPage = 0;
            public int CurrentPage
            {
                get => _currentPage;
                set
                {
                    if (value >= Pages.Count)
                        throw new ArgumentException();
                    else
                        _currentPage = value;
                }
            }

            public int TotalPages => _pages.Count;

            List<Page> _pages;
            public IReadOnlyCollection<Page> Pages { get => _pages; }

            public bool ControlledByInvoker => InvokerUserId != 0;
            public ulong InvokerUserId;

            public SemaphoreSlim Lock { get; } = new SemaphoreSlim(1, 1);

            public DateTime ExpirationDate { get; private set; } = DateTime.Now + PaginatedMessageLife;

            public void ExtendLife() => ExpirationDate = DateTime.Now + PaginatedMessageLife;
        }

        public static readonly Color ColorSuccess = Color.Green;
        public static readonly Color ColorError = Color.Red;
        public static readonly IEmote ArrowLeft = new Emoji("⬅");
        public static readonly IEmote ArrowRight = new Emoji("➡");
        public static readonly TimeSpan PaginatedMessageLife = TimeSpan.FromHours(10);

        Dictionary<ulong, PaginatedMessageContext> _paginatedMessages = new Dictionary<ulong, PaginatedMessageContext>();

        public Config.IEssentialConfig Config { get; set; }
        public Logging.ILogger Logger { get; set; }

        public DefaultCommunicator(Config.IEssentialConfig config, Logging.ILogger logger)
        {
            Config = config;
            Logger = logger;
        }

        public async Task<IUserMessage> CommandReplySuccess(IMessageChannel channel, string message) => await channel.SendMessageAsync(":white_check_mark: " + message);
        public async Task<IUserMessage> CommandReplyError(IMessageChannel channel, string message) => await channel.SendMessageAsync(":no_entry: " + message);

        public async Task<ICollection<IUserMessage>> CommandReply(IMessageChannel channel, string message) => await channel.SendLongStringAsync(message);
        public async Task<ICollection<IUserMessage>> CommandReply(IMessageChannel channel, string message, Func<string, string> chunkDecorator, int maxDecoratorOverhead = 0) => await channel.SendLongStringAsync(message, chunkDecorator, maxDecoratorOverhead);
        public async Task CommandReply(IMessageChannel channel, PageCollection pages, ulong messageOwner = 0) => await SendMessage(channel, pages, messageOwner);

        public async Task<IUserMessage> CommandReplyMissingPermissions(IMessageChannel channel, Commands.CommandRegistration command, IEnumerable<GuildPermission> missingPermissions) =>
            await CommandReplyError(channel, string.Format(Properties.Resources.Command_MissingPermissions, string.Join(", ", missingPermissions)));

        public async Task<IUserMessage> CommandReplyNotOwner(IMessageChannel channel, Commands.CommandRegistration command) =>
            await CommandReplyError(channel, Properties.Resources.Command_NotOwner);

        public async Task<IUserMessage> CommandReplyIncorrectParameters(IMessageChannel channel, Commands.CommandRegistration command, string explanation)
        {
            var embed = new EmbedBuilder()
                    .WithTitle(Properties.Resources.Command_Usage)
                    .WithDescription(command.GetUsage(Config.CommandPrefix));

            return await channel.SendMessageAsync(":no_entry: " + Properties.Resources.Command_IncorrectParameters + " " + explanation, false, embed);
        }

        public async Task<IUserMessage> CommandReplyGenericFailure(IMessageChannel channel, Commands.CommandRegistration command) =>
            await CommandReplyError(channel, Properties.Resources.Command_GenericFailure);

        private static async Task<IUserMessage> ReplyEmbed(IMessageChannel channel, string message, Color? color = null, string title = null, string footer = null)
        {
            var embed = new EmbedBuilder()
                    .WithDescription(message);

            if (color != null)
                embed.WithColor(color.Value);

            if (!string.IsNullOrEmpty(title))
                embed.WithTitle(title);

            if (!string.IsNullOrEmpty(footer))
                embed.WithFooter(footer);

            return await channel.SendMessageAsync("", false, embed);
        }
        
        public async Task SendMessage(IMessageChannel channel, PageCollection pages, ulong messageOwner = 0)
        {
            //Set footers where applicable
            for (int i = 0; i < pages.Count; ++i)
                if (pages[i].Embed != null && pages[i].Embed.Footer == null)
                    pages[i].Embed.WithFooter($"Page {i + 1} of {pages.Count}");

            //Send the first page
            var result = await channel.SendMessageAsync(pages.First().Content, false, pages.First().Embed).ConfigureAwait(false);

            //If there's more pages, save a context
            if (pages.Count > 1)
            {
                var context = new PaginatedMessageContext(pages, messageOwner);
                lock (_paginatedMessages)
                {
                    _paginatedMessages.Add(result.Id, context);
                }

                await result.AddReactionAsync(ArrowLeft);
                await result.AddReactionAsync(ArrowRight);

                //Clean old messages, to avoid tracking too many unnecessary messages when the bots runs for a long time
                CleanupPaginatedMessages();
            }
        }

        public override Task OnReactionAdded(Cacheable<IUserMessage, ulong> message, ISocketMessageChannel channel, SocketReaction reaction)
        {
            Task.Run(async () =>
            {
                try
                {
                    if (!reaction.User.IsSpecified || reaction.User.Value.IsBot)
                        return;

                    //Check for page arrows
                    if (reaction.Emote.Name != ArrowLeft.Name && reaction.Emote.Name != ArrowRight.Name)
                        return;

                    //Lock and check if we have a page context for this message                        
                    PaginatedMessageContext context;
                    lock (_paginatedMessages)
                    {
                        if (!_paginatedMessages.TryGetValue(message.Id, out context))
                            return;
                    }

                    try
                    {
                        await context.Lock.WaitAsync();

                        //If requested, only allow the original invoker of the command to flip pages
                        var concMessage = await message.GetOrDownloadAsync();
                        if (context.ControlledByInvoker && reaction.UserId != context.InvokerUserId)
                            return;

                        //Message was touched -> refresh expiration date
                        context.ExtendLife();

                        //Calculate new page index and check bounds
                        var newPage = context.CurrentPage + (reaction.Emote.Name == ArrowLeft.Name ? -1 : 1);
                        if (newPage < 0 || newPage >= context.TotalPages)
                        {
                            await concMessage.RemoveReactionAsync(reaction.Emote, reaction.User.Value);
                            return;
                        }

                        //Modify message
                        var newMessage = context.Pages.ElementAt(newPage);
                        await concMessage.ModifyAsync(x => { x.Content = newMessage.Content; x.Embed = newMessage.Embed.Build(); });

                        //Update context and remove reaction
                        context.CurrentPage = newPage;
                        await concMessage.RemoveReactionAsync(reaction.Emote, reaction.User.Value);
                    }
                    finally
                    {
                        context.Lock.Release();
                    }                    
                }
                catch (Exception ex)
                {
                    await Logger.Log(new LogMessage(LogSeverity.Error, "Communicator", "Failed to flip a page for PaginatedMessage.", ex));
                }
            });

            return Task.CompletedTask;
        }

        public override async Task OnMessageDeleted(Cacheable<IMessage, ulong> message, ISocketMessageChannel channel)
        {
            try
            {
                lock (_paginatedMessages)
                {
                    _paginatedMessages.Remove(message.Id);
                }
            }
            catch (Exception ex)
            {
                await Logger.Log(new LogMessage(LogSeverity.Error, "Communicator", "Failed to remove a deleted message from paginated messages context.", ex));
            }
        }

        private void CleanupPaginatedMessages()
        {
            lock (_paginatedMessages)
            {
                _paginatedMessages.RemoveAll((x, y) => y.ExpirationDate < DateTime.Now);
            }
        }
    }
}
