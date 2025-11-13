using System;

namespace GlobalTextHelper.Infrastructure.OpenAi;

public sealed class OpenAiClientFactory : IOpenAiClientFactory
{
    private readonly Func<string?> _apiKeyProvider;
    private OpenAiChatClient? _client;

    public OpenAiClientFactory(Func<string?> apiKeyProvider)
    {
        _apiKeyProvider = apiKeyProvider ?? throw new ArgumentNullException(nameof(apiKeyProvider));
    }

    public OpenAiChatClient CreateClient()
    {
        if (_client is not null)
        {
            return _client;
        }

        string? envApiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        if (!string.IsNullOrWhiteSpace(envApiKey))
        {
            _client = OpenAiChatClient.FromEnvironment();
        }
        else
        {
            string? apiKey = _apiKeyProvider();
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                throw new InvalidOperationException("Set an OpenAI API key in the environment or application settings.");
            }

            _client = new OpenAiChatClient(apiKey);
        }

        return _client;
    }

    public void InvalidateClient()
    {
        _client?.Dispose();
        _client = null;
    }
}
