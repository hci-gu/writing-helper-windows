using System;
using System.Windows.Forms;
using GlobalTextHelper.Domain.Actions;
using GlobalTextHelper.Domain.Prompting;
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
    private readonly UserSettings _userSettings;
    private readonly OpenAiClientFactory _openAiClientFactory;

    public AppHost()
    {
        var logger = new ConsoleLogger();
        var clipboardService = new ClipboardService();
        _workflow = new SelectionWorkflow();
        _userSettings = UserSettings.Load();
        _openAiClientFactory = new OpenAiClientFactory(GetStoredApiKey);

        _mainForm = new MainForm();
        _mainForm.CreateControl();
        _mainForm.ExitRequested += (_, __) => ExitThread();
        _mainForm.SettingsRequested += OnSettingsRequested;

        var promptBuilder = new TextSelectionPromptBuilder();
        var actions = new ITextAction[]
        {
            new SimplifySelectionAction(promptBuilder, _openAiClientFactory, logger),
            new RewriteSelectionAction(promptBuilder, _openAiClientFactory, logger)
        };

        _popupController = new PopupController(actions, clipboardService, logger);
        _popupController.PopupClosed += (_, __) => _workflow.MarkSelectionHandled();

        _selectionWatcher = new SelectionWatcher(clipboardService, logger, _mainForm);
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
            RequireApiKey = requireApiKey
        };

        if (dialog.ShowDialog(_mainForm) == DialogResult.OK)
        {
            _userSettings.OpenAiApiKey = dialog.OpenAiApiKey;
            _userSettings.Save();
            _openAiClientFactory.InvalidateClient();
        }
    }

    private void OnSettingsRequested(object? sender, EventArgs e) => ShowSettingsDialog();
}
