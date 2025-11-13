namespace GlobalTextHelper.Infrastructure.OpenAi;

public interface IOpenAiClientFactory
{
    OpenAiChatClient CreateClient();
}
