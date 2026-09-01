using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using KAM.Inventory;
using KAM.Inventory.UI;
using TMPro;

/// <summary>
/// Editor tools for setting up the Inventory system.
/// </summary>
public class InventorySetupHelper : Editor
{
    [MenuItem("KAM/Inventory/Setup Complete Inventory System", priority = 50)]
    public static void SetupCompleteSystem()
    {
        // 1. Setup Inventory Manager
        SetupInventoryManager();

        // 2. Setup Inventory UI
        SetupInventoryUI();

        EditorUtility.DisplayDialog("Inventory System Ready",
            "Complete Inventory System has been set up!\n\n" +
            "- InventoryManager (handles data)\n" +
            "- InventoryUI Canvas (TAB to open)\n" +
            "- QuickSlots HUD (keys 1-4)\n" +
            "- Spirit Fragments HUD\n" +
            "- Pickup Popups\n\n" +
            "Press TAB in Play mode to open inventory!",
            "OK");
    }

    [MenuItem("KAM/Inventory/Setup Inventory UI", priority = 75)]
    public static void SetupInventoryUI()
    {
        // Check for existing
        InventoryUI existingUI = FindFirstObjectByType<InventoryUI>();
        if (existingUI != null)
        {
            Selection.activeGameObject = existingUI.gameObject;
            Debug.Log("[InventorySetup] InventoryUI already exists");
            return;
        }

        // Create Canvas
        GameObject canvasObj = new GameObject("InventoryCanvas");
        Undo.RegisterCreatedObjectUndo(canvasObj, "Create Inventory UI");

        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;

        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        canvasObj.AddComponent<GraphicRaycaster>();

        // Create Inventory Panel (hidden by default)
        GameObject inventoryPanel = CreateInventoryPanel(canvasObj.transform);

        // Add InventoryUI component
        InventoryUI inventoryUI = canvasObj.AddComponent<InventoryUI>();

        // Create assign hint text
        GameObject assignHint = CreateAssignHintText(inventoryPanel.transform);

        // Find slot container (directly under panel now)
        Transform slotContainerTransform = inventoryPanel.transform.Find("SlotContainer");

        // Set references via SerializedObject
        SerializedObject serialized = new SerializedObject(inventoryUI);
        serialized.FindProperty("inventoryPanel").objectReferenceValue = inventoryPanel;
        serialized.FindProperty("slotContainer").objectReferenceValue = slotContainerTransform;
        serialized.FindProperty("titleText").objectReferenceValue = inventoryPanel.transform.Find("Title")?.GetComponent<TextMeshProUGUI>();
        serialized.FindProperty("assignHintText").objectReferenceValue = assignHint.GetComponent<TextMeshProUGUI>();
        serialized.ApplyModifiedProperties();

        Debug.Log($"[InventorySetup] SlotContainer found: {slotContainerTransform != null}");

        // Create HUD elements
        CreateQuickSlotsHUD(canvasObj.transform);
        CreateSpiritFragmentsHUD(canvasObj.transform);
        CreatePickupPopup(canvasObj.transform);

        Selection.activeGameObject = canvasObj;
        Debug.Log("[InventorySetup] Created complete Inventory UI");
    }

    private static GameObject CreateInventoryPanel(Transform parent)
    {
        // Main panel
        GameObject panel = new GameObject("InventoryPanel");
        panel.transform.SetParent(parent, false);

        RectTransform panelRect = panel.AddComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(450, 400);

        Image panelBg = panel.AddComponent<Image>();
        panelBg.color = new Color(0.1f, 0.1f, 0.1f, 0.95f);
        panelBg.raycastTarget = false; // Don't block slot clicks - slots handle their own raycasts

        // Title
        GameObject titleObj = new GameObject("Title");
        titleObj.transform.SetParent(panel.transform, false);

        RectTransform titleRect = titleObj.AddComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0, 1);
        titleRect.anchorMax = new Vector2(1, 1);
        titleRect.pivot = new Vector2(0.5f, 1);
        titleRect.anchoredPosition = new Vector2(0, -10);
        titleRect.sizeDelta = new Vector2(-20, 40);

        TextMeshProUGUI titleText = titleObj.AddComponent<TextMeshProUGUI>();
        titleText.text = "Inventory";
        titleText.fontSize = 24;
        titleText.fontStyle = FontStyles.Bold;
        titleText.alignment = TextAlignmentOptions.Center;
        titleText.raycastTarget = false; // Don't block slot clicks

        // Close hint
        GameObject closeHint = new GameObject("CloseHint");
        closeHint.transform.SetParent(panel.transform, false);

