#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using TMPro;
using KAM.UI.SkillTree;
using KAM.Progression;

namespace KAM.Editor
{
    /// <summary>
    /// Editor helper to automatically create and setup the Skill Tree UI Canvas,
    /// SkillNodeUI prefab, and all wiring.
    /// Access via: KAM > Skill Tree > Setup Skill Tree UI
    /// </summary>
    public static class SkillTreeUISetupHelper
    {
        private const string PrefabPath = "Assets/Prefabs/SkillTree";

        // Colors matching the game's dark spirit theme
        private static readonly Color PanelBgColor = new Color(0.08f, 0.06f, 0.12f, 0.96f);
        private static readonly Color DetailBgColor = new Color(0.1f, 0.08f, 0.15f, 0.95f);
        private static readonly Color NodeBgColor = new Color(0.15f, 0.12f, 0.2f, 0.9f);
        private static readonly Color GoldColor = new Color(1f, 0.84f, 0f);
        private static readonly Color SpiritBlue = new Color(0.2f, 0.7f, 1f);
        private static readonly Color DimText = new Color(0.6f, 0.6f, 0.65f);
        private static readonly Color BranchBg = new Color(0.12f, 0.1f, 0.18f, 0.9f);
        private static readonly Color ButtonColor = new Color(0.15f, 0.4f, 0.7f);
        private static readonly Color ButtonDisabled = new Color(0.3f, 0.3f, 0.35f);

        [MenuItem("KAM/Skill Tree/Setup Full Skill Tree UI", false, 50)]
        public static void SetupFullSkillTreeUI()
        {
            if (EditorUtility.DisplayDialog("Create Skill Tree UI",
                "This will create:\n\n" +
                "- Skill Tree Canvas (in scene)\n" +
                "- Skill Tree Panel with node container\n" +
                "- Detail Panel (info, unlock, branch selection)\n" +
                "- SkillNodeUI Prefab (in Assets/Prefabs/SkillTree)\n\n" +
                "All references will be auto-wired.\n\n" +
                "Continue?", "Create", "Cancel"))
            {
                CreateSkillNodePrefab();
                CreateSkillTreeCanvas();

                Debug.Log("<color=green>[Skill Tree UI Setup] Complete! Check your scene and Assets/Prefabs/SkillTree folder.</color>");
            }
        }

        [MenuItem("KAM/Skill Tree/Create Skill Tree Canvas Only", false, 51)]
        public static void CreateSkillTreeCanvas()
        {
            // Check if already exists
            var existing = Object.FindFirstObjectByType<SkillTreeUI>();
            if (existing != null)
            {
                Debug.LogWarning("[Skill Tree UI Setup] SkillTreeUI already exists in scene!");
                Selection.activeGameObject = existing.gameObject;
                return;
            }

            // ===== CANVAS =====
            GameObject canvasObj = new GameObject("SkillTreeCanvas");
            Undo.RegisterCreatedObjectUndo(canvasObj, "Create Skill Tree UI");

            Canvas canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 20; // Above gameplay, below combat HUD

            CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            canvasObj.AddComponent<GraphicRaycaster>();

            // ===== MAIN PANEL (starts hidden) =====
            GameObject panelObj = CreateSkillTreePanel(canvasObj.transform);

            // ===== HEADER BAR =====
            GameObject titleObj = CreateTitleBar(panelObj.transform);
            TextMeshProUGUI titleTMP = titleObj.transform.Find("TitleText").GetComponent<TextMeshProUGUI>();
            TextMeshProUGUI fragmentTMP = titleObj.transform.Find("FragmentText").GetComponent<TextMeshProUGUI>();

            // ===== NODE CONTAINER (scrollable area for skill nodes) =====
            GameObject nodeContainer = CreateNodeContainer(panelObj.transform);

            // ===== DETAIL PANEL (right side) =====
            GameObject detailPanelObj = CreateDetailPanel(panelObj.transform);
            SkillDetailPanel detailComp = detailPanelObj.GetComponent<SkillDetailPanel>();

            // ===== CLOSE HINT =====
            CreateCloseHint(panelObj.transform);

            // ===== ADD SkillTreeUI COMPONENT AND WIRE =====
            SkillTreeUI skillTreeUI = canvasObj.AddComponent<SkillTreeUI>();

            // Load prefab
            SkillNodeUI nodePrefab = null;
            string prefabFile = PrefabPath + "/SkillNodeUI.prefab";
            if (System.IO.File.Exists(prefabFile))
            {
                GameObject prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(prefabFile);
                if (prefabAsset != null)
                {
                    nodePrefab = prefabAsset.GetComponent<SkillNodeUI>();
                }
            }

            SerializedObject so = new SerializedObject(skillTreeUI);
            so.FindProperty("skillTreePanel").objectReferenceValue = panelObj;
            so.FindProperty("nodeContainer").objectReferenceValue = nodeContainer.transform;
            so.FindProperty("detailPanel").objectReferenceValue = detailComp;
            so.FindProperty("spiritFragmentText").objectReferenceValue = fragmentTMP;
            so.FindProperty("titleText").objectReferenceValue = titleTMP;
            if (nodePrefab != null)
            {
                so.FindProperty("nodePrefab").objectReferenceValue = nodePrefab;
            }
            so.ApplyModifiedProperties();

            panelObj.SetActive(false); // Start hidden

            Selection.activeGameObject = canvasObj;
            Debug.Log("<color=green>[Skill Tree UI Setup] Created Skill Tree Canvas!</color>");

            if (nodePrefab == null)
            {
                Debug.LogWarning("[Skill Tree UI Setup] SkillNodeUI prefab not found. Run 'Create SkillNodeUI Prefab' first, then re-assign manually.");
            }
        }

