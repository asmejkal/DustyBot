using System;

namespace DustyBot.Service.Services.Translator
{
    public class TranslationResult
    {
        public enum StatusType
        {
            Success,
            InvalidLanguageCombination
        }

        public string? Text { get; }
        public StatusType Status { get; }

        public TranslationResult(string text)
        {
            Text = string.IsNullOrEmpty(text) ? throw new ArgumentException("Null or empty", nameof(text)) : text;
            Status = StatusType.Success;
        }

        public TranslationResult(StatusType status)
        {
            Status = status;
        }
    }
}
