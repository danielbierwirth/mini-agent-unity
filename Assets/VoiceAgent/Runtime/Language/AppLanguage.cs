using System;
using UnityEngine;

namespace VoiceAgent
{
    /// <summary>
    /// The one language setting both scenes share (EN / KR / AR toggle, see <see cref="LanguageToggle"/>). It picks the
    /// speech-to-text language, the language Nova / Tiger reply in, and the TTS voice language. It survives scene
    /// switches (static) and app restarts (PlayerPrefs); <see cref="Changed"/> fires on every switch so listeners update
    /// mid-session without a reload.
    /// </summary>
    public static class AppLanguage
    {
        const string PrefKey = "VoiceAgent.Language";

        static Lang? s_Current;

        public static event Action<Lang> Changed;

        public static Lang Current
        {
            get
            {
                if (s_Current == null)
                {
                    var code = "en";
                    try { code = PlayerPrefs.GetString(PrefKey, "en"); }
                    catch (Exception) { /* PlayerPrefs unavailable (e.g. edit-mode tests): default */ }
                    s_Current = Parse(code);
                }
                return s_Current.Value;
            }
        }

        public static void Set(Lang lang)
        {
            if (Current == lang) return;
            s_Current = lang;
            try
            {
                PlayerPrefs.SetString(PrefKey, Code(lang));
                PlayerPrefs.Save();
            }
            catch (Exception) { /* still switches for this session */ }
            Debug.Log($"[Language] {Code(lang)}");
            Changed?.Invoke(lang);
        }

        /// <summary>ISO 639-1 code: en, ko, ar.</summary>
        public static string Code(Lang lang) => lang switch { Lang.Ko => "ko", Lang.Ar => "ar", _ => "en" };

        public static Lang Parse(string code) => (code ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "ko" or "kr" => Lang.Ko,
            "ar" => Lang.Ar,
            _ => Lang.En,
        };

        /// <summary>One of three strings by language.</summary>
        public static string Pick(Lang lang, string en, string ko, string ar) => lang switch { Lang.Ko => ko, Lang.Ar => ar, _ => en };

        public static string Pick(string en, string ko, string ar) => Pick(Current, en, ko, ar);

        // Enter Play Mode without a domain reload keeps statics: drop listeners from the last session (the setting stays).
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetListeners() => Changed = null;
    }
}
