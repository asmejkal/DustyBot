using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Disqord.Bot.Sharding;
using Disqord.Sharding;
using DustyBot.Framework.Attributes;
using DustyBot.Framework.Commands.TypeParsers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Qmmands;

namespace DustyBot.Framework
{
    public class DustyBotSharder : DiscordBotSharder
    {
        public DustyBotSharder(
            IOptions<DiscordBotSharderConfiguration> options,
            ILogger<DiscordBotSharder> logger,
            IServiceProvider services,
            DiscordClientSharder client)
            : base(options, logger, services, client)
        { 
        }

        protected override void MutateModule(ModuleBuilder moduleBuilder)
        {
            var context = new NullabilityInfoContext();
            var commandInfos = moduleBuilder.Type.GetTypeInfo().DeclaredMethods.Where(x => x.GetCustomAttribute<CommandAttribute>() != null);

            foreach (var (command, commandInfo) in moduleBuilder.Commands.Zip(commandInfos))
            {
                var parameterInfos = commandInfo.GetParameters();
                foreach (var (parameter, parameterInfo) in command.Parameters.Zip(parameterInfos))
                {
                    var attribute = parameter.Attributes.OfType<DefaultAttribute>().FirstOrDefault();
                    if (attribute != null)
                    {
                        parameter.DefaultValue = attribute.DefaultValue;
                    }
                    else
                    {
                        var nullableInfo = context.Create(parameterInfo);
                        if (nullableInfo.ReadState == NullabilityState.Nullable)
                            parameter.AddAttribute(new DefaultAttribute(null));
                    }
                }
            }

            base.MutateModule(moduleBuilder);
        }

        protected override ValueTask AddTypeParsersAsync(CancellationToken cancellationToken = default)
        {
            Commands.AddTypeParser(new DateOnlyTypeParser());
            Commands.AddTypeParser(new LocalEmbedTypeParser());
            Commands.AddTypeParser(new TimeOnlyTypeParser());
            Commands.AddTypeParser(new UriTypeParser());

            return base.AddTypeParsersAsync(cancellationToken);
        }
    }
}
