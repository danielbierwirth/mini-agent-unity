using System.IO;
using System.Net.Http;
using Unity.InferenceEngine;
using UnityEditor;
using UnityEngine;

namespace VoiceAgent.Editor
{
    /// <summary>
    /// Arabic speech-to-text needs Whisper (SenseVoice has no Arabic). This downloads an ONNX export, imports it once to
    /// convert it, and writes .sentis files (encoder and decoder weights as FP16) plus vocab.json to
    /// StreamingAssets/Whisper/&lt;size&gt;, which is git-ignored.
    ///
    /// - tiny: Unity's Inference Engine export, huggingface.co/unity/inference-engine-whisper-tiny (Apache 2.0), ~120 MB.
    /// - base / small: Hugging Face Optimum exports, huggingface.co/onnx-community/whisper-base|small, ~150 / ~480 MB.
    /// - small-dialect: huggingface.co/oddadmix/whisper-small-arabic-dialectal-v2 (Apache 2.0), whisper-small fine-tuned on
    ///   13 Arabic dialects. There is no ONNX export of it on Hugging Face, so Tools/export_whisper_dialect.sh makes one
    ///   with Optimum (Python) into Library/WhisperExport; the fine-tune keeps whisper-small's vocabulary.
    ///
    /// All three share the decoder interface (input_ids, encoder_hidden_states → logits), the 80-bin log-mel front end
    /// and the multilingual vocabulary, so the log-mel graph comes from Unity's tiny export for every size. Whisper is
    /// MIT-licensed (github.com/openai/whisper).
    /// </summary>
    public static class WhisperSetup
    {
        const string Unity = "https://huggingface.co/unity/inference-engine-whisper-tiny/resolve/main/";
        const string Import = "Assets/_WhisperImport";

        public const string DialectFolder = "small-dialect";
        const string DialectExport = "Library/WhisperExport/" + DialectFolder;

        static (string Encoder, string Decoder, string Vocab) Source(string size) => size switch
        {
            "tiny" => (Unity + "models/encoder_model.onnx", Unity + "models/decoder_model.onnx", Unity + "data/vocab.json"),
            DialectFolder => (Path.GetFullPath(Path.Combine(DialectExport, "encoder_model.onnx")),
                              Path.GetFullPath(Path.Combine(DialectExport, "decoder_model.onnx")),
                              "https://huggingface.co/onnx-community/whisper-small/resolve/main/vocab.json"),
            _ => ($"https://huggingface.co/onnx-community/whisper-{size}/resolve/main/onnx/encoder_model.onnx",
                  $"https://huggingface.co/onnx-community/whisper-{size}/resolve/main/onnx/decoder_model.onnx",
                  $"https://huggingface.co/onnx-community/whisper-{size}/resolve/main/vocab.json"),
        };

        [MenuItem("VoiceAgent/Speech/Download Whisper Tiny")] static void Tiny() => Debug.Log(Install("tiny"));
        [MenuItem("VoiceAgent/Speech/Download Whisper Base")] static void Base() => Debug.Log(Install("base"));
        [MenuItem("VoiceAgent/Speech/Download Whisper Small")] static void Small() => Debug.Log(Install("small"));

        [MenuItem("VoiceAgent/Speech/Install Whisper Small (Arabic dialects)")]
        static void SmallDialect()
        {
            if (!ExportDialect(out var error)) { Debug.LogError($"[Whisper] dialect export failed: {error}"); return; }
            Debug.Log(Install(DialectFolder));
        }

