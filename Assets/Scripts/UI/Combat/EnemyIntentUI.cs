using UnityEngine;
using UnityEngine.UI;
using TMPro;
using KAM.Core.Combat;
using KAM.Combat.TurnSystem;

namespace KAM.UI.Combat
{
    /// <summary>
    /// Displays enemy intent (what they will do next turn) above their health bar.
    /// Similar to Slay the Spire's intent system.
    /// </summary>
    public class EnemyIntentUI : MonoBehaviour
    {
        [Header("UI Elements")]
        [SerializeField] private Image intentIcon;
        [SerializeField] private TextMeshProUGUI damageText;
        [SerializeField] private Image backgroundPanel;
        [SerializeField] private CanvasGroup canvasGroup;

        [Header("Intent Icons")]
        [SerializeField] private Sprite attackIcon;
        [SerializeField] private Sprite defendIcon;
        [SerializeField] private Sprite buffIcon;
        [SerializeField] private Sprite debuffIcon;
        [SerializeField] private Sprite healIcon;
        [SerializeField] private Sprite specialIcon;
        [SerializeField] private Sprite unknownIcon;

        [Header("Colors")]
        [SerializeField] private Color attackColor = new Color(1f, 0.3f, 0.3f, 1f);
        [SerializeField] private Color defendColor = new Color(0.3f, 0.5f, 1f, 1f);
        [SerializeField] private Color buffColor = new Color(0.3f, 1f, 0.5f, 1f);
        [SerializeField] private Color debuffColor = new Color(0.8f, 0.3f, 0.8f, 1f);
        [SerializeField] private Color healColor = new Color(0.3f, 1f, 0.3f, 1f);
        [SerializeField] private Color unknownColor = new Color(0.5f, 0.5f, 0.5f, 1f);

        [Header("Position")]
        [SerializeField] private Vector3 offset = new Vector3(0f, 0.5f, 0f);
        [SerializeField] private bool billboardToCamera = true;

        [Header("Animation")]
        [SerializeField] private float fadeInDuration = 0.2f;
        [SerializeField] private float pulseScale = 1.1f;
        [SerializeField] private float pulseSpeed = 2f;

        // Runtime
        private EnemyCombatUnit _enemy;
        private Transform _followTarget;
        private Camera _mainCamera;
        private EnemyIntent _currentIntent;
        private bool _isVisible = false;

        private void Awake()
        {
            _mainCamera = Camera.main;

            if (canvasGroup == null)
                canvasGroup = GetComponent<CanvasGroup>();

            // Start hidden
            SetVisible(false);
        }

        private void LateUpdate()
        {
            if (_followTarget == null) return;

            // Follow the target
            transform.position = _followTarget.position + offset;

            // Billboard to camera
            if (billboardToCamera && _mainCamera != null)
            {
                transform.forward = _mainCamera.transform.forward;
            }

            // Pulse animation for attack intents
            if (_isVisible && _currentIntent.ActionType == EnemyIntentType.Attack)
            {
                float pulse = 1f + (Mathf.Sin(Time.time * pulseSpeed) * 0.5f + 0.5f) * (pulseScale - 1f);
                if (intentIcon != null)
                    intentIcon.transform.localScale = Vector3.one * pulse;
            }
        }

        /// <summary>
        /// Initialize for an enemy combat unit.
        /// </summary>
        public void Initialize(EnemyCombatUnit enemy)
        {
            _enemy = enemy;
            _followTarget = enemy.transform;

            // Subscribe to intent changes
            _enemy.OnIntentChanged += UpdateIntent;
            _enemy.OnDeath += HandleEnemyDeath;

            // Subscribe to turn manager events
            TurnManager turnManager = TurnManager.Instance;
            if (turnManager != null)
            {
                turnManager.OnRoundStart += HandleRoundStart;
                turnManager.OnCombatEnd += HandleCombatEnd;
            }
        }

        private void OnDestroy()
        {
            if (_enemy != null)
            {
                _enemy.OnIntentChanged -= UpdateIntent;
                _enemy.OnDeath -= HandleEnemyDeath;
            }

            TurnManager turnManager = TurnManager.Instance;
            if (turnManager != null)
            {
                turnManager.OnRoundStart -= HandleRoundStart;
                turnManager.OnCombatEnd -= HandleCombatEnd;
            }
        }

        /// <summary>
        /// Update the intent display.
        /// </summary>
        public void UpdateIntent(EnemyIntent intent)
        {
            _currentIntent = intent;

            // Update icon
            if (intentIcon != null)
            {
                intentIcon.sprite = GetIconForIntent(intent.ActionType);
                intentIcon.color = GetColorForIntent(intent.ActionType);
                intentIcon.transform.localScale = Vector3.one; // Reset scale
            }

            // Update damage text
            if (damageText != null)
            {
                if (intent.DealsDamage)
                {
                    damageText.gameObject.SetActive(true);
                    if (intent.HitCount > 1)
                        damageText.text = $"{intent.DamageAmount}x{intent.HitCount}";
                    else
                        damageText.text = intent.DamageAmount.ToString();
                }
                else
                {
                    damageText.gameObject.SetActive(false);
                }
            }

            // Show or hide based on intent type
            SetVisible(intent.ActionType != EnemyIntentType.Unknown);
        }

