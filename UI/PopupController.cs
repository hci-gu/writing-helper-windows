using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using GlobalTextHelper.Domain.Actions;
using GlobalTextHelper.Domain.Responding;
using GlobalTextHelper.Domain.Selection;
using GlobalTextHelper.Infrastructure.Analytics;
using GlobalTextHelper.Infrastructure.Clipboard;
using GlobalTextHelper.Infrastructure.Logging;

namespace GlobalTextHelper.UI;

public sealed class PopupController : IDisposable
{
    private readonly IReadOnlyList<ITextAction> _actions;
    private readonly IClipboardService _clipboardService;
    private readonly ILogger _logger;
    private readonly IAnalyticsTracker _analytics;
    private readonly ResponseSuggestionService _responseSuggestionService;
    private PopupForm? _activePopup;
    private SelectionContext? _currentContext;
    private bool _disposed;

    public PopupController(
        IEnumerable<ITextAction> actions,
        IClipboardService clipboardService,
        ILogger logger,
        ResponseSuggestionService responseSuggestionService,
        IAnalyticsTracker analytics)
    {
        _actions = actions.ToList();
        _clipboardService = clipboardService;
        _logger = logger;
        _responseSuggestionService = responseSuggestionService;
        _analytics = analytics ?? throw new ArgumentNullException(nameof(analytics));
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

        var editContext = new TextEditContext(context.SourceWindow, context.OriginalText);
        var popup = new PopupForm(editContext, "Välj en åtgärd för den markerade texten.", 30000, context.OriginalText);
        popup.ActionInvoked += HandleActionInvokedAsync;
        popup.RespondRequested += HandleRespondRequestedAsync;
        popup.RespondSuggestionApplied += HandleRespondSuggestionApplied;
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
        popup.UpdateMessage("Tar fram svarsalternativ…");
        popup.SetRespondStatus("Samlar in svarsidéer…");
        popup.SetBusyState(true);

        try
        {
            var suggestions = await _responseSuggestionService.GenerateSuggestionsAsync(
                selectionText,
                CancellationToken.None);

            popup.SetBusyState(false);
            popup.SetRespondSuggestions(suggestions);
            popup.UpdateMessage("Välj hur du vill svara.");
            popup.SetRespondStatus("Välj ett svar för att fylla i det nedan.");
        }
        catch (Exception ex)
        {
            popup.SetBusyState(false);
            _logger.LogError("Failed to load response suggestions", ex);
            popup.SetRespondStatus("Det gick inte att läsa in svarsalternativ.");
            popup.UpdateMessage("Det går inte att läsa in svarsalternativ just nu.");
            popup.RestartAutoClose(4000);
        }
    }

    private void HandleRespondSuggestionApplied(object? sender, RespondSuggestionAppliedEventArgs args)
    {
        if (_disposed)
        {
            return;
        }

        string suffix = args.Tone.ToString().ToLowerInvariant();
        _analytics.TrackFunctionUsed($"respond-{suffix}");
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
        popup.UpdateMessage($"Kör {action.DisplayName}…");
        popup.SetBusyState(true);

        try
        {
            var result = await action.ExecuteAsync(args.SelectedText, args.OptionId, CancellationToken.None);
            if (!result.Success)
            {
                popup.SetBusyState(false);
                popup.UpdateMessage(result.Message ?? "Åtgärden misslyckades.");
                popup.RestartAutoClose(4000);
                return;
            }

            if (string.IsNullOrWhiteSpace(result.ReplacementText))
            {
                popup.SetBusyState(false);
                popup.UpdateMessage(result.Message ?? "Den valda åtgärden gav ingen text.");
                popup.RestartAutoClose(2500);
                return;
            }

            popup.SetBusyState(false);
            var previewResult = await popup.ShowReplacementPreviewAsync(
                result.ReplacementText,
                result.PreviewAcceptLabel ?? "Använd ersättning");

            popup.ClearActionButtons();

            var replacementText = string.IsNullOrWhiteSpace(popup.GetSelectionText())
                ? result.ReplacementText!
                : popup.GetSelectionText();

            switch (previewResult)
            {
                case ReplacementPreviewResult.Accept:
                    popup.UpdateMessage(result.SuccessMessage ?? "Ersättningen har infogats.");
                    popup.RestartAutoClose(1500);
                    break;
                case ReplacementPreviewResult.CopyToClipboard:
                    await _clipboardService.CopyToClipboardAsync(replacementText, CancellationToken.None);
                    popup.UpdateMessage("Ersättningen har kopierats till urklipp.");
                    popup.RestartAutoClose(1500);
                    break;
                default:
                    popup.UpdateMessage("Ersättningen avbröts.");
                    popup.RestartAutoClose(1500);
                    break;
            }
        }
        catch (Exception ex)
        {
            popup.SetBusyState(false);
            _logger.LogError($"Action '{action.Id}' failed", ex);
            popup.UpdateMessage("Det gick inte att slutföra den valda åtgärden.");
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