        RectTransform closeRect = closeHint.AddComponent<RectTransform>();
        closeRect.anchorMin = new Vector2(1, 1);
        closeRect.anchorMax = new Vector2(1, 1);
        closeRect.pivot = new Vector2(1, 1);
        closeRect.anchoredPosition = new Vector2(-10, -15);
        closeRect.sizeDelta = new Vector2(100, 30);

        TextMeshProUGUI closeText = closeHint.AddComponent<TextMeshProUGUI>();
        closeText.text = "[TAB] Close";
        closeText.fontSize = 14;
        closeText.alignment = TextAlignmentOptions.Right;
        closeText.color = Color.gray;
        closeText.raycastTarget = false; // Don't block slot clicks

        // Simple slot container (no scroll view - simpler and more reliable)
        GameObject slotContainer = new GameObject("SlotContainer");
        slotContainer.transform.SetParent(panel.transform, false);

        RectTransform containerRect = slotContainer.AddComponent<RectTransform>();
        containerRect.anchorMin = new Vector2(0, 0);
        containerRect.anchorMax = new Vector2(1, 1);
        containerRect.offsetMin = new Vector2(15, 35);
        containerRect.offsetMax = new Vector2(-15, -55);

        // Add background for visibility - MUST NOT block raycasts to child slots
        Image containerBg = slotContainer.AddComponent<Image>();
        containerBg.color = new Color(0, 0, 0, 0.2f);
        containerBg.raycastTarget = false; // Critical: don't block clicks on slots

        GridLayoutGroup grid = slotContainer.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(60, 60);
        grid.spacing = new Vector2(5, 5);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 6;
        grid.childAlignment = TextAnchor.UpperCenter; // Center horizontally, align to top
        grid.padding = new RectOffset(10, 10, 10, 10);

        panel.SetActive(false); // Start hidden

