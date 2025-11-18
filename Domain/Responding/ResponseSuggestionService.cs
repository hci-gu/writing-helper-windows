using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using GlobalTextHelper.Infrastructure.Logging;
using GlobalTextHelper.Infrastructure.OpenAi;

namespace GlobalTextHelper.Domain.Responding;

public sealed class ResponseSuggestionService
{
    private const string Instructions =
        "You help draft short replies to a highlighted message. Given the user's input, " +
        "produce three response options:\n" +
        "1. Affirmative response (agree/accept).\n" +
        "2. Negative response (decline).\n" +
        "3. Clarification response (ask a question or request detail).\n\n" +
        "For each option provide:\n" +
        "- snippet: <= 12 words summarizing the response.\n" +
        "- response: a complete message the user can send.\n\n" +
        "Return valid JSON with this structure:\n" +
        "{\n  \"affirmative\": { \"snippet\": \"...\", \"response\": \"...\" },\n" +
        "  \"negative\": { \"snippet\": \"...\", \"response\": \"...\" },\n" +
        "  \"clarification\": { \"snippet\": \"...\", \"response\": \"...\" }\n}";

    private readonly Func<string?> _promptPreambleProvider;
    private readonly IOpenAiClientFactory _clientFactory;
    private readonly ILogger _logger;

    public ResponseSuggestionService(
        Func<string?> promptPreambleProvider,
        IOpenAiClientFactory clientFactory,
        ILogger logger)
    {
        _promptPreambleProvider = promptPreambleProvider ?? throw new ArgumentNullException(nameof(promptPreambleProvider));
        _clientFactory = clientFactory ?? throw new ArgumentNullException(nameof(clientFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<IReadOnlyList<ResponseSuggestion>> GenerateSuggestionsAsync(
        string selectedText,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(selectedText))
            throw new ArgumentException("Selected text cannot be empty.", nameof(selectedText));

        try
        {
            var client = _clientFactory.CreateClient();
            string prompt = BuildPrompt(selectedText);
            string completion = await client.SendPromptAsync(prompt, temperature: 0.4, maxOutputTokens: 600, cancellationToken);
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
            var suggestions = new List<ResponseSuggestion>(capacity: 3);
            suggestions.Add(ParseSuggestion(ResponseTone.Affirmative, root, "affirmative"));
            suggestions.Add(ParseSuggestion(ResponseTone.Negative, root, "negative"));
            suggestions.Add(ParseSuggestion(ResponseTone.Clarification, root, "clarification"));
            return suggestions.Where(s => !string.IsNullOrWhiteSpace(s.FullResponse)).ToList();
        }
    }

    private static ResponseSuggestion ParseSuggestion(ResponseTone tone, JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var element))
            throw new InvalidOperationException($"OpenAI response did not include '{propertyName}'.");

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
