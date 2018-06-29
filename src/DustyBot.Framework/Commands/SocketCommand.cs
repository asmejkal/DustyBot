using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using DustyBot.Framework.Communication;

namespace DustyBot.Framework.Commands
{
    class SocketCommand : ICommand
    {
        public IUserMessage Message { get; private set; }
        public ulong GuildId => (Message.Channel as IGuildChannel).GuildId;
        public IGuild Guild => (Message.Channel as IGuildChannel).Guild;

        public string Prefix { get; private set; }
        public string Invoker { get; private set; }
        public string Body => Message.Content.Substring(Prefix.Length + Invoker.Length).Trim();

        public int ParametersCount => Tokens.Count;
        public ParameterToken GetParameter(int key) => Tokens.ElementAtOrDefault(key) ?? new ParameterToken(null);
        public IEnumerable<ParameterToken> GetParameters() => Tokens;

        private List<ParameterToken> Tokens;

        private SocketCommand(Config.IEssentialConfig config)
        {
            Config = config;
        }

        public Config.IEssentialConfig Config { get; set; }

        public static bool TryCreate(SocketUserMessage message, Config.IEssentialConfig config, out ICommand command)
        {
            var socketCommand = new SocketCommand(config);
            command = socketCommand;
            return socketCommand.TryParse(message);
        }

        private bool TryParse(SocketUserMessage message)
        {
            Message = message;

            Prefix = Config.CommandPrefix;
            if (!message.Content.StartsWith(Prefix))
                return false;

            Invoker = new string(message.Content.TakeWhile(c => !char.IsWhiteSpace(c)).Skip(Prefix.Length).ToArray());

            TokenizeParameters('"');

            return true;
        }

        private void TokenizeParameters(params char[] textQualifiers)
        {
            Tokens = new List<ParameterToken>();

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

                if (textQualifiers.Contains(currentChar) && (nextChar == '\0' || char.IsWhiteSpace(nextChar)) && inString)
                {
                    inString = false;
                    continue;
                }

                if (char.IsWhiteSpace(currentChar) && !inString)
                {
                    if (token.Length > 0)
                    {
                        Tokens.Add(new ParameterToken(token.ToString()));
                        token = token.Remove(0, token.Length);
                    }
                    
                    continue;
                }

                token = token.Append(currentChar);
            }

            if (token.Length > 0)
                Tokens.Add(new ParameterToken(token.ToString()));
        }

        public Task<IUserMessage> ReplySuccess(ICommunicator communicator, string message) => communicator.CommandReplySuccess(Message.Channel, message);
        public Task<IUserMessage> ReplyError(ICommunicator communicator, string message) => communicator.CommandReplyError(Message.Channel, message);
        public Task<ICollection<IUserMessage>> Reply(ICommunicator communicator, string message) => communicator.CommandReply(Message.Channel, message);
        public Task<ICollection<IUserMessage>> Reply(ICommunicator communicator, string message, Func<string, string> chunkDecorator, int maxDecoratorOverhead = 0) => communicator.CommandReply(Message.Channel, message, chunkDecorator, maxDecoratorOverhead);
    }
}
