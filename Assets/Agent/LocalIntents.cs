// Deterministic intents merged from the voice agent (voice-agent-unity Commands.json / Polarity.json) that
// FunctionGemma's trained dev block has no function for: stop_vacuum, volume_up, volume_down, introduce_self.
//
// Runs BEFORE generation and is deliberately narrow: it only fires on a single-intent utterance that names the
// subject and exactly one direction, and never on compound commands, other devices, or explicit numbers. Everything
// else goes to the model unchanged. It matters most for stop_vacuum: the model's closest tool is start_vacuum, so
// "stop the vacuum" would otherwise START it.
//
// Pure C# (no Unity dependency). English cues match whole words; Korean cues match inside words (Korean attaches
// endings to verb stems), the same rule the voice agent's PolarityGuard uses.
using System;
using System.Linq;
using System.Text;

namespace NeedleTest
{
    public static class LocalIntents
    {
        static readonly string[] VacuumWords = { "vacuum", "robot vacuum", "robot cleaner", "cleaner", "cleaning", "roomba", "청소기", "청소" };
        static readonly string[] VacuumStart = { "on", "start", "run", "begin", "돌려", "켜", "시작" };
        static readonly string[] VacuumStop = { "off", "stop", "halt", "pause", "dock", "cancel", "꺼", "끄", "멈춰", "멈추", "정지", "중지", "그만" };

        static readonly string[] VolumeWords = { "volume", "sound", "볼륨", "소리", "음량" };
        static readonly string[] VolumeUp = { "up", "louder", "increase", "raise", "turn up", "올려", "키워", "크게", "높여", "업" };
        static readonly string[] VolumeDown = { "down", "quieter", "softer", "decrease", "lower", "turn down", "내려", "줄여", "작게", "낮춰", "다운" };
        // "too loud" / "너무 시끄러워" don't name the action asked for; leave them to the model / cloud.
        static readonly string[] VolumeSkip = { "too", "너무", "들려" };
        static readonly string[] VolumeStandaloneUp = { "louder" };
        static readonly string[] VolumeStandaloneDown = { "quieter", "softer" };

        static readonly string[] IntroducePhrases =
        {
            "who are you", "what is your name", "whats your name", "what's your name", "what can you do",
            "introduce yourself", "tell me about yourself",
            "누구야", "누구세요", "누구니", "이름이 뭐", "이름 뭐", "자기소개", "소개해", "뭘 할 수 있", "무엇을 할 수 있",
        };

        // Words that mean the utterance is about something else as well (compound commands go to the model).
        static readonly string[] OtherSubjects =
        {
            "light", "lights", "lamp", "tv", "television", "computer", "pc", "music", "song", "weather", "time",
            "불", "조명", "티비", "텔레비전", "컴퓨터", "음악", "노래", "날씨", "시간",
        };
        static readonly string[] Conjunctions = { "and", "then", "also", "그리고", "하고", "끄고", "켜고", "다음에" };

        /// <summary>The local intent this utterance asks for (one of the four), or null to let the model handle it.</summary>
        public static string Match(string utterance)
        {
            if (string.IsNullOrWhiteSpace(utterance)) return null;
            if (utterance.IndexOf(',') >= 0 || utterance.IndexOf('，') >= 0) return null;   // compound
            var text = Normalize(utterance);
            if (text.Length == 0) return null;

            if (HasAny(text, Conjunctions) || HasAny(text, OtherSubjects)) return null;

            if (HasAny(text, IntroducePhrases)) return "introduce_self";

            if (HasAny(text, VacuumWords))
            {
                bool start = HasAny(text, VacuumStart), stop = HasAny(text, VacuumStop);
                return stop && !start ? "stop_vacuum" : null;   // start_vacuum stays with the model
            }

            // An explicit level ("volume 30") is set_volume: the model's job.
            if (text.Any(char.IsDigit) || HasAny(text, VolumeSkip)) return null;
            bool subject = HasAny(text, VolumeWords);
            bool up = HasAny(text, VolumeUp) || HasAny(text, VolumeStandaloneUp);
            bool down = HasAny(text, VolumeDown) || HasAny(text, VolumeStandaloneDown);
            bool standalone = HasAny(text, VolumeStandaloneUp) || HasAny(text, VolumeStandaloneDown);
            if ((subject || standalone) && up != down) return up ? "volume_up" : "volume_down";
            return null;
        }

        /// <summary>Lowercase, punctuation to spaces, single-spaced (apostrophes kept so "what's" survives).</summary>
        public static string Normalize(string s)
        {
            var sb = new StringBuilder(s.Length);
            bool space = false;
            foreach (var c in s.ToLowerInvariant())
            {
                bool keep = char.IsLetterOrDigit(c) || c == '\'';
                if (keep) { sb.Append(c); space = false; }
                else if (!space && sb.Length > 0) { sb.Append(' '); space = true; }
            }
            return sb.ToString().Trim();
        }

        static bool HasAny(string text, string[] cues)
        {
            var padded = $" {text} ";
            foreach (var cue in cues)
            {
                bool ascii = cue.All(c => c < 128);
                if (ascii ? padded.Contains($" {cue} ", StringComparison.Ordinal) : text.Contains(cue, StringComparison.Ordinal))
                    return true;
            }
            return false;
        }
    }
}
