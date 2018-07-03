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
        
        [Command("credential", "add", "Saves a credential. Direct message only."), DirectMessageOnly]
        [Parameters(ParameterType.String, ParameterType.String, ParameterType.String)]
        [Usage("{p}credential add Login Password CustomName\n*CustomName* - type anything for you to recognize these credentials later\n\n__Example:__ {p}johndoe1 mysecretpassword \"Google Mail\"\n\nYour credentials are stored in an encrypted database and retrieved by the bot only when necessary. However, keep in mind that it is theoretically possible for the bot owner to view them.\n\nFrom a security standpoint, creating a new dedicated account instead of using your personal account is preferred.")]
        public async Task AddCredential(ICommand command)
        {
            var id = await Settings.ModifyUser(command.Message.Author.Id, (UserCredentials s) => 
            {
                var c = new Credential { Login = (string)command.GetParameter(0), Password = ((string)command.GetParameter(1)).ToSecureString(), Name = (string)command.GetParameter(2) };
                s.Credentials.Add(c);
                return c.Id;
            });

            await command.ReplySuccess(Communicator, $"A credential with ID `{id}` has been added! Use `credential list` to view all your saved credentials.").ConfigureAwait(false);
        }

        [Command("credential", "remove", "Removes a saved credential."), DirectMessageAllow]
        [Parameters(ParameterType.String)]
        [Usage("{p}credential remove CredentialId\n\nUse `credential list` to view your saved credentials.\nExample: {p}credential remove 6F3DB03E-FBC7-4868-9D73-6147DE4D3DB0")]
        public async Task RemoveCredential(ICommand command)
        {
            Guid id;
            if (!Guid.TryParse((string)command.GetParameter(0), out id))
                throw new Framework.Exceptions.IncorrectParametersCommandException("Invalid ID format. Use `credential list` to view all your saved credentials and their IDs.");

            var removed = await Settings.ModifyUser(command.Message.Author.Id, (UserCredentials s) =>
            {
                return s.Credentials.RemoveAll(x => x.Id == id) > 0;
            });

            if (removed)
                await command.ReplySuccess(Communicator, $"Credential has been removed.").ConfigureAwait(false);
            else
                await command.ReplyError(Communicator, $"Couldn't find a credential with ID `{id}`. Use `credential list` to view all your saved credentials and their IDs.").ConfigureAwait(false);
        }

        [Command("credential", "clear", "Removes all your saved credentials."), DirectMessageAllow]
        [Usage("{p}credential clear")]
        public async Task ClearCredential(ICommand command)
        {
            await Settings.ModifyUser(command.Message.Author.Id, (UserCredentials s) => s.Credentials.Clear());

            await command.ReplySuccess(Communicator, $"All your credentials have been removed.").ConfigureAwait(false);
        }

        [Command("credential", "list", "Lists all your saved credentials."), DirectMessageAllow]
        [Usage("{p}credential list")]
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
