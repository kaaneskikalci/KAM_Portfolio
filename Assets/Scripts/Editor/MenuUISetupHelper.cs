using KAM.UI.Menus;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace KAM.EditorTools
{
    public static class MenuUISetupHelper
    {
        [MenuItem("KAM/UI/Create Main Menu UI In Current Scene", false, 20)]
        public static void CreateMainMenuUI()
        {
            MainMenuUI existing = Object.FindFirstObjectByType<MainMenuUI>();
            MainMenuUI menu = existing != null ? existing : CreateRoot<MainMenuUI>("MainMenuUI");
            Undo.RecordObject(menu, "Create Main Menu UI");
            menu.RebuildUI();
            MarkDirty(menu.gameObject);
            Debug.Log("[MenuUISetupHelper] Main Menu UI created.");
        }

        [MenuItem("KAM/UI/Create Pause UI In Current Scene", false, 21)]
        public static void CreatePauseUI()
        {
            PauseUI existing = Object.FindFirstObjectByType<PauseUI>();
            PauseUI pause = existing != null ? existing : CreateRoot<PauseUI>("PauseUI");
            Undo.RecordObject(pause, "Create Pause UI");
            pause.RebuildUI();
            MarkDirty(pause.gameObject);
            Debug.Log("[MenuUISetupHelper] Pause UI created.");
        }

        private static T CreateRoot<T>(string objectName) where T : Component
        {
            GameObject obj = new GameObject(objectName);
            Undo.RegisterCreatedObjectUndo(obj, $"Create {objectName}");
            return obj.AddComponent<T>();
        }

        private static void MarkDirty(GameObject obj)
        {
            EditorUtility.SetDirty(obj);
            EditorSceneManager.MarkSceneDirty(obj.scene);
        }
    }
}
