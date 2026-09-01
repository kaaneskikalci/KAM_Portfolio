#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using KAM.Core.Combat;
using KAM.Combat.TurnSystem;

/// <summary>
/// Editor helper to set up combat system components on GameObjects.
/// Access via menu: Tools > KAM Combat Setup
/// </summary>
public class CombatSetupHelper : EditorWindow
{
    [MenuItem("Tools/KAM Combat Setup/Setup Window")]
    public static void ShowWindow()
    {
        GetWindow<CombatSetupHelper>("KAM Combat Setup");
    }

    private void OnGUI()
    {
        GUILayout.Label("KAM Combat System Setup", EditorStyles.boldLabel);
        EditorGUILayout.Space(10);

        // Player Setup Section
        EditorGUILayout.LabelField("Player Setup", EditorStyles.boldLabel);
        if (GUILayout.Button("Setup Selected as Player"))
        {
            SetupPlayer();
        }
        EditorGUILayout.HelpBox("Select your Player GameObject and click to add PlayerCombatUnit component.", MessageType.Info);

        EditorGUILayout.Space(10);

        // Enemy Setup Section
        EditorGUILayout.LabelField("Enemy Setup", EditorStyles.boldLabel);
        if (GUILayout.Button("Setup Selected as Enemy"))
        {
            SetupEnemy();
        }
        EditorGUILayout.HelpBox("Select an Enemy GameObject with EnemyStateMachine and click to add EnemyCombatUnit.", MessageType.Info);

        EditorGUILayout.Space(10);

        // Manager Setup Section
        EditorGUILayout.LabelField("Manager Setup", EditorStyles.boldLabel);
        if (GUILayout.Button("Create Combat Managers"))
        {
            CreateManagers();
        }
        EditorGUILayout.HelpBox("Creates BattleManager and TurnManager if they don't exist.", MessageType.Info);

        EditorGUILayout.Space(10);

        // Full Scene Setup
        EditorGUILayout.LabelField("Full Scene Setup", EditorStyles.boldLabel);
        if (GUILayout.Button("Auto-Setup Entire Scene"))
        {
            AutoSetupScene();
        }
        EditorGUILayout.HelpBox("Automatically finds Player and all Enemies in scene and sets them up.", MessageType.Info);

        EditorGUILayout.Space(20);

        // Status
        EditorGUILayout.LabelField("Current Scene Status", EditorStyles.boldLabel);
        ShowSceneStatus();
    }

    private void ShowSceneStatus()
    {
        var player = FindFirstObjectByType<PlayerCombatUnit>();
        var enemies = FindObjectsByType<EnemyCombatUnit>(FindObjectsSortMode.None);
        var battleManager = FindFirstObjectByType<BattleManager>();
        var turnManager = FindFirstObjectByType<TurnManager>();

        EditorGUILayout.LabelField($"PlayerCombatUnit: {(player != null ? "✓ Found" : "✗ Not Found")}");
        EditorGUILayout.LabelField($"EnemyCombatUnits: {enemies.Length} found");
        EditorGUILayout.LabelField($"BattleManager: {(battleManager != null ? "✓ Found" : "✗ Not Found")}");
        EditorGUILayout.LabelField($"TurnManager: {(turnManager != null ? "✓ Found" : "✗ Not Found")}");
    }

    [MenuItem("Tools/KAM Combat Setup/Setup Selected as Player")]
    public static void SetupPlayer()
    {
        GameObject selected = Selection.activeGameObject;
        if (selected == null)
        {
            EditorUtility.DisplayDialog("No Selection", "Please select a GameObject to set up as Player.", "OK");
            return;
        }

        // Check for PlayerMovement
        var playerMovement = selected.GetComponent<PlayerMovement>();
        if (playerMovement == null)
        {
            EditorUtility.DisplayDialog("Invalid Selection",
                "Selected GameObject doesn't have PlayerMovement component.\nPlease select your Player GameObject.", "OK");
            return;
        }

        // Add PlayerCombatUnit if not present
        var combatUnit = selected.GetComponent<PlayerCombatUnit>();
        if (combatUnit == null)
        {
            Undo.AddComponent<PlayerCombatUnit>(selected);
            Debug.Log($"Added PlayerCombatUnit to {selected.name}");
        }
        else
        {
            Debug.Log($"{selected.name} already has PlayerCombatUnit");
        }

        EditorUtility.DisplayDialog("Success", $"Player '{selected.name}' is now set up for combat!", "OK");
    }

