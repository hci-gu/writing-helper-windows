using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GlobalTextHelper.Domain.Prompting;
using GlobalTextHelper.Infrastructure.Analytics;
using GlobalTextHelper.Infrastructure.Logging;
using GlobalTextHelper.Infrastructure.OpenAi;

namespace GlobalTextHelper.Domain.Actions;

public sealed class RewriteSelectionAction : ITextAction
{
    private static readonly IReadOnlyList<TextActionOption> _options = new List<TextActionOption>
    {
        new("minimal", "Minimal"),
        new("spelling", "Korrigera stavning"),
        new("shorter", "Kortare"),
        new("longer", "Längre"),
        new("formal", "Formell"),
        new("casual", "Vardaglig")
    };

    private readonly TextSelectionPromptBuilder _promptBuilder;
    private readonly IOpenAiClientFactory _clientFactory;
    private readonly ILogger _logger;
    private readonly IAnalyticsTracker _analytics;

    public RewriteSelectionAction(
        TextSelectionPromptBuilder promptBuilder,
        IOpenAiClientFactory clientFactory,
        ILogger logger,
        IAnalyticsTracker analytics)
    {
        _promptBuilder = promptBuilder;
        _clientFactory = clientFactory;
        _logger = logger;
        _analytics = analytics ?? throw new ArgumentNullException(nameof(analytics));
    }

    public string Id => "rewrite";
    public string DisplayName => "Skriv om…";
    public bool IsPrimaryAction => false;
    public IReadOnlyList<TextActionOption> Options => _options;

    public async Task<TextActionResult> ExecuteAsync(string selectedText, string? optionId, CancellationToken cancellationToken)
    {
        try
        {
            _analytics.TrackFunctionUsed(Id);
            var client = _clientFactory.CreateClient();
            string style = optionId ?? _options.First().Id;
            string rewritten = await _promptBuilder.RewriteSelectionAsync(client, selectedText, style);
            if (string.IsNullOrWhiteSpace(rewritten))
            {
                return TextActionResult.Failure("Assistenten returnerade inget svar.");
            }

            string displayStyle = TryGetDisplayName(style);
            return TextActionResult.Replacement(
                rewritten,
                "Använd omskriven text",
                $"Omskriven text infogad ({displayStyle}).");
        }
        catch (System.Exception ex)
        {
            _logger.LogError("RewriteSelectionAction failed", ex);
            return TextActionResult.Failure("Det gick inte att skriva om den markerade texten.");
        }
    }

    private static string TryGetDisplayName(string optionId)
    {
        var match = _options.FirstOrDefault(o => o.Id == optionId);
        return match?.Label ?? optionId;
    }
}
