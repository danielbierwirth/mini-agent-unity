using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using Newtonsoft.Json;
using Unity.InferenceEngine;
using UnityEngine;

namespace VoiceAgent
{
    /// <summary>
    /// Whisper (multilingual, github.com/openai/whisper, MIT) for the language SenseVoice can't transcribe: Arabic.
    /// tiny, base and small share the graph interface, the 80-bin log-mel front end and the vocabulary, so any of them
    /// loads from its folder (see <see cref="ModelRoots.WhisperSize"/>). One utterance at a time, no partials: log-mel → encoder once, then greedy decoding with the prompt
    /// &lt;|startoftranscript|&gt;&lt;|ar|&gt;&lt;|transcribe|&gt;&lt;|notimestamps|&gt;, so the language is set
    /// explicitly instead of detected. The decoder reruns on the whole token prefix each step (no KV cache): a voice
    /// command is a few dozen tokens, and it keeps the graph to one model.
    ///
    /// Models: StreamingAssets/Whisper/&lt;size&gt;/*.sentis, made by VoiceAgent ▸ Speech ▸ Download Whisper … (not in git).
    /// </summary>
    public sealed class WhisperRecognizer : IDisposable
    {
        public const int SampleRate = 16000;
        public const string SpectrogramFile = "logmel_spectrogram.sentis", EncoderFile = "encoder.sentis", DecoderFile = "decoder.sentis", VocabFile = "vocab.json";
        const int MaxSamples = 30 * SampleRate, MaxTokens = 120, VocabSize = 51865;
        const int EndOfText = 50257, StartOfTranscript = 50258, Transcribe = 50359, NoTimestamps = 50363;

        /// <summary>Whisper's language tokens follow &lt;|startoftranscript|&gt; in this order (en = 50259).</summary>
        static readonly string[] s_Languages = { "en", "zh", "de", "es", "ru", "ko", "fr", "ja", "pt", "tr", "pl", "ca", "nl", "ar" };

        readonly BackendType m_Backend;
        Worker m_Spectrogram, m_Encoder, m_Decoder;
        string[] m_Tokens;
        readonly Dictionary<char, byte> m_ByteOf = new();
        bool m_Busy;

        public bool IsLoaded => m_Decoder != null;
        public double LastMs { get; private set; }

        public WhisperRecognizer(BackendType backend = BackendType.GPUCompute) => m_Backend = backend;

        public static bool Exists(string root) =>
            File.Exists(Path.Combine(root, SpectrogramFile)) && File.Exists(Path.Combine(root, EncoderFile)) &&
            File.Exists(Path.Combine(root, DecoderFile)) && File.Exists(Path.Combine(root, VocabFile));

        public void Load(string root)
        {
            Dispose();
            if (!Exists(root)) throw new FileNotFoundException($"Whisper models are missing in {root}; run VoiceAgent ▸ Speech ▸ Download Whisper …");
            var vocab = JsonConvert.DeserializeObject<Dictionary<string, int>>(File.ReadAllText(Path.Combine(root, VocabFile), Encoding.UTF8));
            m_Tokens = new string[VocabSize];
            foreach (var (piece, id) in vocab)
                if (id >= 0 && id < VocabSize) m_Tokens[id] = piece;
            BuildByteDecoder();

            m_Spectrogram = new Worker(ModelLoader.Load(Path.Combine(root, SpectrogramFile)), m_Backend);
            m_Encoder = new Worker(ModelLoader.Load(Path.Combine(root, EncoderFile)), m_Backend);
            m_Decoder = new Worker(WithArgMax(ModelLoader.Load(Path.Combine(root, DecoderFile))), m_Backend);
        }

        /// <summary>The decoder with "logits" replaced by their argmax per position [1, n], so only token ids are read back.</summary>
        static Model WithArgMax(Model decoder)
        {
            var logits = decoder.outputs.FindIndex(o => o.name == "logits");
            if (logits < 0) logits = 0;
            var graph = new FunctionalGraph();
            var inputs = graph.AddInputs(decoder);
            var outputs = Functional.Forward(decoder, inputs);
            graph.AddOutput(Functional.ArgMax(outputs[logits], -1), "ids");
            return graph.Compile();
        }