    [MenuItem("Tools/KAM Combat Setup/Setup Selected as Enemy")]
    public static void SetupEnemy()
    {
        GameObject selected = Selection.activeGameObject;
        if (selected == null)
        {
            EditorUtility.DisplayDialog("No Selection", "Please select a GameObject to set up as Enemy.", "OK");
            return;
        }

        // Check for EnemyStateMachine
        var stateMachine = selected.GetComponent<EnemyStateMachine>();
        if (stateMachine == null)
        {
            EditorUtility.DisplayDialog("Invalid Selection",
                "Selected GameObject doesn't have EnemyStateMachine component.\nPlease select an Enemy GameObject.", "OK");
            return;
        }

        // Add EnemyCombatUnit if not present
        var combatUnit = selected.GetComponent<EnemyCombatUnit>();
        if (combatUnit == null)
        {
            Undo.AddComponent<EnemyCombatUnit>(selected);
            Debug.Log($"Added EnemyCombatUnit to {selected.name}");
        }
        else
        {
            Debug.Log($"{selected.name} already has EnemyCombatUnit");
        }

        EditorUtility.DisplayDialog("Success", $"Enemy '{selected.name}' is now set up for combat!", "OK");
    }

    [MenuItem("Tools/KAM Combat Setup/Create Combat Managers")]
    public static void CreateManagers()
    {
        bool created = false;

        // Check/Create BattleManager
        var battleManager = FindFirstObjectByType<BattleManager>();
        if (battleManager == null)
        {
            GameObject bmObj = new GameObject("BattleManager");
            bmObj.AddComponent<BattleManager>();
            Undo.RegisterCreatedObjectUndo(bmObj, "Create BattleManager");
            Debug.Log("Created BattleManager");
            created = true;
        }

        // Check/Create TurnManager
        var turnManager = FindFirstObjectByType<TurnManager>();
        if (turnManager == null)
        {
            GameObject tmObj = new GameObject("TurnManager");
            tmObj.AddComponent<TurnManager>();
            Undo.RegisterCreatedObjectUndo(tmObj, "Create TurnManager");
            Debug.Log("Created TurnManager");
            created = true;
        }

        if (created)
        {
            EditorUtility.DisplayDialog("Success", "Combat managers created!", "OK");
        }
        else
        {
            EditorUtility.DisplayDialog("Already Exists", "All combat managers already exist in scene.", "OK");
        }
    }

    [MenuItem("Tools/KAM Combat Setup/Auto-Setup Entire Scene")]
    public static void AutoSetupScene()
    {
        int setupCount = 0;

        // Create managers first
        var battleManager = FindFirstObjectByType<BattleManager>();
        if (battleManager == null)
        {
            GameObject bmObj = new GameObject("BattleManager");
            bmObj.AddComponent<BattleManager>();
            Undo.RegisterCreatedObjectUndo(bmObj, "Create BattleManager");
            setupCount++;
        }

        var turnManager = FindFirstObjectByType<TurnManager>();
        if (turnManager == null)
        {
            GameObject tmObj = new GameObject("TurnManager");
            tmObj.AddComponent<TurnManager>();
            Undo.RegisterCreatedObjectUndo(tmObj, "Create TurnManager");
            setupCount++;
        }

        // Find and setup Player
        var playerMovement = FindFirstObjectByType<PlayerMovement>();
        if (playerMovement != null)
        {
            if (playerMovement.GetComponent<PlayerCombatUnit>() == null)
            {
                Undo.AddComponent<PlayerCombatUnit>(playerMovement.gameObject);
                Debug.Log($"Added PlayerCombatUnit to {playerMovement.name}");
                setupCount++;
            }
        }
        else
        {
            Debug.LogWarning("No PlayerMovement found in scene!");
        }

        // Find and setup all Enemies
        var enemies = FindObjectsByType<EnemyStateMachine>(FindObjectsSortMode.None);
        foreach (var enemy in enemies)
        {
            if (enemy.GetComponent<EnemyCombatUnit>() == null)
            {
                Undo.AddComponent<EnemyCombatUnit>(enemy.gameObject);
                Debug.Log($"Added EnemyCombatUnit to {enemy.name}");
                setupCount++;
            }
        }

        EditorUtility.DisplayDialog("Auto-Setup Complete",
            $"Setup complete!\n\n" +
            $"- Player: {(playerMovement != null ? "✓" : "✗ Not Found")}\n" +
            $"- Enemies: {enemies.Length} found\n" +
            $"- Components added: {setupCount}", "OK");
    }
}
#endif
