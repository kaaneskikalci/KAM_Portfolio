using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Clears stale editor selections that can leave the Inspector targeting destroyed objects.
/// This avoids built-in inspector exceptions like SerializedObjectNotCreatableException
/// when a selected scene object is deleted, reloaded, or recreated during play mode / recompiles.
/// </summary>
[InitializeOnLoad]
public static class EditorSelectionGuard
{
    private static bool _sanitizeQueued;

    static EditorSelectionGuard()
    {
        QueueSanitize();

        Selection.selectionChanged += OnSelectionChanged;
        EditorApplication.hierarchyChanged += OnHierarchyChanged;
        EditorApplication.projectChanged += QueueSanitize;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        AssemblyReloadEvents.afterAssemblyReload += QueueSanitize;
    }

    [MenuItem("Tools/KAM Repair/Clear Stale Inspector Selection", false, 5)]
    public static void ClearSelectionNow()
    {
        Selection.objects = Array.Empty<UnityEngine.Object>();
        ActiveEditorTracker.sharedTracker.ForceRebuild();
        Debug.Log("[EditorSelectionGuard] Inspector selection cleared.");
    }

    private static void OnSelectionChanged()
    {
        if (HasInvalidSelection())
            QueueSanitize();
    }

    private static void OnHierarchyChanged()
    {
        if (HasInvalidSelection())
            QueueSanitize();
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.ExitingEditMode || state == PlayModeStateChange.ExitingPlayMode)
        {
            ClearSelectionSilently();
            return;
        }

        QueueSanitize();
    }

    private static bool HasInvalidSelection()
    {
        UnityEngine.Object[] selection = Selection.objects;

        if (selection == null || selection.Length == 0)
            return false;

        if (Selection.activeObject == null)
            return true;

        for (int i = 0; i < selection.Length; i++)
        {
            if (selection[i] == null)
                return true;
        }

        return false;
    }

    private static void QueueSanitize()
    {
        if (_sanitizeQueued)
            return;

        _sanitizeQueued = true;
        EditorApplication.delayCall += SanitizeSelection;
    }

    private static void SanitizeSelection()
    {
        _sanitizeQueued = false;

        UnityEngine.Object[] selection = Selection.objects;
        if (selection == null || selection.Length == 0)
        {
            ActiveEditorTracker.sharedTracker.ForceRebuild();
            return;
        }

        UnityEngine.Object[] validSelection = selection
            .Where(obj => obj != null)
            .Distinct()
            .ToArray();

        bool needsRepair = validSelection.Length != selection.Length || Selection.activeObject == null;
        if (!needsRepair)
            return;

        Selection.objects = validSelection;
        ActiveEditorTracker.sharedTracker.ForceRebuild();

        Debug.LogWarning($"[EditorSelectionGuard] Repaired stale inspector selection. Valid targets: {validSelection.Length}");
    }

    private static void ClearSelectionSilently()
    {
        Selection.objects = Array.Empty<UnityEngine.Object>();
        Selection.activeObject = null;
        ActiveEditorTracker.sharedTracker.ForceRebuild();
        UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
    }
}
