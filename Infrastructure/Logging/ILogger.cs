namespace GlobalTextHelper.Infrastructure.Logging;

public interface ILogger
{
    void LogInformation(string message);
    void LogError(string message, Exception exception);
}
