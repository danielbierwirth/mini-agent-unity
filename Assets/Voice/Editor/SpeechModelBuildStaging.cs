using System.IO;
using System.Linq;
using System.Text;
using NeedleTest;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// Player builds read the speech models from StreamingAssets/SpeechModels (see <see cref="SpeechModelFiles"/>): before a
/// build this copies each package's Models~ folder there and writes the file manifest Android copies out on first launch;
/// after the build it removes the copy, so the Editor keeps reading the package cache. Same scheme as
/// FunctionGemmaBuildStaging, in its own folder so the two never touch each other's files.
/// </summary>
sealed class SpeechModelBuildStaging : IPreprocessBuildWithReport, IPostprocessBuildWithReport
{
    public int callbackOrder => 0;

    static string Staged => Path.Combine(Application.streamingAssetsPath, SpeechModelFiles.StreamingFolder);

    public void OnPreprocessBuild(BuildReport report)
    {
        Clean();
        var manifest = new StringBuilder();
        foreach (var package in SpeechModelFiles.Packages)
        {
            var src = SpeechModelFiles.Root(package);
            if (src == null || !Directory.Exists(src))
                throw new BuildFailedException($"[Voice] {package} not found in the package cache (see Packages/manifest.json).");
            foreach (var file in Directory.GetFiles(src).OrderBy(f => f))
            {
                var name = Path.GetFileName(file);
                if (name.StartsWith(".")) continue;
                var dst = Path.Combine(Staged, package, name);
                Directory.CreateDirectory(Path.GetDirectoryName(dst));
                File.Copy(file, dst, true);
                manifest.Append(package).Append('/').Append(name).Append('\t').Append(new FileInfo(file).Length).Append('\n');
            }
        }
        File.WriteAllText(Path.Combine(Staged, SpeechModelFiles.ManifestFile), manifest.ToString());
    }

    public void OnPostprocessBuild(BuildReport report) => Clean();

    /// <summary>Removes a copy left behind by a build that failed before its postprocess step.</summary>
    [InitializeOnLoadMethod]
    static void CleanAfterFailedBuild()
    {
        if (!BuildPipeline.isBuildingPlayer) Clean();
    }

    static void Clean()
    {
        if (Directory.Exists(Staged)) Directory.Delete(Staged, true);
        File.Delete(Staged + ".meta");
    }
}
