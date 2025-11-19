using System;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace GlobalTextHelper.Infrastructure.OpenAi
{
    /// <summary>
    /// Minimal helper for issuing chat completion requests to the OpenAI API.
    /// </summary>
    public sealed class OpenAiChatClient : IDisposable
    {
        private static readonly Uri DefaultBaseUri = new("https://api.openai.com/v1/", UriKind.Absolute);
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

        private readonly HttpClient _httpClient;
        private readonly bool _disposeClient;
        private readonly string _model;

        public OpenAiChatClient(string apiKey, string model = "gpt-4o-mini", HttpClient? httpClient = null, Uri? baseUri = null)
        {
            if (string.IsNullOrWhiteSpace(apiKey))
                throw new ArgumentException("En OpenAI-API-nyckel måste anges.", nameof(apiKey));

            _model = string.IsNullOrWhiteSpace(model) ? "gpt-4o-mini" : model;

            if (httpClient is null)
            {
                _httpClient = new HttpClient();
                _disposeClient = true;
            }
            else
            {
                _httpClient = httpClient;
                _disposeClient = false;
            }

            var resolvedBase = baseUri ?? DefaultBaseUri;
            if (_httpClient.BaseAddress is null)
            {
                _httpClient.BaseAddress = resolvedBase;
            }
            else if (baseUri is not null && _httpClient.BaseAddress != resolvedBase)
            {
                _httpClient.BaseAddress = resolvedBase;
            }

            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

            if (!_httpClient.DefaultRequestHeaders.UserAgent.Any())
            {
                _httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("GlobalTextHelper", "0.1"));
            }
        }

        public static OpenAiChatClient FromEnvironment(string? model = null, HttpClient? httpClient = null)
        {
            string? apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
            if (string.IsNullOrWhiteSpace(apiKey))
                throw new InvalidOperationException("Ställ in miljövariabeln OPENAI_API_KEY innan du använder OpenAI-funktioner.");

            string? baseUrl = Environment.GetEnvironmentVariable("OPENAI_API_BASE");
            Uri? baseUri = null;
            if (!string.IsNullOrWhiteSpace(baseUrl))
            {
                if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out baseUri))
                {
                    throw new InvalidOperationException("Miljövariabeln OPENAI_API_BASE måste vara en absolut URL.");
                }

                if (!baseUri.AbsoluteUri.EndsWith('/'))
                {
                    baseUri = new Uri(baseUri.AbsoluteUri + "/", UriKind.Absolute);
                }
            }

            return new OpenAiChatClient(apiKey, model ?? "gpt-4o-mini", httpClient, baseUri);
        }

        public async Task<string> SendPromptAsync(string prompt, double temperature = 0.7, int? maxOutputTokens = null, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(prompt))
                throw new ArgumentException("Prompten får inte vara tom.", nameof(prompt));

            var request = new ChatCompletionRequest(
                Model: _model,
                Messages: new[] { new ChatMessage("user", prompt) },
                Temperature: temperature,
                MaxTokens: maxOutputTokens);

            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "chat/completions")
            {
                Content = new StringContent(JsonSerializer.Serialize(request, JsonOptions), Encoding.UTF8, "application/json")
            };

            using HttpResponseMessage response = await _httpClient.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            string json = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var error = TryDeserialize<OpenAiErrorResponse>(json);
                string message = error?.Error?.Message ?? $"HTTP {(int)response.StatusCode} {response.ReasonPhrase}";
                throw new HttpRequestException($"OpenAI-begäran misslyckades: {message}");
            }

            var completion = TryDeserialize<ChatCompletionResponse>(json)
                ?? throw new InvalidOperationException("OpenAI returnerade ett oväntat svar.");

            string? content = completion.Choices?
                .OrderBy(choice => choice.Index)
                .Select(choice => choice.Message?.Content?.Trim())
                .FirstOrDefault(c => !string.IsNullOrEmpty(c));

            if (string.IsNullOrEmpty(content))
                throw new InvalidOperationException("OpenAI-svaret innehöll inget meddelandeinnehåll.");

            return content;
        }

        private static T? TryDeserialize<T>(string json)
        {
            try
            {
                return JsonSerializer.Deserialize<T>(json, JsonOptions);
            }
            catch (JsonException)
            {
                return default;
            }
        }

        public void Dispose()
        {
            if (_disposeClient)
            {
                _httpClient.Dispose();
            }
        }

        private sealed record ChatCompletionRequest(
            [property: JsonPropertyName("model")] string Model,
            [property: JsonPropertyName("messages")] ChatMessage[] Messages,
            [property: JsonPropertyName("temperature")] double Temperature,
            [property: JsonPropertyName("max_tokens")] int? MaxTokens);

        private sealed record ChatMessage(
            [property: JsonPropertyName("role")] string Role,
            [property: JsonPropertyName("content")] string Content);

        private sealed record ChatCompletionResponse(
            [property: JsonPropertyName("choices")] ChatChoice[]? Choices);

        private sealed record ChatChoice(
            [property: JsonPropertyName("index")] int Index,
            [property: JsonPropertyName("message")] ChatMessage? Message);

        private sealed record OpenAiErrorResponse(
            [property: JsonPropertyName("error")] OpenAiError? Error);

        private sealed record OpenAiError(
            [property: JsonPropertyName("message")] string? Message,
            [property: JsonPropertyName("type")] string? Type);
    }
}
