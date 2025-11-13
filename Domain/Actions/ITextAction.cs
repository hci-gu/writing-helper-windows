using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace GlobalTextHelper.Domain.Actions;

public interface ITextAction
{
    string Id { get; }
    string DisplayName { get; }
    bool IsPrimaryAction { get; }
    IReadOnlyList<TextActionOption> Options { get; }
    Task<TextActionResult> ExecuteAsync(string selectedText, string? optionId, CancellationToken cancellationToken);
}