        private Sprite GetIconForIntent(EnemyIntentType type)
        {
            switch (type)
            {
                case EnemyIntentType.Attack:
                    return attackIcon;
                case EnemyIntentType.Defend:
                    return defendIcon;
                case EnemyIntentType.Buff:
                    return buffIcon;
                case EnemyIntentType.Debuff:
                    return debuffIcon;
                case EnemyIntentType.Heal:
                    return healIcon;
                case EnemyIntentType.Special:
                case EnemyIntentType.Summon:
                    return specialIcon;
                default:
                    return unknownIcon;
            }
        }

        private Color GetColorForIntent(EnemyIntentType type)
        {
            switch (type)
            {
                case EnemyIntentType.Attack:
                    return attackColor;
                case EnemyIntentType.Defend:
                    return defendColor;
                case EnemyIntentType.Buff:
                case EnemyIntentType.Heal:
                    return buffColor;
                case EnemyIntentType.Debuff:
                    return debuffColor;
                default:
                    return unknownColor;
            }
        }

        private void SetVisible(bool visible)
        {
            _isVisible = visible;

            if (canvasGroup != null)
            {
                canvasGroup.alpha = visible ? 1f : 0f;
            }

            gameObject.SetActive(visible);
        }

        #region Event Handlers

        private void HandleRoundStart(int roundNumber)
        {
            // At the start of each round, have enemy decide their intent
            if (_enemy != null && _enemy.IsAlive)
            {
                _enemy.DecideNextIntent();
            }
        }

        private void HandleEnemyDeath(CombatUnit unit)
        {
            SetVisible(false);
        }

        private void HandleCombatEnd()
        {
            SetVisible(false);
        }

        #endregion

        #region Static Factory

        /// <summary>
        /// Create an intent UI for an enemy.
        /// </summary>
        public static EnemyIntentUI CreateForEnemy(EnemyCombatUnit enemy)
        {
            // Create canvas object
            GameObject intentObj = new GameObject($"IntentUI_{enemy.UnitName}");

            // Add Canvas for world-space rendering
            Canvas canvas = intentObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 10;

            // Scale down for world space
            RectTransform canvasRect = intentObj.GetComponent<RectTransform>();
            canvasRect.sizeDelta = new Vector2(100, 50);
            canvasRect.localScale = new Vector3(0.01f, 0.01f, 0.01f);

            // Add canvas group for fading
            CanvasGroup canvasGroup = intentObj.AddComponent<CanvasGroup>();

            // Create background panel
            GameObject panelObj = new GameObject("Background");
            panelObj.transform.SetParent(intentObj.transform, false);
            RectTransform panelRect = panelObj.AddComponent<RectTransform>();
            panelRect.anchorMin = Vector2.zero;
            panelRect.anchorMax = Vector2.one;
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;
            Image panelImage = panelObj.AddComponent<Image>();
            panelImage.color = new Color(0, 0, 0, 0.5f);

            // Create icon
            GameObject iconObj = new GameObject("IntentIcon");
            iconObj.transform.SetParent(intentObj.transform, false);
            RectTransform iconRect = iconObj.AddComponent<RectTransform>();
            iconRect.anchorMin = new Vector2(0, 0.5f);
            iconRect.anchorMax = new Vector2(0, 0.5f);
            iconRect.pivot = new Vector2(0, 0.5f);
            iconRect.anchoredPosition = new Vector2(5, 0);
            iconRect.sizeDelta = new Vector2(40, 40);
            Image iconImage = iconObj.AddComponent<Image>();

            // Create damage text
            GameObject textObj = new GameObject("DamageText");
            textObj.transform.SetParent(intentObj.transform, false);
            RectTransform textRect = textObj.AddComponent<RectTransform>();
            textRect.anchorMin = new Vector2(1, 0.5f);
            textRect.anchorMax = new Vector2(1, 0.5f);
            textRect.pivot = new Vector2(1, 0.5f);
            textRect.anchoredPosition = new Vector2(-5, 0);
            textRect.sizeDelta = new Vector2(50, 40);
            TextMeshProUGUI damageText = textObj.AddComponent<TextMeshProUGUI>();
            damageText.fontSize = 24;
            damageText.fontStyle = FontStyles.Bold;
            damageText.alignment = TextAlignmentOptions.Right;
            damageText.color = Color.white;

            // Add the EnemyIntentUI component
            EnemyIntentUI intentUI = intentObj.AddComponent<EnemyIntentUI>();
            intentUI.canvasGroup = canvasGroup;
            intentUI.backgroundPanel = panelImage;
            intentUI.intentIcon = iconImage;
            intentUI.damageText = damageText;

            // Position above enemy
            intentObj.transform.position = enemy.transform.position + new Vector3(0, 3f, 0);

            // Initialize
            intentUI.Initialize(enemy);

            return intentUI;
        }

        #endregion
    }
}
