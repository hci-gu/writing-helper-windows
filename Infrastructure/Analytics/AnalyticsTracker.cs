using System;
using GlobalTextHelper.Infrastructure.Logging;

namespace GlobalTextHelper.Infrastructure.Analytics;

public sealed class FunctionUsedEventArgs : EventArgs
{
    public FunctionUsedEventArgs(string functionName)
    {
        FunctionName = functionName ?? throw new ArgumentNullException(nameof(functionName));
    }

    public string FunctionName { get; }
}

public interface IAnalyticsTracker
{
    event EventHandler<FunctionUsedEventArgs>? FunctionUsed;

    void TrackFunctionUsed(string functionName);
}

public sealed class AnalyticsTracker : IAnalyticsTracker
{
    private readonly ILogger _logger;

    public AnalyticsTracker(ILogger logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public event EventHandler<FunctionUsedEventArgs>? FunctionUsed;

    public void TrackFunctionUsed(string functionName)
    {
        if (string.IsNullOrWhiteSpace(functionName))
        {
            throw new ArgumentException("Function name cannot be empty.", nameof(functionName));
        }

        _logger.LogInformation($"Function used: {functionName}");
        var handler = FunctionUsed;
        if (handler is null)
        {
            return;
        }

        try
        {
            handler.Invoke(this, new FunctionUsedEventArgs(functionName));
        }
        catch (Exception ex)
        {
            _logger.LogError("Analytics event handler failed", ex);
        }
    }
}
