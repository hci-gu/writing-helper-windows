namespace GlobalTextHelper.Infrastructure.OpenAi;

public sealed class OpenAiClientFactory : IOpenAiClientFactory
{
    private OpenAiChatClient? _client;

    public OpenAiChatClient CreateClient()
    {
        return _client ??= OpenAiChatClient.FromEnvironment();
    }
}
