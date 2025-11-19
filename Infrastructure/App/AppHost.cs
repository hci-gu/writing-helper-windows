using System;
using System.Collections.Generic;
using System.Windows.Forms;
using GlobalTextHelper.Domain.Actions;
using GlobalTextHelper.Domain.Prompting;
using GlobalTextHelper.Domain.Responding;
using GlobalTextHelper.Domain.Selection;
using GlobalTextHelper.Infrastructure.Clipboard;
using GlobalTextHelper.Infrastructure.Logging;
using GlobalTextHelper.Infrastructure.OpenAi;
using GlobalTextHelper.Infrastructure.Selection;
using GlobalTextHelper.UI;

namespace GlobalTextHelper.Infrastructure.App;

internal sealed class AppHost : ApplicationContext
{
    private readonly MainForm _mainForm;
    private readonly SelectionWorkflow _workflow;
    private readonly SelectionWatcher _selectionWatcher;
    private readonly PopupController _popupController;
    private readonly ResponseSuggestionService _responseSuggestionService;
    private readonly UserSettings _userSettings;
    private readonly OpenAiClientFactory _openAiClientFactory;
    private readonly ILogger _logger;
    private readonly IClipboardService _clipboardService;
    private readonly IReadOnlyList<ITextAction> _actions;
    private bool _isEditorOpen;

    public AppHost()
    {
        _logger = new ConsoleLogger();
        _clipboardService = new ClipboardService();
        _workflow = new SelectionWorkflow();
        _userSettings = UserSettings.Load();
        _openAiClientFactory = new OpenAiClientFactory(GetStoredApiKey);

        _mainForm = new MainForm();
        _mainForm.AutoShowOnSelection = _userSettings.AutoShowOnSelection;
        _mainForm.CreateControl();
        _mainForm.ExitRequested += (_, __) => ExitThread();
        _mainForm.SettingsRequested += OnSettingsRequested;
        _mainForm.EditorRequested += OnEditorRequested;
        _mainForm.AutoShowOnSelectionChanged += OnAutoShowOnSelectionChanged;

        var promptBuilder = new TextSelectionPromptBuilder(() => _userSettings.PromptPreamble);
        _responseSuggestionService = new ResponseSuggestionService(
            () => _userSettings.PromptPreamble,
            _openAiClientFactory,
            _logger);
        _actions = new ITextAction[]
        {
            new SimplifySelectionAction(promptBuilder, _openAiClientFactory, _logger),
            new RewriteSelectionAction(promptBuilder, _openAiClientFactory, _logger)
        };

        _popupController = new PopupController(_actions, _clipboardService, _logger, _responseSuggestionService);
        _popupController.PopupClosed += (_, __) => _workflow.MarkSelectionHandled();

        _selectionWatcher = new SelectionWatcher(_clipboardService, _logger, _mainForm, () => _userSettings.AutoShowOnSelection);
        _selectionWatcher.SelectionCaptured += OnSelectionCaptured;

        MainForm = _mainForm;

        if (!HasConfiguredApiKey())
        {
            if (_mainForm.IsHandleCreated)
            {
                _mainForm.BeginInvoke(new Action(() => ShowSettingsDialog(requireApiKey: true)));
            }
            else
            {
                EventHandler? handler = null;
                handler = (_, __) =>
                {
                    _mainForm.HandleCreated -= handler;
                    _mainForm.BeginInvoke(new Action(() => ShowSettingsDialog(requireApiKey: true)));
                };
                _mainForm.HandleCreated += handler;
            }
        }
    }

    private void OnSelectionCaptured(object? sender, SelectionCapturedEventArgs e)
    {
        if (_isEditorOpen)
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
            _mainForm.AutoShowOnSelectionChanged -= OnAutoShowOnSelectionChanged;
            _mainForm.Dispose();
        }

        base.Dispose(disposing);
    }

    private string? GetStoredApiKey()
    {
        return string.IsNullOrWhiteSpace(_userSettings.OpenAiApiKey)
            ? null
            : _userSettings.OpenAiApiKey;
    }

    private bool HasConfiguredApiKey()
    {
        if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("OPENAI_API_KEY")))
        {
            return true;
        }

        return !string.IsNullOrWhiteSpace(_userSettings.OpenAiApiKey);
    }

    private void ShowSettingsDialog(bool requireApiKey = false)
    {
        using var dialog = new SettingsForm
        {
            OpenAiApiKey = _userSettings.OpenAiApiKey,
            PromptPreamble = _userSettings.PromptPreamble,
            RequireApiKey = requireApiKey
        };

        if (dialog.ShowDialog(_mainForm) == DialogResult.OK)
        {
            _userSettings.OpenAiApiKey = dialog.OpenAiApiKey;
            _userSettings.PromptPreamble = dialog.PromptPreamble;
            _userSettings.Save();
            _openAiClientFactory.InvalidateClient();
        }
    }

    private void OnSettingsRequested(object? sender, EventArgs e) => ShowSettingsDialog();

    private void OnAutoShowOnSelectionChanged(object? sender, EventArgs e)
    {
        _userSettings.AutoShowOnSelection = _mainForm.AutoShowOnSelection;
        _userSettings.Save();
    }

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
