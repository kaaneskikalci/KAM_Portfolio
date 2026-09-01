using UnityEngine;
using UnityEngine.UI;
using TMPro;
using KAM.Core.Combat;
using KAM.Core.Stats;

namespace KAM.UI.Combat
{
    /// <summary>
    /// World-space health bar that follows a combat unit.
    /// Displays current/max HP with animated bar and damage/heal numbers.
    /// </summary>
    public class HealthBarUI : MonoBehaviour
    {
        [Header("UI Elements")]
        [SerializeField] private Image healthBarFill;
        [SerializeField] private Image healthBarBackground;
        [SerializeField] private Image damageBarFill; // Delayed damage indicator
        [SerializeField] private TextMeshProUGUI healthText;
        [SerializeField] private TextMeshProUGUI unitNameText;
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private Image selectionBorder; // Border shown when enemy is targeted

        [Header("Position Settings")]
        [SerializeField] private Vector3 worldOffset = new Vector3(0f, 2.5f, 0f);
        [SerializeField] private bool billboardToCamera = true;

        [Header("Animation Settings")]
        [SerializeField] private float fillAnimationSpeed = 3f;
        [SerializeField] private float damageBarDelay = 0.5f;
        [SerializeField] private float damageBarSpeed = 2f;
        [SerializeField] private float fadeOutDelay = 2f;
        [SerializeField] private float fadeOutDuration = 0.5f;

        [Header("Colors")]
        [SerializeField] private Color healthyColor = new Color(0.2f, 0.8f, 0.2f);
        [SerializeField] private Color damagedColor = new Color(0.8f, 0.8f, 0.2f);
        [SerializeField] private Color criticalColor = new Color(0.8f, 0.2f, 0.2f);
        [SerializeField] private Color damageBarColor = new Color(0.8f, 0.3f, 0.3f, 0.5f);
        [SerializeField] private Color selectedBorderColor = new Color(1f, 0.2f, 0.2f, 1f); // Red border when selected
        [SerializeField] private Color hoveredBorderColor = new Color(1f, 1f, 0.2f, 0.8f); // Yellow border when hovered
        [SerializeField] private float criticalThreshold = 0.25f;
        [SerializeField] private float damagedThreshold = 0.5f;

        [Header("Settings")]
        [SerializeField] private bool showForPlayer = true;
        [SerializeField] private bool showForEnemies = true;
        [SerializeField] private bool hideWhenFull = false;
        [SerializeField] private bool alwaysVisible = false;

        // Runtime
        private CombatUnit _unit;
        private Transform _targetTransform;
        private Camera _mainCamera;
        private float _currentFillAmount;
        private float _targetFillAmount;
        private float _damageBarFillAmount;
        private bool _isVisible = false;
        private float _lastDamageTime;
        private Coroutine _fadeCoroutine;
        private bool _isSelected = false;
        private bool _isHovered = false;

        private void Awake()
        {
            _mainCamera = Camera.main;

            if (canvasGroup == null)
                canvasGroup = GetComponent<CanvasGroup>();

            if (damageBarFill != null)
                damageBarFill.color = damageBarColor;
        }

        private void LateUpdate()
        {
            if (_targetTransform == null) return;

            UpdatePosition();
            UpdateFillAnimation();
            UpdateDamageBar();
            UpdateSelectionState();

            if (billboardToCamera && _mainCamera != null)
            {
                transform.forward = _mainCamera.transform.forward;
            }
        }

        /// <summary>
        /// Check if this unit is selected/hovered and update border visuals.
        /// </summary>
        private void UpdateSelectionState()
        {
            if (_unit == null || !_unit.IsEnemy) return;

            EnemyCombatUnit enemyUnit = _unit as EnemyCombatUnit;
            if (enemyUnit == null) return;

            // Check selection state from EnemyTargetSelector
            bool isSelected = false;
            bool isHovered = false;

            if (EnemyTargetSelector.Instance != null)
            {
                isSelected = EnemyTargetSelector.Instance.IsSelected(enemyUnit);
                isHovered = EnemyTargetSelector.Instance.IsHovered(enemyUnit);
            }

            // Only update if state changed
            if (isSelected != _isSelected || isHovered != _isHovered)
            {
                _isSelected = isSelected;
                _isHovered = isHovered;
                UpdateSelectionBorder();
            }
        }

        private void OnDestroy()
        {
            UnsubscribeFromUnit();
        }

        #region Setup

