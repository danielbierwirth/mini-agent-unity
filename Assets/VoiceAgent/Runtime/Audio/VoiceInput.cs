using System;
using System.Collections.Generic;
using System.Reflection;
using SentisModels;
using Unity.InferenceEngine;
using UnityEngine;

// Copied from voice-agent-unity and shared by both scenes (VoiceAgentDemo and MiniAgentScene's VoiceCommandInput).
// mini-agent changes: IsLoaded, the VAD backlog is drained with Pump()'s return value (silero-vad 0.1.2), and
// SenseVoice spreads its encoder pass over frames on mobile (sensevoice 0.1.1+) so a 3D scene keeps its frame rate.
// Language: the selected AppLanguage, set explicitly (SenseVoice for English/Korean, Whisper for Arabic).
namespace VoiceAgent
{
    /// <summary>
    /// Microphone → Silero VAD → SenseVoice, one utterance per <see cref="Listen"/> (tap to talk).
    /// Speech start opens a SenseVoice utterance (with 0.4 s of pre-roll), speech end closes it; partials
    /// arrive while talking and one final per utterance. The language is the selected <see cref="AppLanguage"/>, passed
    /// to the model explicitly: SenseVoice for English and Korean; Arabic, which SenseVoice can't transcribe, goes to
    /// Whisper (ModelRoots.WhisperSize) with the Arabic language token (final transcript only, no partials).
    /// </summary>
    public sealed class VoiceInput : IDisposable
    {
        const int PreRollSamples = MicrophoneStream.SampleRate * 2 / 5;
        const float EndSilenceSeconds = 0.7f, NoSpeechTimeout = 6f;
        const int VadFrameSamples = 512, MaxVadFramesPerTick = 16;
        // Mobile GPUs share the queue with rendering: ~1/40 of the encoder per frame instead of one long stall.
        const int MobileFramesPerInference = 40;

        readonly MicrophoneStream m_Mic = new();
        readonly SileroVad m_Vad;
        readonly SenseVoiceRecognizer m_Stt;
        WhisperRecognizer m_Whisper;
        Lang m_Language;
        bool m_WhisperUtterance;
        readonly float[] m_PreRoll = new float[PreRollSamples];
        readonly List<float> m_Utterance = new();
        float[] m_Finished = Array.Empty<float>();
        long m_PreRollEnd;
        bool m_Listening, m_InSpeech, m_Disposed;
        float m_ListenStarted;

        public event Action<string> Partial;
        public event Action<string> Final;
        public event Action SpeechStarted, ListeningStopped;

        public bool IsListening => m_Listening;
        public float Level => m_Mic.Level;
        public SenseVoiceRecognizer Recognizer => m_Stt;
        public bool IsLoaded => m_Stt.IsLoaded;
        public Lang Language => m_Language;
        /// <summary>Whisper's last transcription time, for the timing panel.</summary>
        public double LastWhisperMs => m_Whisper?.LastMs ?? 0;

        public VoiceInput()
        {
            m_Vad = new SileroVad(BackendType.CPU, 0.5f, 0.35f, EndSilenceSeconds);
            m_Vad.Load(ModelRoots.Vad);
            m_Stt = new SenseVoiceRecognizer(BackendType.GPUCompute, SenseVoiceLanguage.Auto, partialIntervalSeconds: 0.4f);
            if (Application.isMobilePlatform) m_Stt.FramesPerInference = MobileFramesPerInference;
            m_Stt.Load(ModelRoots.Stt);

            m_Mic.Samples += OnSamples;
            m_Vad.SpeechStarted += OnSpeechStarted;
            m_Vad.SpeechEnded += OnSpeechEnded;
            m_Stt.PartialTranscript += text => Partial?.Invoke(text);
            m_Stt.ResultReady += (result, final) =>
            {
                if (final && !m_Disposed) Final?.Invoke(result.Text);
            };

            SetLanguage(AppLanguage.Current);
            AppLanguage.Changed += SetLanguage;
        }

        // SenseVoiceRecognizer takes its language only in the constructor; reloading the model on every switch would cost
        // seconds, so the field is set directly (the package reads it on every pass).
        static readonly FieldInfo s_SenseVoiceLanguage = typeof(SenseVoiceRecognizer).GetField("m_Language", BindingFlags.Instance | BindingFlags.NonPublic);

        /// <summary>The language the next utterance is transcribed in; Arabic loads Whisper on first use.</summary>
        public void SetLanguage(Lang lang)
        {
            m_Language = lang;
            if (SpeechLanguage.UsesWhisper(lang))
            {
                LoadWhisper();
                return;
            }
            if (s_SenseVoiceLanguage != null) s_SenseVoiceLanguage.SetValue(m_Stt, SpeechLanguage.SenseVoice(lang));
            else Debug.LogWarning("[Voice] this SenseVoice version has no m_Language field; it keeps auto-detecting the language");
        }

