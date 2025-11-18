using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using GlobalTextHelper.Domain.Actions;
using GlobalTextHelper.Domain.Responding;
using GlobalTextHelper.Domain.Selection;
using GlobalTextHelper.Infrastructure.Clipboard;
using GlobalTextHelper.Infrastructure.Logging;

namespace GlobalTextHelper.UI;

public sealed class PopupController : IDisposable
{
    private readonly IReadOnlyList<ITextAction> _actions;
    private readonly IClipboardService _clipboardService;
    private readonly ILogger _logger;
    private readonly ResponseSuggestionService _responseSuggestionService;
    private PopupForm? _activePopup;
    private SelectionContext? _currentContext;
    private bool _disposed;

    public PopupController(
        IEnumerable<ITextAction> actions,
        IClipboardService clipboardService,
        ILogger logger,
        ResponseSuggestionService responseSuggestionService)
    {
        _actions = actions.ToList();
        _clipboardService = clipboardService;
        _logger = logger;
        _responseSuggestionService = responseSuggestionService;
    }

    public event EventHandler? PopupClosed;

    public void ShowForSelection(SelectionContext context)
    {
        if (_disposed)
        {
            return;
        }

        ClosePopup();
        _currentContext = context;

        var popup = new PopupForm("Choose an action for the selected text.", 30000, context.OriginalText);
        popup.ActionInvoked += HandleActionInvokedAsync;
        popup.RespondRequested += HandleRespondRequestedAsync;
        popup.FormClosed += (_, __) =>
        {
            if (ReferenceEquals(_activePopup, popup))
            {
                _activePopup = null;
                PopupClosed?.Invoke(this, EventArgs.Empty);
            }
        };

        popup.SetActions(_actions.Select(action => new PopupActionDescriptor(
            action.Id,
            action.DisplayName,
            action.IsPrimaryAction,
            action.Options)));

        var cursor = Cursor.Position;
        int x = cursor.X + 12;
        int y = cursor.Y + 12;
        popup.ShowNear(new System.Drawing.Point(x, y));
        _activePopup = popup;
    }

    private async Task HandleRespondRequestedAsync(string selectionText)
    {
        if (_disposed)
        {
            return;
        }

        if (_activePopup is null || _responseSuggestionService is null)
        {
            return;
        }

        var popup = _activePopup;
        popup.StopAutoClose();
        popup.UpdateMessage("Generating response options…");
        popup.SetRespondStatus("Gathering response ideas…");
        popup.SetBusyState(true);

        try
        {
            var suggestions = await _responseSuggestionService.GenerateSuggestionsAsync(
                selectionText,
                CancellationToken.None);

            popup.SetBusyState(false);
            popup.SetRespondSuggestions(suggestions);
            popup.UpdateMessage("Choose how you would like to respond.");
            popup.SetRespondStatus("Select a response to populate it below.");
        }
        catch (Exception ex)
        {
            popup.SetBusyState(false);
            _logger.LogError("Failed to load response suggestions", ex);
            popup.SetRespondStatus("Unable to load response options.");
            popup.UpdateMessage("Unable to load response options right now.");
            popup.RestartAutoClose(4000);
        }
    }

    private async Task HandleActionInvokedAsync(PopupActionInvokedEventArgs args)
    {
        if (_disposed)
        {
            return;
        }

        if (_activePopup is null || _currentContext is null)
        {
            return;
        }

        var action = _actions.FirstOrDefault(a => string.Equals(a.Id, args.ActionId, StringComparison.OrdinalIgnoreCase));
        if (action is null)
        {
            return;
        }

        var popup = _activePopup;
        popup.StopAutoClose();
        popup.UpdateMessage($"Running {action.DisplayName}…");
        popup.SetBusyState(true);

        try
        {
            var result = await action.ExecuteAsync(args.SelectedText, args.OptionId, CancellationToken.None);
            if (!result.Success)
            {
                popup.SetBusyState(false);
                popup.UpdateMessage(result.Message ?? "Action failed.");
                popup.RestartAutoClose(4000);
                return;
            }

            if (string.IsNullOrWhiteSpace(result.ReplacementText))
            {
                popup.SetBusyState(false);
                popup.UpdateMessage(result.Message ?? "The selected action did not return any text.");
                popup.RestartAutoClose(2500);
                return;
            }

            popup.SetBusyState(false);
            var previewResult = await popup.ShowReplacementPreviewAsync(
                result.ReplacementText,
                result.PreviewAcceptLabel ?? "Use Replacement");

            popup.ClearActionButtons();

            var replacementText = string.IsNullOrWhiteSpace(popup.GetSelectionText())
                ? result.ReplacementText!
                : popup.GetSelectionText();

            switch (previewResult)
            {
                case ReplacementPreviewResult.Accept:
                    await _clipboardService.ReplaceSelectionAsync(
                        _currentContext.OriginalText,
                        replacementText,
                        _currentContext.SourceWindow,
                        CancellationToken.None);
                    popup.UpdateMessage(result.SuccessMessage ?? "Replacement inserted.");
                    popup.RestartAutoClose(1500);
                    break;
                case ReplacementPreviewResult.CopyToClipboard:
                    await _clipboardService.CopyToClipboardAsync(replacementText, CancellationToken.None);
                    popup.UpdateMessage("Replacement copied to clipboard.");
                    popup.RestartAutoClose(1500);
                    break;
                default:
                    popup.UpdateMessage("Replacement canceled.");
                    popup.RestartAutoClose(1500);
                    break;
            }
        }
        catch (Exception ex)
        {
            popup.SetBusyState(false);
            _logger.LogError($"Action '{action.Id}' failed", ex);
            popup.UpdateMessage("Unable to complete the selected action.");
            popup.RestartAutoClose(4000);
        }
    }

    public void ClosePopup()
    {
        if (_activePopup is null)
        {
            return;
        }

        try
        {
            _activePopup.Close();
        }
        finally
        {
            _activePopup = null;
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        ClosePopup();
    }
}