        [MenuItem("KAM/Skill Tree/Create SkillNodeUI Prefab Only", false, 52)]
        public static void CreateSkillNodePrefab()
        {
            // Ensure directory
            EnsurePrefabFolder();

            string prefabFile = PrefabPath + "/SkillNodeUI.prefab";

            // Create the node UI GameObject
            GameObject nodeObj = new GameObject("SkillNodeUI");

            // Root button
            RectTransform nodeRect = nodeObj.AddComponent<RectTransform>();
            nodeRect.anchorMin = new Vector2(0, 0.5f);
            nodeRect.anchorMax = new Vector2(0, 0.5f);
            nodeRect.sizeDelta = new Vector2(100, 120);

            Button nodeButton = nodeObj.AddComponent<Button>();
            Image nodeBg = nodeObj.AddComponent<Image>();
            nodeBg.color = NodeBgColor;
            nodeButton.targetGraphic = nodeBg;

            // Set button colors
            ColorBlock cb = nodeButton.colors;
            cb.normalColor = NodeBgColor;
            cb.highlightedColor = new Color(0.2f, 0.17f, 0.28f, 0.95f);
            cb.pressedColor = new Color(0.25f, 0.2f, 0.35f, 1f);
            cb.selectedColor = new Color(0.2f, 0.17f, 0.28f, 0.95f);
            nodeButton.colors = cb;

            // Border image (colored ring around the node)
            GameObject borderObj = new GameObject("Border");
            borderObj.transform.SetParent(nodeObj.transform, false);

            RectTransform borderRect = borderObj.AddComponent<RectTransform>();
            borderRect.anchorMin = new Vector2(0.5f, 1f);
            borderRect.anchorMax = new Vector2(0.5f, 1f);
            borderRect.pivot = new Vector2(0.5f, 1f);
            borderRect.anchoredPosition = new Vector2(0, -5);
            borderRect.sizeDelta = new Vector2(76, 76);

            Image borderImage = borderObj.AddComponent<Image>();
            borderImage.color = Color.gray;
            borderImage.raycastTarget = false;

            // Icon image (inside the border)
            GameObject iconObj = new GameObject("Icon");
            iconObj.transform.SetParent(borderObj.transform, false);

            RectTransform iconRect = iconObj.AddComponent<RectTransform>();
            iconRect.anchorMin = Vector2.zero;
            iconRect.anchorMax = Vector2.one;
            iconRect.offsetMin = new Vector2(4, 4);
            iconRect.offsetMax = new Vector2(-4, -4);

            Image iconImage = iconObj.AddComponent<Image>();
            iconImage.color = Color.white;
            iconImage.raycastTarget = false;

            // Lock overlay (semi-transparent dark with lock visual)
            GameObject lockObj = new GameObject("LockOverlay");
            lockObj.transform.SetParent(borderObj.transform, false);

            RectTransform lockRect = lockObj.AddComponent<RectTransform>();
            lockRect.anchorMin = Vector2.zero;
            lockRect.anchorMax = Vector2.one;
            lockRect.offsetMin = Vector2.zero;
            lockRect.offsetMax = Vector2.zero;

            Image lockImage = lockObj.AddComponent<Image>();
            lockImage.color = new Color(0, 0, 0, 0.6f);
            lockImage.raycastTarget = false;

            // Lock icon text (simple "X" or lock symbol)
            GameObject lockIconText = new GameObject("LockIcon");
            lockIconText.transform.SetParent(lockObj.transform, false);

            RectTransform lockIconRect = lockIconText.AddComponent<RectTransform>();
            lockIconRect.anchorMin = Vector2.zero;
            lockIconRect.anchorMax = Vector2.one;
            lockIconRect.offsetMin = Vector2.zero;
            lockIconRect.offsetMax = Vector2.zero;

            TextMeshProUGUI lockTMP = lockIconText.AddComponent<TextMeshProUGUI>();
            lockTMP.text = "<size=24><b>X</b></size>";
            lockTMP.alignment = TextAlignmentOptions.Center;
            lockTMP.color = new Color(1f, 0.3f, 0.3f, 0.8f);
            lockTMP.raycastTarget = false;

            // Name text (below the icon)
            GameObject nameObj = new GameObject("NameText");
            nameObj.transform.SetParent(nodeObj.transform, false);

            RectTransform nameRect = nameObj.AddComponent<RectTransform>();
            nameRect.anchorMin = new Vector2(0, 0);
            nameRect.anchorMax = new Vector2(1, 0);
            nameRect.pivot = new Vector2(0.5f, 0);
            nameRect.anchoredPosition = new Vector2(0, 2);
            nameRect.sizeDelta = new Vector2(0, 30);

            TextMeshProUGUI nameTMP = nameObj.AddComponent<TextMeshProUGUI>();
            nameTMP.text = "Skill Name";
            nameTMP.fontSize = 11;
            nameTMP.alignment = TextAlignmentOptions.Center;
            nameTMP.color = Color.white;
            nameTMP.enableWordWrapping = true;
            nameTMP.overflowMode = TextOverflowModes.Truncate;
            nameTMP.raycastTarget = false;

            // Add SkillNodeUI component and wire references
            SkillNodeUI nodeUI = nodeObj.AddComponent<SkillNodeUI>();
            SerializedObject so = new SerializedObject(nodeUI);
            so.FindProperty("iconImage").objectReferenceValue = iconImage;
            so.FindProperty("borderImage").objectReferenceValue = borderImage;
            so.FindProperty("lockOverlay").objectReferenceValue = lockImage;
            so.FindProperty("nameText").objectReferenceValue = nameTMP;
            so.FindProperty("button").objectReferenceValue = nodeButton;
            so.ApplyModifiedProperties();

            // Save as prefab
            PrefabUtility.SaveAsPrefabAsset(nodeObj, prefabFile);
            Object.DestroyImmediate(nodeObj);

            Debug.Log($"<color=green>[Skill Tree UI Setup] Created SkillNodeUI prefab at {prefabFile}</color>");
        }

