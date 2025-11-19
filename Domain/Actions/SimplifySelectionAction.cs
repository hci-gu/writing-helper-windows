using System;
using System.Threading;
using System.Threading.Tasks;
using GlobalTextHelper.Domain.Prompting;
using GlobalTextHelper.Infrastructure.Logging;
using GlobalTextHelper.Infrastructure.OpenAi;

namespace GlobalTextHelper.Domain.Actions;

public sealed class SimplifySelectionAction : ITextAction
{
    private readonly TextSelectionPromptBuilder _promptBuilder;
    private readonly IOpenAiClientFactory _clientFactory;
    private readonly ILogger _logger;

    public SimplifySelectionAction(TextSelectionPromptBuilder promptBuilder, IOpenAiClientFactory clientFactory, ILogger logger)
    {
        _promptBuilder = promptBuilder;
        _clientFactory = clientFactory;
        _logger = logger;
    }

    public string Id => "simplify";
    public string DisplayName => "Förenkla och ersätt";
    public bool IsPrimaryAction => true;
    public IReadOnlyList<TextActionOption> Options { get; } = Array.Empty<TextActionOption>();

    public async Task<TextActionResult> ExecuteAsync(string selectedText, string? optionId, CancellationToken cancellationToken)
    {
        try
        {
            var client = _clientFactory.CreateClient();
            string simplified = await _promptBuilder.SimplifySelectionAsync(client, selectedText);
            if (string.IsNullOrWhiteSpace(simplified))
            {
                return TextActionResult.Failure("Assistenten returnerade inget svar.");
            }

            return TextActionResult.Replacement(simplified, "Använd förenklad text", "Förenklad text infogad.");
        }
        catch (System.Exception ex)
        {
            _logger.LogError("SimplifySelectionAction failed", ex);
            return TextActionResult.Failure("Det gick inte att förenkla den markerade texten.");
        }
    }
}
