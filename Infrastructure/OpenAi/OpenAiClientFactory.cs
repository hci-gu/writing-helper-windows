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

        string? apiKey = _apiKeyProvider();
        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            _client = new OpenAiChatClient(apiKey);
        }
        else
        {
            string? envApiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
            if (string.IsNullOrWhiteSpace(envApiKey))
            {
                throw new InvalidOperationException("Ange en OpenAI-API-nyckel i miljön eller i appens inställningar.");
            }

            _client = OpenAiChatClient.FromEnvironment();
        }

        return _client;
    }

    public void InvalidateClient()
    {
        _client?.Dispose();
        _client = null;
    }
}
