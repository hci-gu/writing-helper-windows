using System;
using System.Threading;
using System.Threading.Tasks;
using GlobalTextHelper.Infrastructure.OpenAi;

namespace GlobalTextHelper.Domain.Prompting
{
    /// <summary>
    /// Builds prompts for simplifying highlighted text and optionally
    /// delegates the call to <see cref="OpenAiChatClient"/>.
    /// </summary>
    public sealed class TextSelectionPromptBuilder
    {
        private const string SimplifierInstructions =
            "You are an assistant that simplifies messages to make them easier to respond to. " +
            "Given any input text, extract only the essential information needed for a clear and concise response.\n\n" +
            "Include:\n\n" +
            "Purpose of the message (e.g., invitation, request, question).\n" +
            "Key details (e.g., time, date, place, options to choose from).\n" +
            "Instructions for a response (e.g., \"Choose one option\" or \"Confirm attendance\").\n\n" +
            "Remove:\n\n" +
            "Greetings, unnecessary explanations, or extra context.\n" +
            "Emotional or polite phrases unless they are crucial to the response.\n\n" +
            "Format the output:\n\n" +
            "Use bullet points or short sentences.\n" +
            "Be as concise as possible while keeping all necessary details.\n\n" +
            "Example Input:\n\"Hej, vi vill bjuda in dig till ett födelsedagsfirande nästa helg på söndag kl. 18 på Restaurang Måltiden. " +
            "Välj gärna din mat i förväg från följande: 1) Lax, 2) Pasta, 3) Hamburgare. Säg till om inget passar. Hoppas du kan komma!\"\n\n" +
            "Output:\n" +
            "Födelsedagsfirande: Söndag kl. 18, Restaurang Måltiden\n" +
            "Välj mat:\n" +
            "Lax\n" +
            "Pasta\n" +
            "Hamburgare\n" +
            "Säg till om inget passar";

        private readonly Func<string?> _promptPreambleProvider;

        public TextSelectionPromptBuilder()
            : this(() => null)
        {
        }

        public TextSelectionPromptBuilder(Func<string?> promptPreambleProvider)
        {
            _promptPreambleProvider = promptPreambleProvider ?? throw new ArgumentNullException(nameof(promptPreambleProvider));
        }

        /// <summary>
        /// Builds a prompt instructing the model to simplify the supplied text selection.
        /// </summary>
        /// <param name="selectedText">The raw text captured from the user's selection.</param>
        /// <returns>A prompt string that can be sent to <see cref="OpenAiChatClient"/>.</returns>
        /// <exception cref="ArgumentException">Thrown when <paramref name="selectedText"/> is null or whitespace.</exception>
        public string BuildSimplificationPrompt(string selectedText)
        {
            if (string.IsNullOrWhiteSpace(selectedText))
                throw new ArgumentException("Selected text cannot be empty.", nameof(selectedText));

            string sanitized = selectedText.Trim();

            string promptBody =
                $"{SimplifierInstructions}\n\nInput text:\n\"\"\n{sanitized}\n\"\"\n\nSimplified response:";

            return ApplyPromptPreamble(promptBody);
        }

        /// <summary>
        /// Uses the provided <see cref="OpenAiChatClient"/> to simplify the supplied selection.
        /// </summary>
        /// <param name="client">The OpenAI chat client used to issue the request.</param>
        /// <param name="selectedText">The highlighted text to simplify.</param>
        /// <param name="temperature">Generation temperature to use for the request.</param>
        /// <param name="cancellationToken">Cancellation token to observe during the request.</param>
        /// <returns>The simplified version of <paramref name="selectedText"/>.</returns>
        public Task<string> SimplifySelectionAsync(
            OpenAiChatClient client,
            string selectedText,
            double temperature = 0.2,
            CancellationToken cancellationToken = default)
        {
            if (client is null)
                throw new ArgumentNullException(nameof(client));

            string prompt = BuildSimplificationPrompt(selectedText);
            return client.SendPromptAsync(prompt, temperature, cancellationToken);
        }

