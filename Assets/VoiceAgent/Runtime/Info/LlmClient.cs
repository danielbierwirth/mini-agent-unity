using System;
using System.Diagnostics;
using System.Text;
using System.Threading;
using UnityEngine;
using UnityEngine.Networking;

namespace VoiceAgent.Info
{
    /// <summary>
    /// The language model that writes the assistants' open answers (web answers and conversation) in both scenes: an
    /// OpenAI-compatible chat-completions endpoint, by default the local Ollama daemon with qwen3.5:9b. Nothing leaves the
    /// computer unless the model is a cloud one the daemon proxies. A failure (Ollama not running, a 4xx, a parse error)
    /// returns null and the caller falls back to its on-device answer.
    ///
    /// Note: localhost is plain HTTP, so players need PlayerSettings.insecureHttpOption allowed for it.
    /// </summary>
    public sealed class LlmClient
    {
        public const string DefaultEndpoint = "http://localhost:11434/v1/chat/completions";
        public const string DefaultModel = "qwen3.5:9b";
        /// <summary>"none" turns thinking off for hybrid models like qwen3.5, which otherwise spend the token budget thinking
        /// and return empty content; "low" suits gpt-oss; empty omits the field (qwen2.5 rejects it).</summary>
        public const string DefaultReasoningEffort = "none";

        public string Endpoint = DefaultEndpoint;
        public string Model = DefaultModel;
        public string ReasoningEffort = DefaultReasoningEffort;
        public int MaxTokens = 512;
        public float Temperature = 0.2f;
        public int TimeoutSeconds = 30;

        /// <summary>Elapsed time of the last request, and why it failed (null when it succeeded).</summary>
        public double LastMs { get; private set; }
        public string LastError { get; private set; }

        /// <summary>The model's name for timing labels ("qwen3.5:9b" → "qwen3.5").</summary>
        public string ShortName => Model.Split(':')[0];

        public LlmClient() { }

        public LlmClient(string endpoint, string model, string reasoningEffort)
        {
            if (!string.IsNullOrEmpty(endpoint)) Endpoint = endpoint;
            if (!string.IsNullOrEmpty(model)) Model = model;
            ReasoningEffort = reasoningEffort;
        }

        [Serializable] sealed class Response { public Choice[] choices; }
        [Serializable] sealed class Choice { public Message message; }
        [Serializable] sealed class Message { public string content; }

        /// <summary>
        /// Loads the model into the daemon with a one-token request, so the first real answer doesn't pay the load
        /// (~5–8 s for qwen3.5:9b). Failures are ignored: the first answer just takes longer, or falls back.
        /// </summary>
        public async Awaitable WarmupAsync()
        {
            var tokens = MaxTokens;
            MaxTokens = 1;
            try { await CompleteAsync("Reply with OK.", "hi"); }
            finally { MaxTokens = tokens; }
            UnityEngine.Debug.Log($"[LLM] {Model} {(LastError == null ? "ready" : "not reachable: " + LastError)} ({LastMs:F0} ms)");
        }

        /// <summary>The model's reply to <paramref name="user"/> under <paramref name="system"/>, or null on any failure.</summary>
        public async Awaitable<string> CompleteAsync(string system, string user, CancellationToken cancel = default)
        {
            var body = "{\"model\":\"" + Json(Model) + "\",\"messages\":[" +
                       "{\"role\":\"system\",\"content\":\"" + Json(system) + "\"}," +
                       "{\"role\":\"user\",\"content\":\"" + Json(user) + "\"}]," +
                       $"\"temperature\":{Temperature.ToString(System.Globalization.CultureInfo.InvariantCulture)},\"top_p\":0.95,\"max_tokens\":{MaxTokens}," +
                       (string.IsNullOrEmpty(ReasoningEffort) ? string.Empty : "\"reasoning_effort\":\"" + Json(ReasoningEffort) + "\",") +
                       "\"stream\":false}";
            var clock = Stopwatch.StartNew();
            LastError = null;
            try
            {
                // One retry on a transport error or 5xx (the daemon loading the model); a 4xx won't change on retry.
                for (var attempt = 0; attempt < 2; attempt++)
                {
                    using var request = new UnityWebRequest(Endpoint, "POST");
                    request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
                    request.downloadHandler = new DownloadHandlerBuffer();
                    request.SetRequestHeader("Content-Type", "application/json");
                    request.timeout = TimeoutSeconds;
                    await request.SendWebRequest();
                    cancel.ThrowIfCancellationRequested();

                    if (request.result == UnityWebRequest.Result.Success)
                    {
                        var content = JsonUtility.FromJson<Response>(request.downloadHandler.text)?.choices?[0]?.message?.content?.Trim();
                        if (string.IsNullOrEmpty(content)) LastError = "empty reply";
                        return string.IsNullOrEmpty(content) ? null : content;
                    }
                    LastError = request.responseCode > 0 ? $"HTTP {request.responseCode}: {request.downloadHandler.text}" : request.error;
                    if (request.responseCode >= 400 && request.responseCode < 500) return null;
                }
                return null;
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                LastError = e.Message;
                return null;
            }
            finally
            {
                LastMs = clock.Elapsed.TotalMilliseconds;
            }
        }

        static string Json(string s)
        {
            var sb = new StringBuilder(s.Length + 16);
            foreach (var c in s)
            {
                if (c == '\\' || c == '"') sb.Append('\\').Append(c);
                else if (c == '\n') sb.Append("\\n");
                else if (c == '\r') sb.Append("\\r");
                else if (c == '\t') sb.Append("\\t");
                else if (c < ' ') sb.Append($"\\u{(int)c:x4}");
                else sb.Append(c);
            }
            return sb.ToString();
        }
    }
}
