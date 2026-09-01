using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using TMPro;
using KAM.UI.Combat;
using KAM.Combat.TimedHit;

/// <summary>
/// Editor helper to automatically create and setup Combat UI.
/// Access via: Tools > KAM Combat Setup > Create Combat UI
/// </summary>
public class CombatUISetupHelper : EditorWindow
{
    [MenuItem("Tools/KAM Combat Setup/Create Combat UI (Full Setup)", false, 20)]
    public static void CreateFullCombatUI()
    {
        if (EditorUtility.DisplayDialog("Create Combat UI",
            "This will create:\n" +
            "• Combat HUD Canvas\n" +
            "• Turn Order Bar\n" +
            "• Action Menu\n" +
            "• Health Bar Prefab\n" +
            "• Damage Popup Prefab\n" +
            "• Portrait Slot Prefab\n\n" +
            "Continue?", "Create", "Cancel"))
        {
            CreateCombatHUDCanvas();
            CreatePrefabs();
            Debug.Log("<color=green>[Combat UI Setup] Complete! Check your scene and Assets/Prefabs/Combat folder.</color>");
        }
    }

    [MenuItem("Tools/KAM Combat Setup/Create Combat HUD Canvas Only", false, 21)]
    public static void CreateCombatHUDCanvas()
    {
        // Check if already exists
        var existing = Object.FindFirstObjectByType<CombatHUDManager>();
        if (existing != null)
        {
            Debug.LogWarning("[Combat UI Setup] CombatHUDManager already exists in scene!");
            Selection.activeGameObject = existing.gameObject;
            return;
        }

        // Create main canvas
        GameObject canvasObj = new GameObject("CombatHUD_Canvas");
        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        canvasObj.AddComponent<GraphicRaycaster>();

        // Create HUD Root (for showing/hiding)
        GameObject hudRoot = new GameObject("HUDRoot");
        hudRoot.transform.SetParent(canvasObj.transform, false);
        RectTransform hudRootRect = hudRoot.AddComponent<RectTransform>();
        hudRootRect.anchorMin = Vector2.zero;
        hudRootRect.anchorMax = Vector2.one;
        hudRootRect.offsetMin = Vector2.zero;
        hudRootRect.offsetMax = Vector2.zero;
        CanvasGroup hudCanvasGroup = hudRoot.AddComponent<CanvasGroup>();

        // Add CombatHUDManager
        CombatHUDManager hudManager = canvasObj.AddComponent<CombatHUDManager>();

        // Create Turn Order UI
        GameObject turnOrderObj = CreateTurnOrderUI(hudRoot.transform);
        TurnOrderUI turnOrderUI = turnOrderObj.GetComponent<TurnOrderUI>();

        // Create Action Menu UI
        GameObject actionMenuObj = CreateActionMenuUI(hudRoot.transform);
        ActionMenuUI actionMenuUI = actionMenuObj.GetComponent<ActionMenuUI>();

        // Set references via SerializedObject
        SerializedObject serializedHUD = new SerializedObject(hudManager);
        serializedHUD.FindProperty("combatHUDRoot").objectReferenceValue = hudRoot;
        serializedHUD.FindProperty("turnOrderUI").objectReferenceValue = turnOrderUI;
        serializedHUD.FindProperty("actionMenuUI").objectReferenceValue = actionMenuUI;
        serializedHUD.ApplyModifiedProperties();

        // Add CombatUIController to connect UI to combat
        CombatUIController uiController = canvasObj.AddComponent<CombatUIController>();
        SerializedObject serializedController = new SerializedObject(uiController);
        serializedController.FindProperty("actionMenuUI").objectReferenceValue = actionMenuUI;
        serializedController.ApplyModifiedProperties();

        // Register undo
        Undo.RegisterCreatedObjectUndo(canvasObj, "Create Combat HUD Canvas");

        Selection.activeGameObject = canvasObj;
        Debug.Log("<color=green>[Combat UI Setup] Created Combat HUD Canvas with UI Controller!</color>");
    }

    private static GameObject CreateTurnOrderUI(Transform parent)
    {
        // Turn Order Container
        GameObject turnOrderObj = new GameObject("TurnOrderUI");
        turnOrderObj.transform.SetParent(parent, false);

        RectTransform rect = turnOrderObj.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0, 1);
        rect.anchorMax = new Vector2(1, 1);
        rect.pivot = new Vector2(0.5f, 1);
        rect.anchoredPosition = new Vector2(0, -20);
        rect.sizeDelta = new Vector2(-40, 100);

        TurnOrderUI turnOrderUI = turnOrderObj.AddComponent<TurnOrderUI>();

        // Background panel
        GameObject bgPanel = new GameObject("Background");
        bgPanel.transform.SetParent(turnOrderObj.transform, false);
        RectTransform bgRect = bgPanel.AddComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.offsetMin = Vector2.zero;
        bgRect.offsetMax = Vector2.zero;

        Image bgImage = bgPanel.AddComponent<Image>();
        bgImage.color = new Color(0, 0, 0, 0.5f);

        // Portrait Container (HorizontalLayoutGroup)
        GameObject containerObj = new GameObject("PortraitContainer");
        containerObj.transform.SetParent(turnOrderObj.transform, false);

        RectTransform containerRect = containerObj.AddComponent<RectTransform>();
        containerRect.anchorMin = new Vector2(0, 0.5f);
        containerRect.anchorMax = new Vector2(1, 0.5f);
        containerRect.pivot = new Vector2(0, 0.5f);
        containerRect.anchoredPosition = new Vector2(20, 0);
        containerRect.sizeDelta = new Vector2(-40, 80);

        HorizontalLayoutGroup layout = containerObj.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 10;
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childControlWidth = false;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        // Set container reference
        SerializedObject serialized = new SerializedObject(turnOrderUI);
        serialized.FindProperty("portraitContainer").objectReferenceValue = containerObj.transform;
        serialized.ApplyModifiedProperties();