        #region Panel Creation Helpers

        private static GameObject CreateSkillTreePanel(Transform parent)
        {
            GameObject panel = new GameObject("SkillTreePanel");
            panel.transform.SetParent(parent, false);

            RectTransform rect = panel.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(40, 40);
            rect.offsetMax = new Vector2(-40, -40);

            Image bg = panel.AddComponent<Image>();
            bg.color = PanelBgColor;
            bg.raycastTarget = true; // Block clicks behind panel

            return panel;
        }

        private static GameObject CreateTitleBar(Transform parent)
        {
            GameObject titleBar = new GameObject("TitleBar");
            titleBar.transform.SetParent(parent, false);

            RectTransform barRect = titleBar.AddComponent<RectTransform>();
            barRect.anchorMin = new Vector2(0, 1);
            barRect.anchorMax = new Vector2(1, 1);
            barRect.pivot = new Vector2(0.5f, 1);
            barRect.anchoredPosition = Vector2.zero;
            barRect.sizeDelta = new Vector2(0, 60);

            Image barBg = titleBar.AddComponent<Image>();
            barBg.color = new Color(0.05f, 0.04f, 0.08f, 0.9f);
            barBg.raycastTarget = false;

            // Title text (left side)
            GameObject titleTextObj = new GameObject("TitleText");
            titleTextObj.transform.SetParent(titleBar.transform, false);

            RectTransform titleRect = titleTextObj.AddComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0, 0);
            titleRect.anchorMax = new Vector2(0.5f, 1);
            titleRect.offsetMin = new Vector2(20, 0);
            titleRect.offsetMax = Vector2.zero;

