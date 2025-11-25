using System;

namespace GlobalTextHelper.Infrastructure.OpenAi;

public sealed class OpenAiClientFactory : IOpenAiClientFactory
{
    private readonly Func<string?> _apiKeyProvider;
    private readonly Func<string?> _modelProvider;
    private OpenAiChatClient? _client;

    public OpenAiClientFactory(Func<string?> apiKeyProvider, Func<string?> modelProvider)
    {
        _apiKeyProvider = apiKeyProvider ?? throw new ArgumentNullException(nameof(apiKeyProvider));
        _modelProvider = modelProvider ?? throw new ArgumentNullException(nameof(modelProvider));
    }

    public OpenAiChatClient CreateClient()
    {
        if (_client is not null)
        {
            return _client;
        }

        string? apiKey = _apiKeyProvider();
        string model = ResolveModel();
        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            _client = new OpenAiChatClient(apiKey, model);
        }
        else
        {
            string? envApiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
            if (string.IsNullOrWhiteSpace(envApiKey))
            {
                throw new InvalidOperationException("Ange en OpenAI-API-nyckel i miljön eller i appens inställningar.");
            }

            _client = OpenAiChatClient.FromEnvironment(model);
        }

        return _client;
    }

    public void InvalidateClient()
    {
        _client?.Dispose();
        _client = null;
    }

    private string ResolveModel()
    {
        string? model = _modelProvider();
        return string.IsNullOrWhiteSpace(model) ? OpenAiChatClient.DefaultModel : model;
    }
}
