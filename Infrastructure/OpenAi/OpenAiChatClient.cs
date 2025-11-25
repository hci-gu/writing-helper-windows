using System;
using System.Collections.Generic;
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
        public const string DefaultModel = "gpt-5-mini";
        public const string AlternateModel = "gpt-4o-mini";
        private const string Gpt5ApiVersion = "2024-12-01-preview";
        private const string Gpt4oApiVersion = "2024-02-15-preview";
        private static readonly Uri DefaultBaseUri = new("https://gu-ai-006.openai.azure.com/", UriKind.Absolute);
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
        private static readonly JsonSerializerOptions DebugJsonOptions = new(JsonSerializerDefaults.General)
        {
            WriteIndented = true
        };

        private readonly HttpClient _httpClient;
        private readonly bool _disposeClient;
        private readonly string _model;
        private readonly string _apiVersion;
        private readonly string _chatCompletionsPath;

        public OpenAiChatClient(string apiKey, string model = DefaultModel, HttpClient? httpClient = null, Uri? baseUri = null)
        {
            if (string.IsNullOrWhiteSpace(apiKey))
                throw new ArgumentException("En OpenAI-API-nyckel måste anges.", nameof(apiKey));

            _model = string.IsNullOrWhiteSpace(model) ? DefaultModel : model;
            _apiVersion = ResolveApiVersion(_model);
            _chatCompletionsPath = BuildChatCompletionsPath(_model, _apiVersion);

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

            _httpClient.DefaultRequestHeaders.Authorization = null;
            _httpClient.DefaultRequestHeaders.Remove("api-key");
            _httpClient.DefaultRequestHeaders.Add("api-key", apiKey);

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

            return new OpenAiChatClient(apiKey, string.IsNullOrWhiteSpace(model) ? DefaultModel : model, httpClient, baseUri);
        }

        public Task<string> SendPromptAsync(string prompt, double temperature = 0.7, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(prompt))
                throw new ArgumentException("Prompten får inte vara tom.", nameof(prompt));

            return SendPromptAsyncInternal(prompt, temperature, cancellationToken);
        }

        private async Task<string> SendPromptAsyncInternal(string prompt, double temperature, CancellationToken cancellationToken)
        {
            double resolvedTemperature = NormalizeTemperatureForModel(_model, temperature);
            var request = new ChatCompletionRequest(
                Messages: new[] { new ChatRequestMessage("user", prompt) },
                Temperature: resolvedTemperature);

            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, _chatCompletionsPath)
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

            bool truncatedByLength = completion.Choices?
                .Any(choice => string.Equals(choice.FinishReason, "length", StringComparison.OrdinalIgnoreCase))
                ?? false;

            string? content = completion.Choices?
                .OrderBy(choice => choice.Index)
                .Select(choice => ExtractResponseContent(choice.Message))
                .FirstOrDefault(c => !string.IsNullOrWhiteSpace(c));

            if (string.IsNullOrEmpty(content))
            {
                if (truncatedByLength)
                {
                    Console.WriteLine("OpenAI response was truncated because it hit the max token limit.");
                    throw new InvalidOperationException("OpenAI-svaret avbröts innan det hann bli klart. Försök igen senare.");
                }

                Console.WriteLine("OpenAI response missing message content; dumping payload for inspection.");
                throw new InvalidOperationException("OpenAI-svaret innehöll inget meddelandeinnehåll.");
            }

            return content;
        }

        private static string? ExtractResponseContent(ChatResponseMessage? message)
        {
            if (message is null)
                return null;

            string? normalized = NormalizeMessageContent(message.Content);
            return string.IsNullOrWhiteSpace(normalized) ? null : normalized.Trim();
        }

        private static string? NormalizeMessageContent(JsonElement content)
        {
            switch (content.ValueKind)
            {
                case JsonValueKind.String:
                    return content.GetString();
                case JsonValueKind.Array:
                    List<string>? segments = null;
                    foreach (JsonElement item in content.EnumerateArray())
                    {
                        string? text = NormalizeMessageContent(item);
                        if (!string.IsNullOrWhiteSpace(text))
                        {
                            segments ??= new List<string>();
                            segments.Add(text.Trim());
                        }
                    }

                    return segments is null ? null : string.Join("\n", segments);
                case JsonValueKind.Object:
                    return ExtractTextFromObject(content);
                case JsonValueKind.Number:
                case JsonValueKind.True:
                case JsonValueKind.False:
                    return content.GetRawText();
                case JsonValueKind.Undefined:
                case JsonValueKind.Null:
                default:
                    return null;
            }
        }

        private static string? ExtractTextFromObject(JsonElement element)
        {
            if (element.TryGetProperty("text", out JsonElement textElement))
            {
                string? text = textElement.GetString();
                if (!string.IsNullOrWhiteSpace(text))
                    return text;
            }

            if (element.TryGetProperty("content", out JsonElement nestedContent))
            {
                string? nestedText = NormalizeMessageContent(nestedContent);
                if (!string.IsNullOrWhiteSpace(nestedText))
                    return nestedText;
            }

            if (element.TryGetProperty("value", out JsonElement valueElement))
            {
                string? valueText = NormalizeMessageContent(valueElement);
                if (!string.IsNullOrWhiteSpace(valueText))
                    return valueText;
            }

            string raw = element.GetRawText();
            return string.IsNullOrWhiteSpace(raw) ? null : raw;
        }

        private static void LogRawResponse(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return;

            try
            {
                using JsonDocument document = JsonDocument.Parse(json);
                string pretty = JsonSerializer.Serialize(document.RootElement, DebugJsonOptions);
                Console.WriteLine("OpenAI raw response (prettified):");
                Console.WriteLine(pretty);
            }
            catch (JsonException)
            {
                Console.WriteLine("OpenAI raw response (raw):");
                Console.WriteLine(json);
            }
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

        private static double NormalizeTemperatureForModel(string model, double requestedTemperature)
        {
            if (RequiresDefaultTemperature(model))
            {
                return 1.0;
            }

            return requestedTemperature;
        }

        private static bool RequiresDefaultTemperature(string model)
        {
            if (string.IsNullOrWhiteSpace(model))
                return false;

            // Azure GPT-5 deployments currently reject non-default temperature values.
            return model.StartsWith("gpt-5", StringComparison.OrdinalIgnoreCase);
        }

        private static string BuildChatCompletionsPath(string deploymentName, string apiVersion)
        {
            string safeDeploymentName = Uri.EscapeDataString(deploymentName);
            return $"openai/deployments/{safeDeploymentName}/chat/completions?api-version={apiVersion}";
        }

        private static string ResolveApiVersion(string model)
        {
            if (!string.IsNullOrWhiteSpace(model) && model.StartsWith("gpt-4o", StringComparison.OrdinalIgnoreCase))
            {
                return Gpt4oApiVersion;
            }

            return Gpt5ApiVersion;
        }

        private sealed record ChatCompletionRequest(
            [property: JsonPropertyName("messages")] ChatRequestMessage[] Messages,
            [property: JsonPropertyName("temperature")] double Temperature);

        private sealed record ChatRequestMessage(
            [property: JsonPropertyName("role")] string Role,
            [property: JsonPropertyName("content")] string Content);

        private sealed record ChatCompletionResponse(
            [property: JsonPropertyName("choices")] ChatChoice[]? Choices);

        private sealed record ChatChoice(
            [property: JsonPropertyName("index")] int Index,
            [property: JsonPropertyName("message")] ChatResponseMessage? Message,
            [property: JsonPropertyName("finish_reason")] string? FinishReason);

        private sealed record ChatResponseMessage(
            [property: JsonPropertyName("role")] string Role,
            [property: JsonPropertyName("content")] JsonElement Content);

        private sealed record OpenAiErrorResponse(
            [property: JsonPropertyName("error")] OpenAiError? Error);

        private sealed record OpenAiError(
            [property: JsonPropertyName("message")] string? Message,
            [property: JsonPropertyName("type")] string? Type);
    }
}