        /// <summary>Transcribes 16 kHz mono audio (up to 30 s) in <paramref name="language"/> ("ar", "en", "ko", …).</summary>
        public async Awaitable<string> TranscribeAsync(float[] audio, string language, CancellationToken cancel = default)
        {
            if (!IsLoaded) throw new InvalidOperationException("Load the Whisper models first.");
            var lang = Array.IndexOf(s_Languages, language);
            if (lang < 0) throw new ArgumentException($"Unsupported Whisper language: {language}");
            while (m_Busy) await Awaitable.NextFrameAsync(cancel);
            m_Busy = true;
            var clock = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                var padded = new float[MaxSamples];
                Array.Copy(audio, padded, Math.Min(audio.Length, MaxSamples));
                using var input = new Tensor<float>(new TensorShape(1, MaxSamples), padded);
                m_Spectrogram.Schedule(input);
                m_Encoder.Schedule(m_Spectrogram.PeekOutput() as Tensor<float>);
                var encoded = m_Encoder.PeekOutput() as Tensor<float>;

                var tokens = new List<int> { StartOfTranscript, 50259 + lang, Transcribe, NoTimestamps };
                var prompt = tokens.Count;
                while (tokens.Count < MaxTokens)
                {
                    cancel.ThrowIfCancellationRequested();
                    using var ids = new Tensor<int>(new TensorShape(1, tokens.Count), tokens.ToArray());
                    m_Decoder.SetInput("input_ids", ids);
                    m_Decoder.SetInput("encoder_hidden_states", encoded);
                    m_Decoder.Schedule();
                    using var next = await (m_Decoder.PeekOutput("ids") as Tensor<int>).ReadbackAndCloneAsync();
                    var token = next[0, tokens.Count - 1];
                    if (token == EndOfText || token >= EndOfText) break;
                    tokens.Add(token);
                    // Greedy decoding can loop on one token; four in a row ends the utterance.
                    if (tokens.Count - prompt >= 4 && tokens[^1] == tokens[^2] && tokens[^2] == tokens[^3] && tokens[^3] == tokens[^4]) break;
                }
                return Decode(tokens, prompt).Trim();
            }
            finally
            {
                LastMs = clock.Elapsed.TotalMilliseconds;
                m_Busy = false;
            }
        }

        /// <summary>Byte-level BPE: each token's characters stand for bytes; the bytes of all tokens together are UTF-8.</summary>
        string Decode(List<int> tokens, int start)
        {
            var bytes = new List<byte>();
            for (var i = start; i < tokens.Count; i++)
            {
                var piece = tokens[i] < m_Tokens.Length ? m_Tokens[tokens[i]] : null;
                if (piece == null) continue;
                foreach (var c in piece)
                    if (m_ByteOf.TryGetValue(c, out var b)) bytes.Add(b);
            }
            return Encoding.UTF8.GetString(bytes.ToArray());
        }

        /// <summary>GPT-2's bytes_to_unicode, inverted: printable bytes map to themselves, the rest to 256 + n.</summary>
        void BuildByteDecoder()
        {
            m_ByteOf.Clear();
            var printable = new List<int>();
            for (var b = '!'; b <= '~'; b++) printable.Add(b);
            for (var b = '¡'; b <= '¬'; b++) printable.Add(b);
            for (var b = '®'; b <= 'ÿ'; b++) printable.Add(b);
            var n = 0;
            for (var b = 0; b < 256; b++)
                m_ByteOf[printable.Contains(b) ? (char)b : (char)(256 + n++)] = (byte)b;
        }

        public void Dispose()
        {
            m_Spectrogram?.Dispose();
            m_Encoder?.Dispose();
            m_Decoder?.Dispose();
            m_Spectrogram = m_Encoder = m_Decoder = null;
        }
    }
}
