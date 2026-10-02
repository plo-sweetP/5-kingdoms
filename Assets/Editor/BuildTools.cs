using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// Build entry points for the menu and for the command line
/// (Unity.exe -batchmode -quit -projectPath . -executeMethod BuildTools.BuildWindowsDev).
/// </summary>
static class BuildTools
{
    [MenuItem("5 Kingdoms/Build/Windows (development)")]
    public static void BuildWindowsDev() =>
        Build(BuildTarget.StandaloneWindows64, "Builds/Windows/5Kingdoms.exe", BuildOptions.Development);

    [MenuItem("5 Kingdoms/Build/Android APK (development)")]
    public static void BuildAndroidDev() =>
        Build(BuildTarget.Android, "Builds/Android/5Kingdoms-dev.apk", BuildOptions.Development);

    static void Build(BuildTarget target, string path, BuildOptions options)
    {
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(),
            locationPathName = path,
            target = target,
            targetGroup = BuildPipeline.GetBuildTargetGroup(target),
            options = options,
        });
        var summary = report.summary;
        Debug.Log($"Build {target}: {summary.result}, {summary.totalSize / (1024 * 1024)} MB, {summary.totalTime}, " +
                  $"{summary.totalErrors} errors -> {path}");
        if (Application.isBatchMode) EditorApplication.Exit(summary.result == BuildResult.Succeeded ? 0 : 1);
    }
}
