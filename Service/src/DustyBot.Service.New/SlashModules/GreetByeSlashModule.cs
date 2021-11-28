using System.Linq;
using System.Threading.Tasks;
using Disqord;
using Disqord.Bot.Hosting;
using Disqord.Gateway;
using Disqord.Rest;

namespace DustyBot.Service.SlashModules
{
    public class GreetByeSlashModule : DiscordBotService
    {
        protected override async ValueTask OnReady(ReadyEventArgs e)
        {
            var app = await Bot.FetchCurrentApplicationAsync();

            var commands = await Bot.FetchGuildApplicationCommandsAsync(app.Id, 272435615327322112);
            // if (!commands.Any(x => x is ISlashCommand && x.Name == "test"))
            {
                var command = new LocalSlashCommand()
                    .WithName("test")
                    .WithDescription("Does many things.")
                    .WithOptions(new LocalSlashCommandOption()
                        .WithType(SlashCommandOptionType.SubcommandGroup)
                        .WithName("testgroup")
                        .WithDescription("Does things.")
                        .WithOptions(new LocalSlashCommandOption()
                            .WithType(SlashCommandOptionType.Subcommand)
                            .WithName("testcommand")
                            .WithChannelTypes(ChannelType.Text)
                            .WithDescription("Does thing.")
                            .WithOptions(new LocalSlashCommandOption()
                                .WithType(SlashCommandOptionType.String)
                                .WithName("option3")
                                .WithIsRequired()
                                .WithDescription("Does thing 3."),
                                new LocalSlashCommandOption()
                                .WithType(SlashCommandOptionType.String)
                                .WithName("option1")
                                .WithDescription("Does thing 1."),
                                new LocalSlashCommandOption()
                                .WithType(SlashCommandOptionType.String)
                                .WithName("option2")
                                .WithDescription("Does thing 2."))));

                await Bot.CreateGuildApplicationCommandAsync(app.Id, 272435615327322112, command);
            }
        }

        protected override async ValueTask OnInteractionReceived(InteractionReceivedEventArgs e)
        {
            var interaction = (ISlashCommandInteraction)e.Interaction;
            var args = interaction.Options["testgroup"].Options["testcommand"].Options;
            await interaction.Response().SendMessageAsync(
                new LocalInteractionResponse(InteractionResponseType.ChannelMessage)
                    .WithContent($"woop: {(args.TryGetValue("option1", out var value) ? value.Value : "none")}, {(args.TryGetValue("option2", out var value2) ? value2.Value : "none")}")
                    .WithIsEphemeral());
        }
    }
}
