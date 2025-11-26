using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using GlobalTextHelper.Infrastructure.Analytics;
using GlobalTextHelper.Infrastructure.Logging;
using GlobalTextHelper.Infrastructure.OpenAi;

namespace GlobalTextHelper.Domain.Responding;

public sealed class ResponseSuggestionService
{
    private const string AnalyticsFunctionName = "respond";

    private const string Instructions =
        "You help draft short replies to a highlighted message. Given the user's input, " +
        "write every snippet and response in the same language as the input message " +
        "(e.g., Swedish emails should get Swedish snippets and replies; do not translate to English). " +
        "Generate a handful of options per tone: always include at least one affirmative, one negative, " +
        "and one clarification response. When the message offers multiple ways to say yes (for example, " +
        "several meeting times or options to confirm), return 2-3 distinct affirmative responses that cover " +
        "the main choices. Include extra variations for the other tones only when they add meaningful choice. " +
        "Keep snippets under 12 words.\n\n" +
        "Return valid JSON with this structure:\n" +
        "{\n" +
        "  \"affirmative\": [ { \"snippet\": \"...\", \"response\": \"...\" }, ... ],\n" +
        "  \"negative\": [ { \"snippet\": \"...\", \"response\": \"...\" }, ... ],\n" +
        "  \"clarification\": [ { \"snippet\": \"...\", \"response\": \"...\" }, ... ]\n" +
        "}\n" +
        "Always return arrays (even if there is only one option in a tone).";

    private readonly Func<string?> _promptPreambleProvider;
    private readonly IOpenAiClientFactory _clientFactory;
    private readonly ILogger _logger;
    private readonly IAnalyticsTracker _analytics;

    public ResponseSuggestionService(
        Func<string?> promptPreambleProvider,
        IOpenAiClientFactory clientFactory,
        ILogger logger,
        IAnalyticsTracker analytics)
    {
        _promptPreambleProvider = promptPreambleProvider ?? throw new ArgumentNullException(nameof(promptPreambleProvider));
        _clientFactory = clientFactory ?? throw new ArgumentNullException(nameof(clientFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _analytics = analytics ?? throw new ArgumentNullException(nameof(analytics));
    }

    public async Task<IReadOnlyList<ResponseSuggestion>> GenerateSuggestionsAsync(
        string selectedText,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(selectedText))
            throw new ArgumentException("Selected text cannot be empty.", nameof(selectedText));

        try
        {
            _analytics.TrackFunctionUsed(AnalyticsFunctionName);
            var client = _clientFactory.CreateClient();
            string prompt = BuildPrompt(selectedText);
            string completion = await client.SendPromptAsync(prompt, temperature: 0.4, cancellationToken);
            return ParseSuggestions(completion);
        }
        catch (Exception ex)
        {
            _logger.LogError("Failed to generate response suggestions", ex);
            throw;
        }
    }

    private string BuildPrompt(string selectedText)
    {
        string sanitized = selectedText.Trim();
        var builder = new StringBuilder();
        string? preamble = _promptPreambleProvider()?.Trim();
        if (!string.IsNullOrWhiteSpace(preamble))
        {
            builder.AppendLine("User context:");
            builder.AppendLine(preamble);
            builder.AppendLine();
        }

        builder.AppendLine(Instructions);
        builder.AppendLine();
        builder.AppendLine("Input message:");
        builder.AppendLine("\"\"\"");
        builder.AppendLine(sanitized);
        builder.AppendLine("\"\"\"");
        builder.AppendLine();
        builder.AppendLine("JSON:");

        return builder.ToString();
    }

    private static IReadOnlyList<ResponseSuggestion> ParseSuggestions(string completion)
    {
        if (string.IsNullOrWhiteSpace(completion))
            throw new InvalidOperationException("OpenAI returned an empty response.");

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(completion);
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException("OpenAI response was not valid JSON.", ex);
        }

        using (document)
        {
            var root = document.RootElement;
            var suggestions = new List<ResponseSuggestion>();
            suggestions.AddRange(ParseSuggestionGroup(ResponseTone.Affirmative, root, "affirmative"));
            suggestions.AddRange(ParseSuggestionGroup(ResponseTone.Negative, root, "negative"));
            suggestions.AddRange(ParseSuggestionGroup(ResponseTone.Clarification, root, "clarification"));

            var filtered = suggestions.Where(s => !string.IsNullOrWhiteSpace(s.FullResponse)).ToList();
            if (filtered.Count == 0)
            {
                throw new InvalidOperationException("OpenAI response did not include any suggestions.");
            }

            return filtered;
        }
    }

    private static IEnumerable<ResponseSuggestion> ParseSuggestionGroup(
        ResponseTone tone,
        JsonElement root,
        string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var element))
            throw new InvalidOperationException($"OpenAI response did not include '{propertyName}'.");

        if (element.ValueKind == JsonValueKind.Array)
        {
            int index = 0;
            foreach (var child in element.EnumerateArray())
            {
                if (child.ValueKind != JsonValueKind.Object)
                {
                    throw new InvalidOperationException($"OpenAI response '{propertyName}[{index}]' must be an object.");
                }

                yield return ParseSuggestion(tone, child, $"{propertyName}[{index}]");
                index++;
            }

            if (index == 0)
            {
                throw new InvalidOperationException($"OpenAI response '{propertyName}' array was empty.");
            }

            yield break;
        }

        if (element.ValueKind == JsonValueKind.Object)
        {
            yield return ParseSuggestion(tone, element, propertyName);
            yield break;
        }

        throw new InvalidOperationException($"OpenAI response '{propertyName}' must be an object or array.");
    }

    private static ResponseSuggestion ParseSuggestion(ResponseTone tone, JsonElement element, string propertyName)
    {
        string snippet = element.TryGetProperty("snippet", out var snippetElement)
            ? snippetElement.GetString() ?? string.Empty
            : string.Empty;

        string response = element.TryGetProperty("response", out var responseElement)
            ? responseElement.GetString() ?? string.Empty
            : string.Empty;

        if (string.IsNullOrWhiteSpace(response))
            throw new InvalidOperationException($"OpenAI response did not include a '{propertyName}' response.");

        return new ResponseSuggestion(tone, snippet, response);
    }
}
