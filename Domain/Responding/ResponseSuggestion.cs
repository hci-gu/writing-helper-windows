using System;

namespace GlobalTextHelper.Domain.Responding;

public sealed class ResponseSuggestion
{
    public ResponseSuggestion(ResponseTone tone, string snippet, string fullResponse)
    {
        Tone = tone;
        Snippet = snippet ?? string.Empty;
        FullResponse = fullResponse ?? string.Empty;

        if (string.IsNullOrWhiteSpace(FullResponse))
        {
            throw new ArgumentException("Response text cannot be empty.", nameof(fullResponse));
        }
    }

    public ResponseTone Tone { get; }
    public string Snippet { get; }
    public string FullResponse { get; }
}
