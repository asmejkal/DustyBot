using System.Threading.Tasks;

namespace DustyBot.Service.Services.Translator
{
    public interface ITranslatorService
    {
        Task<TranslationResult> TranslateAsync(string from, string to, string message);
    }
}