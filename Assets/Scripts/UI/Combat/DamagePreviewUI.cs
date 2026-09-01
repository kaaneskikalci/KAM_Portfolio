using UnityEngine;
using UnityEngine.UI;
using TMPro;
using KAM.Core.Combat;
using KAM.Combat.TurnSystem;

namespace KAM.UI.Combat
{
    /// <summary>
    /// Shows damage preview when hovering over an enemy target.
    /// Displays damage range, critical chance, and "KILL" indicator if attack will kill.
    /// </summary>
    public class DamagePreviewUI : MonoBehaviour
    {
        public static DamagePreviewUI Instance { get; private set; }

        [Header("UI Elements")]
        [SerializeField] private GameObject previewPanel;
        [SerializeField] private TextMeshProUGUI damageRangeText;
        [SerializeField] private TextMeshProUGUI critChanceText;
        [SerializeField] private TextMeshProUGUI killIndicatorText;
        [SerializeField] private Image backgroundPanel;
        [SerializeField] private CanvasGroup canvasGroup;

        [Header("Colors")]
        [SerializeField] private Color normalDamageColor = Color.white;
        [SerializeField] private Color highDamageColor = new Color(1f, 0.8f, 0.3f); // Gold
        [SerializeField] private Color killColor = new Color(1f, 0.3f, 0.3f); // Red
        [SerializeField] private Color critColor = new Color(1f, 0.8f, 0f); // Yellow

        [Header("Position")]
        [SerializeField] private Vector2 offset = new Vector2(20f, 20f);
        [SerializeField] private bool followMouse = true;

        [Header("Settings")]
        [SerializeField] private float damageVariance = 0.15f; // +/- 15% damage variance

        // References
        private TurnManager _turnManager;
        private PlayerCombatUnit _player;
        private EnemyTargetSelector _targetSelector;
        private Camera _mainCamera;
        private RectTransform _rectTransform;
        private Canvas _parentCanvas;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            _rectTransform = GetComponent<RectTransform>();
            _mainCamera = Camera.main;

            if (canvasGroup == null)
                canvasGroup = GetComponent<CanvasGroup>();

            // Start hidden
            SetVisible(false);
        }

        private void Start()
        {
            _turnManager = TurnManager.Instance;
            _targetSelector = EnemyTargetSelector.Instance;
            _player = FindFirstObjectByType<PlayerCombatUnit>();
            _parentCanvas = GetComponentInParent<Canvas>();

            SubscribeToEvents();
        }

        private void SubscribeToEvents()
        {
            // Subscribe to target selector events
            if (_targetSelector != null)
            {
                _targetSelector.OnTargetHoverChanged += HandleHoverChanged;
            }

            // Subscribe to combat events
            if (_turnManager != null)
            {
                _turnManager.OnCombatEnd += HandleCombatEnd;
                // Re-subscribe when combat starts in case references weren't ready
                _turnManager.OnCombatStart += HandleCombatStart;
            }
        }

        private void HandleCombatStart()
        {
            // Re-find references if they were null at Start
            if (_targetSelector == null)
            {
                _targetSelector = EnemyTargetSelector.Instance;
                if (_targetSelector != null)
                    _targetSelector.OnTargetHoverChanged += HandleHoverChanged;
            }

            if (_player == null)
                _player = FindFirstObjectByType<PlayerCombatUnit>();
        }

        private void OnDestroy()
        {
            if (_targetSelector != null)
            {
                _targetSelector.OnTargetHoverChanged -= HandleHoverChanged;
            }

            if (_turnManager != null)
            {
                _turnManager.OnCombatEnd -= HandleCombatEnd;
                _turnManager.OnCombatStart -= HandleCombatStart;
            }

            if (Instance == this)
                Instance = null;
        }

        private void Update()
        {
            // Only show during player turn in combat
            if (_turnManager == null || !_turnManager.IsInCombat || !_turnManager.IsPlayerTurn)
            {
                SetVisible(false);
                return;
            }

            // Follow mouse position
            if (followMouse && previewPanel != null && previewPanel.activeSelf)
            {
                UpdatePosition();
            }
        }

