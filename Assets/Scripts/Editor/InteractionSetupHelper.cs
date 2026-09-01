using UnityEngine;
using UnityEditor;
using KAM.Interaction;

/// <summary>
/// Editor helper for setting up the Interaction system in scenes.
/// </summary>
public class InteractionSetupHelper : Editor
{
    [MenuItem("KAM/Interaction/Setup Interaction System on Player", priority = 100)]
    public static void SetupInteractionOnPlayer()
    {
        // Find player
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player == null)
        {
            // Try to find by name
            player = GameObject.Find("Player");
        }

        if (player == null)
        {
            EditorUtility.DisplayDialog("Player Not Found",
                "Could not find a GameObject tagged 'Player' or named 'Player'.\n\n" +
                "Please select the player GameObject and use 'Add to Selected' instead.",
                "OK");
            return;
        }

        AddInteractionToGameObject(player);
    }

    [MenuItem("KAM/Interaction/Add Interaction Detector to Selected", priority = 101)]
    public static void AddInteractionToSelected()
    {
        GameObject selected = Selection.activeGameObject;
        if (selected == null)
        {
            EditorUtility.DisplayDialog("No Selection",
                "Please select a GameObject to add the Interaction Detector to.",
                "OK");
            return;
        }

        AddInteractionToGameObject(selected);
    }

    private static void AddInteractionToGameObject(GameObject target)
    {
        Undo.RecordObject(target, "Add Interaction System");

        // Add InteractionDetector
        InteractionDetector detector = target.GetComponent<InteractionDetector>();
        if (detector == null)
        {
            detector = Undo.AddComponent<InteractionDetector>(target);
            Debug.Log($"[InteractionSetup] Added InteractionDetector to {target.name}");
        }
        else
        {
            Debug.Log($"[InteractionSetup] InteractionDetector already exists on {target.name}");
        }

        // Create InteractionUI
        CreateInteractionUI(detector);

        EditorUtility.DisplayDialog("Interaction System Setup",
            $"Interaction system added to {target.name}!\n\n" +
            "- InteractionDetector: Detects nearby interactables\n" +
            "- InteractionUI: Shows 'Press E' prompt\n\n" +
            "Make sure interactable objects have:\n" +
            "- A Collider component\n" +
            "- A script implementing IInteractable (LoreObject, TreasureChest, etc.)",
            "OK");
    }

    private static void CreateInteractionUI(InteractionDetector detector)
    {
        // Check if UI already exists
        InteractionUI existingUI = FindFirstObjectByType<InteractionUI>();
        if (existingUI != null)
        {
            Debug.Log("[InteractionSetup] InteractionUI already exists in scene");
            return;
        }

        // Create UI GameObject
        GameObject uiObj = new GameObject("InteractionUI");
        Undo.RegisterCreatedObjectUndo(uiObj, "Create Interaction UI");

        InteractionUI ui = uiObj.AddComponent<InteractionUI>();

        // Set reference to detector via SerializedObject
        SerializedObject serializedUI = new SerializedObject(ui);
        SerializedProperty detectorProp = serializedUI.FindProperty("detector");
        if (detectorProp != null)
        {
            detectorProp.objectReferenceValue = detector;
            serializedUI.ApplyModifiedProperties();
        }

        Debug.Log("[InteractionSetup] Created InteractionUI");
    }

    [MenuItem("KAM/Interaction/Create Lore Object", priority = 200)]
    public static void CreateLoreObject()
    {
        GameObject loreObj = new GameObject("LoreObject");
        Undo.RegisterCreatedObjectUndo(loreObj, "Create Lore Object");

        // Add components
        BoxCollider collider = loreObj.AddComponent<BoxCollider>();
        collider.isTrigger = true;
        collider.size = new Vector3(1, 2, 1);

        LoreObject lore = loreObj.AddComponent<LoreObject>();

        // Set default values via SerializedObject
        SerializedObject serializedLore = new SerializedObject(lore);

        SerializedProperty titleProp = serializedLore.FindProperty("loreTitle");
        if (titleProp != null)
            titleProp.stringValue = "Ancient Inscription";

        SerializedProperty pagesProp = serializedLore.FindProperty("lorePages");
        if (pagesProp != null)
        {
            pagesProp.arraySize = 1;
            pagesProp.GetArrayElementAtIndex(0).stringValue = "The ancient spirits whisper secrets of a forgotten age...";
        }

        serializedLore.ApplyModifiedProperties();

        // Position in front of camera
        SceneView sceneView = SceneView.lastActiveSceneView;
        if (sceneView != null)
        {
            loreObj.transform.position = sceneView.camera.transform.position +
                                         sceneView.camera.transform.forward * 5f;
        }

        Selection.activeGameObject = loreObj;

        Debug.Log("[InteractionSetup] Created LoreObject - configure lore text in inspector");
    }

    [MenuItem("KAM/Interaction/Create Treasure Chest", priority = 201)]
    public static void CreateTreasureChest()
    {
        GameObject chestObj = new GameObject("TreasureChest");
        Undo.RegisterCreatedObjectUndo(chestObj, "Create Treasure Chest");

        // Add components
        BoxCollider collider = chestObj.AddComponent<BoxCollider>();
        collider.isTrigger = true;
        collider.size = new Vector3(1.5f, 1f, 1f);

        TreasureChest chest = chestObj.AddComponent<TreasureChest>();

        // Create a simple visual
        GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
        visual.name = "ChestBody";
        visual.transform.SetParent(chestObj.transform);
        visual.transform.localPosition = Vector3.zero;
        visual.transform.localScale = new Vector3(1.5f, 0.8f, 1f);

        // Remove the cube's collider (we use the parent's)
        DestroyImmediate(visual.GetComponent<BoxCollider>());

        // Create lid
        GameObject lid = GameObject.CreatePrimitive(PrimitiveType.Cube);
        lid.name = "Lid";
        lid.transform.SetParent(chestObj.transform);
        lid.transform.localPosition = new Vector3(0, 0.5f, -0.4f);
        lid.transform.localScale = new Vector3(1.4f, 0.2f, 0.9f);

        DestroyImmediate(lid.GetComponent<BoxCollider>());

        // Set lid reference
        SerializedObject serializedChest = new SerializedObject(chest);
        SerializedProperty lidProp = serializedChest.FindProperty("lidTransform");
        if (lidProp != null)
        {
            lidProp.objectReferenceValue = lid.transform;
        }

        SerializedProperty fragmentsProp = serializedChest.FindProperty("spiritFragments");
        if (fragmentsProp != null)
        {
            fragmentsProp.intValue = Random.Range(5, 25);
        }

        serializedChest.ApplyModifiedProperties();

        // Position in front of camera
        SceneView sceneView = SceneView.lastActiveSceneView;
        if (sceneView != null)
        {
            chestObj.transform.position = sceneView.camera.transform.position +
                                          sceneView.camera.transform.forward * 5f;
        }

        Selection.activeGameObject = chestObj;

        Debug.Log("[InteractionSetup] Created TreasureChest - configure contents in inspector");
    }

    [MenuItem("KAM/Interaction/Make Selected Interactable (Lore)", priority = 300)]
    public static void MakeSelectedLore()
    {
        GameObject selected = Selection.activeGameObject;
        if (selected == null)
        {
            EditorUtility.DisplayDialog("No Selection", "Please select a GameObject to make interactable.", "OK");
            return;
        }

        Undo.RecordObject(selected, "Make Interactable");

        // Add collider if missing
        if (selected.GetComponent<Collider>() == null)
        {
            BoxCollider col = Undo.AddComponent<BoxCollider>(selected);
            col.isTrigger = true;
        }

        // Add LoreObject
        if (selected.GetComponent<LoreObject>() == null)
        {
            Undo.AddComponent<LoreObject>(selected);
        }

        Debug.Log($"[InteractionSetup] Made {selected.name} a LoreObject interactable");
    }

    [MenuItem("KAM/Interaction/Make Selected Interactable (Chest)", priority = 301)]
    public static void MakeSelectedChest()
    {
        GameObject selected = Selection.activeGameObject;
        if (selected == null)
        {
            EditorUtility.DisplayDialog("No Selection", "Please select a GameObject to make interactable.", "OK");
            return;
        }

        Undo.RecordObject(selected, "Make Interactable");

        // Add collider if missing
        if (selected.GetComponent<Collider>() == null)
        {
            BoxCollider col = Undo.AddComponent<BoxCollider>(selected);
            col.isTrigger = true;
        }

        // Add TreasureChest
        if (selected.GetComponent<TreasureChest>() == null)
        {
            Undo.AddComponent<TreasureChest>(selected);
        }

        Debug.Log($"[InteractionSetup] Made {selected.name} a TreasureChest interactable");
    }
}
