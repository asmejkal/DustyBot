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
using DustyBot.Settings;
using System.Text.RegularExpressions;
using Discord.WebSocket;
using System.Threading;
using System.Net.Http;
using Nito.Collections;

namespace DustyBot.Modules
{
    [Module("Song Ranking", "Find your favorite songs.", true)]
    class SongRankModule : Module
    {
        class Session
        {
            public static readonly IEmote ArrowLeft = new Emoji("⬅");
            public static readonly IEmote ArrowRight = new Emoji("➡");

            SemaphoreSlim _lock = new SemaphoreSlim(1, 1);
            Deque<string> _toSort;
            List<string> _sorted = new List<string>();
            Tuple<string, string> _current;
            IUserMessage _message;
            IUser _user;
            
            ICommunicator _communicator;

            public Session(IUser user, IEnumerable<string> songs, ICommunicator communicator)
            {
                _user = user;
                _toSort = new Deque<string>(songs);

                if (_toSort.Count < 1)
                    throw new ArgumentException();

                _communicator = communicator;
            }

            public async Task SaveDecision(bool left)
            {
                if (_current == null)
                    throw new InvalidOperationException();

                _sorted.Add(left ? _current.Item2 : _current.Item1);
                _toSort.AddToFront(left ? _current.Item1 : _current.Item2);
                await _message.RemoveReactionAsync(left ? ArrowLeft : ArrowRight, _user);
            }

            public async Task<bool> Next(ITextChannel channel)
            {
                await _lock.WaitAsync();
                try
                {
                    if (_toSort.Count < 1)
                        throw new InvalidOperationException();
                    else if (_toSort.Count == 1)
                    {
                        _sorted.Add(_toSort.RemoveFromBack());

                        await ShowResult(channel);
                        return false;
                    }
                    else
                    {
                        await ShowRound(channel);
                        return true;
                    }
                }
                finally
                {
                    _lock.Release();
                }
            }

            public async Task ShowResult(ITextChannel channel)
            {
                var builder = new StringBuilder();
                var pos = 1;
                for (int i = _sorted.Count - 1; i >= 0; --i)
                    builder.AppendLine($"`{pos++}.` {_sorted[i]}");

                await _communicator.SendMessage(channel, builder.ToString());
            }

            public async Task ShowRound(ITextChannel channel)
            {
                _current = Tuple.Create(_toSort.RemoveFromBack(), _toSort.RemoveFromBack());
                if (_message == null)
                {
                    _message = await channel.SendMessageAsync($"Remaining: {_toSort.Count}\n**{_current.Item1}** vs. **{_current.Item2}**");
                    TaskHelper.FireForget(async () =>
                    {
                        await _message.AddReactionAsync(ArrowLeft);
                        await _message.AddReactionAsync(ArrowRight);
                    });
                }
                else
                    await _message.ModifyAsync(x => x.Content = $"Remaining: {_toSort.Count}\n**{_current.Item1}** vs. **{_current.Item2}**");
            }
        }

        Dictionary<ulong, Session> _sessions = new Dictionary<ulong, Session>();

        public ICommunicator Communicator { get; private set; }
        public ISettingsProvider Settings { get; private set; }
        public ILogger Logger { get; private set; }

        public SongRankModule(ICommunicator communicator, ISettingsProvider settings, ILogger logger)
        {
            Communicator = communicator;
            Settings = settings;
            Logger = logger;
        }

        [Command("songrank", "", CommandFlags.RunAsync)]
        [Parameter("Artist", ParameterType.String, "Name of an artist, as it appears on https://musicbrainz.org/.")]
        public async Task RankSongs(ICommand command)
        {
            Session session = null;
            lock (_sessions)
            {
                _sessions.TryGetValue(command.Message.Author.Id, out session);
            }

            if (session == null)
            {
                var songs = new HashSet<string>();
                var offset = 0;
                const int limit = 50;
                do
                {
                    var request = WebRequest.CreateHttp($"http://musicbrainz.org/ws/2/recording/?query=artistname:{command["Artist"]}&limit={limit}&offset={offset}&fmt=json");
                    request.Headers["User-Agent"] = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/67.0.3396.99 Safari/537.36";

                    using (var response = (HttpWebResponse)await request.GetResponseAsync().ConfigureAwait(false))
                    using (var stream = response.GetResponseStream())
                    using (var reader = new StreamReader(stream))
                    {
                        var result = await reader.ReadToEndAsync();

                        var recordings = JObject.Parse(result)["recordings"];
                        var count = 0;
                        foreach (var recording in recordings)
                        {
                            songs.Add((string)recording["title"]);
                            count++;
                        }

                        if (count < limit)
                            break;

                        offset += limit;
                    }
                }
                while (true);

                session = new Session(command.Message.Author, songs, Communicator);
                lock (_sessions)
                {
                    _sessions.Add(command.Message.Author.Id, session);
                }
            }

            await session.Next((ITextChannel)command.Message.Channel);
        }

        public override Task OnReactionAdded(Cacheable<IUserMessage, ulong> message, ISocketMessageChannel channel, SocketReaction reaction)
        {
            TaskHelper.FireForget(async () =>
            {
                try
                {
                    if (!reaction.User.IsSpecified || reaction.User.Value.IsBot)
                        return;

                    //Check for page arrows
                    if (reaction.Emote.Name != Session.ArrowLeft.Name && reaction.Emote.Name != Session.ArrowRight.Name)
                        return;

                    var textChannel = channel as ITextChannel;
                    if (textChannel == null)
                        return;

                    //Lock and check if we have context for this message                        
                    Session session;
                    lock (_sessions)
                    {
                        if (!_sessions.TryGetValue(reaction.UserId, out session))
                            return;
                    }

                    await session.SaveDecision(reaction.Emote.Name == Session.ArrowLeft.Name);
                    if (await session.Next(textChannel) == false)
                    {
                        lock (_sessions)
                        {
                            _sessions.Remove(reaction.UserId);
                        }
                    }
                }
                catch (Exception ex)
                {
                    await Logger.Log(new LogMessage(LogSeverity.Error, "SongRank", "Failed to process a reaction.", ex));
                }
            });

            return Task.CompletedTask;
        }
    }
}