        /// <summary>
        /// Builds a prompt instructing the model to rewrite the supplied text using the specified style.
        /// </summary>
        /// <param name="selectedText">The raw text captured from the user's selection.</param>
        /// <param name="style">The rewrite style to apply.</param>
        /// <returns>A prompt string that can be sent to <see cref="OpenAiChatClient"/>.</returns>
        /// <exception cref="ArgumentException">Thrown when <paramref name="selectedText"/> or <paramref name="style"/> are null or whitespace.</exception>
        public string BuildRewritePrompt(string selectedText, string style)
        {
            if (string.IsNullOrWhiteSpace(selectedText))
                throw new ArgumentException("Selected text cannot be empty.", nameof(selectedText));

            if (string.IsNullOrWhiteSpace(style))
                throw new ArgumentException("Style cannot be empty.", nameof(style));

            string sanitized = selectedText.Trim();
            string normalizedStyle = style.Trim().ToLowerInvariant();
            string instructions = normalizedStyle switch
            {
                "minimal" => "Rewrite the text to only have the essential information needed for a response, remove any unnecessary details, fluffy language, or extra context.",
                "spelling" => "Rewrite the text to correct any spelling or grammatical errors. Do not change anything else whatsoever.",
                "shorter" => "Rewrite the text to make it significantly more concise, even if the original is already short. Remove as many words as possible while keeping the meaning intact. Aim for brevity over detail, condensing the text to its absolute essentials. Always make the text 25% shorter than the original, cut out sentences if needed.",
                "longer" => "Expand the rewritten text by adding relevant details, examples, or context to make it noticeably longer. Avoid introducing new ideas, but enhance clarity and completeness where possible.",
                "formal" => "Adjust the rewritten text to use a more formal tone. Use complete sentences and proper grammar, avoiding contractions and overly casual phrasing.",
                "casual" => "Make the rewritten text sound more relaxed and conversational. Use contractions, simple phrasing, and a friendly tone while keeping the meaning clear.",
                _ => throw new ArgumentException($"Unknown rewrite style: '{style}'.", nameof(style))
            };

            string promptBody = "You are an assistant that rewrites text according to a specified style. " +
                                 "Follow the provided instructions exactly and only rewrite the text without adding commentary.\n\n" +
                                 $"Requested style: {normalizedStyle}\n" +
                                 $"Style instructions: {instructions}\n\n" +
                                 "Original text:\n\"\"\"\n" + sanitized + "\n\"\"\"\n\n" +
                                 "Rewritten text:";

            return ApplyPromptPreamble(promptBody);
        }

        /// <summary>
        /// Uses the provided <see cref="OpenAiChatClient"/> to rewrite the supplied selection using the specified style.
        /// </summary>
        /// <param name="client">The OpenAI chat client used to issue the request.</param>
        /// <param name="selectedText">The highlighted text to rewrite.</param>
        /// <param name="style">The rewrite style to apply.</param>
        /// <param name="temperature">Generation temperature to use for the request.</param>
        /// <param name="cancellationToken">Cancellation token to observe during the request.</param>
        /// <returns>The rewritten version of <paramref name="selectedText"/>.</returns>
        public Task<string> RewriteSelectionAsync(
            OpenAiChatClient client,
            string selectedText,
            string style,
            double temperature = 0.3,
            CancellationToken cancellationToken = default)
        {
            if (client is null)
                throw new ArgumentNullException(nameof(client));

            string prompt = BuildRewritePrompt(selectedText, style);
            return client.SendPromptAsync(prompt, temperature, cancellationToken);
        }

        private string ApplyPromptPreamble(string promptBody)
        {
            string? preamble = _promptPreambleProvider()?.Trim();
            if (string.IsNullOrWhiteSpace(preamble))
            {
                return promptBody;
            }

            return $"User context:\n{preamble}\n\n{promptBody}";
        }
    }
}
