using SentisModels;

namespace VoiceAgent
{
    /// <summary>
    /// Which speech-to-text model hears the selected language, and with which language id. SenseVoice transcribes English
    /// and Korean and is told the language explicitly (no auto-detection, which used to label short Korean as Japanese
    /// and short English as Chinese). SenseVoice has no Arabic at all, so Arabic goes to Whisper (base by default, ModelRoots.WhisperSize) with &lt;|ar|&gt;.
    /// </summary>
    public static class SpeechLanguage
    {
        public static bool UsesWhisper(Lang lang) => lang == Lang.Ar;

        public static SenseVoiceLanguage SenseVoice(Lang lang) => lang == Lang.Ko ? SenseVoiceLanguage.Ko : SenseVoiceLanguage.En;
    }
}
