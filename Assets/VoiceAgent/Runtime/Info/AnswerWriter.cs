using System.Collections.Generic;
using System.Linq;
using System.Threading;
using UnityEngine;

namespace VoiceAgent.Info
{
    /// <summary>
    /// Writes the spoken answer to a web question from the evidence <see cref="InfoAgent"/> found, with the
    /// <see cref="LlmClient"/> (qwen by default), in the selected language. Both scenes use it, so a question gets the same
    /// kind of answer in either. With no evidence the model answers from its own knowledge and says so.
    /// </summary>
    public sealed class AnswerWriter
    {
        public readonly LlmClient Llm;

        public AnswerWriter(LlmClient llm) => Llm = llm;

        /// <summary>The one-language rule every generated reply gets: naming only the target language keeps small and large
        /// models from answering in the user's language instead.</summary>
        public static string LanguageRule(Lang lang) => AppLanguage.Pick(lang,
            "Reply ONLY in English, whatever language the user writes in.",
            "Reply ONLY in Korean (polite 해요체), whatever language the user writes in.",
            "Reply ONLY in Modern Standard Arabic, in Arabic script, whatever language the user writes in.");

        /// <summary>Replies are read aloud by the TTS and shown in a small bubble.</summary>
        public const string SpokenStyle = "Write plain spoken sentences: no lists, markdown, links or emoji.";

        public static string SearchSystemPrompt(Lang lang) =>
            "You answer the user's question using the web search results provided. Ground your answer in them; if they are " +
            "insufficient, answer from your own knowledge and say so briefly. Answer directly: don't mention the search, the " +
            "results or sources, and never invent any. " +
            LanguageRule(lang) + " One or two short sentences. " + SpokenStyle;

        /// <summary>The answer, cleaned for speech, or null when the model couldn't be reached.</summary>
        public async Awaitable<string> WriteSearchAnswerAsync(string question, IReadOnlyList<string> evidence, Lang lang, CancellationToken cancel = default)
        {
            var prompt = evidence == null || evidence.Count == 0
                ? $"Question: {question}\n\n(No search results were found.)"
                : $"Question: {question}\n\nWeb search results:\n{string.Join("\n", evidence.Select(e => "- " + e))}";
            var answer = await Llm.CompleteAsync(SearchSystemPrompt(lang), prompt, cancel);
            return Clean(answer, lang);
        }

        /// <summary>
        /// Generated text as the assistants show and speak it: no emoji or markdown, and null when it isn't in the selected
        /// script (a model drifting to English in an Arabic turn) so the caller falls back instead of answering in the
        /// wrong language.
        /// </summary>
        public static string Clean(string text, Lang lang)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;
            var clean = ReplyText.Clean(text);
            if (lang == Lang.Ar && !ArabicText.ContainsArabic(clean)) return null;
            return clean.Length == 0 ? null : clean;
        }
    }
}
