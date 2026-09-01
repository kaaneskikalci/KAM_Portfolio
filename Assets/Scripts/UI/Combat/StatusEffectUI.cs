using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using KAM.Core.Combat;

namespace KAM.UI.Combat
{
    /// <summary>
    /// Displays status effect icons for a combat unit.
    /// Shows as a row of icons above or below the health bar.
    /// </summary>
    public class StatusEffectUI : MonoBehaviour
    {
        [Header("UI Settings")]
        [SerializeField] private Transform iconContainer;
        [SerializeField] private GameObject iconPrefab;
        [SerializeField] private float iconSize = 24f;
        [SerializeField] private float iconSpacing = 4f;
        [SerializeField] private int maxVisibleIcons = 8;

        [Header("Position")]
        [SerializeField] private Vector3 offset = new Vector3(0f, 0.3f, 0f);

        [Header("Tooltip")]
        [SerializeField] private bool showTooltipOnHover = true;

        // Runtime
        private CombatUnit _unit;
        private List<StatusEffectIcon> _icons = new List<StatusEffectIcon>();
        private Dictionary<StatusEffect, StatusEffectIcon> _effectToIcon = new Dictionary<StatusEffect, StatusEffectIcon>();

        /// <summary>
        /// Initialize for a combat unit.
        /// </summary>
        public void Initialize(CombatUnit unit)
        {
            _unit = unit;

            // Create icon container if not assigned
            if (iconContainer == null)
            {
                CreateIconContainer();
            }

            // Subscribe to effect events
            if (_unit != null)
            {
                _unit.OnEffectAdded += HandleEffectAdded;
                _unit.OnEffectRemoved += HandleEffectRemoved;
                _unit.OnEffectsCleared += HandleEffectsCleared;
            }

            // Show existing effects
            RefreshEffects();
        }

        private void OnDestroy()
        {
            if (_unit != null)
            {
                _unit.OnEffectAdded -= HandleEffectAdded;
                _unit.OnEffectRemoved -= HandleEffectRemoved;
                _unit.OnEffectsCleared -= HandleEffectsCleared;
            }

            ClearAllIcons();
        }

        private void CreateIconContainer()
        {
            GameObject containerObj = new GameObject("StatusIconContainer");
            containerObj.transform.SetParent(transform, false);

            RectTransform containerRect = containerObj.AddComponent<RectTransform>();
            containerRect.anchoredPosition = offset;

            HorizontalLayoutGroup layout = containerObj.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = iconSpacing;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            iconContainer = containerObj.transform;
        }

        /// <summary>
        /// Refresh the display to match current effects.
        /// </summary>
        public void RefreshEffects()
        {
            if (_unit == null) return;

            ClearAllIcons();

            var effects = _unit.GetActiveEffects();
            int displayCount = Mathf.Min(effects.Count, maxVisibleIcons);

            for (int i = 0; i < displayCount; i++)
            {
                CreateIconForEffect(effects[i]);
            }
        }

        private void HandleEffectAdded(StatusEffect effect)
        {
            if (_effectToIcon.ContainsKey(effect))
            {
                // Effect already has an icon, just update it
                _effectToIcon[effect].UpdateDisplay(effect);
            }
            else if (_icons.Count < maxVisibleIcons)
            {
                CreateIconForEffect(effect);
            }
        }

        private void HandleEffectRemoved(StatusEffect effect)
        {
            if (_effectToIcon.TryGetValue(effect, out StatusEffectIcon icon))
            {
                _icons.Remove(icon);
                _effectToIcon.Remove(effect);
                Destroy(icon.gameObject);
            }
        }

        private void HandleEffectsCleared()
        {
            ClearAllIcons();
        }

        private void CreateIconForEffect(StatusEffect effect)
        {
            GameObject iconObj;

            if (iconPrefab != null)
            {
                iconObj = Instantiate(iconPrefab, iconContainer);
            }
            else
            {
                iconObj = CreateDefaultIcon();
            }

            StatusEffectIcon icon = iconObj.GetComponent<StatusEffectIcon>();
            if (icon == null)
            {
                icon = iconObj.AddComponent<StatusEffectIcon>();
            }

            icon.Initialize(effect, showTooltipOnHover);
            _icons.Add(icon);
            _effectToIcon[effect] = icon;
        }

