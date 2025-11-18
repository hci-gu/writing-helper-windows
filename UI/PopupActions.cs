using System.Collections.Generic;
using GlobalTextHelper.Domain.Actions;

namespace GlobalTextHelper.UI;

public sealed class PopupActionDescriptor
{
    public PopupActionDescriptor(string id, string label, bool isPrimary, IReadOnlyList<TextActionOption> options)
    {
        Id = id;
        Label = label;
        IsPrimary = isPrimary;
        Options = options ?? new List<TextActionOption>();
    }

    public string Id { get; }
    public string Label { get; }
    public bool IsPrimary { get; }
    public IReadOnlyList<TextActionOption> Options { get; }
}

public sealed class PopupActionInvokedEventArgs
{
    public PopupActionInvokedEventArgs(string actionId, string? optionId, string selectedText)
    {
        ActionId = actionId;
        OptionId = optionId;
        SelectedText = selectedText;
    }

    public string ActionId { get; }
    public string? OptionId { get; }
    public string SelectedText { get; }
}