        /// <summary>
        /// Initialize the health bar for a combat unit
        /// </summary>
        public void Initialize(CombatUnit unit)
        {
            UnsubscribeFromUnit();

            _unit = unit;
            _targetTransform = unit.transform;

            // Check visibility settings
            if (unit.IsPlayer && !showForPlayer) return;
            if (unit.IsEnemy && !showForEnemies) return;

            // Subscribe to unit events
            _unit.Stats.OnHealthChanged += HandleHealthChanged;
            _unit.OnDeath += HandleDeath;
            _unit.OnEnterCombat += HandleEnterCombat;
            _unit.OnExitCombat += HandleExitCombat;

            // Set initial values
            if (unitNameText != null)
                unitNameText.text = unit.UnitName;

            _targetFillAmount = unit.Stats.HealthPercentage;
            _currentFillAmount = _targetFillAmount;
            _damageBarFillAmount = _targetFillAmount;

            UpdateHealthDisplay(unit.Stats.CurrentHealth, unit.Stats.MaxHealth);

            if (alwaysVisible || (hideWhenFull && unit.Stats.HealthPercentage >= 1f))
            {
                SetVisible(!hideWhenFull || unit.Stats.HealthPercentage < 1f);
            }
            else
            {
                SetVisible(true);
            }
        }

        /// <summary>
        /// Setup as a static health bar (not following a unit)
        /// </summary>
        public void SetupStatic(string name, int currentHealth, int maxHealth)
        {
            if (unitNameText != null)
                unitNameText.text = name;

            float percentage = maxHealth > 0 ? (float)currentHealth / maxHealth : 0f;
            _targetFillAmount = percentage;
            _currentFillAmount = percentage;
            _damageBarFillAmount = percentage;

            UpdateHealthDisplay(currentHealth, maxHealth);
            SetVisible(true);
        }

        private void UnsubscribeFromUnit()
        {
            if (_unit != null)
            {
                _unit.Stats.OnHealthChanged -= HandleHealthChanged;
                _unit.OnDeath -= HandleDeath;
                _unit.OnEnterCombat -= HandleEnterCombat;
                _unit.OnExitCombat -= HandleExitCombat;
            }
        }

        #endregion

        #region Event Handlers

        private void HandleHealthChanged(int current, int max)
        {
            float newPercentage = max > 0 ? (float)current / max : 0f;

            // Track damage bar position before update
            if (newPercentage < _targetFillAmount)
            {
                // Taking damage - damage bar should lag behind
                _lastDamageTime = Time.time;
            }
            else
            {
                // Healing - damage bar should snap to new value
                _damageBarFillAmount = newPercentage;
            }

            _targetFillAmount = newPercentage;
            UpdateHealthDisplay(current, max);

            // Show health bar when health changes
            if (!_isVisible)
            {
                SetVisible(true);
            }

            // Cancel any pending fade
            if (_fadeCoroutine != null)
            {
                StopCoroutine(_fadeCoroutine);
                _fadeCoroutine = null;
            }

            // Hide if full and setting enabled
            if (hideWhenFull && newPercentage >= 1f)
            {
                _fadeCoroutine = StartCoroutine(FadeOutAfterDelay());
            }
        }

        private void HandleDeath(CombatUnit unit)
        {
            // Fade out on death
            if (_fadeCoroutine != null)
                StopCoroutine(_fadeCoroutine);

            _fadeCoroutine = StartCoroutine(FadeOutAfterDelay(0.5f));
        }

        private void HandleEnterCombat(CombatUnit unit)
        {
            SetVisible(true);
        }

        private void HandleExitCombat(CombatUnit unit)
        {
            if (!alwaysVisible)
            {
                SetVisible(false);
            }
        }

        #endregion

        #region Visual Updates

        private void UpdatePosition()
        {
            if (_targetTransform != null)
            {
                transform.position = _targetTransform.position + worldOffset;
            }
        }

        private void UpdateFillAnimation()
        {
            if (Mathf.Approximately(_currentFillAmount, _targetFillAmount)) return;

            _currentFillAmount = Mathf.MoveTowards(_currentFillAmount, _targetFillAmount,
                fillAnimationSpeed * Time.deltaTime);

            if (healthBarFill != null)
            {
                healthBarFill.fillAmount = _currentFillAmount;
                healthBarFill.color = GetHealthColor(_currentFillAmount);
            }
        }

        private void UpdateDamageBar()
        {
            if (damageBarFill == null) return;

            // Only update damage bar after delay
            if (Time.time - _lastDamageTime < damageBarDelay) return;

            if (_damageBarFillAmount > _targetFillAmount)
            {
                _damageBarFillAmount = Mathf.MoveTowards(_damageBarFillAmount, _targetFillAmount,
                    damageBarSpeed * Time.deltaTime);
                damageBarFill.fillAmount = _damageBarFillAmount;
            }
            else
            {
                _damageBarFillAmount = _targetFillAmount;
                damageBarFill.fillAmount = _damageBarFillAmount;
            }
        }

