using Discord;
using DustyBot.Framework.Commands;
using DustyBot.Framework.Communication;
using DustyBot.Framework.Logging;
using DustyBot.Framework.Modules;
using DustyBot.Framework.Settings;
using DustyBot.Settings;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using DustyBot.Framework.Utility;

namespace DustyBot.Modules
{
    [Module("Translator", "The ability to translate.")]
    class TranslatorModule : Module
    {
        private ILogger Logger;
        public ICommunicator Communicator { get; }
        public ISettingsProvider Settings { get; }

        public TranslatorModule(ICommunicator communicator, ISettingsProvider settings, ILogger logger)
        {
            Communicator = communicator;
            Settings = settings;
            //Now not use to it. but it will be use to later.
            Logger = logger;
        }

        [Command("tr", "translate")]
        [Alias("번역")]
        [Parameter("Start", ParameterType.String, "Select the language that needs to be translated.")]
        [Parameter("End", ParameterType.String, "Select the language to translate.")]
        [Parameter("Message", ParameterType.String, ParameterFlags.Remainder, "Input the word or sentence you want to translate.")]
        [Comment("Korean = ko \nJapan = ja \nEnglish = en \nChinese(Simplified) = zh-CH \nChinese(Traditional) = zh - TW \nSpanish = es \nFrench = fr \nGerman = de \nRussian = ru \nPortuguese = pt \nItalian = it \nVietnamese = vi \nThai = th \nIndonesian = id")]
        public async Task Translation(ICommand command)
        {
            var config = await Settings.ReadGlobal<BotConfig>();

            await command.Message.Channel.TriggerTypingAsync();
            var stringMessage = command["Message"].ToString();
            var firstLang = command["Start"].ToString();
            var lastLang = command["End"].ToString();

            var data = new Dictionary<string, string>()
            {
                {"source", firstLang},
                {"target", lastLang},
                {"text", stringMessage},
            };

            //TODO(BJRambo) : checking to why did Reload httpclient later.
            var papagoClient = new HttpClient();
            papagoClient.DefaultRequestHeaders.Clear();
            papagoClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            papagoClient.DefaultRequestHeaders.TryAddWithoutValidation("Content-Type", "application/json");

            using (var request = new HttpRequestMessage(HttpMethod.Post, "https://openapi.naver.com/v1/papago/n2mt") { Content = new FormUrlEncodedContent(data) })
            {
                request.Headers.Add("X-Naver-Client-Id", config.PapagoClientId);
                request.Headers.Add("X-Naver-Client-Secret", config.PapagoClientSecret);
                var responseClient = await papagoClient.SendAsync(request);
                if (responseClient.IsSuccessStatusCode)
                {
                    var ParserObject = JObject.Parse(await responseClient.Content.ReadAsStringAsync());
                    var trMessage = ParserObject["message"]["result"]["translatedText"].ToString();

                    var translateSentence = trMessage.Truncate(EmbedBuilder.MaxDescriptionLength);

                    EmbedBuilder embedBuilder = new EmbedBuilder()
                    {
                        Title = $"Translate from **{firstLang.ToUpper()}** to **{lastLang.ToUpper()}**"
                    };

                    embedBuilder.WithDescription(translateSentence);
                    embedBuilder.WithColor(new Color(0, 206, 56));
                    embedBuilder.WithFooter("Powered by Papago");
                    await command.Message.Channel.SendMessageAsync(string.Empty, false, embedBuilder.Build()).ConfigureAwait(false);
                }
            }
        }
    }
}
