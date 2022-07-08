using System;
using System.Threading.Tasks;

namespace DustyBot.Service.Services.Translator
{
    internal class TranslatorService : ITranslatorService
    {
        private readonly Func<PapagoClient> _papagoClientFactory;

        public TranslatorService(Func<PapagoClient> papagoClientFactory)
        {
            _papagoClientFactory = papagoClientFactory;
        }

        public Task<TranslationResult> TranslateAsync(string from, string to, string message)
        {
            return _papagoClientFactory().TranslateAsync(from, to, message);
        }
    }
}
