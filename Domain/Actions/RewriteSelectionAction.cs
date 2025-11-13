using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GlobalTextHelper.Domain.Prompting;
using GlobalTextHelper.Infrastructure.Logging;
using GlobalTextHelper.Infrastructure.OpenAi;

namespace GlobalTextHelper.Domain.Actions;

public sealed class RewriteSelectionAction : ITextAction
{
    private static readonly IReadOnlyList<TextActionOption> _options = new List<TextActionOption>
    {
        new("minimal", "Minimal"),
        new("spelling", "Fix Spelling"),
        new("shorter", "Shorter"),
        new("longer", "Longer"),
        new("formal", "Formal"),
        new("casual", "Casual")
    };

    private readonly TextSelectionPromptBuilder _promptBuilder;
    private readonly IOpenAiClientFactory _clientFactory;
    private readonly ILogger _logger;

    public RewriteSelectionAction(TextSelectionPromptBuilder promptBuilder, IOpenAiClientFactory clientFactory, ILogger logger)
    {
        _promptBuilder = promptBuilder;
        _clientFactory = clientFactory;
        _logger = logger;
    }

    public string Id => "rewrite";
    public string DisplayName => "Rewrite…";
    public bool IsPrimaryAction => false;
    public IReadOnlyList<TextActionOption> Options => _options;

    public async Task<TextActionResult> ExecuteAsync(string selectedText, string? optionId, CancellationToken cancellationToken)
    {
        try
        {
            var client = _clientFactory.CreateClient();
            string style = optionId ?? _options.First().Id;
            string rewritten = await _promptBuilder.RewriteSelectionAsync(client, selectedText, style);
            if (string.IsNullOrWhiteSpace(rewritten))
            {
                return TextActionResult.Failure("The assistant returned an empty response.");
            }

            string displayStyle = TryGetDisplayName(style);
            return TextActionResult.Replacement(
                rewritten,
                "Use Rewritten Text",
                $"Rewritten text inserted ({displayStyle}).");
        }
        catch (System.Exception ex)
        {
            _logger.LogError("RewriteSelectionAction failed", ex);
            return TextActionResult.Failure("Unable to rewrite the selected text.");
        }
    }

    private static string TryGetDisplayName(string optionId)
    {
        var match = _options.FirstOrDefault(o => o.Id == optionId);
        return match?.Label ?? optionId;
    }
}
