using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord;
using DustyBot.Framework.Modules;

namespace DustyBot.Framework
{
    /// <summary>
    /// Initialization, composition root
    /// </summary>
    public class Framework : IModuleCollection
    {
        private HashSet<Modules.IModule> _modules;
        public IEnumerable<IModule> Modules => _modules;

        public Framework(DiscordSocketClient client, IEnumerable<Modules.IModule> modules, Config.IEssentialConfig config, Communication.ICommunicator communicator = null, Logging.ILogger logger = null)
        {
            _modules = new HashSet<Modules.IModule>(modules);

            if (communicator == null)
                communicator = new Communication.DefaultCommunicator();

            if (logger == null)
                logger = new Logging.ConsoleLogger(client);

            var eventRouter = new Events.SocketEventRouter(modules, client);
            var commandRouter = new Commands.CommandRouter(modules, communicator, logger, config);
            eventRouter.Register(commandRouter);
        }
    }
}
