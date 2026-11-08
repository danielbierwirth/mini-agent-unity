using System;
using System.Diagnostics;
using SentisModels;
using VoiceAgent.Info;
using VoiceAgent.UI;
using UnityEngine;

namespace VoiceAgent.Scenes
{
    /// <summary>
    /// Everything together: mic → Silero VAD → SenseVoice (partials re-rank the chips live) → Gemma3 + Decision AI picks the
    /// command → the home changes or the web answers → Supertonic 3 speaks as Nova. Chat and low-confidence turns go to
    /// Gemma3 generation with the persona prompt.
    /// </summary>
    public sealed class VoiceAgentDemoScene : MonoBehaviour
    {
        [SerializeField] AssistantView m_View;
        [SerializeField] HomePanel m_HomePanel;
        [SerializeField] AmbientBackground m_Background;
        [SerializeField] ProceduralMusic m_Music;
        [SerializeField] SpeechOutput m_Speech;
        [SerializeField] TimingPanel m_Timing;

        AssistantController m_Controller;
        VoiceInput m_Voice;
        Gemma3Model m_Gemma;
        DecisionAIRanker m_Ranker;
        readonly Stopwatch m_SinceSpeechEnd = new();
        float m_SpeechStarted, m_SpeechSeconds;
        int m_Partials;

        async void Start()
        {
            var persona = Persona.Load();
            var catalog = CommandCatalog.Load();
            var home = new SmartHome();
            m_HomePanel.Bind(home);
            m_Background.Bind(home);
            m_View.SetMicState(MicState.Off);
            m_View.SetInteractable(false);
            m_View.SetStatus("Loading models…");
            await Awaitable.NextFrameAsync();
            await Awaitable.NextFrameAsync();
            if (!this) return;
            await ModelRoots.PrepareAsync(m_View.SetStatus);
            if (!this) return;

            var clock = Stopwatch.StartNew();
            m_Speech.Voice = persona.voice;
            try
            {
                m_Speech.Load(ModelRoots.Tts);
            }
            catch (System.Exception e)
            {
                // Replies still show as text (AssistantController skips speech when TTS isn't loaded).
                UnityEngine.Debug.LogException(e);
            }
            try
            {
                m_Gemma = ModelRoots.LoadGemma(persona.system_prompt);
                m_Ranker = new DecisionAIRanker(m_Gemma, ModelRoots.IntentHead, catalog);
            }
            catch (System.Exception e)
            {
                UnityEngine.Debug.LogException(e);
                m_View.SetStatus($"Couldn't load Gemma3 or the intent head: {e.Message}");
                return;
            }
            m_Ranker.Score("warmup");
            m_View.SetStatus("Warming up Gemma3 generation…");
            await m_Gemma.GenerateAsync("hi", 2);
            if (!this) return;
            m_Voice = new VoiceInput();
            var info = new InfoAgent(new UnityWebClient(), m_Gemma);
            _ = info.WarmupAsync();

            m_Controller = new AssistantController(catalog, persona, home, m_Ranker, m_Gemma, m_View, m_Music, m_Speech, info, m_Timing);
            m_View.MicClicked += ToggleListen;
            m_Voice.SpeechStarted += () =>
            {
                m_View.SetInputText("…");
                m_SpeechStarted = Time.realtimeSinceStartup;
                m_Partials = 0;
                m_Controller.BeginVoiceTurn();
            };
            m_Voice.Partial += text =>
            {
                m_View.SetInputText(text);
                m_Controller.ShowChips(text);
                m_Controller.ShowPartials(++m_Partials);
            };
            m_Voice.ListeningStopped += () =>
            {
                // The latency the user feels starts when VAD ends the utterance.
                m_SinceSpeechEnd.Restart();
                m_SpeechSeconds = m_SpeechStarted > 0 ? Time.realtimeSinceStartup - m_SpeechStarted : 0f;
                m_Music.Ducked = false;
                m_View.SetStatus(string.Empty);
                if (!m_Controller.IsBusy) m_View.SetMicState(MicState.Idle);
            };
            m_Voice.Final += async text =>
            {
                m_View.SetInputText(string.Empty);
                if (!string.IsNullOrWhiteSpace(text))
                    await m_Controller.Submit(text, new VoiceTiming(m_SinceSpeechEnd.Elapsed.TotalMilliseconds, m_SpeechSeconds, m_Partials));
            };

            m_View.SetInteractable(true);
            m_View.SetMicState(MicState.Idle);
            m_View.SetStatus(string.Empty);
            UnityEngine.Debug.Log($"[VoiceAgent] ready in {clock.Elapsed.TotalSeconds:F1} s");
            m_View.AddMessage(persona.Greeting(AppLanguage.Current), false);
        }