        /// <summary>Runs Tools/export_whisper_dialect.sh unless Library/WhisperExport already holds the export.</summary>
        static bool ExportDialect(out string error)
        {
            error = null;
            if (File.Exists(Path.Combine(DialectExport, "encoder_model.onnx")) && File.Exists(Path.Combine(DialectExport, "decoder_model.onnx")))
                return true;
            EditorUtility.DisplayProgressBar("Whisper small (Arabic dialects)", "Exporting to ONNX with Python (first run takes a few minutes)…", 0.3f);
            try
            {
                var start = new System.Diagnostics.ProcessStartInfo("/bin/bash", "Tools/export_whisper_dialect.sh")
                {
                    WorkingDirectory = Path.GetFullPath("."),
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                };
                // The Editor's PATH lacks the usual Python locations when it was started from the Hub.
                start.EnvironmentVariables["PATH"] = "/opt/homebrew/bin:/usr/local/bin:" + System.Environment.GetEnvironmentVariable("PATH");
                using var process = System.Diagnostics.Process.Start(start);
                var stderr = process.StandardError.ReadToEndAsync();
                var stdout = process.StandardOutput.ReadToEnd();
                process.WaitForExit();
                if (process.ExitCode == 0) return true;
                var log = (stdout + stderr.Result).Trim();
                error = $"exit code {process.ExitCode}; run Tools/export_whisper_dialect.sh in a terminal to see why.\n" +
                        (log.Length > 2000 ? log[^2000..] : log);
                return false;
            }
            catch (System.Exception e)
            {
                error = e.Message;
                return false;
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        /// <param name="sourceFolder">A folder that already holds the downloaded files (flat), or null to download them.</param>
        public static string Install(string size, string sourceFolder = null)
        {
            var target = ModelRoots.WhisperFor(size);
            Directory.CreateDirectory(target);
            Directory.CreateDirectory(Import);
            var (encoder, decoder, vocab) = Source(size);
            try
            {
                using var http = new HttpClient { Timeout = System.TimeSpan.FromMinutes(30) };
                byte[] Fetch(string url)
                {
                    var local = sourceFolder != null ? Path.Combine(sourceFolder, Path.GetFileName(url)) : null;
                    if (local != null && File.Exists(local)) return File.ReadAllBytes(local);
                    if (Path.IsPathRooted(url)) return File.ReadAllBytes(url);   // a local export (small-dialect)
                    EditorUtility.DisplayProgressBar($"Whisper-{size}", $"Downloading {Path.GetFileName(url)}…", 0.5f);
                    return http.GetByteArrayAsync(url).GetAwaiter().GetResult();
                }

                File.WriteAllBytes(Path.Combine(target, WhisperRecognizer.VocabFile), Fetch(vocab));
                Convert(Fetch(Unity + "models/logmel_spectrogram.onnx"), Path.Combine(target, WhisperRecognizer.SpectrogramFile), false);
                Convert(Fetch(encoder), Path.Combine(target, WhisperRecognizer.EncoderFile), true);
                Convert(Fetch(decoder), Path.Combine(target, WhisperRecognizer.DecoderFile), true);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                AssetDatabase.DeleteAsset(Import);
            }
            AssetDatabase.Refresh();
            return $"Whisper-{size} installed in {target}";
        }

        static void Convert(byte[] onnx, string target, bool fp16)
        {
            var path = $"{Import}/model.onnx";
            File.WriteAllBytes(path, onnx);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var model = ModelLoader.Load(AssetDatabase.LoadAssetAtPath<ModelAsset>(path));
            if (fp16) ModelQuantizer.QuantizeWeights(QuantizationType.Float16, ref model);
            ModelWriter.Save(target, model);
            AssetDatabase.DeleteAsset(path);
        }

        /// <summary>The graph inputs and outputs of an installed size, for checking the export.</summary>
        public static string Describe(string size)
        {
            var sb = new System.Text.StringBuilder();
            foreach (var file in new[] { WhisperRecognizer.SpectrogramFile, WhisperRecognizer.EncoderFile, WhisperRecognizer.DecoderFile })
            {
                var model = ModelLoader.Load(Path.Combine(ModelRoots.WhisperFor(size), file));
                sb.Append(size).Append('/').Append(file).Append(": in ");
                foreach (var i in model.inputs) sb.Append(i.name).Append(i.shape).Append(' ');
                sb.Append("| out ");
                for (var i = 0; i < Mathf.Min(2, model.outputs.Count); i++) sb.Append(model.outputs[i].name).Append(' ');
                sb.Append($"({model.outputs.Count} outputs)\n");
            }
            return sb.ToString();
        }
    }
}
