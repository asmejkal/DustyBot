using Discord;
using DustyBot.Framework.Commands;
using DustyBot.Framework.Communication;
using DustyBot.Framework.Exceptions;
using DustyBot.Framework.Logging;
using DustyBot.Framework.Modules;
using DustyBot.Framework.Settings;
using DustyBot.Core.Utility;
using DustyBot.Settings;
using Newtonsoft.Json.Linq;
using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace DustyBot.Modules
{
    [Module("Translator", "Lets you translate between languages.")]
    class TranslatorModule : Module
    {
        private const string LanguageRegex = @"^[a-zA-Z]{2}(?:-[a-zA-Z]{2})?$";

        private ILogger Logger { get; }
        private ICommunicator Communicator { get; }
        private ISettingsProvider Settings { get; }

        public TranslatorModule(ICommunicator communicator, ISettingsProvider settings, ILogger logger)
        {
            Communicator = communicator;
            Settings = settings;
            Logger = logger;
        }

        [Command("translate", "Translates a piece of text.")]
        [Alias("tr"), Alias("번역")]
        [Parameter("From", LanguageRegex, ParameterType.String, "the language of the message")]
        [Parameter("To", LanguageRegex, ParameterType.String, "the language to translate into")]
        [Parameter("Message", ParameterType.String, ParameterFlags.Remainder, "the word or sentence you want to translate")]
        [Comment("Korean = `ko` \nJapan = `ja` \nEnglish = `en` \nChinese(Simplified) = `zh-CH` \nChinese(Traditional) = `zh-TW` \nSpanish = `es` \nFrench = `fr` \nGerman = `de` \nRussian = `ru` \nPortuguese = `pt` \nItalian = `it` \nVietnamese = `vi` \nThai = `th` \nIndonesian = `id`")]
        public async Task Translation(ICommand command)
        {
            var config = await Settings.ReadGlobal<BotConfig>();

            await command.Message.Channel.TriggerTypingAsync();
            var stringMessage = command["Message"].ToString();
            var firstLang = command["From"].ToString();
            var lastLang = command["To"].ToString();

            var byteDataParams = Encoding.UTF8.GetBytes($"source={Uri.EscapeDataString(firstLang)}&target={Uri.EscapeDataString(lastLang)}&text={Uri.EscapeDataString(stringMessage)}");

            try
            {
                var request = WebRequest.CreateHttp("https://openapi.naver.com/v1/papago/n2mt");
                request.Method = "POST";
                request.ContentType = "application/x-www-form-urlencoded";
                request.Headers.Add("X-Naver-Client-Id", config.PapagoClientId);
                request.Headers.Add("X-Naver-Client-Secret", config.PapagoClientSecret);
                request.ContentLength = byteDataParams.Length;
                using (var st = request.GetRequestStream())
                {
                    st.Write(byteDataParams, 0, byteDataParams.Length);
                }

                using (var responseClient = await request.GetResponseAsync())
                using (var reader = new StreamReader(responseClient.GetResponseStream()))
                {
                    var parserObject = JObject.Parse(await reader.ReadToEndAsync());
                    var trMessage = parserObject["message"]["result"]["translatedText"].ToString();

                    var translateSentence = trMessage.Truncate(EmbedBuilder.MaxDescriptionLength);

                    EmbedBuilder embedBuilder = new EmbedBuilder()
                        .WithTitle($"Translation from **{firstLang.ToUpper()}** to **{lastLang.ToUpper()}**")
                        .WithDescription(translateSentence)
                        .WithColor(new Color(0, 206, 56))
                        .WithFooter("Powered by Papago");

                    await command.Message.Channel.SendMessageAsync(string.Empty, false, embedBuilder.Build()).ConfigureAwait(false);
                }
            }
            catch (WebException e) when (e.Response is HttpWebResponse r && r.StatusCode == HttpStatusCode.BadRequest)
            {
                throw new IncorrectParametersCommandException("Unsupported language combination.");
            }
            catch (WebException e)
            {
                await Logger.Log(new LogMessage(LogSeverity.Error, "Events", "Failed to reach Papago", e));
                await command.Reply(Communicator, $"Couldn't reach Papago (error {(e.Response as HttpWebResponse)?.StatusCode.ToString() ?? e.Status.ToString()}). Please try again in a few seconds.");
            }
        }
    }
}
