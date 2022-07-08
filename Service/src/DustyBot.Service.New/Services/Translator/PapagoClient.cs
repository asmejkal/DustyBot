using System;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using DustyBot.Service.Configuration;
using Microsoft.Extensions.Options;

namespace DustyBot.Service.Services.Translator
{
    public class PapagoClient : IPapagoClient
    {
        private static readonly Uri ApiUrl = new("https://openapi.naver.com/v1/papago");

        private readonly HttpClient _httpClient;

        public PapagoClient(HttpClient httpClient, IOptions<PapagoOptions> options)
        {
            _httpClient = httpClient;

            _httpClient.BaseAddress = ApiUrl;
            _httpClient.DefaultRequestHeaders.Add("X-Naver-Client-Id", options.Value.ClientId);
            _httpClient.DefaultRequestHeaders.Add("X-Naver-Client-Secret", options.Value.ClientSecret);
        }

        public async Task<TranslationResult> TranslateAsync(string from, string to, string message)
        {
            var content = new StringContent($"source={Uri.EscapeDataString(from)}&target={Uri.EscapeDataString(to)}&text={Uri.EscapeDataString(message)}");
            using var response = await _httpClient.PostAsync("n2mt", content);

            if (response.StatusCode == HttpStatusCode.BadRequest)
                return new(TranslationResult.StatusType.InvalidLanguageCombination);

            response.EnsureSuccessStatusCode();

            using var stream = await response.Content.ReadAsStreamAsync();
            var result = await JsonSerializer.DeserializeAsync<JsonObject>(stream);
            var translated = result?["message"]?["result"]?["translatedText"]?.GetValue<string>();
            if (string.IsNullOrEmpty(translated))
                throw new JsonException("Element not found");

            return new(translated);
        }
    }
}