        return panel;
    }

    private static GameObject CreateAssignHintText(Transform parent)
    {
        GameObject hintObj = new GameObject("AssignHint");
        hintObj.transform.SetParent(parent, false);

        RectTransform hintRect = hintObj.AddComponent<RectTransform>();
        hintRect.anchorMin = new Vector2(0, 0);
        hintRect.anchorMax = new Vector2(1, 0);
        hintRect.pivot = new Vector2(0.5f, 0);
        hintRect.anchoredPosition = new Vector2(0, 5);
        hintRect.sizeDelta = new Vector2(-20, 25);

        TextMeshProUGUI hintText = hintObj.AddComponent<TextMeshProUGUI>();
        hintText.text = "[1-4] Quick Slot  [DblClick/RMB] Use";
        hintText.fontSize = 12;
        hintText.alignment = TextAlignmentOptions.Center;
        hintText.color = new Color(0.8f, 0.8f, 0.6f, 1f);
        hintText.enabled = false; // Hidden until item selected
        hintText.raycastTarget = false; // Don't block slot clicks

        return hintObj;
    }

    private static void CreateQuickSlotsHUD(Transform parent)
    {
        GameObject quickSlotsObj = new GameObject("QuickSlotsHUD");
        quickSlotsObj.transform.SetParent(parent, false);

        RectTransform rect = quickSlotsObj.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0);
        rect.anchorMax = new Vector2(0.5f, 0);
        rect.pivot = new Vector2(0.5f, 0);
        rect.anchoredPosition = new Vector2(0, 20);
        rect.sizeDelta = new Vector2(250, 60);

        QuickSlotsUI quickSlots = quickSlotsObj.AddComponent<QuickSlotsUI>();
        quickSlots.CreateUIElements();

        Debug.Log("[InventorySetup] Created QuickSlots HUD");
    }

    private static void CreateSpiritFragmentsHUD(Transform parent)
    {
        GameObject fragmentsObj = new GameObject("SpiritFragmentsHUD");
        fragmentsObj.transform.SetParent(parent, false);

        RectTransform rect = fragmentsObj.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(1, 1);
        rect.anchorMax = new Vector2(1, 1);
        rect.pivot = new Vector2(1, 1);
        rect.anchoredPosition = new Vector2(-20, -20);
        rect.sizeDelta = new Vector2(150, 40);

        SpiritFragmentsUI fragments = fragmentsObj.AddComponent<SpiritFragmentsUI>();
        fragments.CreateUIElements();

        Debug.Log("[InventorySetup] Created Spirit Fragments HUD");
    }

    private static void CreatePickupPopup(Transform parent)
    {
        GameObject popupObj = new GameObject("ItemPickupPopup");
        popupObj.transform.SetParent(parent, false);

        RectTransform rect = popupObj.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(1, 1);
        rect.anchorMax = new Vector2(1, 1);
        rect.pivot = new Vector2(1, 1);
        rect.anchoredPosition = new Vector2(-20, -70);
        rect.sizeDelta = new Vector2(220, 300);

        ItemPickupPopup popup = popupObj.AddComponent<ItemPickupPopup>();

        // Set reference
        SerializedObject serialized = new SerializedObject(popup);
        serialized.FindProperty("popupContainer").objectReferenceValue = rect;
        serialized.ApplyModifiedProperties();

        Debug.Log("[InventorySetup] Created Pickup Popup");
    }

    [MenuItem("KAM/Inventory/Setup Inventory Manager", priority = 100)]
    public static void SetupInventoryManager()
    {
        // Check if already exists
        InventoryManager existing = FindFirstObjectByType<InventoryManager>();
        if (existing != null)
        {
            Selection.activeGameObject = existing.gameObject;
            EditorUtility.DisplayDialog("Inventory Manager",
                "InventoryManager already exists in the scene.",
                "OK");
            return;
        }

        // Create new
        GameObject managerObj = new GameObject("InventoryManager");
        Undo.RegisterCreatedObjectUndo(managerObj, "Create Inventory Manager");

        InventoryManager manager = managerObj.AddComponent<InventoryManager>();

        Selection.activeGameObject = managerObj;

        EditorUtility.DisplayDialog("Inventory Manager Created",
            "InventoryManager has been added to the scene.\n\n" +
            "It will persist across scenes (DontDestroyOnLoad).\n\n" +
            "Configure settings in the Inspector.",
            "OK");
    }

    [MenuItem("KAM/Inventory/Create Sample Items", priority = 200)]
    public static void CreateSampleItems()
    {
        // Create folder
        string folderPath = "Assets/ScriptableObjects/Items";
        if (!AssetDatabase.IsValidFolder(folderPath))
        {
            if (!AssetDatabase.IsValidFolder("Assets/ScriptableObjects"))
            {
                AssetDatabase.CreateFolder("Assets", "ScriptableObjects");
            }
            AssetDatabase.CreateFolder("Assets/ScriptableObjects", "Items");
        }

        // Create sample material
        MaterialData herb = ScriptableObject.CreateInstance<MaterialData>();
        SetPrivateField(herb, "itemId", "spirit_herb");
        SetPrivateField(herb, "itemName", "Spirit Herb");
        SetPrivateField(herb, "description", "A glowing herb found in the Spirit Realm. Used to craft healing potions.");
        SetPrivateField(herb, "itemType", ItemType.Material);
        SetPrivateField(herb, "rarity", ItemRarity.Common);
        SetPrivateField(herb, "maxStackSize", 99);
        SetPrivateField(herb, "category", MaterialCategory.Herb);
        AssetDatabase.CreateAsset(herb, folderPath + "/Spirit_Herb.asset");

        // Create sample potion
        PotionData healingPotion = ScriptableObject.CreateInstance<PotionData>();
        SetPrivateField(healingPotion, "itemId", "healing_potion");
        SetPrivateField(healingPotion, "itemName", "Healing Potion");
        SetPrivateField(healingPotion, "description", "A basic potion that restores health.");
        SetPrivateField(healingPotion, "itemType", ItemType.Potion);
        SetPrivateField(healingPotion, "rarity", ItemRarity.Common);
        SetPrivateField(healingPotion, "maxStackSize", 10);
        SetPrivateField(healingPotion, "isConsumable", true);
        SetPrivateField(healingPotion, "effectType", PotionEffectType.Heal);
        SetPrivateField(healingPotion, "effectValue", 30f);
        SetPrivateField(healingPotion, "requiresBonusAction", true);
        AssetDatabase.CreateAsset(healingPotion, folderPath + "/Healing_Potion.asset");

        // Create sample talisman
        TalismanData talisman = ScriptableObject.CreateInstance<TalismanData>();
        SetPrivateField(talisman, "itemId", "wolf_fang_talisman");
        SetPrivateField(talisman, "itemName", "Wolf Fang Talisman");
        SetPrivateField(talisman, "description", "A talisman crafted from a spirit wolf's fang. Increases critical hit chance.");
        SetPrivateField(talisman, "itemType", ItemType.Talisman);
        SetPrivateField(talisman, "rarity", ItemRarity.Uncommon);
        SetPrivateField(talisman, "maxStackSize", 1);
        SetPrivateField(talisman, "statModifiers", new StatModifier[]
        {
            new StatModifier
            {
                statType = StatModifier.StatType.CritChance,
                modifierType = StatModifier.ModifierType.Percentage,
                value = 10f
            }
        });
        AssetDatabase.CreateAsset(talisman, folderPath + "/Wolf_Fang_Talisman.asset");

        // Create spirit fragment (currency)
        ItemData spiritFragment = ScriptableObject.CreateInstance<ItemData>();
        SetPrivateField(spiritFragment, "itemId", "spirit_fragment");
        SetPrivateField(spiritFragment, "itemName", "Spirit Fragment");
        SetPrivateField(spiritFragment, "description", "Crystallized spirit energy. Used to unlock skills in the Skill Tree.");
        SetPrivateField(spiritFragment, "itemType", ItemType.KeyItem);
        SetPrivateField(spiritFragment, "rarity", ItemRarity.Rare);
        SetPrivateField(spiritFragment, "maxStackSize", 9999);
        SetPrivateField(spiritFragment, "isDroppable", false);
        SetPrivateField(spiritFragment, "isSellable", false);
        AssetDatabase.CreateAsset(spiritFragment, folderPath + "/Spirit_Fragment.asset");

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        // Select the folder
        Selection.activeObject = AssetDatabase.LoadAssetAtPath<Object>(folderPath);

        EditorUtility.DisplayDialog("Sample Items Created",
            $"Created 4 sample items in:\n{folderPath}\n\n" +
            "- Spirit Herb (Material)\n" +
            "- Healing Potion (Potion)\n" +
            "- Wolf Fang Talisman (Talisman)\n" +
            "- Spirit Fragment (Currency)",
            "OK");
    }

    [MenuItem("KAM/Inventory/Create Item Pickup", priority = 300)]
    public static void CreateItemPickup()
    {
        // Find any item in project
        string[] guids = AssetDatabase.FindAssets("t:ItemData");
        ItemData defaultItem = null;
        if (guids.Length > 0)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[0]);
            defaultItem = AssetDatabase.LoadAssetAtPath<ItemData>(path);
        }

        GameObject pickupObj = new GameObject("ItemPickup");
        Undo.RegisterCreatedObjectUndo(pickupObj, "Create Item Pickup");

        // Add collider
        SphereCollider collider = pickupObj.AddComponent<SphereCollider>();
        collider.isTrigger = true;
        collider.radius = 0.5f;

        // Add pickup component
        ItemPickup pickup = pickupObj.AddComponent<ItemPickup>();

        // Set item if found
        if (defaultItem != null)
        {
            SerializedObject serializedPickup = new SerializedObject(pickup);
            SerializedProperty itemProp = serializedPickup.FindProperty("item");
            if (itemProp != null)
            {
                itemProp.objectReferenceValue = defaultItem;
                serializedPickup.ApplyModifiedProperties();
            }
        }

        // Create visual
        GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
        visual.name = "Visual";
        visual.transform.SetParent(pickupObj.transform);
        visual.transform.localPosition = Vector3.zero;
        visual.transform.localScale = Vector3.one * 0.3f;
        DestroyImmediate(visual.GetComponent<Collider>());

        // Position in scene view
        SceneView sceneView = SceneView.lastActiveSceneView;
        if (sceneView != null)
        {
            pickupObj.transform.position = sceneView.camera.transform.position +
                                           sceneView.camera.transform.forward * 5f;
        }

        Selection.activeGameObject = pickupObj;

        Debug.Log("[InventorySetup] Created ItemPickup - assign an ItemData in the Inspector");
    }

    // Helper to set private serialized fields
    private static void SetPrivateField(Object obj, string fieldName, object value)
    {
        SerializedObject serialized = new SerializedObject(obj);
        SerializedProperty prop = serialized.FindProperty(fieldName);
        if (prop != null)
        {
            switch (value)
            {
                case string s:
                    prop.stringValue = s;
                    break;
                case int i:
                    prop.intValue = i;
                    break;
                case float f:
                    prop.floatValue = f;
                    break;
                case bool b:
                    prop.boolValue = b;
                    break;
                case System.Enum e:
                    prop.enumValueIndex = System.Convert.ToInt32(e);
                    break;
                case Object o:
                    prop.objectReferenceValue = o;
                    break;
                case StatModifier[] mods:
                    prop.arraySize = mods.Length;
                    for (int i = 0; i < mods.Length; i++)
                    {
                        var element = prop.GetArrayElementAtIndex(i);
                        element.FindPropertyRelative("statType").enumValueIndex = (int)mods[i].statType;
                        element.FindPropertyRelative("modifierType").enumValueIndex = (int)mods[i].modifierType;
                        element.FindPropertyRelative("value").floatValue = mods[i].value;
                    }
                    break;
            }
            serialized.ApplyModifiedProperties();
        }
    }
}
