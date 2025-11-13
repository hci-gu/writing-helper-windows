namespace GlobalTextHelper.Domain.Actions;

public sealed class TextActionResult
{
    private TextActionResult(
        bool success,
        string? message,
        string? replacementText,
        string? previewAcceptLabel,
        string? successMessage)
    {
        Success = success;
        Message = message;
        ReplacementText = replacementText;
        PreviewAcceptLabel = previewAcceptLabel;
        SuccessMessage = successMessage;
    }

    public bool Success { get; }
    public string? Message { get; }
    public string? ReplacementText { get; }
    public string? PreviewAcceptLabel { get; }
    public string? SuccessMessage { get; }

    public static TextActionResult Failure(string message)
        => new(false, message, null, null, null);

    public static TextActionResult Replacement(string replacementText, string previewAcceptLabel, string successMessage)
        => new(true, null, replacementText, previewAcceptLabel, successMessage);
}