        // ---- test hooks (play mode, e.g. via script eval): the same paths typed text and a spoken utterance take ----

        public bool IsReady => m_Controller != null;
        public bool IsBusy => m_Controller == null || m_Controller.IsBusy || m_Speech.IsSpeaking;

        /// <summary>Runs a turn as if <paramref name="text"/> were typed and submitted.</summary>
        public void SubmitText(string text) => _ = m_Controller.Submit(text);

        /// <summary>Runs a turn as if speech-to-text had just produced <paramref name="transcript"/>.</summary>
        public void InjectTranscript(string transcript) => _ = m_Controller.Submit(transcript, new VoiceTiming(0, 0, 0));

        /// <summary>
        /// Speaks <paramref name="text"/> with Supertonic, resamples it to 16 kHz and transcribes it with the selected
        /// language's speech-to-text model, whose final transcript then runs a voice turn. Returns the transcript.
        /// </summary>
        public async Awaitable<string> SpeakAndListen(string text)
        {
            var (pcm, rate) = await m_Speech.SynthesizeClip(text);
            var audio = Resample(pcm, rate, MicrophoneStream.SampleRate);
            string heard = null;
            void Capture(string t) => heard = t;
            m_Voice.Final += Capture;
            try
            {
                await m_Voice.TranscribeClipAsync(audio);
            }
            finally
            {
                m_Voice.Final -= Capture;
            }
            return heard;
        }

        static float[] Resample(float[] pcm, int from, int to)
        {
            if (from == to || pcm.Length == 0) return pcm;
            // Box low-pass over one output period first, so the decimation doesn't fold the highs into the speech band.
            var width = Math.Max(1, (int)Math.Round((double)from / to));
            var smooth = new float[pcm.Length];
            double sum = 0;
            for (var i = 0; i < pcm.Length; i++)
            {
                sum += pcm[i];
                if (i >= width) sum -= pcm[i - width];
                smooth[i] = (float)(sum / Math.Min(i + 1, width));
            }
            pcm = smooth;
            var output = new float[(int)((long)pcm.Length * to / from)];
            for (var i = 0; i < output.Length; i++)
            {
                var at = (double)i * from / to;
                var j = (int)at;
                var t = (float)(at - j);
                output[i] = j + 1 < pcm.Length ? pcm[j] * (1 - t) + pcm[j + 1] * t : pcm[^1];
            }
            return output;
        }

        void ToggleListen()
        {
            if (m_Voice == null) return;
            if (m_Voice.IsListening)
            {
                m_Voice.Stop();
                return;
            }
            // A turn submitted now would be dropped (one turn at a time).
            if (m_Controller.IsBusy)
            {
                m_View.SetStatus(AssistantView.BusyNotice);
                return;
            }
            m_Speech.Stop();
            if (!m_Voice.Listen())
            {
                m_View.SetStatus("Can't listen: no microphone, or the speech model didn't load.");
                return;
            }
            m_Music.Ducked = true;
            m_View.SetMicState(MicState.Listening);
            m_View.SetStatus("Listening…");
        }

        void Update()
        {
            if (m_Voice == null) return;
            m_Voice.Tick();
            m_Controller.Tick();
            if (m_Voice.IsListening) m_View.SetMicState(MicState.Listening, m_Voice.Level);
        }

        void OnDestroy()
        {
            m_Voice?.Dispose();
            m_Ranker?.Dispose();
            m_Gemma?.Dispose();
        }
    }
}
