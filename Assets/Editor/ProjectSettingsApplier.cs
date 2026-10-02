using UnityEditor;
using UnityEngine;

// Applies baseline player settings once on editor load. Idempotent: only writes when a value differs.
// Landscape-only for phone + tablet; IL2CPP/ARM64 because Google Play requires 64-bit.
[InitializeOnLoad]
static class ProjectSettingsApplier
{
    static ProjectSettingsApplier()
    {
        EditorApplication.delayCall += Apply;
    }

    static void Apply()
    {
        bool changed = false;

        if (PlayerSettings.defaultInterfaceOrientation != UIOrientation.AutoRotation)
        {
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            changed = true;
        }
        changed |= Set(() => PlayerSettings.allowedAutorotateToPortrait, v => PlayerSettings.allowedAutorotateToPortrait = v, false);
        changed |= Set(() => PlayerSettings.allowedAutorotateToPortraitUpsideDown, v => PlayerSettings.allowedAutorotateToPortraitUpsideDown = v, false);
        changed |= Set(() => PlayerSettings.allowedAutorotateToLandscapeLeft, v => PlayerSettings.allowedAutorotateToLandscapeLeft = v, true);
        changed |= Set(() => PlayerSettings.allowedAutorotateToLandscapeRight, v => PlayerSettings.allowedAutorotateToLandscapeRight = v, true);

        if (PlayerSettings.GetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Android) != ScriptingImplementation.IL2CPP)
        {
            PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            changed = true;
        }
        if (PlayerSettings.Android.targetArchitectures != AndroidArchitecture.ARM64)
        {
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            changed = true;
        }

        if (changed)
        {
            AssetDatabase.SaveAssets();
            Debug.Log("ProjectSettingsApplier: applied landscape-only + Android IL2CPP/ARM64 settings.");
        }
    }

    static bool Set(System.Func<bool> get, System.Action<bool> set, bool value)
    {
        if (get() == value) return false;
        set(value);
        return true;
    }
}