            TextMeshProUGUI titleTMP = titleTextObj.AddComponent<TextMeshProUGUI>();
            titleTMP.text = "Kam's Skill Tree";
            titleTMP.fontSize = 28;
            titleTMP.fontStyle = FontStyles.Bold;
            titleTMP.alignment = TextAlignmentOptions.MidlineLeft;
            titleTMP.color = GoldColor;
            titleTMP.raycastTarget = false;

            // Spirit fragments counter (right side)
            GameObject fragObj = new GameObject("FragmentText");
            fragObj.transform.SetParent(titleBar.transform, false);

            RectTransform fragRect = fragObj.AddComponent<RectTransform>();
            fragRect.anchorMin = new Vector2(0.5f, 0);
            fragRect.anchorMax = new Vector2(1, 1);
            fragRect.offsetMin = Vector2.zero;
            fragRect.offsetMax = new Vector2(-20, 0);

            TextMeshProUGUI fragTMP = fragObj.AddComponent<TextMeshProUGUI>();
            fragTMP.text = "Spirit Fragments: 0";
            fragTMP.fontSize = 20;
            fragTMP.alignment = TextAlignmentOptions.MidlineRight;
            fragTMP.color = SpiritBlue;
            fragTMP.raycastTarget = false;

            return titleBar;
        }

        private static GameObject CreateNodeContainer(Transform parent)
        {
            // Scroll view for nodes
            GameObject scrollObj = new GameObject("NodeScrollView");
            scrollObj.transform.SetParent(parent, false);

            RectTransform scrollRect = scrollObj.AddComponent<RectTransform>();
            scrollRect.anchorMin = new Vector2(0, 0);
            scrollRect.anchorMax = new Vector2(0.6f, 1); // Left 60% for nodes
            scrollRect.offsetMin = new Vector2(15, 15);
            scrollRect.offsetMax = new Vector2(0, -65); // Below title bar

            ScrollRect scroll = scrollObj.AddComponent<ScrollRect>();
            scroll.horizontal = true;
            scroll.vertical = true;

            Image scrollBg = scrollObj.AddComponent<Image>();
            scrollBg.color = new Color(0, 0, 0, 0.15f);
            scrollBg.raycastTarget = true;

            // RectMask2D clips content outside the viewport and ensures raycasts
            // only hit nodes that are actually visible within the scroll area.
            scrollObj.AddComponent<RectMask2D>();

            scroll.viewport = scrollRect;

            // Content (node container)
            GameObject contentObj = new GameObject("NodeContainer");
            contentObj.transform.SetParent(scrollObj.transform, false);

            RectTransform contentRect = contentObj.AddComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0, 0);
            contentRect.anchorMax = new Vector2(0, 1);
            contentRect.pivot = new Vector2(0, 0.5f);
            contentRect.anchoredPosition = Vector2.zero;
            // Large enough to hold the tree (5 tiers × ~200px spacing + padding)
            contentRect.sizeDelta = new Vector2(1200, 600);

            scroll.content = contentRect;

            // Tier labels
            string[] tierNames = { "Tier 1", "Tier 2", "Tier 3", "Tier 4", "Tier 5" };
            for (int i = 0; i < tierNames.Length; i++)
            {
                GameObject tierLabel = new GameObject($"TierLabel_{i}");
                tierLabel.transform.SetParent(contentObj.transform, false);

                RectTransform tierRect = tierLabel.AddComponent<RectTransform>();
                tierRect.anchorMin = new Vector2(0, 1);
                tierRect.anchorMax = new Vector2(0, 1);
                tierRect.pivot = new Vector2(0, 1);
                tierRect.anchoredPosition = new Vector2(20 + i * 220, -5);
                tierRect.sizeDelta = new Vector2(180, 25);

                TextMeshProUGUI tierTMP = tierLabel.AddComponent<TextMeshProUGUI>();
                tierTMP.text = tierNames[i];
                tierTMP.fontSize = 14;
                tierTMP.fontStyle = FontStyles.Bold;
                tierTMP.alignment = TextAlignmentOptions.Center;
                tierTMP.color = DimText;
                tierTMP.raycastTarget = false;
            }

