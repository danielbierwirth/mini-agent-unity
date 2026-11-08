using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

namespace NeedleTest
{
    /// <summary>
    /// Where the speech models (Silero VAD + SenseVoice) are read from. Same scheme as FunctionGemmaFiles: in the Editor,
    /// each package's Models~ folder in the package cache; in a player, the copy the build puts in
    /// StreamingAssets/SpeechModels/&lt;package&gt; (see SpeechModelBuildStaging). Android keeps StreamingAssets inside
    /// the APK, where files can't be opened, so <see cref="CopyOutOfApk"/> first copies them to persistentDataPath.
    /// </summary>
    public static class SpeechModelFiles
    {
        public const string VadPackage = "com.sky.sentis.silero-vad";
        public const string SttPackage = "com.sky.sentis.sensevoice";
        public static readonly string[] Packages = { VadPackage, SttPackage };

        /// <summary>StreamingAssets folder a player build copies the Models~ folders into.</summary>
        public const string StreamingFolder = "SpeechModels";
        /// <summary>Inside <see cref="StreamingFolder"/>: one "package/file\tbytes" line per staged file.</summary>
        public const string ManifestFile = "files.txt";

        /// <summary>True where StreamingAssets is a URL inside the app package (Android) instead of a folder.</summary>
        static bool StreamingAssetsInPackage => Application.streamingAssetsPath.Contains("://");

        public static string Vad => Root(VadPackage);
        public static string Stt => Root(SttPackage);

        /// <summary>Folder holding a package's model files, or null in the Editor when the package is missing.</summary>
        public static string Root(string package)
        {
#if UNITY_EDITOR
            var info = UnityEditor.PackageManager.PackageInfo.FindForAssetPath($"Packages/{package}");
            return info != null ? Path.Combine(info.resolvedPath, "Models~") : null;
#else
            return Path.Combine(StreamingAssetsInPackage ? Application.persistentDataPath : Application.streamingAssetsPath,
                                StreamingFolder, package);
#endif
        }

        /// <summary>
        /// Android: copies every staged file from the APK to persistentDataPath, skipping files already copied at the
        /// right size. Other platforms (and the Editor) read the files in place.
        /// </summary>
        public static IEnumerator CopyOutOfApk()
        {
            if (Application.isEditor || !StreamingAssetsInPackage) yield break;
            string manifest;
            using (var req = UnityWebRequest.Get($"{Application.streamingAssetsPath}/{StreamingFolder}/{ManifestFile}"))
            {
                yield return req.SendWebRequest();
                if (req.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogError($"[Voice] {StreamingFolder}/{ManifestFile} missing from the build: {req.error}");
                    yield break;
                }
                manifest = req.downloadHandler.text;
            }

            var root = Path.Combine(Application.persistentDataPath, StreamingFolder);
            foreach (var line in manifest.Split('\n'))
            {
                var fields = line.Trim().Split('\t');
                if (fields.Length != 2 || !long.TryParse(fields[1], out var bytes)) continue;
                var dst = Path.Combine(root, fields[0]);
                if (File.Exists(dst) && new FileInfo(dst).Length == bytes) continue;
                Directory.CreateDirectory(Path.GetDirectoryName(dst));
                var part = dst + ".part";
                using (var req = UnityWebRequest.Get($"{Application.streamingAssetsPath}/{StreamingFolder}/{fields[0]}"))
                {
                    req.downloadHandler = new DownloadHandlerFile(part) { removeFileOnAbort = true };
                    Debug.Log($"[Voice] copying {fields[0]} to {dst}");
                    yield return req.SendWebRequest();
                    if (req.result != UnityWebRequest.Result.Success)
                    {
                        Debug.LogError($"[Voice] copy of {fields[0]} failed: {req.error}");
                        yield break;
                    }
                }
                if (File.Exists(dst)) File.Delete(dst);
                File.Move(part, dst);
            }
        }
    }
}
