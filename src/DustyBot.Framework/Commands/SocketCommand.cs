using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using DustyBot.Framework.Communication;

namespace DustyBot.Framework.Commands
{
    public class SocketCommand : ICommand
    {
        private List<string> _verbs = new List<string>();
        private List<Tuple<int, ParameterToken>> _tokens;
        public Remainder Remainder { get; private set; }

        public IUserMessage Message { get; private set; }
        public ulong GuildId => (Message.Channel as IGuildChannel)?.GuildId ?? 0;
        public IGuild Guild => (Message.Channel as IGuildChannel)?.Guild;

        public string Prefix => Config.CommandPrefix;
        public string Invoker { get; private set; }

        public IEnumerable<string> Verbs => _verbs;

        public string Body { get; private set; }

        public int ParametersCount => _tokens.Count;
        public int GetIndex(string name) => _tokens.FindIndex(x => string.Compare(x.Item2.Parameter?.Name, name, true) == 0);
        public ParameterToken GetParameter(int key) => _tokens.ElementAtOrDefault(key)?.Item2 ?? new ParameterToken(null, Guild as SocketGuild);
        public ParameterToken GetParameter(string name) => _tokens.FirstOrDefault(x => string.Compare(x.Item2.Parameter?.Name, name, true) == 0)?.Item2 ?? new ParameterToken(null, Guild as SocketGuild);
        public IEnumerable<ParameterToken> GetParameters() => _tokens.Select(x => x.Item2);
        public ParameterToken this[int key] => GetParameter(key);
        public ParameterToken this[string name] => GetParameter(name);

        public Config.IEssentialConfig Config { get; set; }

        private SocketCommand(Config.IEssentialConfig config)
        {
            Config = config;
        }

        public static string ParseInvoker(SocketUserMessage message, string prefix)
        {
            if (!message.Content.StartsWith(prefix))
                return null;

            return new string(message.Content.TakeWhile(c => !char.IsWhiteSpace(c)).Skip(prefix.Length).ToArray());
        }

        public static List<string> ParseVerbs(SocketUserMessage message)
        {
            return message.Content.Split(new char[0], StringSplitOptions.RemoveEmptyEntries).Skip(1).ToList();
        }

        public static string ParseBody(SocketUserMessage message, int verbCount = 0)
        {
            var content = message.Content;
            int toSkip = verbCount + 1;
            int skipped = 0;
            bool inWhiteSpace = true;
            for (int i = 0; i < content.Length; ++i)
            {
                if (char.IsWhiteSpace(content[i]))
                {
                    inWhiteSpace = true;
                }
                else if (inWhiteSpace)
                {
                    if (skipped >= toSkip)
                        return content.Substring(i);

                    inWhiteSpace = false;
                    skipped++;
                }
            }

            return string.Empty;
        }

        public static bool TryCreate(SocketUserMessage message, Config.IEssentialConfig config, out ICommand command, int verbCount = 0)
        {
            var socketCommand = new SocketCommand(config);
            command = socketCommand;
            return socketCommand.TryParse(message, verbCount);
        }

        private bool TryParse(SocketUserMessage message, int verbCount = 0)
        {
            Message = message;

            if (string.IsNullOrEmpty(Invoker = ParseInvoker(message, Prefix)))
                return false;

            var allVerbs = ParseVerbs(message);
            if (verbCount > allVerbs.Count)
                return false;

            _verbs = allVerbs.Take(verbCount).ToList();

            Body = ParseBody(message, verbCount);

            TokenizeParameters('"');

            Remainder = new SocketRemainder(_tokens.Select(x => x.Item1), Body, Guild as SocketGuild);

            return true;
        }

        private void TokenizeParameters(params char[] textQualifiers)
        {
            _tokens = new List<Tuple<int, ParameterToken>>();

            string allParams = Body;
            if (allParams == null)
                return;

            char prevChar = '\0', nextChar = '\0', currentChar = '\0';
            bool inString = false;

            StringBuilder token = new StringBuilder();
            for (int i = 0; i < allParams.Length; i++)
            {
                currentChar = allParams[i];
                prevChar = i > 0 ? prevChar = allParams[i - 1] : '\0';
                nextChar = i + 1 < allParams.Length ? allParams[i + 1] : '\0';

                if (textQualifiers.Contains(currentChar) && (prevChar == '\0' || char.IsWhiteSpace(prevChar)) && !inString)
                {
                    inString = true;
                    continue;
                }

                if (textQualifiers.Contains(currentChar) && (nextChar == '\0' || char.IsWhiteSpace(nextChar)) && inString && prevChar != '\\')
                {
                    inString = false;
                    continue;
                }

                if (char.IsWhiteSpace(currentChar) && !inString)
                {
                    if (token.Length > 0)
                    {
                        _tokens.Add(Tuple.Create(i, new ParameterToken(token.ToString(), Guild as SocketGuild)));
                        token = token.Remove(0, token.Length);
                    }
                    
                    continue;
                }

                token = token.Append(currentChar);
            }

            if (token.Length > 0)
                _tokens.Add(Tuple.Create(allParams.Length, new ParameterToken(token.ToString(), Guild as SocketGuild)));
        }

        public Task<IUserMessage> ReplySuccess(ICommunicator communicator, string message) => communicator.CommandReplySuccess(Message.Channel, message);
        public Task<IUserMessage> ReplyError(ICommunicator communicator, string message) => communicator.CommandReplyError(Message.Channel, message);
        public Task<ICollection<IUserMessage>> Reply(ICommunicator communicator, string message) => communicator.CommandReply(Message.Channel, message);
        public Task<ICollection<IUserMessage>> Reply(ICommunicator communicator, string message, Func<string, string> chunkDecorator, int maxDecoratorOverhead = 0) => communicator.CommandReply(Message.Channel, message, chunkDecorator, maxDecoratorOverhead);
        public Task Reply(ICommunicator communicator, PageCollection pages, bool controlledByInvoker = false, bool resend = false) => communicator.CommandReply(Message.Channel, pages, controlledByInvoker ? Message.Author.Id : 0, resend);
    }

    public class SocketRemainder : Remainder
    {
        List<int> _tokenPositions;
        SocketGuild _guild;
        Dictionary<int, ParameterToken> _cache = new Dictionary<int, ParameterToken>();

        public SocketRemainder(IEnumerable<int> tokenPositions, string body, SocketGuild guild)
            : base(body.Trim(), guild)
        {
            _tokenPositions = new List<int>(tokenPositions);
            _guild = guild;
        }

        public override ParameterToken After(int count)
        {
            ParameterToken result;
            if (_cache.TryGetValue(count, out result))
                return result;

            result = new ParameterToken(Raw.Substring(count <= 0 ? 0 : (_tokenPositions.Count < count ? Raw.Length : _tokenPositions.ElementAtOrDefault(count - 1))).Trim(), _guild);
            _cache.Add(count, result);
            return result;
        }
    }
}