            return contentObj;
        }

        private static GameObject CreateDetailPanel(Transform parent)
        {
            GameObject detailObj = new GameObject("DetailPanel");
            detailObj.transform.SetParent(parent, false);

            RectTransform detailRect = detailObj.AddComponent<RectTransform>();
            detailRect.anchorMin = new Vector2(0.6f, 0);
            detailRect.anchorMax = new Vector2(1, 1);
            detailRect.offsetMin = new Vector2(5, 15);
            detailRect.offsetMax = new Vector2(-15, -65);

            Image detailBg = detailObj.AddComponent<Image>();
            detailBg.color = DetailBgColor;
            detailBg.raycastTarget = false;

            // ----- Skill Icon -----
            GameObject iconObj = new GameObject("SkillIcon");
            iconObj.transform.SetParent(detailObj.transform, false);

            RectTransform iconRect = iconObj.AddComponent<RectTransform>();
            iconRect.anchorMin = new Vector2(0.5f, 1);
            iconRect.anchorMax = new Vector2(0.5f, 1);
            iconRect.pivot = new Vector2(0.5f, 1);
            iconRect.anchoredPosition = new Vector2(0, -15);
            iconRect.sizeDelta = new Vector2(80, 80);

            Image skillIcon = iconObj.AddComponent<Image>();
            skillIcon.color = Color.white;
            skillIcon.raycastTarget = false;

            // ----- Skill Name -----
            GameObject nameObj = new GameObject("SkillNameText");
            nameObj.transform.SetParent(detailObj.transform, false);

            RectTransform nameRect = nameObj.AddComponent<RectTransform>();
            nameRect.anchorMin = new Vector2(0, 1);
            nameRect.anchorMax = new Vector2(1, 1);
            nameRect.pivot = new Vector2(0.5f, 1);
            nameRect.anchoredPosition = new Vector2(0, -100);
            nameRect.sizeDelta = new Vector2(-20, 35);

            TextMeshProUGUI nameTMP = nameObj.AddComponent<TextMeshProUGUI>();
            nameTMP.text = "Skill Name";
            nameTMP.fontSize = 22;
            nameTMP.fontStyle = FontStyles.Bold;
            nameTMP.alignment = TextAlignmentOptions.Center;
            nameTMP.color = GoldColor;
            nameTMP.raycastTarget = false;

            // ----- Description -----
            GameObject descObj = new GameObject("DescriptionText");
            descObj.transform.SetParent(detailObj.transform, false);

            RectTransform descRect = descObj.AddComponent<RectTransform>();
            descRect.anchorMin = new Vector2(0, 1);
            descRect.anchorMax = new Vector2(1, 1);
            descRect.pivot = new Vector2(0.5f, 1);
            descRect.anchoredPosition = new Vector2(0, -140);
            descRect.sizeDelta = new Vector2(-30, 60);

            TextMeshProUGUI descTMP = descObj.AddComponent<TextMeshProUGUI>();
            descTMP.text = "Skill description goes here.";
            descTMP.fontSize = 14;
            descTMP.alignment = TextAlignmentOptions.Center;
            descTMP.color = Color.white;
            descTMP.enableWordWrapping = true;
            descTMP.raycastTarget = false;

            // ----- Cost -----
            GameObject costObj = new GameObject("CostText");
            costObj.transform.SetParent(detailObj.transform, false);

            RectTransform costRect = costObj.AddComponent<RectTransform>();
            costRect.anchorMin = new Vector2(0, 1);
            costRect.anchorMax = new Vector2(1, 1);
            costRect.pivot = new Vector2(0.5f, 1);
            costRect.anchoredPosition = new Vector2(0, -205);
            costRect.sizeDelta = new Vector2(-20, 25);

            TextMeshProUGUI costTMP = costObj.AddComponent<TextMeshProUGUI>();
            costTMP.text = "Cost: 50 Spirit Fragments";
            costTMP.fontSize = 16;
            costTMP.alignment = TextAlignmentOptions.Center;
            costTMP.color = SpiritBlue;
            costTMP.raycastTarget = false;

            // ----- Prerequisites -----
            GameObject prereqObj = new GameObject("PrerequisitesText");
            prereqObj.transform.SetParent(detailObj.transform, false);

            RectTransform prereqRect = prereqObj.AddComponent<RectTransform>();
            prereqRect.anchorMin = new Vector2(0, 1);
            prereqRect.anchorMax = new Vector2(1, 1);
            prereqRect.pivot = new Vector2(0.5f, 1);
            prereqRect.anchoredPosition = new Vector2(0, -232);
            prereqRect.sizeDelta = new Vector2(-20, 25);

            TextMeshProUGUI prereqTMP = prereqObj.AddComponent<TextMeshProUGUI>();
            prereqTMP.text = "No prerequisites";
            prereqTMP.fontSize = 13;
            prereqTMP.alignment = TextAlignmentOptions.Center;
            prereqTMP.color = DimText;
            prereqTMP.enableAutoSizing = false;
            prereqTMP.richText = true;
            prereqTMP.raycastTarget = false;

            // ----- Unlock Button -----
            GameObject unlockBtnObj = new GameObject("UnlockButton");
            unlockBtnObj.transform.SetParent(detailObj.transform, false);

            RectTransform unlockRect = unlockBtnObj.AddComponent<RectTransform>();
            unlockRect.anchorMin = new Vector2(0.5f, 1);
            unlockRect.anchorMax = new Vector2(0.5f, 1);
            unlockRect.pivot = new Vector2(0.5f, 1);
            unlockRect.anchoredPosition = new Vector2(0, -268);
            unlockRect.sizeDelta = new Vector2(200, 45);

            Image unlockBg = unlockBtnObj.AddComponent<Image>();
            unlockBg.color = ButtonColor;

            Button unlockButton = unlockBtnObj.AddComponent<Button>();
            unlockButton.targetGraphic = unlockBg;
            ColorBlock ucb = unlockButton.colors;
            ucb.normalColor = ButtonColor;
            ucb.highlightedColor = new Color(0.2f, 0.5f, 0.85f);
            ucb.pressedColor = new Color(0.1f, 0.3f, 0.6f);
            ucb.disabledColor = ButtonDisabled;
            unlockButton.colors = ucb;

            // Button text
            GameObject unlockTextObj = new GameObject("Text");
            unlockTextObj.transform.SetParent(unlockBtnObj.transform, false);

            RectTransform unlockTextRect = unlockTextObj.AddComponent<RectTransform>();
            unlockTextRect.anchorMin = Vector2.zero;
            unlockTextRect.anchorMax = Vector2.one;
            unlockTextRect.offsetMin = Vector2.zero;
            unlockTextRect.offsetMax = Vector2.zero;

            TextMeshProUGUI unlockBtnTMP = unlockTextObj.AddComponent<TextMeshProUGUI>();
            unlockBtnTMP.text = "Unlock";
            unlockBtnTMP.fontSize = 20;
            unlockBtnTMP.fontStyle = FontStyles.Bold;
            unlockBtnTMP.alignment = TextAlignmentOptions.Center;
            unlockBtnTMP.color = Color.white;
            unlockBtnTMP.raycastTarget = false;

            // ----- Unlocked Indicator (hidden by default) -----
            GameObject unlockedObj = new GameObject("UnlockedIndicator");
            unlockedObj.transform.SetParent(detailObj.transform, false);

            RectTransform unlockedRect = unlockedObj.AddComponent<RectTransform>();
            unlockedRect.anchorMin = new Vector2(0.5f, 1);
            unlockedRect.anchorMax = new Vector2(0.5f, 1);
            unlockedRect.pivot = new Vector2(0.5f, 1);
            unlockedRect.anchoredPosition = new Vector2(0, -268);
            unlockedRect.sizeDelta = new Vector2(200, 45);

            TextMeshProUGUI unlockedTMP = unlockedObj.AddComponent<TextMeshProUGUI>();
            unlockedTMP.text = "UNLOCKED";
            unlockedTMP.fontSize = 22;
            unlockedTMP.fontStyle = FontStyles.Bold;
            unlockedTMP.alignment = TextAlignmentOptions.Center;
            unlockedTMP.color = GoldColor;
            unlockedTMP.raycastTarget = false;
            unlockedObj.SetActive(false);

            // ----- Branch Container (hidden by default) -----
            GameObject branchContainer = CreateBranchContainer(detailObj.transform);

            // ===== ADD SkillDetailPanel COMPONENT AND WIRE =====
            SkillDetailPanel detailComp = detailObj.AddComponent<SkillDetailPanel>();

            SerializedObject so = new SerializedObject(detailComp);
            so.FindProperty("skillNameText").objectReferenceValue = nameTMP;
            so.FindProperty("descriptionText").objectReferenceValue = descTMP;
            so.FindProperty("costText").objectReferenceValue = costTMP;
            so.FindProperty("prerequisitesText").objectReferenceValue = prereqTMP;
            so.FindProperty("skillIcon").objectReferenceValue = skillIcon;
            so.FindProperty("unlockButton").objectReferenceValue = unlockButton;
            so.FindProperty("unlockButtonText").objectReferenceValue = unlockBtnTMP;
            so.FindProperty("unlockedIndicator").objectReferenceValue = unlockedObj;
            so.FindProperty("branchContainer").objectReferenceValue = branchContainer;

            // Wire branch buttons
            Button branchABtn = branchContainer.transform.Find("BranchA").GetComponent<Button>();
            Button branchBBtn = branchContainer.transform.Find("BranchB").GetComponent<Button>();
            so.FindProperty("branchAButton").objectReferenceValue = branchABtn;
            so.FindProperty("branchBButton").objectReferenceValue = branchBBtn;

            so.FindProperty("branchANameText").objectReferenceValue =
                branchContainer.transform.Find("BranchA/BranchAName").GetComponent<TextMeshProUGUI>();
            so.FindProperty("branchADescText").objectReferenceValue =
                branchContainer.transform.Find("BranchA/BranchADesc").GetComponent<TextMeshProUGUI>();
            so.FindProperty("branchBNameText").objectReferenceValue =
                branchContainer.transform.Find("BranchB/BranchBName").GetComponent<TextMeshProUGUI>();
            so.FindProperty("branchBDescText").objectReferenceValue =
                branchContainer.transform.Find("BranchB/BranchBDesc").GetComponent<TextMeshProUGUI>();

            so.FindProperty("branchAHighlight").objectReferenceValue =
                branchContainer.transform.Find("BranchA").GetComponent<Image>();
            so.FindProperty("branchBHighlight").objectReferenceValue =
                branchContainer.transform.Find("BranchB").GetComponent<Image>();

            so.ApplyModifiedProperties();

            detailObj.SetActive(false); // Hidden until a node is selected

            return detailObj;
        }

        private static GameObject CreateBranchContainer(Transform parent)
        {
            GameObject container = new GameObject("BranchContainer");
            container.transform.SetParent(parent, false);

            RectTransform containerRect = container.AddComponent<RectTransform>();
            containerRect.anchorMin = new Vector2(0, 0);
            containerRect.anchorMax = new Vector2(1, 1);
            containerRect.offsetMin = new Vector2(10, 15);
            containerRect.offsetMax = new Vector2(-10, -325);

            // ----- Branch A (top half) -----
            GameObject branchA = CreateBranchButton(container.transform, "BranchA", "BranchAName", "BranchADesc",
                "Branch A", "Choose this path", new Vector2(0, 0.52f));

            // ----- Branch B (bottom half) -----
            GameObject branchB = CreateBranchButton(container.transform, "BranchB", "BranchBName", "BranchBDesc",
                "Branch B", "Choose this path", new Vector2(0, 0.03f));

            // "Choose a path:" label
            GameObject labelObj = new GameObject("ChooseLabel");
            labelObj.transform.SetParent(container.transform, false);
            labelObj.transform.SetAsFirstSibling();

            RectTransform labelRect = labelObj.AddComponent<RectTransform>();
            labelRect.anchorMin = new Vector2(0, 1);
            labelRect.anchorMax = new Vector2(1, 1);
            labelRect.pivot = new Vector2(0.5f, 1);
            labelRect.anchoredPosition = new Vector2(0, 5);
            labelRect.sizeDelta = new Vector2(0, 25);

            TextMeshProUGUI labelTMP = labelObj.AddComponent<TextMeshProUGUI>();
            labelTMP.text = "Choose a path:";
            labelTMP.fontSize = 15;
            labelTMP.fontStyle = FontStyles.Italic;
            labelTMP.alignment = TextAlignmentOptions.Center;
            labelTMP.color = GoldColor;
            labelTMP.raycastTarget = false;

            container.SetActive(false); // Hidden until skill is unlocked + has branches

            return container;
        }

        private static GameObject CreateBranchButton(Transform parent, string objName,
            string nameChildName, string descChildName,
            string defaultName, string defaultDesc, Vector2 anchorMinY)
        {
            GameObject branchObj = new GameObject(objName);
            branchObj.transform.SetParent(parent, false);

            RectTransform branchRect = branchObj.AddComponent<RectTransform>();
            branchRect.anchorMin = new Vector2(0, anchorMinY.y);
            branchRect.anchorMax = new Vector2(1, anchorMinY.y + 0.47f);
            branchRect.offsetMin = new Vector2(0, 2);
            branchRect.offsetMax = new Vector2(0, -2);

            Image branchBg = branchObj.AddComponent<Image>();
            branchBg.color = BranchBg;

            Button branchButton = branchObj.AddComponent<Button>();
            branchButton.targetGraphic = branchBg;
            ColorBlock bcb = branchButton.colors;
            bcb.normalColor = BranchBg;
            bcb.highlightedColor = new Color(0.18f, 0.15f, 0.25f, 0.95f);
            bcb.pressedColor = new Color(0.22f, 0.18f, 0.3f, 1f);
            branchButton.colors = bcb;

            // Branch name
            GameObject nameObj = new GameObject(nameChildName);
            nameObj.transform.SetParent(branchObj.transform, false);

            RectTransform nameRect = nameObj.AddComponent<RectTransform>();
            nameRect.anchorMin = new Vector2(0, 0.5f);
            nameRect.anchorMax = new Vector2(1, 1);
            nameRect.offsetMin = new Vector2(10, 0);
            nameRect.offsetMax = new Vector2(-10, -5);

            TextMeshProUGUI nameTMP = nameObj.AddComponent<TextMeshProUGUI>();
            nameTMP.text = defaultName;
            nameTMP.fontSize = 16;
            nameTMP.fontStyle = FontStyles.Bold;
            nameTMP.alignment = TextAlignmentOptions.MidlineLeft;
            nameTMP.color = Color.white;
            nameTMP.raycastTarget = false;

            // Branch description
            GameObject descObj = new GameObject(descChildName);
            descObj.transform.SetParent(branchObj.transform, false);

            RectTransform descRect = descObj.AddComponent<RectTransform>();
            descRect.anchorMin = new Vector2(0, 0);
            descRect.anchorMax = new Vector2(1, 0.5f);
            descRect.offsetMin = new Vector2(10, 5);
            descRect.offsetMax = new Vector2(-10, 0);

            TextMeshProUGUI descTMP = descObj.AddComponent<TextMeshProUGUI>();
            descTMP.text = defaultDesc;
            descTMP.fontSize = 12;
            descTMP.alignment = TextAlignmentOptions.MidlineLeft;
            descTMP.color = DimText;
            descTMP.enableWordWrapping = true;
            descTMP.raycastTarget = false;

            return branchObj;
        }

        private static void CreateCloseHint(Transform parent)
        {
            GameObject hintObj = new GameObject("CloseHint");
            hintObj.transform.SetParent(parent, false);

            RectTransform hintRect = hintObj.AddComponent<RectTransform>();
            hintRect.anchorMin = new Vector2(1, 0);
            hintRect.anchorMax = new Vector2(1, 0);
            hintRect.pivot = new Vector2(1, 0);
            hintRect.anchoredPosition = new Vector2(-20, 18);
            hintRect.sizeDelta = new Vector2(200, 25);

            TextMeshProUGUI hintTMP = hintObj.AddComponent<TextMeshProUGUI>();
            hintTMP.text = "[T] or [ESC] Close";
            hintTMP.fontSize = 14;
            hintTMP.alignment = TextAlignmentOptions.Right;
            hintTMP.color = DimText;
            hintTMP.raycastTarget = false;
        }

        #endregion

        #region Utilities

        private static void EnsurePrefabFolder()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Prefabs"))
                AssetDatabase.CreateFolder("Assets", "Prefabs");
            if (!AssetDatabase.IsValidFolder(PrefabPath))
                AssetDatabase.CreateFolder("Assets/Prefabs", "SkillTree");
        }

        #endregion
    }
}
#endif
