using KAM.UI.Combat;
using UnityEditor;
using UnityEngine;

public static class CombatUnitHUDPanelSetupEditor
{
    private const string MenuPath = "KAM/Combat UI/Auto Setup Selected HUD Panel";

    [MenuItem(MenuPath)]
    private static void AutoSetupSelectedHudPanel()
    {
        foreach (GameObject selected in Selection.gameObjects)
        {
            if (selected == null)
            {
                continue;
            }

            CombatUnitHUDPanel panel = selected.GetComponent<CombatUnitHUDPanel>();
            if (panel == null)
            {
                panel = Undo.AddComponent<CombatUnitHUDPanel>(selected);
            }

            Undo.RecordObject(panel, "Auto Setup HUD Panel");
            panel.AutoSetupHudPanel();
            EditorUtility.SetDirty(panel);
            PrefabUtility.RecordPrefabInstancePropertyModifications(panel);
        }

        AssetDatabase.SaveAssets();
    }

    [MenuItem(MenuPath, true)]
    private static bool CanAutoSetupSelectedHudPanel()
    {
        return Selection.gameObjects != null && Selection.gameObjects.Length > 0;
    }
}
