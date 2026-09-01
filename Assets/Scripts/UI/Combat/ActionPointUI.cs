using UnityEngine;
using UnityEngine.UI;
using TMPro;
using KAM.Core.Combat;
using KAM.Core.Stats;
using KAM.Combat.TurnSystem;

namespace KAM.UI.Combat
{
    /// <summary>
    /// Displays the player's current action points as diamond-shaped indicators.
    /// Shows filled diamonds for available AP, empty for spent AP.
    /// Created automatically by CombatUIController.
    /// </summary>
    [ExecuteAlways]
    public class ActionPointUI : MonoBehaviour
    {
        public static ActionPointUI Instance { get; private set; }

        [Header("Colors")]
        [SerializeField] private Color filledColor = new Color(1f, 0.84f, 0f); // Gold
        [SerializeField] private Color emptyColor = new Color(0.4f, 0.4f, 0.4f, 0.8f); // Visible gray
        [SerializeField] private Color labelColor = new Color(0.8f, 0.8f, 0.8f);

        [Header("Sprites")]
        [SerializeField] private Sprite filledSprite;
        [SerializeField] private Sprite emptySprite;
        [SerializeField] private Sprite panelBackgroundSprite;

        [Header("Layout")]
        [SerializeField] private Vector2 panelSize = new Vector2(220, 55);
        [SerializeField] private Vector2 panelAnchoredPosition = new Vector2(-40, 340);
        [SerializeField] private Vector2 diamondSize = new Vector2(32, 32);
        [SerializeField] private float diamondSpacing = 12f;

        [Header("Authoring")]
        [SerializeField] private bool preserveSceneAuthoredLayout = true;
        [SerializeField] private bool preserveSceneAuthoredSprites = true;

        [Header("Editor Preview")]
        [SerializeField] private bool showPreviewInEditMode = true;
        [SerializeField] private int previewCurrentAP = 2;
        [SerializeField] private int previewMaxAP = 2;

        // Runtime
        private Image[] _apDiamonds;
        private TextMeshProUGUI _apText;
        private PlayerCombatUnit _player;
        private int _maxAP = 2;
        private Image _panelBackground;
        private RectTransform _panelRect;
        private HorizontalLayoutGroup _diamondLayout;
        private bool _wasCreatedAtRuntime;

