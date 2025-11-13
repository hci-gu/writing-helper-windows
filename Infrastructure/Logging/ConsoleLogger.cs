using System;

namespace GlobalTextHelper.Infrastructure.Logging;

public sealed class ConsoleLogger : ILogger
{
    public void LogInformation(string message)
    {
        Console.WriteLine($"[info] {message}");
    }

    public void LogError(string message, Exception exception)
    {
        Console.WriteLine($"[error] {message}: {exception.Message}");
    }
}