        private void UpdatePosition()
        {
            if (_rectTransform == null) return;

            Vector2 mousePos = Input.mousePosition;

            // Convert to canvas space if needed
            if (_parentCanvas != null && _parentCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
            {
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _parentCanvas.transform as RectTransform,
                    mousePos,
                    _mainCamera,
                    out mousePos);
            }

            // Apply offset and keep on screen
            Vector2 targetPos = mousePos + offset;

            // Clamp to screen bounds
            if (_parentCanvas != null)
            {
                RectTransform canvasRect = _parentCanvas.transform as RectTransform;
                float maxX = canvasRect.rect.width - _rectTransform.rect.width;
                float maxY = canvasRect.rect.height - _rectTransform.rect.height;

                targetPos.x = Mathf.Clamp(targetPos.x, 0, maxX);
                targetPos.y = Mathf.Clamp(targetPos.y, 0, maxY);
            }

            _rectTransform.anchoredPosition = targetPos;
        }

        /// <summary>
        /// Handle when the hovered enemy changes.
        /// </summary>
        private void HandleHoverChanged(EnemyCombatUnit hoveredEnemy)
        {
            if (hoveredEnemy != null && _turnManager != null && _turnManager.IsPlayerTurn)
            {
                ShowPreviewForEnemy(hoveredEnemy);
            }
            else
            {
                SetVisible(false);
            }
        }

        /// <summary>
        /// Show damage preview for a specific enemy.
        /// </summary>
        public void ShowPreviewForEnemy(EnemyCombatUnit enemy)
        {
            if (enemy == null || _player == null)
            {
                SetVisible(false);
                return;
            }

            // Calculate damage range
            int baseDamage = _player.Stats.EffectiveDamage;
            int minDamage = Mathf.Max(1, Mathf.RoundToInt(baseDamage * (1f - damageVariance)));
            int maxDamage = Mathf.RoundToInt(baseDamage * (1f + damageVariance));

            // Account for enemy defense
            int defense = enemy.Stats.EffectiveDefense;
            minDamage = Mathf.Max(1, minDamage - defense);
            maxDamage = Mathf.Max(1, maxDamage - defense);

            // Get crit info
            float critChance = _player.Stats.CriticalChance * 100f;
            float critMultiplier = _player.Stats.CriticalMultiplier;

            // Check if attack can kill
            bool canKill = maxDamage >= enemy.Stats.CurrentHealth;
            bool willDefinitelyKill = minDamage >= enemy.Stats.CurrentHealth;

            // Update UI
            UpdateDamageText(minDamage, maxDamage, canKill, willDefinitelyKill);
            UpdateCritText(critChance, critMultiplier);
            UpdateKillIndicator(canKill, willDefinitelyKill, enemy.Stats.CurrentHealth);

            SetVisible(true);
        }

        private void UpdateDamageText(int minDamage, int maxDamage, bool canKill, bool willKill)
        {
            if (damageRangeText == null) return;

            if (minDamage == maxDamage)
            {
                damageRangeText.text = $"{minDamage} DMG";
            }
            else
            {
                damageRangeText.text = $"{minDamage}-{maxDamage} DMG";
            }

            // Color based on kill potential
            if (willKill)
                damageRangeText.color = killColor;
            else if (canKill)
                damageRangeText.color = highDamageColor;
            else
                damageRangeText.color = normalDamageColor;
        }

        private void UpdateCritText(float critChance, float critMultiplier)
        {
            if (critChanceText == null) return;

            if (critChance > 0)
            {
                critChanceText.gameObject.SetActive(true);
                critChanceText.text = $"{critChance:F0}% CRIT";
                critChanceText.color = critColor;
            }
            else
            {
                critChanceText.gameObject.SetActive(false);
            }
        }

        private void UpdateKillIndicator(bool canKill, bool willKill, int enemyHealth)
        {
            if (killIndicatorText == null) return;

            if (willKill)
            {
                killIndicatorText.gameObject.SetActive(true);
                killIndicatorText.text = "LETHAL";
                killIndicatorText.color = killColor;
            }
            else if (canKill)
            {
                killIndicatorText.gameObject.SetActive(true);
                killIndicatorText.text = $"CAN KILL ({enemyHealth} HP)";
                killIndicatorText.color = highDamageColor;
            }
            else
            {
                killIndicatorText.gameObject.SetActive(false);
            }
        }

        private void SetVisible(bool visible)
        {
            if (previewPanel != null)
                previewPanel.SetActive(visible);

            if (canvasGroup != null)
                canvasGroup.alpha = visible ? 1f : 0f;
        }