        private void Awake()
        {
            if (Application.isPlaying && Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            if (Instance == null || Instance == this)
                Instance = this;

            CacheReferences();
            ApplyPanelVisuals();

            if (Application.isPlaying)
            {
                RefreshVisibilityFromCombatState();
            }
        }

        private void OnEnable()
        {
            CacheReferences();
            ApplyPanelVisuals();

            if (Application.isPlaying)
            {
                RefreshVisibilityFromCombatState();
            }

            if (!Application.isPlaying && showPreviewInEditMode)
            {
                RefreshEditorPreview();
            }
        }

        private void OnValidate()
        {
            previewCurrentAP = Mathf.Max(0, previewCurrentAP);
            previewMaxAP = Mathf.Max(1, previewMaxAP);

            CacheReferences();
            ApplyPanelVisuals();

            if (!Application.isPlaying && showPreviewInEditMode)
            {
                RefreshEditorPreview();
            }
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;

            // Unsubscribe from player events
            if (_player != null && _player.PlayerSpecificStats != null)
            {
                _player.PlayerSpecificStats.OnActionPointsChanged -= UpdateDisplay;
            }

            // Unsubscribe from turn events
            if (TurnManager.Instance != null)
            {
                TurnManager.Instance.OnCombatStart -= HandleCombatStart;
                TurnManager.Instance.OnCombatEnd -= HandleCombatEnd;
                TurnManager.Instance.OnTurnStart -= HandleTurnStart;
            }
        }

        private bool _subscribedToEvents = false;

        private void Start()
        {
            if (!Application.isPlaying)
            {
                if (showPreviewInEditMode)
                    RefreshEditorPreview();
                return;
            }

            SubscribeToTurnEvents();
            FindPlayer();
            RefreshVisibilityFromCombatState();
        }

        public void SubscribeToTurnEvents()
        {
            if (_subscribedToEvents) return;
            if (TurnManager.Instance != null)
            {
                TurnManager.Instance.OnCombatStart += HandleCombatStart;
                TurnManager.Instance.OnCombatEnd += HandleCombatEnd;
                TurnManager.Instance.OnTurnStart += HandleTurnStart;
                _subscribedToEvents = true;
            }
        }

        private void HandleCombatStart()
        {
            FindPlayer();
            SetPanelVisible(true);
        }

        private void HandleCombatEnd()
        {
            SetPanelVisible(false);
        }

        private void HandleTurnStart(CombatUnit unit)
        {
            if (_player == null) FindPlayer();
            UpdateDisplay();
        }

        private void FindPlayer()
        {
            if (_player != null) return;

            _player = FindFirstObjectByType<PlayerCombatUnit>();
            if (_player != null && _player.PlayerSpecificStats != null)
            {
                _player.PlayerSpecificStats.OnActionPointsChanged += UpdateDisplay;
                _maxAP = Mathf.Max(2, _player.PlayerSpecificStats.MaxActions);
                Debug.Log($"[ActionPointUI] Found player, MaxActions={_player.PlayerSpecificStats.MaxActions}, using maxAP={_maxAP}");
                BuildDiamonds();
                UpdateDisplay();
            }
        }

        private void BuildDiamonds()
        {
            Debug.Log($"[ActionPointUI] BuildDiamonds: maxAP={_maxAP}");

            Transform container = transform;
            Image[] existingDiamonds = GetComponentsInChildren<Image>(true);
            Image[] activeDiamonds = new Image[_maxAP];

            for (int i = 0; i < _maxAP; i++)
            {
                Image img = FindDiamondByName(existingDiamonds, $"AP_{i}");
                if (img == null)
                {
                    GameObject diamondObj = new GameObject($"AP_{i}");
                    diamondObj.transform.SetParent(container, false);
                    img = diamondObj.AddComponent<Image>();
                }

                ConfigureDiamond(img, i);
                activeDiamonds[i] = img;
            }

            foreach (Image img in existingDiamonds)
            {
                if (img == null || img.transform.parent != container)
                    continue;

                if (!img.gameObject.name.StartsWith("AP_"))
                    continue;

                bool shouldBeActive = false;
                for (int i = 0; i < activeDiamonds.Length; i++)
                {
                    if (activeDiamonds[i] == img)
                    {
                        shouldBeActive = true;
                        break;
                    }
                }

                img.gameObject.SetActive(shouldBeActive);
            }

            _apDiamonds = activeDiamonds;
        }

        private Image FindDiamondByName(Image[] images, string diamondName)
        {
            for (int i = 0; i < images.Length; i++)
            {
                Image image = images[i];
                if (image == null || image.transform.parent != transform)
                    continue;

                if (image.gameObject.name == diamondName)
                    return image;
            }

            return null;
        }

        private void ConfigureDiamond(Image img, int index)
        {
            img.gameObject.SetActive(true);
            img.transform.SetSiblingIndex(index);

            RectTransform rect = img.GetComponent<RectTransform>();
            if (rect == null)
                rect = img.gameObject.AddComponent<RectTransform>();

            if (_wasCreatedAtRuntime || !preserveSceneAuthoredLayout)
            {
                rect.sizeDelta = diamondSize;
                rect.localRotation = Quaternion.Euler(0, 0, 45);
            }

            img.color = emptyColor;
            img.raycastTarget = false;
            if (emptySprite != null || _wasCreatedAtRuntime || !preserveSceneAuthoredSprites)
            {
                img.sprite = emptySprite;
                img.preserveAspect = emptySprite != null || filledSprite != null;
            }
        }

        public void UpdateDisplay()
        {
            if (_player == null || _player.PlayerSpecificStats == null) return;

            int currentAP = _player.PlayerSpecificStats.CurrentActions;

            if (_apDiamonds == null || _apDiamonds.Length != _maxAP)
            {
                _maxAP = _player.PlayerSpecificStats.MaxActions;
                BuildDiamonds();
            }

            Debug.Log($"[ActionPointUI] UpdateDisplay: currentAP={currentAP}, maxAP={_maxAP}, diamonds={_apDiamonds?.Length}");

            for (int i = 0; i < _apDiamonds.Length; i++)
            {
                if (_apDiamonds[i] != null)
                {
                    bool isFilled = i < currentAP;
                    _apDiamonds[i].color = isFilled ? filledColor : emptyColor;

                    if (filledSprite != null || emptySprite != null || _wasCreatedAtRuntime || !preserveSceneAuthoredSprites)
                    {
                        _apDiamonds[i].sprite = isFilled ? (filledSprite != null ? filledSprite : emptySprite) : emptySprite;
                        _apDiamonds[i].preserveAspect = _apDiamonds[i].sprite != null;
                    }
                }
            }

            if (_apText != null)
            {
                _apText.text = $"AP: {currentAP}/{_maxAP}";
            }
        }

        private void RefreshEditorPreview()
        {
            if (Application.isPlaying)
                return;

            CacheReferences();

            if (transform.parent != null)
                transform.parent.gameObject.SetActive(true);
            gameObject.SetActive(true);

            _maxAP = Mathf.Max(1, previewMaxAP);
            BuildDiamonds();

            int currentAP = Mathf.Clamp(previewCurrentAP, 0, _maxAP);
            for (int i = 0; i < _apDiamonds.Length; i++)
            {
                if (_apDiamonds[i] == null)
                    continue;

                bool isFilled = i < currentAP;
                _apDiamonds[i].color = isFilled ? filledColor : emptyColor;

                if (filledSprite != null || emptySprite != null || _wasCreatedAtRuntime || !preserveSceneAuthoredSprites)
                {
                    _apDiamonds[i].sprite = isFilled ? (filledSprite != null ? filledSprite : emptySprite) : emptySprite;
                    _apDiamonds[i].preserveAspect = _apDiamonds[i].sprite != null;
                }
            }
        }

        private void CacheReferences()
        {
            if (_panelRect == null && transform.parent != null)
                _panelRect = transform.parent as RectTransform;

            if (_panelBackground == null && transform.parent != null)
                _panelBackground = transform.parent.GetComponent<Image>();

            if (_diamondLayout == null)
                _diamondLayout = GetComponent<HorizontalLayoutGroup>();
        }

        private void ApplyPanelVisuals()
        {
            CacheReferences();

            bool shouldApplyLayout = _wasCreatedAtRuntime || !preserveSceneAuthoredLayout;
            bool shouldApplySprites = _wasCreatedAtRuntime || !preserveSceneAuthoredSprites;

            if (_panelRect != null && shouldApplyLayout)
            {
                _panelRect.sizeDelta = panelSize;
                _panelRect.anchoredPosition = panelAnchoredPosition;
            }

            if (_panelBackground != null)
            {
                if (panelBackgroundSprite != null || shouldApplySprites)
                {
                    _panelBackground.sprite = panelBackgroundSprite;
                    _panelBackground.type = panelBackgroundSprite != null ? Image.Type.Sliced : Image.Type.Simple;
                }

                if (_wasCreatedAtRuntime || panelBackgroundSprite != null)
                    _panelBackground.color = Color.white;
            }

            if (_diamondLayout != null && shouldApplyLayout)
                _diamondLayout.spacing = diamondSpacing;
        }

        private void SetPanelVisible(bool visible)
        {
            if (!Application.isPlaying)
                return;

            if (transform.parent != null)
                transform.parent.gameObject.SetActive(visible);
            else
                gameObject.SetActive(visible);
        }

        private void RefreshVisibilityFromCombatState()
        {
            if (!Application.isPlaying)
                return;

            bool isInCombat = TurnManager.Instance != null && TurnManager.Instance.IsInCombat;
            SetPanelVisible(isInCombat);
        }

        /// <summary>
        /// Find an existing ActionPointUI in the loaded scene, including inactive objects.
        /// </summary>
        public static ActionPointUI FindExistingInScene()
        {
            var all = Resources.FindObjectsOfTypeAll<ActionPointUI>();
            foreach (var candidate in all)
            {
                if (candidate == null)
                    continue;

                if (!candidate.gameObject.scene.IsValid())
                    continue;

                return candidate;
            }

            return null;
        }

        /// <summary>
        /// Create the AP UI instance in the scene.
        /// </summary>
        public static ActionPointUI CreateInstance(bool startHidden = true)
        {
            ActionPointUI existing = FindExistingInScene();
            if (existing != null)
            {
                Instance = existing;
                existing.CacheReferences();
                existing.ApplyPanelVisuals();

                if (Application.isPlaying)
                    existing.SubscribeToTurnEvents();

                return existing;
            }

            // Always create a dedicated canvas with proper scaling
            GameObject canvasObj = new GameObject("CombatAPCanvas");
            Canvas canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 15;
            var scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            canvasObj.AddComponent<GraphicRaycaster>();

            // Create AP panel
            GameObject panelObj = new GameObject("ActionPointUI");
            panelObj.transform.SetParent(canvas.transform, false);

            RectTransform panelRect = panelObj.AddComponent<RectTransform>();
            // Position above the action menu (bottom-right area)
            panelRect.anchorMin = new Vector2(1, 0);
            panelRect.anchorMax = new Vector2(1, 0);
            panelRect.pivot = new Vector2(1, 0);
            panelRect.anchoredPosition = new Vector2(-40, 340);
            panelRect.sizeDelta = new Vector2(220, 55);

            // Background
            Image bg = panelObj.AddComponent<Image>();
            bg.color = new Color(0.1f, 0.1f, 0.15f, 0.85f);
            bg.raycastTarget = false;

            // "AP" label
            GameObject labelObj = new GameObject("APLabel");
            labelObj.transform.SetParent(panelObj.transform, false);

            RectTransform labelRect = labelObj.AddComponent<RectTransform>();
            labelRect.anchorMin = new Vector2(0, 0);
            labelRect.anchorMax = new Vector2(0.35f, 1);
            labelRect.offsetMin = new Vector2(8, 0);
            labelRect.offsetMax = new Vector2(0, 0);

            TextMeshProUGUI labelTMP = labelObj.AddComponent<TextMeshProUGUI>();
            labelTMP.text = "AP";
            labelTMP.fontSize = 18;
            labelTMP.fontStyle = FontStyles.Bold;
            labelTMP.alignment = TextAlignmentOptions.MidlineLeft;
            labelTMP.color = new Color(1f, 0.84f, 0f);
            labelTMP.raycastTarget = false;

            // Diamond container
            GameObject diamondContainer = new GameObject("Diamonds");
            diamondContainer.transform.SetParent(panelObj.transform, false);

            RectTransform dcRect = diamondContainer.AddComponent<RectTransform>();
            dcRect.anchorMin = new Vector2(0.35f, 0);
            dcRect.anchorMax = new Vector2(1, 1);
            dcRect.offsetMin = Vector2.zero;
            dcRect.offsetMax = Vector2.zero;

            HorizontalLayoutGroup hLayout = diamondContainer.AddComponent<HorizontalLayoutGroup>();
            hLayout.spacing = 12;
            hLayout.childAlignment = TextAnchor.MiddleCenter;
            hLayout.childControlWidth = false;
            hLayout.childControlHeight = false;
            hLayout.childForceExpandWidth = false;
            hLayout.childForceExpandHeight = false;

            // Add component
            ActionPointUI apUI = diamondContainer.AddComponent<ActionPointUI>();
            apUI._wasCreatedAtRuntime = true;
            apUI._panelBackground = bg;
            apUI._panelRect = panelRect;
            apUI._diamondLayout = hLayout;
            apUI.ApplyPanelVisuals();

            // Subscribe to events immediately (before hiding, since Start won't run while inactive)
            if (Application.isPlaying)
                apUI.SubscribeToTurnEvents();

            // Start hidden only in gameplay-created mode
            panelObj.SetActive(!startHidden);

            return apUI;
        }
    }
}
