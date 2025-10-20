using System;
using System.Text.RegularExpressions;

namespace GlobalTextHelper
{
    public static class Summarizer
    {
        // Extremely naive placeholder: collapse whitespace, trim length, try to end on sentence.
        public static string Summarize(string input, int maxChars)
        {
            if (string.IsNullOrWhiteSpace(input)) return string.Empty;

            string text = Regex.Replace(input, @"\s+", " ").Trim();

            if (text.Length <= maxChars) return text;

            int cut = text.LastIndexOfAny(new[] { '.', '!', '?' }, Math.Min(maxChars, text.Length - 1));
            if (cut < maxChars / 2) cut = maxChars; // fallback

            return text[..Math.Min(cut + 1, text.Length)] + " …";
        }
    }
}
