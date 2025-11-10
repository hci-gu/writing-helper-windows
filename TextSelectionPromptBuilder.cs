using System;
using System.Threading;
using System.Threading.Tasks;

namespace GlobalTextHelper
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

            return $"{SimplifierInstructions}\n\nInput text:\n\"\"\n{sanitized}\n\"\"\n\nSimplified response:";
        }

        /// <summary>
        /// Uses the provided <see cref="OpenAiChatClient"/> to simplify the supplied selection.
        /// </summary>
        /// <param name="client">The OpenAI chat client used to issue the request.</param>
        /// <param name="selectedText">The highlighted text to simplify.</param>
        /// <param name="temperature">Generation temperature to use for the request.</param>
        /// <param name="maxOutputTokens">Optional token cap for the model's response.</param>
        /// <param name="cancellationToken">Cancellation token to observe during the request.</param>
        /// <returns>The simplified version of <paramref name="selectedText"/>.</returns>
        public Task<string> SimplifySelectionAsync(
            OpenAiChatClient client,
            string selectedText,
            double temperature = 0.2,
            int? maxOutputTokens = 300,
            CancellationToken cancellationToken = default)
        {
            if (client is null)
                throw new ArgumentNullException(nameof(client));

            string prompt = BuildSimplificationPrompt(selectedText);
            return client.SendPromptAsync(prompt, temperature, maxOutputTokens, cancellationToken);
        }
    }
}