        return turnOrderObj;
    }

    private static GameObject CreateActionMenuUI(Transform parent)
    {
        // Action Menu Container
        GameObject actionMenuObj = new GameObject("ActionMenuUI");
        actionMenuObj.transform.SetParent(parent, false);

        RectTransform rect = actionMenuObj.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0, 0);
        rect.anchorMax = new Vector2(0, 0);
        rect.pivot = new Vector2(0, 0);
        rect.anchoredPosition = new Vector2(50, 50);
        rect.sizeDelta = new Vector2(300, 250);

        ActionMenuUI actionMenuUI = actionMenuObj.AddComponent<ActionMenuUI>();
        CanvasGroup menuCanvasGroup = actionMenuObj.AddComponent<CanvasGroup>();

        // Menu Root
        GameObject menuRoot = new GameObject("MenuRoot");
        menuRoot.transform.SetParent(actionMenuObj.transform, false);
        RectTransform menuRootRect = menuRoot.AddComponent<RectTransform>();
        menuRootRect.anchorMin = Vector2.zero;
        menuRootRect.anchorMax = Vector2.one;
        menuRootRect.offsetMin = Vector2.zero;
        menuRootRect.offsetMax = Vector2.zero;

        // Background
        Image menuBg = menuRoot.AddComponent<Image>();
        menuBg.color = new Color(0.1f, 0.1f, 0.15f, 0.9f);

        // Vertical layout for buttons
        VerticalLayoutGroup vLayout = menuRoot.AddComponent<VerticalLayoutGroup>();
        vLayout.padding = new RectOffset(15, 15, 15, 15);
        vLayout.spacing = 10;
        vLayout.childAlignment = TextAnchor.UpperCenter;
        vLayout.childControlWidth = true;
        vLayout.childControlHeight = false;
        vLayout.childForceExpandWidth = true;
        vLayout.childForceExpandHeight = false;

        // Create buttons
        Button attackBtn = CreateMenuButton(menuRoot.transform, "Attack", new Color(0.8f, 0.3f, 0.3f));
        Button skillsBtn = CreateMenuButton(menuRoot.transform, "Skills", new Color(0.3f, 0.5f, 0.8f));
        Button itemsBtn = CreateMenuButton(menuRoot.transform, "Items", new Color(0.3f, 0.7f, 0.4f));
        Button defendBtn = CreateMenuButton(menuRoot.transform, "Defend", new Color(0.6f, 0.6f, 0.3f));

        // Skills Submenu (hidden by default)
        GameObject skillsSubmenu = CreateSubmenu(menuRoot.transform, "SkillsSubmenu");
        skillsSubmenu.SetActive(false);

        // Items Submenu (hidden by default)
        GameObject itemsSubmenu = CreateSubmenu(menuRoot.transform, "ItemsSubmenu");
        itemsSubmenu.SetActive(false);

        // Set references
        SerializedObject serialized = new SerializedObject(actionMenuUI);
        serialized.FindProperty("menuRoot").objectReferenceValue = menuRoot;
        serialized.FindProperty("canvasGroup").objectReferenceValue = menuCanvasGroup;
        serialized.FindProperty("attackButton").objectReferenceValue = attackBtn;
        serialized.FindProperty("skillsButton").objectReferenceValue = skillsBtn;
        serialized.FindProperty("itemsButton").objectReferenceValue = itemsBtn;
        serialized.FindProperty("defendButton").objectReferenceValue = defendBtn;
        serialized.FindProperty("skillsSubmenu").objectReferenceValue = skillsSubmenu;
        serialized.FindProperty("itemsSubmenu").objectReferenceValue = itemsSubmenu;
        serialized.FindProperty("skillsContainer").objectReferenceValue = skillsSubmenu.transform.Find("Container");
        serialized.FindProperty("itemsContainer").objectReferenceValue = itemsSubmenu.transform.Find("Container");
        serialized.ApplyModifiedProperties();

        return actionMenuObj;
    }

    private static Button CreateMenuButton(Transform parent, string text, Color color)
    {
        GameObject btnObj = new GameObject(text + "Button");
        btnObj.transform.SetParent(parent, false);

        RectTransform rect = btnObj.AddComponent<RectTransform>();
        rect.sizeDelta = new Vector2(0, 45);

        Image btnImage = btnObj.AddComponent<Image>();
        btnImage.color = color;

        Button btn = btnObj.AddComponent<Button>();
        btn.targetGraphic = btnImage;

        // Button colors
        ColorBlock colors = btn.colors;
        colors.normalColor = color;
        colors.highlightedColor = color * 1.2f;
        colors.pressedColor = color * 0.8f;
        colors.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.5f);
        btn.colors = colors;

        // Text
        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(btnObj.transform, false);

        RectTransform textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        TextMeshProUGUI tmpText = textObj.AddComponent<TextMeshProUGUI>();
        tmpText.text = text;
        tmpText.fontSize = 24;
        tmpText.alignment = TextAlignmentOptions.Center;
        tmpText.color = Color.white;

        return btn;
    }

    private static GameObject CreateSubmenu(Transform parent, string name)
    {
        GameObject submenu = new GameObject(name);
        submenu.transform.SetParent(parent, false);

        RectTransform rect = submenu.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(1, 0);
        rect.anchorMax = new Vector2(1, 1);
        rect.pivot = new Vector2(0, 0.5f);
        rect.anchoredPosition = new Vector2(10, 0);
        rect.sizeDelta = new Vector2(200, 0);

        Image bg = submenu.AddComponent<Image>();
        bg.color = new Color(0.15f, 0.15f, 0.2f, 0.95f);

        // Container for buttons
        GameObject container = new GameObject("Container");
        container.transform.SetParent(submenu.transform, false);

        RectTransform containerRect = container.AddComponent<RectTransform>();
        containerRect.anchorMin = Vector2.zero;
        containerRect.anchorMax = Vector2.one;
        containerRect.offsetMin = new Vector2(10, 10);
        containerRect.offsetMax = new Vector2(-10, -10);

        VerticalLayoutGroup vLayout = container.AddComponent<VerticalLayoutGroup>();
        vLayout.spacing = 5;
        vLayout.childControlWidth = true;
        vLayout.childControlHeight = false;

        ContentSizeFitter fitter = submenu.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        return submenu;
    }

    [MenuItem("Tools/KAM Combat Setup/Create Combat UI Prefabs", false, 22)]
    public static void CreatePrefabs()
    {
        // Create Prefabs folder if needed
        string prefabPath = "Assets/Prefabs/Combat";
        if (!AssetDatabase.IsValidFolder("Assets/Prefabs"))
        {
            AssetDatabase.CreateFolder("Assets", "Prefabs");
        }
        if (!AssetDatabase.IsValidFolder(prefabPath))
        {
            AssetDatabase.CreateFolder("Assets/Prefabs", "Combat");
        }

        // Also create Resources folder for DamagePopup
        if (!AssetDatabase.IsValidFolder("Assets/Resources"))
        {
            AssetDatabase.CreateFolder("Assets", "Resources");
        }
        if (!AssetDatabase.IsValidFolder("Assets/Resources/Prefabs"))
        {
            AssetDatabase.CreateFolder("Assets/Resources", "Prefabs");
        }

        CreateTurnPortraitSlotPrefab(prefabPath);
        CreateHealthBarPrefab(prefabPath);
        CreateDamagePopupPrefab();
        CreateActionButtonPrefab(prefabPath);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("<color=green>[Combat UI Setup] Created prefabs in " + prefabPath + "</color>");
    }

    private static void CreateTurnPortraitSlotPrefab(string path)
    {
        GameObject slotObj = new GameObject("TurnPortraitSlot");

        RectTransform rect = slotObj.AddComponent<RectTransform>();
        rect.sizeDelta = new Vector2(70, 80);

        TurnPortraitSlot slot = slotObj.AddComponent<TurnPortraitSlot>();

        // Border
        GameObject borderObj = new GameObject("Border");
        borderObj.transform.SetParent(slotObj.transform, false);
        RectTransform borderRect = borderObj.AddComponent<RectTransform>();
        borderRect.anchorMin = Vector2.zero;
        borderRect.anchorMax = Vector2.one;
        borderRect.offsetMin = Vector2.zero;
        borderRect.offsetMax = Vector2.zero;
        Image borderImage = borderObj.AddComponent<Image>();
        borderImage.color = Color.white;

        // Portrait background
        GameObject portraitBg = new GameObject("PortraitBackground");
        portraitBg.transform.SetParent(slotObj.transform, false);
        RectTransform portraitBgRect = portraitBg.AddComponent<RectTransform>();
        portraitBgRect.anchorMin = new Vector2(0.5f, 0.6f);
        portraitBgRect.anchorMax = new Vector2(0.5f, 0.6f);
        portraitBgRect.sizeDelta = new Vector2(50, 50);
        Image portraitBgImage = portraitBg.AddComponent<Image>();
        portraitBgImage.color = new Color(0.2f, 0.2f, 0.2f);

        // Portrait
        GameObject portraitObj = new GameObject("Portrait");
        portraitObj.transform.SetParent(portraitBg.transform, false);
        RectTransform portraitRect = portraitObj.AddComponent<RectTransform>();
        portraitRect.anchorMin = Vector2.zero;
        portraitRect.anchorMax = Vector2.one;
        portraitRect.offsetMin = new Vector2(3, 3);
        portraitRect.offsetMax = new Vector2(-3, -3);
        Image portraitImage = portraitObj.AddComponent<Image>();
        portraitImage.color = Color.gray;

        // Highlight
        GameObject highlightObj = new GameObject("Highlight");
        highlightObj.transform.SetParent(slotObj.transform, false);
        RectTransform highlightRect = highlightObj.AddComponent<RectTransform>();
        highlightRect.anchorMin = Vector2.zero;
        highlightRect.anchorMax = Vector2.one;
        highlightRect.offsetMin = new Vector2(-5, -5);
        highlightRect.offsetMax = new Vector2(5, 5);
        Image highlightImage = highlightObj.AddComponent<Image>();
        highlightImage.color = Color.yellow;
        highlightObj.SetActive(false);

        // Order Number
        GameObject orderObj = new GameObject("OrderNumber");
        orderObj.transform.SetParent(slotObj.transform, false);
        RectTransform orderRect = orderObj.AddComponent<RectTransform>();
        orderRect.anchorMin = new Vector2(0, 1);
        orderRect.anchorMax = new Vector2(0, 1);
        orderRect.pivot = new Vector2(0, 1);
        orderRect.anchoredPosition = new Vector2(5, -5);
        orderRect.sizeDelta = new Vector2(20, 20);
        TextMeshProUGUI orderText = orderObj.AddComponent<TextMeshProUGUI>();
        orderText.text = "1";
        orderText.fontSize = 16;
        orderText.alignment = TextAlignmentOptions.Center;
        orderText.color = Color.white;

        // Unit Name
        GameObject nameObj = new GameObject("UnitName");
        nameObj.transform.SetParent(slotObj.transform, false);
        RectTransform nameRect = nameObj.AddComponent<RectTransform>();
        nameRect.anchorMin = new Vector2(0, 0);
        nameRect.anchorMax = new Vector2(1, 0);
        nameRect.pivot = new Vector2(0.5f, 0);
        nameRect.anchoredPosition = new Vector2(0, 5);
        nameRect.sizeDelta = new Vector2(0, 20);
        TextMeshProUGUI nameText = nameObj.AddComponent<TextMeshProUGUI>();
        nameText.text = "Unit";
        nameText.fontSize = 12;
        nameText.alignment = TextAlignmentOptions.Center;
        nameText.color = Color.white;

        // Set references
        SerializedObject serialized = new SerializedObject(slot);
        serialized.FindProperty("portraitImage").objectReferenceValue = portraitImage;
        serialized.FindProperty("borderImage").objectReferenceValue = borderImage;
        serialized.FindProperty("highlightImage").objectReferenceValue = highlightImage;
        serialized.FindProperty("orderNumberText").objectReferenceValue = orderText;
        serialized.FindProperty("unitNameText").objectReferenceValue = nameText;
        serialized.ApplyModifiedProperties();

        // Save prefab
        string prefabFile = path + "/TurnPortraitSlot.prefab";
        PrefabUtility.SaveAsPrefabAsset(slotObj, prefabFile);
        Object.DestroyImmediate(slotObj);

        // Link to TurnOrderUI if exists
        var turnOrderUI = Object.FindFirstObjectByType<TurnOrderUI>();
        if (turnOrderUI != null)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<TurnPortraitSlot>(prefabFile);
            SerializedObject turnOrderSerialized = new SerializedObject(turnOrderUI);
            turnOrderSerialized.FindProperty("portraitSlotPrefab").objectReferenceValue = prefab;
            turnOrderSerialized.ApplyModifiedProperties();
        }
    }

    private static void CreateHealthBarPrefab(string path)
    {
        // Create World Space Canvas for health bar
        GameObject canvasObj = new GameObject("HealthBarUI");

        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;

        RectTransform canvasRect = canvasObj.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(200, 50);
        canvasRect.localScale = Vector3.one * 0.01f;

        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.dynamicPixelsPerUnit = 100;

        CanvasGroup canvasGroup = canvasObj.AddComponent<CanvasGroup>();

        HealthBarUI healthBarUI = canvasObj.AddComponent<HealthBarUI>();

        // Background
        GameObject bgObj = new GameObject("Background");
        bgObj.transform.SetParent(canvasObj.transform, false);
        RectTransform bgRect = bgObj.AddComponent<RectTransform>();
        bgRect.anchorMin = new Vector2(0, 0.3f);
        bgRect.anchorMax = new Vector2(1, 0.7f);
        bgRect.offsetMin = Vector2.zero;
        bgRect.offsetMax = Vector2.zero;
        Image bgImage = bgObj.AddComponent<Image>();
        bgImage.color = new Color(0.2f, 0.2f, 0.2f, 0.8f);

        // Damage Bar (behind fill)
        GameObject damageBarObj = new GameObject("DamageBar");
        damageBarObj.transform.SetParent(bgObj.transform, false);
        RectTransform damageRect = damageBarObj.AddComponent<RectTransform>();
        damageRect.anchorMin = Vector2.zero;
        damageRect.anchorMax = Vector2.one;
        damageRect.offsetMin = new Vector2(2, 2);
        damageRect.offsetMax = new Vector2(-2, -2);
        Image damageImage = damageBarObj.AddComponent<Image>();
        damageImage.color = new Color(0.8f, 0.3f, 0.3f, 0.5f);
        damageImage.type = Image.Type.Filled;
        damageImage.fillMethod = Image.FillMethod.Horizontal;

        // Health Fill
        GameObject fillObj = new GameObject("HealthFill");
        fillObj.transform.SetParent(bgObj.transform, false);
        RectTransform fillRect = fillObj.AddComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = new Vector2(2, 2);
        fillRect.offsetMax = new Vector2(-2, -2);
        Image fillImage = fillObj.AddComponent<Image>();
        fillImage.color = new Color(0.2f, 0.8f, 0.2f);
        fillImage.type = Image.Type.Filled;
        fillImage.fillMethod = Image.FillMethod.Horizontal;

        // Health Text
        GameObject textObj = new GameObject("HealthText");
        textObj.transform.SetParent(canvasObj.transform, false);
        RectTransform textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = new Vector2(0, 0.3f);
        textRect.anchorMax = new Vector2(1, 0.7f);
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
        TextMeshProUGUI healthText = textObj.AddComponent<TextMeshProUGUI>();
        healthText.text = "100/100";
        healthText.fontSize = 18;
        healthText.alignment = TextAlignmentOptions.Center;
        healthText.color = Color.white;

        // Unit Name
        GameObject nameObj = new GameObject("UnitName");
        nameObj.transform.SetParent(canvasObj.transform, false);
        RectTransform nameRect = nameObj.AddComponent<RectTransform>();
        nameRect.anchorMin = new Vector2(0, 0.7f);
        nameRect.anchorMax = new Vector2(1, 1);
        nameRect.offsetMin = Vector2.zero;
        nameRect.offsetMax = Vector2.zero;
        TextMeshProUGUI nameText = nameObj.AddComponent<TextMeshProUGUI>();
        nameText.text = "Unit Name";
        nameText.fontSize = 14;
        nameText.alignment = TextAlignmentOptions.Center;
        nameText.color = Color.white;

        // Set references
        SerializedObject serialized = new SerializedObject(healthBarUI);
        serialized.FindProperty("healthBarFill").objectReferenceValue = fillImage;
        serialized.FindProperty("healthBarBackground").objectReferenceValue = bgImage;
        serialized.FindProperty("damageBarFill").objectReferenceValue = damageImage;
        serialized.FindProperty("healthText").objectReferenceValue = healthText;
        serialized.FindProperty("unitNameText").objectReferenceValue = nameText;
        serialized.FindProperty("canvasGroup").objectReferenceValue = canvasGroup;
        serialized.ApplyModifiedProperties();

        // Save prefab
        PrefabUtility.SaveAsPrefabAsset(canvasObj, path + "/HealthBarUI.prefab");
        Object.DestroyImmediate(canvasObj);
    }

    private static void CreateDamagePopupPrefab()
    {
        // Create World Space Canvas for damage popup
        GameObject canvasObj = new GameObject("DamagePopup");

        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;

        RectTransform canvasRect = canvasObj.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(200, 100);
        canvasRect.localScale = Vector3.one * 0.015f;

        CanvasGroup canvasGroup = canvasObj.AddComponent<CanvasGroup>();
        DamagePopup popup = canvasObj.AddComponent<DamagePopup>();

        // Damage Text
        GameObject textObj = new GameObject("DamageText");
        textObj.transform.SetParent(canvasObj.transform, false);
        RectTransform textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        TextMeshProUGUI text = textObj.AddComponent<TextMeshProUGUI>();
        text.text = "0";
        text.fontSize = 48;
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.white;
        text.fontStyle = FontStyles.Bold;
        text.enableWordWrapping = false;

        // Add outline
        // Note: Outline settings would typically be in the TMP material

        // Set references
        SerializedObject serialized = new SerializedObject(popup);
        serialized.FindProperty("damageText").objectReferenceValue = text;
        serialized.FindProperty("canvasGroup").objectReferenceValue = canvasGroup;
        serialized.ApplyModifiedProperties();

        // Save to Resources folder (for static factory method)
        PrefabUtility.SaveAsPrefabAsset(canvasObj, "Assets/Resources/Prefabs/DamagePopup.prefab");

        // Also save to regular prefabs folder
        PrefabUtility.SaveAsPrefabAsset(canvasObj, "Assets/Prefabs/Combat/DamagePopup.prefab");

        Object.DestroyImmediate(canvasObj);
    }

    private static void CreateActionButtonPrefab(string path)
    {
        GameObject btnObj = new GameObject("ActionButton");

        RectTransform rect = btnObj.AddComponent<RectTransform>();
        rect.sizeDelta = new Vector2(180, 35);

        Image btnImage = btnObj.AddComponent<Image>();
        btnImage.color = new Color(0.3f, 0.3f, 0.4f);

        Button btn = btnObj.AddComponent<Button>();
        btn.targetGraphic = btnImage;

        ActionButton actionBtn = btnObj.AddComponent<ActionButton>();

        // Name Text
        GameObject nameObj = new GameObject("NameText");
        nameObj.transform.SetParent(btnObj.transform, false);
        RectTransform nameRect = nameObj.AddComponent<RectTransform>();
        nameRect.anchorMin = new Vector2(0, 0.5f);
        nameRect.anchorMax = new Vector2(1, 1);
        nameRect.offsetMin = new Vector2(10, 0);
        nameRect.offsetMax = new Vector2(-10, 0);
        TextMeshProUGUI nameText = nameObj.AddComponent<TextMeshProUGUI>();
        nameText.text = "Action";
        nameText.fontSize = 16;
        nameText.alignment = TextAlignmentOptions.Left;
        nameText.color = Color.white;

        // Description Text
        GameObject descObj = new GameObject("DescriptionText");
        descObj.transform.SetParent(btnObj.transform, false);
        RectTransform descRect = descObj.AddComponent<RectTransform>();
        descRect.anchorMin = new Vector2(0, 0);
        descRect.anchorMax = new Vector2(1, 0.5f);
        descRect.offsetMin = new Vector2(10, 0);
        descRect.offsetMax = new Vector2(-10, 0);
        TextMeshProUGUI descText = descObj.AddComponent<TextMeshProUGUI>();
        descText.text = "Description";
        descText.fontSize = 11;
        descText.alignment = TextAlignmentOptions.Left;
        descText.color = new Color(0.7f, 0.7f, 0.7f);

        // Set references
        SerializedObject serialized = new SerializedObject(actionBtn);
        serialized.FindProperty("button").objectReferenceValue = btn;
        serialized.FindProperty("nameText").objectReferenceValue = nameText;
        serialized.FindProperty("descriptionText").objectReferenceValue = descText;
        serialized.ApplyModifiedProperties();

        // Save prefab
        string prefabFile = path + "/ActionButton.prefab";
        PrefabUtility.SaveAsPrefabAsset(btnObj, prefabFile);
        Object.DestroyImmediate(btnObj);

        // Link to ActionMenuUI if exists
        var actionMenuUI = Object.FindFirstObjectByType<ActionMenuUI>();
        if (actionMenuUI != null)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<ActionButton>(prefabFile);
            SerializedObject menuSerialized = new SerializedObject(actionMenuUI);
            menuSerialized.FindProperty("actionButtonPrefab").objectReferenceValue = prefab;
            menuSerialized.ApplyModifiedProperties();
        }
    }

    [MenuItem("Tools/KAM Combat Setup/Fix Combat UI References", false, 25)]
    public static void FixCombatUIReferences()
    {
        // Find TurnOrderUI and assign prefab
        var turnOrderUI = Object.FindFirstObjectByType<TurnOrderUI>();
        if (turnOrderUI != null)
        {
            // Load prefab as GameObject first, then get the component
            var prefabGO = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Combat/TurnPortraitSlot.prefab");
            if (prefabGO != null)
            {
                var portraitPrefab = prefabGO.GetComponent<TurnPortraitSlot>();
                if (portraitPrefab != null)
                {
                    SerializedObject serialized = new SerializedObject(turnOrderUI);
                    serialized.FindProperty("portraitSlotPrefab").objectReferenceValue = portraitPrefab;
                    serialized.ApplyModifiedProperties();
                    EditorUtility.SetDirty(turnOrderUI);
                    Debug.Log("<color=green>[Combat UI Setup] Assigned TurnPortraitSlot prefab to TurnOrderUI</color>");
                }
                else
                {
                    Debug.LogWarning("[Combat UI Setup] TurnPortraitSlot component not found on prefab!");
                }
            }
            else
            {
                Debug.LogWarning("[Combat UI Setup] TurnPortraitSlot.prefab not found. Run 'Create Combat UI Prefabs' first.");
            }
        }
        else
        {
            Debug.LogWarning("[Combat UI Setup] No TurnOrderUI found in scene.");
        }

        // Find ActionMenuUI and assign button prefab + fix button references
        var actionMenuUI = Object.FindFirstObjectByType<ActionMenuUI>();
        if (actionMenuUI != null)
        {
            var buttonPrefab = AssetDatabase.LoadAssetAtPath<ActionButton>("Assets/Prefabs/Combat/ActionButton.prefab");
            SerializedObject serialized = new SerializedObject(actionMenuUI);

            if (buttonPrefab != null)
            {
                serialized.FindProperty("actionButtonPrefab").objectReferenceValue = buttonPrefab;
                Debug.Log("<color=green>[Combat UI Setup] Assigned ActionButton prefab to ActionMenuUI</color>");
            }

            // Find and assign main action buttons
            Transform menuRoot = actionMenuUI.transform.Find("MenuRoot");
            if (menuRoot != null)
            {
                var attackBtn = menuRoot.Find("AttackButton")?.GetComponent<Button>();
                var skillsBtn = menuRoot.Find("SkillsButton")?.GetComponent<Button>();
                var itemsBtn = menuRoot.Find("ItemsButton")?.GetComponent<Button>();
                var defendBtn = menuRoot.Find("DefendButton")?.GetComponent<Button>();

                if (attackBtn != null)
                {
                    serialized.FindProperty("attackButton").objectReferenceValue = attackBtn;
                    Debug.Log("<color=green>[Combat UI Setup] Found and assigned Attack button</color>");
                }
                if (skillsBtn != null)
                {
                    serialized.FindProperty("skillsButton").objectReferenceValue = skillsBtn;
                    Debug.Log("<color=green>[Combat UI Setup] Found and assigned Skills button</color>");
                }
                if (itemsBtn != null)
                {
                    serialized.FindProperty("itemsButton").objectReferenceValue = itemsBtn;
                    Debug.Log("<color=green>[Combat UI Setup] Found and assigned Items button</color>");
                }
                if (defendBtn != null)
                {
                    serialized.FindProperty("defendButton").objectReferenceValue = defendBtn;
                    Debug.Log("<color=green>[Combat UI Setup] Found and assigned Defend button</color>");
                }
            }
            else
            {
                Debug.LogWarning("[Combat UI Setup] MenuRoot not found under ActionMenuUI");
            }

            serialized.ApplyModifiedProperties();
        }

        // Find CombatUIController and assign references
        var uiController = Object.FindFirstObjectByType<CombatUIController>();
        if (uiController != null)
        {
            SerializedObject serialized = new SerializedObject(uiController);

            var popupPrefab = AssetDatabase.LoadAssetAtPath<DamagePopup>("Assets/Prefabs/Combat/DamagePopup.prefab");
            if (popupPrefab != null)
            {
                serialized.FindProperty("damagePopupPrefab").objectReferenceValue = popupPrefab;
                Debug.Log("<color=green>[Combat UI Setup] Assigned DamagePopup prefab to CombatUIController</color>");
            }

            // Also assign ActionMenuUI reference
            if (actionMenuUI != null)
            {
                serialized.FindProperty("actionMenuUI").objectReferenceValue = actionMenuUI;
                Debug.Log("<color=green>[Combat UI Setup] Assigned ActionMenuUI to CombatUIController</color>");
            }

            serialized.ApplyModifiedProperties();
        }

        Debug.Log("<color=green>[Combat UI Setup] Reference fix complete!</color>");
    }

    [MenuItem("Tools/KAM Combat Setup/Clean Duplicate Combat UIs", false, 26)]
    public static void CleanDuplicateCombatUIs()
    {
        var allHUDManagers = Object.FindObjectsByType<CombatHUDManager>(FindObjectsSortMode.None);

        if (allHUDManagers.Length <= 1)
        {
            Debug.Log("[Combat UI Setup] No duplicates found.");
            return;
        }

        // Keep the first one, destroy the rest
        for (int i = 1; i < allHUDManagers.Length; i++)
        {
            Debug.Log($"[Combat UI Setup] Removing duplicate: {allHUDManagers[i].gameObject.name}");
            Undo.DestroyObjectImmediate(allHUDManagers[i].gameObject);
        }

        Debug.Log($"<color=green>[Combat UI Setup] Removed {allHUDManagers.Length - 1} duplicate Combat UI(s)</color>");
    }

    [MenuItem("Tools/KAM Combat Setup/Add Health Bar to Selected Unit", false, 30)]
    public static void AddHealthBarToSelected()
    {
        GameObject selected = Selection.activeGameObject;
        if (selected == null)
        {
            EditorUtility.DisplayDialog("No Selection", "Please select a combat unit in the scene.", "OK");
            return;
        }

        // Load health bar prefab
        var prefab = AssetDatabase.LoadAssetAtPath<HealthBarUI>("Assets/Prefabs/Combat/HealthBarUI.prefab");
        if (prefab == null)
        {
            EditorUtility.DisplayDialog("Prefab Not Found",
                "HealthBarUI prefab not found. Run 'Create Combat UI Prefabs' first.", "OK");
            return;
        }

        // Instantiate as child
        var healthBar = (HealthBarUI)PrefabUtility.InstantiatePrefab(prefab);
        healthBar.transform.SetParent(selected.transform);
        healthBar.transform.localPosition = new Vector3(0, 2.5f, 0);

        Undo.RegisterCreatedObjectUndo(healthBar.gameObject, "Add Health Bar");
        Selection.activeGameObject = healthBar.gameObject;

        Debug.Log($"<color=green>[Combat UI Setup] Added Health Bar to {selected.name}</color>");
    }

    #region Timed Hit / QTE Setup

    [MenuItem("Tools/KAM Combat Setup/Setup Timed Hit (QTE) System", false, 40)]
    public static void SetupTimedHitSystem()
    {
        if (EditorUtility.DisplayDialog("Setup Timed Hit System",
            "This will create:\n" +
            "• TimedHit Manager (Controller, Input, Bridge)\n" +
            "• TimedHitConfig ScriptableObject\n" +
            "• Rune Display UI\n\n" +
            "This is required for the QTE (Quick Time Event) system to work.\n\n" +
            "Continue?", "Create", "Cancel"))
        {
            CreateTimedHitManager();
            CreateTimedHitConfig();
            CreateRuneDisplayUI();

            Debug.Log("<color=green>[Combat UI Setup] Timed Hit (QTE) System setup complete!</color>");
            Debug.Log("<color=yellow>[Combat UI Setup] Make sure QTE is enabled in CombatUIController settings.</color>");
        }
    }

    [MenuItem("Tools/KAM Combat Setup/Create Timed Hit Manager Only", false, 41)]
    public static void CreateTimedHitManager()
    {
        // Check if already exists
        var existing = Object.FindFirstObjectByType<TimedHitController>();
        if (existing != null)
        {
            Debug.LogWarning("[Combat UI Setup] TimedHitController already exists in scene!");
            Selection.activeGameObject = existing.gameObject;
            return;
        }

        // Create TimedHit Manager GameObject
        GameObject timedHitObj = new GameObject("TimedHitManager");

        // Add components
        TimedHitController controller = timedHitObj.AddComponent<TimedHitController>();
        TimedHitInput input = timedHitObj.AddComponent<TimedHitInput>();
        TimedHitCombatBridge bridge = timedHitObj.AddComponent<TimedHitCombatBridge>();

        // Try to load existing config
        var config = AssetDatabase.LoadAssetAtPath<TimedHitConfig>("Assets/ScriptableObjects/TimedHitConfig.asset");
        if (config != null)
        {
            SerializedObject serializedController = new SerializedObject(controller);
            serializedController.FindProperty("config").objectReferenceValue = config;
            serializedController.ApplyModifiedProperties();
        }

        // Set references on bridge
        SerializedObject serializedBridge = new SerializedObject(bridge);
        serializedBridge.FindProperty("controller").objectReferenceValue = controller;
        serializedBridge.FindProperty("input").objectReferenceValue = input;
        serializedBridge.ApplyModifiedProperties();

        // Set controller reference on input
        SerializedObject serializedInput = new SerializedObject(input);
        serializedInput.FindProperty("controller").objectReferenceValue = controller;
        serializedInput.ApplyModifiedProperties();

        // Enable debug mode for testing
        SerializedObject serializedCtrl = new SerializedObject(controller);
        serializedCtrl.FindProperty("debugMode").boolValue = true;
        serializedCtrl.ApplyModifiedProperties();

        Undo.RegisterCreatedObjectUndo(timedHitObj, "Create Timed Hit Manager");
        Selection.activeGameObject = timedHitObj;

        Debug.Log("<color=green>[Combat UI Setup] Created TimedHitManager with Controller, Input, and Bridge!</color>");
    }

    [MenuItem("Tools/KAM Combat Setup/Create Timed Hit Config", false, 42)]
    public static void CreateTimedHitConfig()
    {
        // Create ScriptableObjects folder if needed
        string soPath = "Assets/ScriptableObjects";
        if (!AssetDatabase.IsValidFolder(soPath))
        {
            AssetDatabase.CreateFolder("Assets", "ScriptableObjects");
        }

        string configPath = soPath + "/TimedHitConfig.asset";

        // Check if already exists
        var existing = AssetDatabase.LoadAssetAtPath<TimedHitConfig>(configPath);
        if (existing != null)
        {
            Debug.Log("[Combat UI Setup] TimedHitConfig already exists.");
            Selection.activeObject = existing;

            // Try to assign to controller
            AssignConfigToController(existing);
            return;
        }

        // Create new config
        TimedHitConfig config = ScriptableObject.CreateInstance<TimedHitConfig>();
        AssetDatabase.CreateAsset(config, configPath);
        AssetDatabase.SaveAssets();

        Selection.activeObject = config;

        // Assign to controller
        AssignConfigToController(config);

        Debug.Log("<color=green>[Combat UI Setup] Created TimedHitConfig at " + configPath + "</color>");
    }

    private static void AssignConfigToController(TimedHitConfig config)
    {
        var controller = Object.FindFirstObjectByType<TimedHitController>();
        if (controller != null)
        {
            SerializedObject serialized = new SerializedObject(controller);
            serialized.FindProperty("config").objectReferenceValue = config;
            serialized.ApplyModifiedProperties();
            Debug.Log("<color=green>[Combat UI Setup] Assigned TimedHitConfig to TimedHitController</color>");
        }
    }

    [MenuItem("Tools/KAM Combat Setup/Create Rune Display UI", false, 43)]
    public static void CreateRuneDisplayUI()
    {
        // Check if already exists
        var existing = Object.FindFirstObjectByType<RuneDisplay>();
        if (existing != null)
        {
            Debug.LogWarning("[Combat UI Setup] RuneDisplay already exists in scene!");
            Selection.activeGameObject = existing.gameObject;
            return;
        }

        // Find Combat HUD Canvas or create parent canvas
        Canvas parentCanvas = null;
        var hudManager = Object.FindFirstObjectByType<CombatHUDManager>();
        if (hudManager != null)
        {
            parentCanvas = hudManager.GetComponent<Canvas>();
        }

        if (parentCanvas == null)
        {
            // Create a separate canvas for Rune Display
            GameObject canvasObj = new GameObject("RuneDisplay_Canvas");
            parentCanvas = canvasObj.AddComponent<Canvas>();
            parentCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            parentCanvas.sortingOrder = 200; // Above combat UI

            CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            canvasObj.AddComponent<GraphicRaycaster>();
            Undo.RegisterCreatedObjectUndo(canvasObj, "Create Rune Display Canvas");
        }

        // Create Rune Display UI
        GameObject runeDisplayObj = new GameObject("RuneDisplay");
        runeDisplayObj.transform.SetParent(parentCanvas.transform, false);

        RectTransform rect = runeDisplayObj.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(0, 100); // Slightly above center
        rect.sizeDelta = new Vector2(200, 200);

        CanvasGroup canvasGroup = runeDisplayObj.AddComponent<CanvasGroup>();
        canvasGroup.alpha = 0f; // Start hidden

        RuneDisplay runeDisplay = runeDisplayObj.AddComponent<RuneDisplay>();

        // Create Outer Glow/Border for visibility
        GameObject outerGlowObj = new GameObject("OuterGlow");
        outerGlowObj.transform.SetParent(runeDisplayObj.transform, false);
        RectTransform outerGlowRect = outerGlowObj.AddComponent<RectTransform>();
        outerGlowRect.anchorMin = new Vector2(0.5f, 0.5f);
        outerGlowRect.anchorMax = new Vector2(0.5f, 0.5f);
        outerGlowRect.sizeDelta = new Vector2(160, 160);
        Image outerGlowImage = outerGlowObj.AddComponent<Image>();
        outerGlowImage.color = new Color(0f, 0f, 0f, 0.8f); // Dark border

        // Create Timing Ring (behind background)
        GameObject ringObj = new GameObject("TimingRing");
        ringObj.transform.SetParent(runeDisplayObj.transform, false);
        RectTransform ringRect = ringObj.AddComponent<RectTransform>();
        ringRect.anchorMin = new Vector2(0.5f, 0.5f);
        ringRect.anchorMax = new Vector2(0.5f, 0.5f);
        ringRect.sizeDelta = new Vector2(150, 150);
        Image ringImage = ringObj.AddComponent<Image>();
        ringImage.color = new Color(1f, 0.9f, 0f, 1f); // Bright yellow
        ringImage.type = Image.Type.Filled;
        ringImage.fillMethod = Image.FillMethod.Radial360;
        ringImage.fillOrigin = 2; // Top
        ringImage.fillClockwise = false;

        // Create Background (dark circle for contrast)
        GameObject bgObj = new GameObject("RuneBackground");
        bgObj.transform.SetParent(runeDisplayObj.transform, false);
        RectTransform bgRect = bgObj.AddComponent<RectTransform>();
        bgRect.anchorMin = new Vector2(0.5f, 0.5f);
        bgRect.anchorMax = new Vector2(0.5f, 0.5f);
        bgRect.sizeDelta = new Vector2(130, 130);
        Image bgImage = bgObj.AddComponent<Image>();
        bgImage.color = new Color(0.1f, 0.1f, 0.15f, 0.95f); // Very dark for contrast

        // Create Perfect Zone Indicator
        GameObject perfectObj = new GameObject("PerfectZone");
        perfectObj.transform.SetParent(runeDisplayObj.transform, false);
        RectTransform perfectRect = perfectObj.AddComponent<RectTransform>();
        perfectRect.anchorMin = new Vector2(0.5f, 0.5f);
        perfectRect.anchorMax = new Vector2(0.5f, 0.5f);
        perfectRect.sizeDelta = new Vector2(140, 140);
        Image perfectImage = perfectObj.AddComponent<Image>();
        perfectImage.color = new Color(0f, 1f, 1f, 0.4f); // Cyan transparent
        perfectImage.type = Image.Type.Filled;
        perfectImage.fillMethod = Image.FillMethod.Radial360;
        perfectImage.fillAmount = 0.2f; // 20% of circle
        perfectObj.SetActive(false);

        // Create Direction Icon (hidden by default, used for sprite arrows if assigned)
        GameObject iconObj = new GameObject("DirectionIcon");
        iconObj.transform.SetParent(runeDisplayObj.transform, false);
        RectTransform iconRect = iconObj.AddComponent<RectTransform>();
        iconRect.anchorMin = new Vector2(0.5f, 0.5f);
        iconRect.anchorMax = new Vector2(0.5f, 0.5f);
        iconRect.sizeDelta = new Vector2(80, 80);
        Image iconImage = iconObj.AddComponent<Image>();
        iconImage.color = new Color(1f, 1f, 1f, 0f); // Transparent - we use text instead

        // Create Direction Text (for arrow symbols) - MAIN ARROW DISPLAY
        GameObject textObj = new GameObject("DirectionText");
        textObj.transform.SetParent(runeDisplayObj.transform, false);
        RectTransform textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = new Vector2(0.5f, 0.5f);
        textRect.anchorMax = new Vector2(0.5f, 0.5f);
        textRect.sizeDelta = new Vector2(120, 120);
        TextMeshProUGUI dirText = textObj.AddComponent<TextMeshProUGUI>();
        dirText.text = "Q";  // Q=Up, Z=Left, X=Down, E=Right
        dirText.fontSize = 96; // Larger for visibility
        dirText.alignment = TextAlignmentOptions.Center;
        dirText.color = new Color(1f, 1f, 0f, 1f); // Bright yellow arrow
        dirText.fontStyle = FontStyles.Bold;
        dirText.outlineWidth = 0.2f; // Add outline for visibility
        dirText.outlineColor = Color.black;

        // Create Feedback Text
        GameObject feedbackObj = new GameObject("FeedbackText");
        feedbackObj.transform.SetParent(runeDisplayObj.transform, false);
        RectTransform feedbackRect = feedbackObj.AddComponent<RectTransform>();
        feedbackRect.anchorMin = new Vector2(0.5f, 0);
        feedbackRect.anchorMax = new Vector2(0.5f, 0);
        feedbackRect.anchoredPosition = new Vector2(0, -30);
        feedbackRect.sizeDelta = new Vector2(250, 60);
        TextMeshProUGUI feedbackText = feedbackObj.AddComponent<TextMeshProUGUI>();
        feedbackText.text = "";
        feedbackText.fontSize = 42; // Larger feedback
        feedbackText.alignment = TextAlignmentOptions.Center;
        feedbackText.color = Color.green;
        feedbackText.fontStyle = FontStyles.Bold;
        feedbackText.outlineWidth = 0.15f;
        feedbackText.outlineColor = Color.black;
        feedbackObj.SetActive(false);

        // Create "Press Arrow Key" hint text
        GameObject hintObj = new GameObject("HintText");
        hintObj.transform.SetParent(runeDisplayObj.transform, false);
        RectTransform hintRect = hintObj.AddComponent<RectTransform>();
        hintRect.anchorMin = new Vector2(0.5f, 1);
        hintRect.anchorMax = new Vector2(0.5f, 1);
        hintRect.anchoredPosition = new Vector2(0, 30);
        hintRect.sizeDelta = new Vector2(350, 40);
        TextMeshProUGUI hintText = hintObj.AddComponent<TextMeshProUGUI>();
        hintText.text = "PRESS Q / E / Z / X";
        hintText.fontSize = 28; // Larger hint
        hintText.alignment = TextAlignmentOptions.Center;
        hintText.color = Color.white;
        hintText.fontStyle = FontStyles.Bold;
        hintText.outlineWidth = 0.15f;
        hintText.outlineColor = Color.black;

        // Set references on RuneDisplay
        SerializedObject serialized = new SerializedObject(runeDisplay);
        serialized.FindProperty("canvasGroup").objectReferenceValue = canvasGroup;
        serialized.FindProperty("runeBackground").objectReferenceValue = bgImage;
        serialized.FindProperty("runeDirectionIcon").objectReferenceValue = iconImage;
        serialized.FindProperty("timingRing").objectReferenceValue = ringImage;
        serialized.FindProperty("perfectZoneIndicator").objectReferenceValue = perfectImage;
        serialized.FindProperty("directionText").objectReferenceValue = dirText;
        serialized.FindProperty("feedbackText").objectReferenceValue = feedbackText;
        serialized.FindProperty("followPlayer").boolValue = false; // Stay at screen center
        serialized.ApplyModifiedProperties();

        // Find and assign controller
        var controller = Object.FindFirstObjectByType<TimedHitController>();
        if (controller != null)
        {
            SerializedObject runeDisplaySerialized = new SerializedObject(runeDisplay);
            runeDisplaySerialized.FindProperty("controller").objectReferenceValue = controller;
            runeDisplaySerialized.ApplyModifiedProperties();
            Debug.Log("<color=green>[Combat UI Setup] Linked RuneDisplay to TimedHitController</color>");
        }

        Undo.RegisterCreatedObjectUndo(runeDisplayObj, "Create Rune Display UI");
        Selection.activeGameObject = runeDisplayObj;

        Debug.Log("<color=green>[Combat UI Setup] Created RuneDisplay UI!</color>");
    }

    [MenuItem("Tools/KAM Combat Setup/Create Action Point UI", false, 42)]
    public static void CreateActionPointUI()
    {
        var existing = Object.FindFirstObjectByType<ActionPointUI>();
        if (existing != null)
        {
            Debug.LogWarning("[Combat UI Setup] ActionPointUI already exists in scene!");
            Selection.activeGameObject = existing.transform.parent != null
                ? existing.transform.parent.gameObject
                : existing.gameObject;
            return;
        }

        ActionPointUI apUI = ActionPointUI.CreateInstance(false);
        GameObject root = apUI.transform.parent != null ? apUI.transform.parent.gameObject : apUI.gameObject;

        Undo.RegisterCreatedObjectUndo(root.transform.root.gameObject, "Create Action Point UI");
        Selection.activeGameObject = root;

        Debug.Log("<color=green>[Combat UI Setup] Created editable ActionPointUI preview!</color>");
    }

    [MenuItem("Tools/KAM Combat Setup/Fix Timed Hit References", false, 44)]
    public static void FixTimedHitReferences()
    {
        var controller = Object.FindFirstObjectByType<TimedHitController>();
        var input = Object.FindFirstObjectByType<TimedHitInput>();
        var bridge = Object.FindFirstObjectByType<TimedHitCombatBridge>();
        var runeDisplay = Object.FindFirstObjectByType<RuneDisplay>();

        int fixCount = 0;

        // Fix controller config
        if (controller != null)
        {
            var config = AssetDatabase.LoadAssetAtPath<TimedHitConfig>("Assets/ScriptableObjects/TimedHitConfig.asset");
            if (config != null)
            {
                SerializedObject serialized = new SerializedObject(controller);
                if (serialized.FindProperty("config").objectReferenceValue == null)
                {
                    serialized.FindProperty("config").objectReferenceValue = config;
                    serialized.ApplyModifiedProperties();
                    Debug.Log("<color=green>[Combat UI Setup] Assigned config to TimedHitController</color>");
                    fixCount++;
                }
            }
        }

        // Fix input reference to controller
        if (input != null && controller != null)
        {
            SerializedObject serialized = new SerializedObject(input);
            if (serialized.FindProperty("controller").objectReferenceValue == null)
            {
                serialized.FindProperty("controller").objectReferenceValue = controller;
                serialized.ApplyModifiedProperties();
                Debug.Log("<color=green>[Combat UI Setup] Assigned controller to TimedHitInput</color>");
                fixCount++;
            }
        }

        // Fix bridge references
        if (bridge != null)
        {
            SerializedObject serialized = new SerializedObject(bridge);
            if (controller != null && serialized.FindProperty("controller").objectReferenceValue == null)
            {
                serialized.FindProperty("controller").objectReferenceValue = controller;
                fixCount++;
            }
            if (input != null && serialized.FindProperty("input").objectReferenceValue == null)
            {
                serialized.FindProperty("input").objectReferenceValue = input;
                fixCount++;
            }
            serialized.ApplyModifiedProperties();
            if (fixCount > 0)
                Debug.Log("<color=green>[Combat UI Setup] Fixed TimedHitCombatBridge references</color>");
        }

        // Fix rune display reference to controller
        if (runeDisplay != null && controller != null)
        {
            SerializedObject serialized = new SerializedObject(runeDisplay);
            if (serialized.FindProperty("controller").objectReferenceValue == null)
            {
                serialized.FindProperty("controller").objectReferenceValue = controller;
                serialized.ApplyModifiedProperties();
                Debug.Log("<color=green>[Combat UI Setup] Assigned controller to RuneDisplay</color>");
                fixCount++;
            }
        }

        if (fixCount == 0)
        {
            Debug.Log("[Combat UI Setup] All Timed Hit references are already set correctly.");
        }
        else
        {
            Debug.Log($"<color=green>[Combat UI Setup] Fixed {fixCount} reference(s)</color>");
        }

        // Check for missing components
        if (controller == null)
            Debug.LogWarning("[Combat UI Setup] TimedHitController not found! Run 'Setup Timed Hit System' first.");
        if (input == null)
            Debug.LogWarning("[Combat UI Setup] TimedHitInput not found! Run 'Setup Timed Hit System' first.");
        if (bridge == null)
            Debug.LogWarning("[Combat UI Setup] TimedHitCombatBridge not found! Run 'Setup Timed Hit System' first.");
        if (runeDisplay == null)
            Debug.LogWarning("[Combat UI Setup] RuneDisplay not found! Run 'Create Rune Display UI' first.");
    }

    #endregion
}
