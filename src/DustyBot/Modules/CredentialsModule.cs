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
using DustyBot.Settings;
using DustyBot.Helpers;
using System.Text.RegularExpressions;
using Discord.WebSocket;
using System.Threading;

namespace DustyBot.Modules
{
    [Module("Credentials", "Manages your credentials for use in other modules.")]
    class CredentialsModule : Module
    {
        public ICommunicator Communicator { get; private set; }
        public ISettingsProvider Settings { get; private set; }

        public CredentialsModule(ICommunicator communicator, ISettingsProvider settings)
        {
            Communicator = communicator;
            Settings = settings;
        }
        
        [Command("credential", "add", "Saves a credential. Direct message only.", CommandFlags.DirectMessageOnly)]
        [Parameter("Login", ParameterType.String)]
        [Parameter("Password", ParameterType.String)]
        [Parameter("Description", ParameterType.String, "type anything for you to recognize these credentials later")]
        [Comment("Your credentials are stored in an encrypted database and retrieved by the bot only when necessary. However, from a security standpoint, creating a new dedicated account instead of using your personal account might be preferred")]
        [Example("johndoe1 mysecretpassword \"Google Mail\"")]
        public async Task AddCredential(ICommand command)
        {
            var id = await Settings.ModifyUser(command.Message.Author.Id, (UserCredentials s) => 
            {
                var c = new Credential { Login = command[0], Password = command[1].AsString.ToSecureString(), Name = command[2] };
                s.Credentials.Add(c);
                return c.Id;
            });

            await command.ReplySuccess(Communicator, $"A credential with ID `{id}` has been added! Use `credential list` to view all your saved credentials.").ConfigureAwait(false);
        }

        [Command("credential", "remove", "Removes a saved credential.", CommandFlags.DirectMessageAllow)]
        [Parameter("CredentialId", ParameterType.Guid)]
        [Comment("Use `credential list` to view your saved credentials.")]
        [Example("5a688c9f-72b0-47fa-bbc0-96f82d400a14")]
        public async Task RemoveCredential(ICommand command)
        {
            var removed = await Settings.ModifyUser(command.Message.Author.Id, (UserCredentials s) =>
            {
                return s.Credentials.RemoveAll(x => x.Id == (Guid)command[0]) > 0;
            });

            if (removed)
                await command.ReplySuccess(Communicator, $"Credential has been removed.").ConfigureAwait(false);
            else
                await command.ReplyError(Communicator, $"Couldn't find a credential with ID `{command[0]}`. Use `credential list` to view all your saved credentials and their IDs.").ConfigureAwait(false);
        }

        [Command("credential", "clear", "Removes all your saved credentials.", CommandFlags.DirectMessageAllow)]
        public async Task ClearCredential(ICommand command)
        {
            await Settings.ModifyUser(command.Message.Author.Id, (UserCredentials s) => s.Credentials.Clear());

            await command.ReplySuccess(Communicator, $"All your credentials have been removed.").ConfigureAwait(false);
        }

        [Command("credential", "list", "Lists all your saved credentials.", CommandFlags.DirectMessageAllow)]
        public async Task ListCredential(ICommand command)
        {
            var settings = await Settings.ReadUser<UserCredentials>(command.Message.Author.Id);
            if (settings.Credentials.Count <= 0)
            {
                await command.Reply(Communicator, "No credential saved. Use `credential add` to save a credential.");
                return;
            }

            var result = string.Empty;
            foreach (var credential in settings.Credentials)
            {
                result += $"\nName: `{credential.Name}` Id: `{credential.Id}`";
            }

            await command.Reply(Communicator, result);
        }

        public static async Task<Credential> GetCredential(ISettingsProvider settings, ulong userId, string id)
        {
            Guid guid;
            if (!Guid.TryParse(id, out guid))
                throw new Framework.Exceptions.IncorrectParametersCommandException("Invalid ID format. Use `credential list` in a Direct Message to view all your saved credentials and their IDs.");

            return await GetCredential(settings, userId, guid);
        }
            
        public static async Task<Credential> GetCredential(ISettingsProvider settings, ulong userId, Guid guid)
        {
            var credentials = await settings.ReadUser<UserCredentials>(userId);
            var credential = credentials.Credentials.FirstOrDefault(x => x.Id == guid);
            if (credential == null)
                throw new Framework.Exceptions.IncorrectParametersCommandException("You don't have a credential saved with this ID. Use `credential add` in a Direct Message to add a credential.");

            return credential;
        }

        public static async Task EnsureCredential(ISettingsProvider settings, ulong userId, string id)
        {
            Guid guid;
            if (!Guid.TryParse(id, out guid))
                throw new Framework.Exceptions.IncorrectParametersCommandException("Invalid ID format. Use `credential list` to view all your saved credentials and their IDs.");

            var credentials = await settings.ReadUser<UserCredentials>(userId);
            if (!credentials.Credentials.Any(x => x.Id == guid))
                throw new Framework.Exceptions.IncorrectParametersCommandException("You don't have a credential saved with this ID. Use `credential add` in a Direct Message to add a credential.");
        }
    }
}
