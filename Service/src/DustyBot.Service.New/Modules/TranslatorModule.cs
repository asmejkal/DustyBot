using System;
using System.Net.Http;
using System.Threading.Tasks;
using Disqord;
using DustyBot.Framework.Commands.Attributes;
using DustyBot.Framework.Modules;
using DustyBot.Service.Services.Translator;
using Microsoft.Extensions.Logging;
using Qmmands;
using Qmmands.Text;
using Disqord.Bot.Commands;

namespace DustyBot.Service.Modules
{
    [Name("Translator"), Description("Translate text between languages.")]
    [TextGroup("reactions", "reaction")]
    public class TranslatorModule : DustyGuildModuleBase
    {
        private const string LanguageRegex = @"^[a-zA-Z]{2}(?:-[a-zA-Z]{2})?$";

        private readonly ITranslatorService _service;

        public TranslatorModule(ITranslatorService service)
        {
            _service = service;
        }

        [TextCommand("translate", "tr", "번역"), Description("Translates a piece of text."), LongRunning]
        [Remark("Korean = `ko`")]
        [Remark("Japan = `ja`")]
        [Remark("English = `en`")]
        [Remark("Chinese(Simplified) = `zh-CH`")]
        [Remark("Chinese(Traditional) = `zh-TW`")]
        [Remark("Spanish = `es`")]
        [Remark("French = `fr`")]
        [Remark("German = `de`")]
        [Remark("Russian = `ru`")]
        [Remark("Portuguese = `pt`")]
        [Remark("Italian = `it`")]
        [Remark("Vietnamese = `vi`")]
        [Remark("Thai = `th`")]
        [Remark("Indonesian = `id`")]
        [Example("ko en 사랑해")]
        public async Task<IDiscordCommandResult> TranslateAsync(
            [Description("the language of the message")]
            [Regex(LanguageRegex)]
            string from,
            [Description("the language to translate into")]
            [Regex(LanguageRegex)]
            string to,
            [Description("the word or sentence you want to translate")]
            [Remainder]
            string message)
        {
            try
            {
                return await _service.TranslateAsync(from, to, message) switch
                {
                    var x when x.Status == TranslationResult.StatusType.Success => Result(new LocalEmbed()
                        .WithTitle($"Translation from **{from.ToUpper()}** to **{to.ToUpper()}**")
                        .WithDescription(x.Text)
                        .WithColor(new Color(0, 206, 56))
                        .WithFooter("Powered by Papago")),
                    { Status: TranslationResult.StatusType.InvalidLanguageCombination } => Failure("Unsupported language combination."),
                    _ => throw new ArgumentOutOfRangeException()
                };
            }
            catch (HttpRequestException ex)
            {
                Logger.LogError(ex, "Failed to reach Papago");
                return Failure($"Couldn't reach Papago. Please try again in a few seconds.");
            }
        }
    }
}
