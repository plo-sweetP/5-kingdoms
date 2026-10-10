using System.IO;
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

    /// <summary>
    /// Where Gradle keeps the package it built last. It patches that file on the next build instead of writing a new
    /// one, and what a patch replaces stays in the file as dead weight: the same 41 MB of content came out as 60, 74
    /// and 92 MB on three builds in a row (2026-10-10).
    /// </summary>
    const string GradlePackage = "Library/Bee/Android/Prj/IL2CPP/Gradle/launcher/build/outputs/apk/debug/launcher-debug.apk";

    [MenuItem("5 Kingdoms/Build/Android APK (development)")]
    public static void BuildAndroidDev()
    {
        // Without the old package Gradle writes a fresh one: the APK is as large as its content, no larger.
        if (File.Exists(GradlePackage)) File.Delete(GradlePackage);
        Build(BuildTarget.Android, "Builds/Android/5Kingdoms-dev.apk", BuildOptions.Development);
    }

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
        var errors = report.steps.SelectMany(step => step.messages.Select(message => (step.name, message)))
            .Where(entry => entry.message.type == LogType.Error || entry.message.type == LogType.Exception);
        foreach (var (step, message) in errors.Take(10))
            Debug.Log($"Build error in '{step}': {message.content}");
        if (Application.isBatchMode) EditorApplication.Exit(summary.result == BuildResult.Succeeded ? 0 : 1);
    }
}