        private GameObject CreateDefaultIcon()
        {
            GameObject iconObj = new GameObject("EffectIcon");
            iconObj.transform.SetParent(iconContainer, false);

            RectTransform iconRect = iconObj.AddComponent<RectTransform>();
            iconRect.sizeDelta = new Vector2(iconSize, iconSize);

            // Background
            Image bgImage = iconObj.AddComponent<Image>();
            bgImage.color = new Color(0, 0, 0, 0.5f);

            // Icon image
            GameObject iconImgObj = new GameObject("Icon");
            iconImgObj.transform.SetParent(iconObj.transform, false);
            RectTransform imgRect = iconImgObj.AddComponent<RectTransform>();
            imgRect.anchorMin = Vector2.zero;
            imgRect.anchorMax = Vector2.one;
            imgRect.offsetMin = new Vector2(2, 2);
            imgRect.offsetMax = new Vector2(-2, -2);
            Image iconImg = iconImgObj.AddComponent<Image>();

            // Duration text
            GameObject durationObj = new GameObject("Duration");
            durationObj.transform.SetParent(iconObj.transform, false);
            RectTransform durRect = durationObj.AddComponent<RectTransform>();
            durRect.anchorMin = new Vector2(0.5f, 0);
            durRect.anchorMax = new Vector2(1, 0.5f);
            durRect.offsetMin = Vector2.zero;
            durRect.offsetMax = Vector2.zero;
            TextMeshProUGUI durText = durationObj.AddComponent<TextMeshProUGUI>();
            durText.fontSize = 10;
            durText.fontStyle = FontStyles.Bold;
            durText.alignment = TextAlignmentOptions.BottomRight;

            return iconObj;
        }

        private void ClearAllIcons()
        {
            foreach (var icon in _icons)
            {
                if (icon != null)
                    Destroy(icon.gameObject);
            }
            _icons.Clear();
            _effectToIcon.Clear();
        }

        #region Factory

        /// <summary>
        /// Create status effect UI for a unit.
        /// </summary>
        public static StatusEffectUI CreateForUnit(CombatUnit unit, Transform parent = null)
        {
            GameObject obj = new GameObject($"StatusEffectUI_{unit.UnitName}");

            if (parent != null)
            {
                obj.transform.SetParent(parent, false);
            }
            else
            {
                obj.transform.position = unit.transform.position + new Vector3(0, 2.8f, 0);
            }

            // Add Canvas for world-space rendering
            Canvas canvas = obj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 11;

            RectTransform canvasRect = obj.GetComponent<RectTransform>();
            canvasRect.sizeDelta = new Vector2(200, 30);
            canvasRect.localScale = new Vector3(0.01f, 0.01f, 0.01f);

            StatusEffectUI ui = obj.AddComponent<StatusEffectUI>();
            ui.Initialize(unit);

            return ui;
        }

        #endregion
    }

    /// <summary>
    /// Individual status effect icon display.
    /// </summary>
    public class StatusEffectIcon : MonoBehaviour
    {
        [SerializeField] private Image iconImage;
        [SerializeField] private Image backgroundImage;
        [SerializeField] private TextMeshProUGUI durationText;

        private StatusEffect _effect;
        private bool _showTooltip;

        public void Initialize(StatusEffect effect, bool showTooltip = true)
        {
            _effect = effect;
            _showTooltip = showTooltip;

            // Find components if not assigned
            if (iconImage == null)
                iconImage = transform.Find("Icon")?.GetComponent<Image>();
            if (backgroundImage == null)
                backgroundImage = GetComponent<Image>();
            if (durationText == null)
                durationText = transform.Find("Duration")?.GetComponent<TextMeshProUGUI>();

            UpdateDisplay(effect);

            // Subscribe to effect events
            effect.OnEffectExpired += HandleExpired;
        }

        private void OnDestroy()
        {
            if (_effect != null)
            {
                _effect.OnEffectExpired -= HandleExpired;
            }
        }

        public void UpdateDisplay(StatusEffect effect)
        {
            _effect = effect;

            // Update icon
            if (iconImage != null)
            {
                if (effect.Icon != null)
                {
                    iconImage.sprite = effect.Icon;
                    iconImage.color = Color.white;
                }
                else
                {
                    // Use effect color as placeholder
                    iconImage.sprite = null;
                    iconImage.color = effect.EffectColor;
                }
            }

            // Update background color based on effect type
            if (backgroundImage != null)
            {
                switch (effect.EffectType)
                {
                    case StatusEffectType.Buff:
                        backgroundImage.color = new Color(0.2f, 0.4f, 0.2f, 0.8f);
                        break;
                    case StatusEffectType.Debuff:
                        backgroundImage.color = new Color(0.4f, 0.2f, 0.2f, 0.8f);
                        break;
                    default:
                        backgroundImage.color = new Color(0.3f, 0.3f, 0.3f, 0.8f);
                        break;
                }
            }

            // Update duration
            if (durationText != null)
            {
                if (effect.IsPermanent)
                {
                    durationText.text = "";
                }
                else
                {
                    durationText.text = effect.TurnsRemaining.ToString();
                }
            }
        }

        private void HandleExpired(StatusEffect effect)
        {
            // Icon will be removed by StatusEffectUI
        }

        // Tooltip on hover (could be expanded)
        private void OnMouseEnter()
        {
            if (_showTooltip && _effect != null)
            {
                // Show tooltip
                Debug.Log($"[StatusEffectIcon] {_effect.EffectName}: {_effect.Description}");
            }
        }
    }
}
