using System;
using UnityEngine;

namespace VoiceAgent
{
    /// <summary>
    /// Who the assistant is (Resources/Persona.json): the self-introduction is a fixed line picked by the
    /// "introduce_self" command, and the system prompt shapes every Gemma3 chat reply. Each user-facing line has an
    /// English, Korean (_ko) and Arabic (_ar) version.
    /// </summary>
    [Serializable]
    public sealed class Persona
    {
        public string name, voice, greeting, introduction, system_prompt, reply_language, search_reply, not_sure, search_miss_prefix, low_confidence;
        public string greeting_ko, introduction_ko, reply_language_ko, not_sure_ko, search_miss_prefix_ko, low_confidence_ko;
        public string greeting_ar, introduction_ar, reply_language_ar, not_sure_ar, search_miss_prefix_ar, low_confidence_ar;

        /// <summary>
        /// The system prompt for a turn in <paramref name="lang"/>: the persona, <see cref="search_reply"/> when Gemma answers
        /// a search question the web couldn't, and one line naming only that language. Without that line Gemma answers
        /// Korean in English; a rule naming both languages made it answer English in Korean.
        /// </summary>
        public string SystemPrompt(Lang lang, bool search = false) =>
            $"{system_prompt} {(search ? search_reply + " " : string.Empty)}{Pick(lang, reply_language, reply_language_ko, reply_language_ar)}";

        public string Greeting(Lang lang) => Pick(lang, greeting, greeting_ko, greeting_ar);
        public string Introduction(Lang lang) => Pick(lang, introduction, introduction_ko, introduction_ar);
        public string NotSure(Lang lang) => Pick(lang, not_sure, not_sure_ko, not_sure_ar);
        public string SearchMissPrefix(Lang lang) => Pick(lang, search_miss_prefix, search_miss_prefix_ko, search_miss_prefix_ar);

        /// <summary>The low-confidence question with the top suggestion filled in.</summary>
        public string Clarify(Command command, Lang lang = Lang.En) => Pick(lang, low_confidence, low_confidence_ko, low_confidence_ar).Replace("{command}", command.Label(lang));

        /// <summary>The line in <paramref name="lang"/>, or the English one when that translation is missing.</summary>
        static string Pick(Lang lang, string en, string ko, string ar)
        {
            var line = AppLanguage.Pick(lang, en, ko, ar);
            return string.IsNullOrEmpty(line) ? en : line;
        }

        public static Persona Load() => JsonUtility.FromJson<Persona>(Resources.Load<TextAsset>("Persona").text);
    }
}
