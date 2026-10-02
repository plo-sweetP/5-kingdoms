using FiveKingdoms;
using UnityEditor;
using UnityEngine;

/// <summary>Developer shortcuts under the 5 Kingdoms menu.</summary>
static class DebugMenu
{
    [MenuItem("5 Kingdoms/Debug/Reset Save (start over at Lv 1)")]
    static void ResetSave()
    {
        if (!EditorUtility.DisplayDialog("Reset save?", $"Delete {SaveSystem.FilePath}? Uzuki goes back to Lv 1.", "Delete", "Cancel")) return;
        SaveSystem.Delete();
        Debug.Log($"Deleted save {SaveSystem.FilePath}");
    }

    [MenuItem("5 Kingdoms/Debug/Show Save File")]
    static void ShowSave() => EditorUtility.RevealInFinder(SaveSystem.FilePath);
}
