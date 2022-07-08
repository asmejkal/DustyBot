using System.Threading.Tasks;

namespace DustyBot.Service.Services.Translator
{
    public interface IPapagoClient
    {
        Task<TranslationResult> TranslateAsync(string from, string to, string message);
    }
}