// Ported from voice-agent-unity (VoiceAgent/Runtime/Audio/SpeechLanguage.cs); namespace changed only.
using SentisModels;

namespace NeedleTest
{
    /// <summary>
    /// SenseVoice detects five languages, but the home agent understands English and Korean. Short utterances are sometimes
    /// labelled with a neighbour (Korean as Japanese, English as Chinese); those are decoded again with the language
    /// that was most likely meant.
    /// </summary>
    public static class SpeechLanguage
    {
        public static SenseVoiceLanguage? Retry(SenseVoiceLanguage detected) => detected switch
        {
            SenseVoiceLanguage.Ja => SenseVoiceLanguage.Ko,
            SenseVoiceLanguage.Zh or SenseVoiceLanguage.Yue => SenseVoiceLanguage.En,
            _ => null,
        };
    }
}
