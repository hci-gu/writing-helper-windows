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

    public AppHost()
    {
        var logger = new ConsoleLogger();
        var clipboardService = new ClipboardService();
        _workflow = new SelectionWorkflow();

        _mainForm = new MainForm();
        _mainForm.CreateControl();
        _mainForm.ExitRequested += (_, __) => ExitThread();

        var promptBuilder = new TextSelectionPromptBuilder();
        var clientFactory = new OpenAiClientFactory();
        var actions = new ITextAction[]
        {
            new SimplifySelectionAction(promptBuilder, clientFactory, logger),
            new RewriteSelectionAction(promptBuilder, clientFactory, logger)
        };

        _popupController = new PopupController(actions, clipboardService, logger);
        _popupController.PopupClosed += (_, __) => _workflow.MarkSelectionHandled();

        _selectionWatcher = new SelectionWatcher(clipboardService, logger, _mainForm);
        _selectionWatcher.SelectionCaptured += OnSelectionCaptured;

        MainForm = _mainForm;
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
            _mainForm.Dispose();
        }

        base.Dispose(disposing);
    }
}
