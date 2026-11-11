using System;
using System.Linq;
using SentisModels;

namespace VoiceAgent
{
    /// <summary>
    /// Which speech-to-text model hears the selected language, and with which language id. SenseVoice transcribes English
    /// and Korean and is told the language explicitly (no auto-detection, which used to label short Korean as Japanese
    /// and short English as Chinese). SenseVoice has no Arabic at all, so Arabic goes to Whisper (the dialect fine-tune of small by default, ModelRoots.WhisperSize) with &lt;|ar|&gt;.
    /// </summary>
    public static class SpeechLanguage
    {
        public static bool UsesWhisper(Lang lang) => lang == Lang.Ar;

        public static SenseVoiceLanguage SenseVoice(Lang lang) => lang == Lang.Ko ? SenseVoiceLanguage.Ko : SenseVoiceLanguage.En;

        /// <summary>
        /// Whisper's "previous text" for Arabic: the words <see cref="ArabicCommands"/> listens for, in the spelling it
        /// expects. Whisper reads it as what was said just before, so a misheard command word ("تفاز", "المكان") is pulled
        /// toward the real one (التلفاز, المكنسة). Fully vocalized verbs (شغّل) help the small models hear the shadda.
        /// </summary>
        public const string ArabicPrompt =
            "أطفئ الأضواء، شغّل الضوء في المطبخ وغرفة النوم وغرفة المعيشة. شغّل التلفاز، أطفئ التلفاز، شغّل الكمبيوتر. " +
            "ارفع الصوت، اخفض الصوت. شغّل الموسيقى، أوقف الموسيقى. شغّل المكنسة، أوقف المكنسة. كيف الطقس اليوم؟ كم الساعة؟";

        static readonly char[] s_ClauseBreaks = { '،', ',', '.', '؟', '?', '!', '؛' };

        static string[] Clauses(string text) =>
            text.Split(s_ClauseBreaks, StringSplitOptions.RemoveEmptyEntries).Select(c => c.Trim()).Where(c => c.Length > 0).ToArray();

        static readonly string[] s_PromptClauses = Clauses(ArabicPrompt);

        /// <summary>
        /// Whisper's failure modes on non-speech, which a prompt makes dangerous because the prompt is full of commands:
        /// on silence or noise it repeats a prompt clause ("شغّل الموسيقى، شغّل الموسيقى، …"), and on unclear speech it can
        /// splice prompt clauses in ("أطفئ أضواء، أطفئ أضواء، شغل الكمبيوتر"). Rejected: any clause said twice, two or more
        /// clauses copied verbatim from the prompt, or more text than <paramref name="seconds"/> of speech can hold.
        /// A real command ("أطفئ الضوء وشغّل التلفاز") joins its parts with و, not the prompt's commas, so it passes.
        /// </summary>
        public static bool IsHallucination(string transcript, double seconds)
        {
            if (string.IsNullOrWhiteSpace(transcript)) return false;
            var clauses = Clauses(transcript);
            if (clauses.Length != clauses.Distinct(StringComparer.Ordinal).Count()) return true;
            if (clauses.Count(c => s_PromptClauses.Contains(c, StringComparer.Ordinal)) >= 2) return true;
            // Spoken Arabic runs ~10-15 characters a second; 25 plus slack leaves room for fast speakers.
            return transcript.Length > 25 * seconds + 20;
        }
    }
}