        private void UpdateHealthDisplay(int current, int max)
        {
            if (healthText != null)
            {
                healthText.text = $"{current}/{max}";
            }
        }

        private Color GetHealthColor(float percentage)
        {
            if (percentage <= criticalThreshold)
                return criticalColor;
            if (percentage <= damagedThreshold)
                return damagedColor;
            return healthyColor;
        }

        #endregion

        #region Visibility

        public void SetVisible(bool visible)
        {
            _isVisible = visible;

            if (canvasGroup != null)
            {
                canvasGroup.alpha = visible ? 1f : 0f;
            }

            gameObject.SetActive(visible || alwaysVisible);
        }

        private System.Collections.IEnumerator FadeOutAfterDelay(float delay = -1f)
        {
            if (delay < 0f) delay = fadeOutDelay;

            yield return new WaitForSeconds(delay);

            if (canvasGroup != null)
            {
                float elapsed = 0f;
                float startAlpha = canvasGroup.alpha;

                while (elapsed < fadeOutDuration)
                {
                    elapsed += Time.deltaTime;
                    canvasGroup.alpha = Mathf.Lerp(startAlpha, 0f, elapsed / fadeOutDuration);
                    yield return null;
                }

                canvasGroup.alpha = 0f;
            }

            _isVisible = false;
            gameObject.SetActive(false);
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Force update health display
        /// </summary>
        public void ForceUpdate()
        {
            if (_unit != null)
            {
                UpdateHealthDisplay(_unit.Stats.CurrentHealth, _unit.Stats.MaxHealth);
                _targetFillAmount = _unit.Stats.HealthPercentage;
            }
        }

        /// <summary>
        /// Manually set the selection state (alternative to auto-detection).
        /// </summary>
        public void SetSelected(bool selected)
        {
            _isSelected = selected;
            UpdateSelectionBorder();
        }

        /// <summary>
        /// Manually set the hover state.
        /// </summary>
        public void SetHovered(bool hovered)
        {
            _isHovered = hovered;
            UpdateSelectionBorder();
        }

        /// <summary>
        /// Update the selection border visual based on current state.
        /// </summary>
        private void UpdateSelectionBorder()
        {
            if (selectionBorder == null)
            {
                // Try to create selection border if it doesn't exist
                CreateSelectionBorder();
            }

            if (selectionBorder != null)
            {
                if (_isSelected)
                {
                    selectionBorder.enabled = true;
                    selectionBorder.color = selectedBorderColor;
                }
                else if (_isHovered)
                {
                    selectionBorder.enabled = true;
                    selectionBorder.color = hoveredBorderColor;
                }
                else
                {
                    selectionBorder.enabled = false;
                }
            }
        }

        /// <summary>
        /// Create selection border if not assigned in inspector.
        /// </summary>
        private void CreateSelectionBorder()
        {
            // Find or create selection border as child
            Transform existingBorder = transform.Find("SelectionBorder");
            if (existingBorder != null)
            {
                selectionBorder = existingBorder.GetComponent<Image>();
                return;
            }

            // Create a new border
            GameObject borderObj = new GameObject("SelectionBorder");
            borderObj.transform.SetParent(transform, false);

            RectTransform borderRect = borderObj.AddComponent<RectTransform>();
            // Position behind the health bar, slightly larger
            borderRect.anchorMin = Vector2.zero;
            borderRect.anchorMax = Vector2.one;
            borderRect.offsetMin = new Vector2(-4, -4);
            borderRect.offsetMax = new Vector2(4, 4);

            selectionBorder = borderObj.AddComponent<Image>();
            selectionBorder.color = selectedBorderColor;
            selectionBorder.enabled = false;

            // Move to back (behind health bar fill)
            borderObj.transform.SetAsFirstSibling();
        }

        /// <summary>
        /// Flash the health bar (for taking damage feedback)
        /// </summary>
        public void FlashDamage()
        {
            StartCoroutine(FlashEffect(Color.white, 0.1f));
        }

        private System.Collections.IEnumerator FlashEffect(Color flashColor, float duration)
        {
            if (healthBarFill == null) yield break;

            Color originalColor = healthBarFill.color;
            healthBarFill.color = flashColor;

            yield return new WaitForSeconds(duration);

            healthBarFill.color = GetHealthColor(_currentFillAmount);
        }

        #endregion
    }
}
