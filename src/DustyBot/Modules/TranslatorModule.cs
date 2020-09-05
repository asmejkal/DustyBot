using Discord;
using DustyBot.Framework.Commands;
using DustyBot.Framework.Communication;
using DustyBot.Framework.Logging;
using DustyBot.Framework.Modules;
using DustyBot.Framework.Settings;
using DustyBot.Framework.Utility;
using DustyBot.Settings;
using Newtonsoft.Json.Linq;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace DustyBot.Modules
{
    [Module("Translator", "The ability to translate.")]
    class TranslatorModule : Module
    {
        private ILogger Logger { get; }
        public ICommunicator Communicator { get; }
        public ISettingsProvider Settings { get; }

        public TranslatorModule(ICommunicator communicator, ISettingsProvider settings, ILogger logger)
        {
            Communicator = communicator;
            Settings = settings;
            //Now not use to it. but it will be use to later.
            Logger = logger;
        }

        [Command("translate", "translate")]
        [Alias("tr", "번역")]
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

            var byteDataParams = Encoding.UTF8.GetBytes("source=" + firstLang + "&target=" + lastLang + "&text=" + stringMessage);

            //TODO(BJRambo) : checking to why did Reload httpclient later.
            try
            {
                var papagoClient = WebRequest.CreateHttp("https://openapi.naver.com/v1/papago/n2mt") as HttpWebRequest;
                papagoClient.Method = "POST";
                papagoClient.ContentType = "application/x-www-form-urlencoded";
                papagoClient.Headers.Add("X-Naver-Client-Id", config.PapagoClientId);
                papagoClient.Headers.Add("X-Naver-Client-Secret", config.PapagoClientSecret);
                papagoClient.ContentLength = byteDataParams.Length;
                using (var st = papagoClient.GetRequestStream())
                {
                    st.Write(byteDataParams, 0, byteDataParams.Length);
                }

                using (var responseClient = await papagoClient.GetResponseAsync())
                using (var reader = new StreamReader(responseClient.GetResponseStream()))
                {
                    var ParserObject = JObject.Parse(await reader.ReadToEndAsync());
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
            catch (WebException e)
            {
                await command.Reply(Communicator, $"Couldn't reach Papago (error {(e.Response as HttpWebResponse)?.StatusCode.ToString() ?? e.Status.ToString()}). Please try again in a few seconds.");
            }
        }
    }
}
