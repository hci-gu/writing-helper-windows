using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
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
    private const string AnalyticsEndpoint = "https://analytics.prod.appadem.in/aphasia-project/events/data";
    private const string ApiKeyHeaderName = "x-api-key";
    private const string ApiKey = "aphasia-project-analytics";
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly ILogger _logger;
    private readonly HttpClient _httpClient;
    private readonly string _userId;
    private readonly Func<string?> _modelProvider;

    public AnalyticsTracker(ILogger logger, string userId, Func<string?> modelProvider, HttpClient? httpClient = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new ArgumentException("User id cannot be empty.", nameof(userId));
        }

        _userId = userId;
        _modelProvider = modelProvider ?? throw new ArgumentNullException(nameof(modelProvider));
        _httpClient = httpClient ?? new HttpClient();
    }

    public event EventHandler<FunctionUsedEventArgs>? FunctionUsed;

    public void TrackFunctionUsed(string functionName)
    {
        if (string.IsNullOrWhiteSpace(functionName))
        {
            throw new ArgumentException("Function name cannot be empty.", nameof(functionName));
        }

        _logger.LogInformation($"Function used: {functionName}");
        _ = SendAnalyticsEventAsync(functionName);
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

    private async Task SendAnalyticsEventAsync(string functionName)
    {
        try
        {
            var payload = new AnalyticsEvent(functionName, DateTimeOffset.UtcNow, _userId, ResolveModel());
            using var request = new HttpRequestMessage(HttpMethod.Post, AnalyticsEndpoint)
            {
                Content = JsonContent.Create(payload, options: SerializerOptions)
            };

            request.Headers.TryAddWithoutValidation(ApiKeyHeaderName, ApiKey);

            var response = await _httpClient.SendAsync(request).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogInformation(
                    $"Analytics endpoint returned status {(int)response.StatusCode} ({response.ReasonPhrase}).");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError("Failed to send analytics event", ex);
        }
    }

    private string ResolveModel()
    {
        string? model = _modelProvider();
        return string.IsNullOrWhiteSpace(model) ? "unknown" : model;
    }

    private sealed record AnalyticsEvent(string Name, DateTimeOffset Timestamp, string UserId, string Model);
}
