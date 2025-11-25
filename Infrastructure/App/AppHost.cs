using System;
using System.Collections.Generic;
using System.Windows.Forms;
using GlobalTextHelper.Domain.Actions;
using GlobalTextHelper.Domain.Prompting;
using GlobalTextHelper.Domain.Responding;
using GlobalTextHelper.Domain.Selection;
using GlobalTextHelper.Infrastructure.Clipboard;
using GlobalTextHelper.Infrastructure.Analytics;
using GlobalTextHelper.Infrastructure.Logging;
using GlobalTextHelper.Infrastructure.OpenAi;
using GlobalTextHelper.Infrastructure.Selection;
using GlobalTextHelper.UI;

namespace GlobalTextHelper.Infrastructure.App;

internal sealed class AppHost : ApplicationContext
{
    private const string HardcodedOpenAiApiKey = "REPLACE_WITH_OPENAI_API_KEY";
    private readonly MainForm _mainForm;
    private readonly SelectionWorkflow _workflow;
    private readonly SelectionWatcher _selectionWatcher;
    private readonly PopupController _popupController;
    private readonly ResponseSuggestionService _responseSuggestionService;
    private readonly UserSettings _userSettings;
    private readonly OpenAiClientFactory _openAiClientFactory;
    private readonly ILogger _logger;
    private readonly IClipboardService _clipboardService;
    private readonly IAnalyticsTracker _analytics;
    private readonly IReadOnlyList<ITextAction> _actions;
    private bool _isEditorOpen;

    public AppHost()
    {
        _logger = new ConsoleLogger();
        _analytics = new AnalyticsTracker(_logger);
        _clipboardService = new ClipboardService();
        _workflow = new SelectionWorkflow();
        _userSettings = UserSettings.Load();
        _openAiClientFactory = new OpenAiClientFactory(GetConfiguredApiKey);

        _mainForm = new MainForm();
        _mainForm.CreateControl();
        _mainForm.ExitRequested += (_, __) => ExitThread();
        _mainForm.SettingsRequested += OnSettingsRequested;
        _mainForm.EditorRequested += OnEditorRequested;

        var promptBuilder = new TextSelectionPromptBuilder(() => _userSettings.PromptPreamble);
        _responseSuggestionService = new ResponseSuggestionService(
            () => _userSettings.PromptPreamble,
            _openAiClientFactory,
            _logger,
            _analytics);
        _actions = new ITextAction[]
        {
            new SimplifySelectionAction(promptBuilder, _openAiClientFactory, _logger, _analytics),
            new RewriteSelectionAction(promptBuilder, _openAiClientFactory, _logger, _analytics)
        };

        _popupController = new PopupController(
            _actions,
            _clipboardService,
            _logger,
            _responseSuggestionService,
            _analytics);
        _popupController.PopupClosed += (_, __) => _workflow.MarkSelectionHandled();

        _selectionWatcher = new SelectionWatcher(_clipboardService, _logger);
        _selectionWatcher.SelectionCaptured += OnSelectionCaptured;

        MainForm = _mainForm;

    }

    private void OnSelectionCaptured(object? sender, SelectionCapturedEventArgs e)
    {
        if (_isEditorOpen || !_userSettings.ShowPopupOnCopy)
        {
            return;
        }

        if (!_workflow.TryHandleSelection(e, out var context) || context is null)
        {
            return;
        }

        _mainForm.BeginInvoke(new Action(() => _popupController.ShowForSelection(context)));
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _selectionWatcher.SelectionCaptured -= OnSelectionCaptured;
            _selectionWatcher.Dispose();
            _popupController.Dispose();
            _mainForm.SettingsRequested -= OnSettingsRequested;
            _mainForm.EditorRequested -= OnEditorRequested;
            _mainForm.Dispose();
        }

        base.Dispose(disposing);
    }

    private string? GetConfiguredApiKey()
    {
        return string.IsNullOrWhiteSpace(HardcodedOpenAiApiKey)
            ? null
            : HardcodedOpenAiApiKey;
    }

    private void ShowSettingsDialog()
    {
        using var dialog = new SettingsForm
        {
            PromptPreamble = _userSettings.PromptPreamble,
            ShowPopupOnCopy = _userSettings.ShowPopupOnCopy
        };

        if (dialog.ShowDialog(_mainForm) == DialogResult.OK)
        {
            _userSettings.PromptPreamble = dialog.PromptPreamble;
            _userSettings.ShowPopupOnCopy = dialog.ShowPopupOnCopy;
            _userSettings.Save();
            _openAiClientFactory.InvalidateClient();
        }
    }

    private void OnSettingsRequested(object? sender, EventArgs e) => ShowSettingsDialog();

    private void OnEditorRequested(object? sender, EventArgs e)
    {
        using var editor = new EditorForm(_actions, _logger);
        try
        {
            _isEditorOpen = true;
            editor.ShowDialog(_mainForm);
        }
        finally
        {
            _isEditorOpen = false;
        }
    }
}