        private void HandleCombatEnd()
        {
            SetVisible(false);
        }

        #region Static Setup

        /// <summary>
        /// Create the damage preview UI if it doesn't exist.
        /// </summary>
        public static DamagePreviewUI CreateInstance()
        {
            if (Instance != null) return Instance;

            // Find a ScreenSpace Overlay canvas, or create one
            Canvas canvas = FindScreenSpaceCanvas();
            if (canvas == null)
            {
                GameObject canvasObj = new GameObject("CombatUICanvas");
                canvas = canvasObj.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 100;
                var scaler = canvasObj.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
                canvasObj.AddComponent<GraphicRaycaster>();
            }

            // Create preview panel
            GameObject previewObj = new GameObject("DamagePreviewUI");
            previewObj.transform.SetParent(canvas.transform, false);

            RectTransform previewRect = previewObj.AddComponent<RectTransform>();
            previewRect.sizeDelta = new Vector2(150, 80);

            CanvasGroup canvasGroup = previewObj.AddComponent<CanvasGroup>();
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;

            // Background
            GameObject bgObj = new GameObject("Background");
            bgObj.transform.SetParent(previewObj.transform, false);
            RectTransform bgRect = bgObj.AddComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.offsetMin = Vector2.zero;
            bgRect.offsetMax = Vector2.zero;
            Image bgImage = bgObj.AddComponent<Image>();
            bgImage.color = new Color(0, 0, 0, 0.8f);

            // Damage text
            GameObject damageObj = new GameObject("DamageText");
            damageObj.transform.SetParent(previewObj.transform, false);
            RectTransform damageRect = damageObj.AddComponent<RectTransform>();
            damageRect.anchorMin = new Vector2(0, 0.6f);
            damageRect.anchorMax = new Vector2(1, 1);
            damageRect.offsetMin = new Vector2(5, 0);
            damageRect.offsetMax = new Vector2(-5, -5);
            TextMeshProUGUI damageText = damageObj.AddComponent<TextMeshProUGUI>();
            damageText.fontSize = 18;
            damageText.fontStyle = FontStyles.Bold;
            damageText.alignment = TextAlignmentOptions.Center;
            damageText.color = Color.white;

            // Crit text
            GameObject critObj = new GameObject("CritText");
            critObj.transform.SetParent(previewObj.transform, false);
            RectTransform critRect = critObj.AddComponent<RectTransform>();
            critRect.anchorMin = new Vector2(0, 0.3f);
            critRect.anchorMax = new Vector2(1, 0.6f);
            critRect.offsetMin = new Vector2(5, 0);
            critRect.offsetMax = new Vector2(-5, 0);
            TextMeshProUGUI critText = critObj.AddComponent<TextMeshProUGUI>();
            critText.fontSize = 12;
            critText.alignment = TextAlignmentOptions.Center;
            critText.color = new Color(1f, 0.8f, 0f);

            // Kill indicator
            GameObject killObj = new GameObject("KillIndicator");
            killObj.transform.SetParent(previewObj.transform, false);
            RectTransform killRect = killObj.AddComponent<RectTransform>();
            killRect.anchorMin = new Vector2(0, 0);
            killRect.anchorMax = new Vector2(1, 0.3f);
            killRect.offsetMin = new Vector2(5, 5);
            killRect.offsetMax = new Vector2(-5, 0);
            TextMeshProUGUI killText = killObj.AddComponent<TextMeshProUGUI>();
            killText.fontSize = 14;
            killText.fontStyle = FontStyles.Bold;
            killText.alignment = TextAlignmentOptions.Center;
            killText.color = new Color(1f, 0.3f, 0.3f);

            // Add component
            DamagePreviewUI preview = previewObj.AddComponent<DamagePreviewUI>();
            preview.previewPanel = previewObj;
            preview.damageRangeText = damageText;
            preview.critChanceText = critText;
            preview.killIndicatorText = killText;
            preview.backgroundPanel = bgImage;
            preview.canvasGroup = canvasGroup;

            return preview;
        }

        private static Canvas FindScreenSpaceCanvas()
        {
            var canvases = FindObjectsByType<Canvas>(FindObjectsSortMode.None);
            foreach (var c in canvases)
            {
                if (c.renderMode == RenderMode.ScreenSpaceOverlay && c.gameObject.activeInHierarchy)
                    return c;
            }
            return null;
        }

        #endregion
    }
}