        void LoadWhisper()
        {
            if (m_Whisper != null || m_Disposed) return;
            var installed = ModelRoots.InstalledWhisper();
            if (installed == null)
            {
                Debug.LogWarning($"[Voice] Arabic speech needs Whisper-{ModelRoots.WhisperSize} in {ModelRoots.Whisper}: install it from the VoiceAgent ▸ Speech menu");
                return;
            }
            if (installed != ModelRoots.WhisperSize)
            {
                Debug.LogWarning($"[Voice] Whisper-{ModelRoots.WhisperSize} isn't installed (VoiceAgent ▸ Speech menu); using Whisper-{installed} for Arabic.");
                ModelRoots.WhisperSize = installed;
            }
            try
            {
                var whisper = new WhisperRecognizer();
                whisper.Load(ModelRoots.Whisper);
                m_Whisper = whisper;
                Debug.Log($"[Voice] Whisper-{ModelRoots.WhisperSize} loaded (Arabic)");
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        /// <summary>Whether the selected language's speech model is loaded.</summary>
        public bool CanListen => SpeechLanguage.UsesWhisper(m_Language) ? m_Whisper != null && m_Whisper.IsLoaded : m_Stt.IsLoaded;

        /// <summary>False without a microphone, or when SenseVoice didn't load (it logs the error).</summary>
        public bool Listen()
        {
            if (m_Listening) return true;
            if (m_Disposed || !CanListen || !m_Mic.Start()) return false;
            m_Vad.StartListening();
            m_Listening = true;
            m_InSpeech = false;
            m_ListenStarted = Time.time;
            return true;
        }

        public void Stop()
        {
            if (!m_Listening) return;
            if (m_InSpeech)
            {
                // The final pass transcribes this copy: the next utterance may already be filling m_Utterance.
                m_Finished = m_Utterance.ToArray();
                if (m_WhisperUtterance) _ = FinishWhisperAsync(m_Finished);
                else m_Stt.EndUtterance();
            }
            m_InSpeech = false;
            m_Listening = false;
            m_Vad.StopListening();
            ListeningStopped?.Invoke();
        }

        /// <summary>Call once per frame.</summary>
        public void Tick()
        {
            m_Mic.Poll();
            for (var i = 0; i < MaxVadFramesPerTick && m_Vad.PendingSamples >= VadFrameSamples && m_Vad.Pump(); i++) { }
            if (m_Listening && !m_InSpeech && Time.time - m_ListenStarted > NoSpeechTimeout) Stop();
        }

        void OnSamples(float[] samples, int count, long sequence)
        {
            // The pre-roll follows the mic even between turns so it never holds audio from an earlier utterance.
            var keep = Math.Min(count, PreRollSamples);
            Array.Copy(m_PreRoll, keep, m_PreRoll, 0, PreRollSamples - keep);
            Array.Copy(samples, count - keep, m_PreRoll, PreRollSamples - keep, keep);
            m_PreRollEnd = sequence + count;

            if (!m_Listening) return;
            m_Vad.PushSamples(samples, count);
            if (m_InSpeech)
            {
                if (!m_WhisperUtterance) m_Stt.PushSamples(samples, count, sequence);
                for (var i = 0; i < count; i++) m_Utterance.Add(samples[i]);
            }
        }

        void OnSpeechStarted()
        {
            if (!m_Listening || m_InSpeech) return;
            m_InSpeech = true;
            // The language can't change inside an utterance: it is fixed here, when speech starts.
            m_WhisperUtterance = SpeechLanguage.UsesWhisper(m_Language) && m_Whisper != null;
            // Right after the mic opens the pre-roll holds fewer real samples than its length: push only those, so their
            // stream positions line up with the chunks that follow.
            var recent = m_PreRoll[(PreRollSamples - (int)Math.Min(PreRollSamples, m_PreRollEnd))..];
            if (!m_WhisperUtterance)
            {
                m_Stt.BeginUtterance();
                m_Stt.PushSamples(recent, recent.Length, m_PreRollEnd - recent.Length);
            }
            m_Utterance.Clear();
            m_Utterance.AddRange(recent);
            SpeechStarted?.Invoke();
        }

        async Awaitable FinishWhisperAsync(float[] audio)
        {
            string text;
            try
            {
                text = await m_Whisper.TranscribeAsync(audio, AppLanguage.Code(Lang.Ar), prompt: SpeechLanguage.ArabicPrompt);
                if (SpeechLanguage.IsHallucination(text, audio.Length / (double)WhisperRecognizer.SampleRate))
                {
                    Debug.Log($"[Voice] Whisper hallucinated (repeated or copied its prompt), dropped: {text}");
                    text = string.Empty;
                }
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                text = string.Empty;
            }
            if (m_Disposed) return;
            Debug.Log($"[Voice] Whisper (ar, {m_Whisper.LastMs:F0} ms): {text}");
            Final?.Invoke(text);
        }

        /// <summary>
        /// Test hook: transcribes a clip (16 kHz mono) with the selected language's model and raises <see cref="Final"/>,
        /// as a spoken utterance would.
        /// </summary>
        public async Awaitable TranscribeClipAsync(float[] audio)
        {
            if (SpeechLanguage.UsesWhisper(m_Language))
            {
                if (m_Whisper == null) LoadWhisper();
                if (m_Whisper != null) await FinishWhisperAsync(audio);
                return;
            }
            var result = await m_Stt.TranscribeAsync(audio, SpeechLanguage.SenseVoice(m_Language));
            if (!m_Disposed) Final?.Invoke(result.Text);
        }

        void OnSpeechEnded()
        {
            if (!m_InSpeech) return;
            Stop();
        }

        public void Dispose()
        {
            m_Disposed = true;
            AppLanguage.Changed -= SetLanguage;
            m_Whisper?.Dispose();
            m_Mic.Dispose();
            m_Vad.Dispose();
            m_Stt.Dispose();
        }
    }
}
