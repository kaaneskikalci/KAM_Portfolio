using System.Collections.Generic;
using KAM.Core.Combat;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KAM.UI.Combat
{
    /// <summary>
    /// Screen-space combat unit panel for HP, status effects, and enemy intent.
    /// Used to keep persistent combat data out of the 3D playfield.
    /// </summary>
    public class CombatUnitHUDPanel : MonoBehaviour
    {
        [Header("Theme")]
        [SerializeField] private Color panelColor = new Color(0.04f, 0.05f, 0.08f, 0.92f);
        [SerializeField] private Color borderColor = new Color(0.88f, 0.78f, 0.58f, 1f);
        [SerializeField] private Color healthColor = new Color(0.3f, 0.85f, 0.35f, 1f);
        [SerializeField] private Color damagedHealthColor = new Color(0.88f, 0.75f, 0.25f, 1f);
        [SerializeField] private Color criticalHealthColor = new Color(0.9f, 0.2f, 0.16f, 1f);
        [SerializeField] private Color textColor = Color.white;
        [SerializeField] private Color accentColor = new Color(1f, 0.84f, 0.05f, 1f);
        [SerializeField] private bool tintHealthFillByHealth = true;

        [Header("Sprites")]
        [SerializeField] private Sprite panelSprite;
        [SerializeField] private Sprite healthBarBackgroundSprite;
        [SerializeField] private Sprite healthFillSprite;
        [SerializeField] private Sprite buffChipSprite;
        [SerializeField] private Sprite debuffChipSprite;

        private CombatUnit _unit;
        private EnemyCombatUnit _enemyUnit;
        private CanvasGroup _canvasGroup;
        private TextMeshProUGUI _headerText;
        private TextMeshProUGUI _nameText;
        private TextMeshProUGUI _healthText;
        private TextMeshProUGUI _intentText;
        private Image _healthFill;
        private Transform _statusContainer;
        private readonly List<GameObject> _statusChips = new List<GameObject>();

        public CombatUnit BoundUnit => _unit;

        public static CombatUnitHUDPanel Create(Transform parent, string header, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPosition, Vector2 size)
        {
            GameObject panelObj = new GameObject($"{header}HUDPanel");
            panelObj.transform.SetParent(parent, false);

            CombatUnitHUDPanel panel = panelObj.AddComponent<CombatUnitHUDPanel>();
            panel.ApplyRect(anchorMin, anchorMax, pivot, anchoredPosition, size);
            panel.EnsureLayout(header);
            panel.SetVisible(false);
            return panel;
        }

        public static CombatUnitHUDPanel CreateFromPrefab(CombatUnitHUDPanel prefab, Transform parent, string header, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPosition, Vector2 size)
        {
            if (prefab == null)
            {
                return Create(parent, header, anchorMin, anchorMax, pivot, anchoredPosition, size);
            }

            CombatUnitHUDPanel panel = Instantiate(prefab, parent, false);
            panel.gameObject.name = $"{header}HUDPanel";
            panel.ApplyRect(anchorMin, anchorMax, pivot, anchoredPosition, size);

            try
            {
                panel.EnsureLayout(header);
                panel.SetVisible(false);
                return panel;
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[CombatUnitHUDPanel] Prefab HUD layout failed for {header}. Falling back to generated HUD. {ex.Message}");

                if (Application.isPlaying)
                {
                    Destroy(panel.gameObject);
                }
                else
                {
                    DestroyImmediate(panel.gameObject);
                }

                return Create(parent, header, anchorMin, anchorMax, pivot, anchoredPosition, size);
            }
        }

        [ContextMenu("Auto Setup HUD Panel")]
        public void AutoSetupHudPanel()
        {
            string header = IsValid(_headerText) && !string.IsNullOrWhiteSpace(_headerText.text)
                ? _headerText.text
                : gameObject.name.Replace("HUDPanel", "").Replace("Panel", "");

            if (string.IsNullOrWhiteSpace(header))
            {
                header = "HUD";
            }

            EnsureLayout(header);

            if (!Application.isPlaying && IsValid(_canvasGroup))
            {
                _canvasGroup.alpha = 1f;
                _canvasGroup.interactable = true;
                _canvasGroup.blocksRaycasts = true;
            }
        }

        public void Bind(CombatUnit unit)
        {
            string header = IsValid(_headerText) && !string.IsNullOrWhiteSpace(_headerText.text)
                ? _headerText.text
                : "HUD";
            EnsureLayout(header);

            if (_unit == unit)
            {
                Refresh();
                return;
            }

            Unsubscribe();
            _unit = unit;
            _enemyUnit = unit as EnemyCombatUnit;

            if (_unit == null)
            {
                SetVisible(false);
                return;
            }

            _unit.Stats.OnHealthChanged += HandleHealthChanged;
            _unit.OnEffectAdded += HandleEffectChanged;
            _unit.OnEffectRemoved += HandleEffectChanged;
            _unit.OnEffectsCleared += HandleEffectsCleared;
            _unit.OnDeath += HandleDeath;

            if (_enemyUnit != null)
            {
                _enemyUnit.OnIntentChanged += HandleIntentChanged;
            }

            SetVisible(true);
            Refresh();
        }

        public void Clear()
        {
            Unsubscribe();
            _unit = null;
            _enemyUnit = null;
            ClearStatusChips();
            SetVisible(false);
        }

        public void Refresh()
        {
            if (_unit == null)
            {
                SetVisible(false);
                return;
            }

            UpdateHealth(_unit.Stats.CurrentHealth, _unit.Stats.MaxHealth);
            UpdateStatusChips();
            UpdateIntent(_enemyUnit != null ? _enemyUnit.NextIntent : EnemyIntent.Unknown());

            if (IsValid(_nameText))
            {
                _nameText.text = _unit.UnitName;
            }
        }

        private void OnDestroy()
        {
            Unsubscribe();
        }

        private void BuildLayout(string header)
        {
            EnsureLayout(header);
        }

        private void EnsureLayout(string header)
        {
            GetOrAddRectTransform(gameObject);

            _canvasGroup = GetOrAddComponent<CanvasGroup>(gameObject);

            Image background = GetOrAddComponent<Image>(gameObject);
            background.color = panelColor;
            ApplySprite(background, panelSprite, Image.Type.Simple);

            Outline outline = GetOrAddComponent<Outline>(gameObject);
            outline.effectColor = borderColor;
            outline.effectDistance = new Vector2(2f, -2f);

            Transform content = FindOrCreateDirectChild(transform, "Content");
            GameObject contentObj = GetGameObject(content);
            if (contentObj == null)
            {
                return;
            }

            RectTransform contentRect = GetOrAddRectTransform(contentObj);
            contentRect.anchorMin = Vector2.zero;
            contentRect.anchorMax = Vector2.one;
            contentRect.offsetMin = new Vector2(12f, 8f);
            contentRect.offsetMax = new Vector2(-12f, -8f);

            _headerText = GetOrCreateText("Header", content, header.ToUpperInvariant(), 13f, FontStyles.Bold, accentColor);
            if (!IsValid(_headerText))
            {
                return;
            }
            RectTransform headerRect = _headerText.rectTransform;
            headerRect.anchorMin = new Vector2(0f, 1f);
            headerRect.anchorMax = new Vector2(1f, 1f);
            headerRect.pivot = new Vector2(0.5f, 1f);
            headerRect.anchoredPosition = Vector2.zero;
            headerRect.sizeDelta = new Vector2(0f, 18f);
            _headerText.alignment = TextAlignmentOptions.Left;

            _nameText = GetOrCreateText("Name", content, "-", 16f, FontStyles.Bold, textColor);
            if (!IsValid(_nameText))
            {
                return;
            }
            RectTransform nameRect = _nameText.rectTransform;
            nameRect.anchorMin = new Vector2(0f, 1f);
            nameRect.anchorMax = new Vector2(0.55f, 1f);
            nameRect.pivot = new Vector2(0f, 1f);
            nameRect.anchoredPosition = new Vector2(0f, -20f);
            nameRect.sizeDelta = new Vector2(0f, 22f);
            _nameText.alignment = TextAlignmentOptions.Left;

            _healthText = GetOrCreateText("HealthText", content, "0/0", 14f, FontStyles.Bold, textColor);
            if (!IsValid(_healthText))
            {
                return;
            }
            RectTransform hpTextRect = _healthText.rectTransform;
            hpTextRect.anchorMin = new Vector2(0.55f, 1f);
            hpTextRect.anchorMax = new Vector2(1f, 1f);
            hpTextRect.pivot = new Vector2(1f, 1f);
            hpTextRect.anchoredPosition = new Vector2(0f, -20f);
            hpTextRect.sizeDelta = new Vector2(0f, 22f);
            _healthText.alignment = TextAlignmentOptions.Right;

            Transform barBg = FindOrCreateDirectChild(content, "HealthBarBackground");
            GameObject barBgObj = GetGameObject(barBg);
            if (barBgObj == null)
            {
                return;
            }

            RectTransform barBgRect = GetOrAddRectTransform(barBgObj);
            barBgRect.anchorMin = new Vector2(0f, 0.42f);
            barBgRect.anchorMax = new Vector2(1f, 0.42f);
            barBgRect.pivot = new Vector2(0.5f, 0.5f);
            barBgRect.anchoredPosition = Vector2.zero;
            barBgRect.sizeDelta = new Vector2(0f, 12f);
            Image barBgImage = GetOrAddComponent<Image>(barBgObj);
            if (!IsValid(barBgImage))
            {
                return;
            }
            barBgImage.color = new Color(0f, 0f, 0f, 0.62f);
            ApplySprite(barBgImage, healthBarBackgroundSprite, Image.Type.Simple);

            Transform fill = FindOrCreateDirectChild(barBg, "HealthFill");
            GameObject fillObj = GetGameObject(fill);
            if (fillObj == null)
            {
                return;
            }

            RectTransform fillRect = GetOrAddRectTransform(fillObj);
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = new Vector2(2f, 2f);
            fillRect.offsetMax = new Vector2(-2f, -2f);
            _healthFill = GetOrAddComponent<Image>(fillObj);
            if (!IsValid(_healthFill))
            {
                return;
            }
            _healthFill.color = healthColor;
            if (healthFillSprite != null)
            {
                _healthFill.sprite = healthFillSprite;
            }
            _healthFill.type = Image.Type.Filled;
            _healthFill.fillMethod = Image.FillMethod.Horizontal;
            _healthFill.fillOrigin = (int)Image.OriginHorizontal.Left;

            Transform status = FindOrCreateDirectChild(content, "StatusEffects");
            GameObject statusObj = GetGameObject(status);
            if (statusObj == null)
            {
                return;
            }

            RectTransform statusRect = GetOrAddRectTransform(statusObj);
            statusRect.anchorMin = new Vector2(0f, 0f);
            statusRect.anchorMax = new Vector2(0.62f, 0f);
            statusRect.pivot = new Vector2(0f, 0f);
            statusRect.anchoredPosition = Vector2.zero;
            statusRect.sizeDelta = new Vector2(0f, 22f);
            HorizontalLayoutGroup statusLayout = GetOrAddComponent<HorizontalLayoutGroup>(statusObj);
            if (!IsValid(statusLayout))
            {
                return;
            }
            statusLayout.spacing = 4f;
            statusLayout.childControlWidth = false;
            statusLayout.childControlHeight = true;
            statusLayout.childForceExpandWidth = false;
            statusLayout.childForceExpandHeight = true;
            _statusContainer = status;

            _intentText = GetOrCreateText("Intent", content, "", 12f, FontStyles.Bold, accentColor);
            if (!IsValid(_intentText))
            {
                return;
            }
            RectTransform intentRect = _intentText.rectTransform;
            intentRect.anchorMin = new Vector2(0.62f, 0f);
            intentRect.anchorMax = new Vector2(1f, 0f);
            intentRect.pivot = new Vector2(1f, 0f);
            intentRect.anchoredPosition = Vector2.zero;
            intentRect.sizeDelta = new Vector2(0f, 22f);
            _intentText.alignment = TextAlignmentOptions.Right;

            _headerText.text = header.ToUpperInvariant();
        }

        private TextMeshProUGUI CreateText(string name, Transform parent, string text, float fontSize, FontStyles style, Color color)
        {
            if (!IsValidTransform(parent))
            {
                return null;
            }

            GameObject textObj = new GameObject(name);
            textObj.transform.SetParent(parent, false);
            TextMeshProUGUI label = textObj.AddComponent<TextMeshProUGUI>();
            label.text = text;
            label.fontSize = fontSize;
            label.fontStyle = style;
            label.color = color;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Ellipsis;
            return label;
        }

        private TextMeshProUGUI GetOrCreateText(string name, Transform parent, string text, float fontSize, FontStyles style, Color color)
        {
            Transform existing = FindDirectChild(parent, name);
            GameObject existingObj = GetGameObject(existing);
            TextMeshProUGUI label = existingObj != null ? existingObj.GetComponent<TextMeshProUGUI>() : null;

            if (IsValid(label))
            {
                return label;
            }

            return CreateText(name, parent, text, fontSize, style, color);
        }

        private void ApplyRect(Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPosition, Vector2 size)
        {
            RectTransform rect = GetOrAddRectTransform(gameObject);
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
        }

        private static Transform FindOrCreateDirectChild(Transform parent, string childName)
        {
            Transform existing = FindDirectChild(parent, childName);
            if (IsValidTransform(existing))
            {
                return existing;
            }

            if (!IsValidTransform(parent))
            {
                return null;
            }

            GameObject child = new GameObject(childName);
            child.transform.SetParent(parent, false);
            return child.transform;
        }

        private static Transform FindDirectChild(Transform parent, string childName)
        {
            if (!IsValidTransform(parent))
            {
                return null;
            }

            try
            {
                for (int i = parent.childCount - 1; i >= 0; i--)
                {
                    Transform child = parent.GetChild(i);
                    if (IsValidTransform(child) && child.name == childName)
                    {
                        return child;
                    }
                }
            }
            catch (MissingReferenceException)
            {
                return null;
            }
            catch (System.NullReferenceException)
            {
                return null;
            }

            return null;
        }

        private static bool IsValidTransform(Transform transform)
        {
            return GetGameObject(transform) != null;
        }

        private static GameObject GetGameObject(Transform transform)
        {
            try
            {
                return transform != null ? transform.gameObject : null;
            }
            catch (MissingReferenceException)
            {
                return null;
            }
            catch (System.NullReferenceException)
            {
                return null;
            }
        }

        private static RectTransform GetOrAddRectTransform(GameObject target)
        {
            return GetOrAddComponent<RectTransform>(target);
        }

        private static T GetOrAddComponent<T>(GameObject target) where T : Component
        {
            if (!IsValid(target))
            {
                return null;
            }

            T component = target.GetComponent<T>();
            if (!IsValid(component))
            {
                component = target.AddComponent<T>();
            }

            return component;
        }

        private static bool IsValid(UnityEngine.Object obj)
        {
            try
            {
                if (obj == null)
                {
                    return false;
                }

                _ = obj.name;
                return true;
            }
            catch (MissingReferenceException)
            {
                return false;
            }
            catch (System.NullReferenceException)
            {
                return false;
            }
        }

        private void UpdateHealth(int current, int max)
        {
            float percent = max > 0 ? Mathf.Clamp01((float)current / max) : 0f;

            if (IsValid(_healthFill))
            {
                _healthFill.fillAmount = percent;
                if (tintHealthFillByHealth)
                {
                    _healthFill.color = percent <= 0.25f ? criticalHealthColor : percent <= 0.5f ? damagedHealthColor : healthColor;
                }
            }

            if (IsValid(_healthText))
            {
                _healthText.text = $"{current}/{max}";
            }
        }

        private void UpdateStatusChips()
        {
            ClearStatusChips();

            if (_unit == null || !IsValid(_statusContainer))
            {
                return;
            }

            List<StatusEffect> effects = _unit.GetActiveEffects();
            int count = Mathf.Min(effects.Count, 6);

            for (int i = 0; i < count; i++)
            {
                CreateStatusChip(effects[i]);
            }
        }

        private void CreateStatusChip(StatusEffect effect)
        {
            GameObject chipObj = new GameObject(effect.EffectName);
            chipObj.transform.SetParent(_statusContainer, false);
            RectTransform rect = chipObj.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(58f, 18f);

            Image bg = chipObj.AddComponent<Image>();
            Sprite chipSprite = effect.EffectType == StatusEffectType.Buff ? buffChipSprite : debuffChipSprite;
            ApplySprite(bg, chipSprite, Image.Type.Simple);
            bg.color = effect.EffectType == StatusEffectType.Buff
                ? new Color(0.12f, 0.36f, 0.2f, 0.9f)
                : new Color(0.42f, 0.12f, 0.14f, 0.9f);

            TextMeshProUGUI text = CreateText("Label", chipObj.transform, FormatEffectLabel(effect), 9f, FontStyles.Bold, Color.white);
            RectTransform textRect = text.rectTransform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(3f, 0f);
            textRect.offsetMax = new Vector2(-3f, 0f);
            text.alignment = TextAlignmentOptions.Center;

            _statusChips.Add(chipObj);
        }

        private static void ApplySprite(Image image, Sprite sprite, Image.Type defaultType)
        {
            if (!IsValid(image) || sprite == null)
            {
                return;
            }

            image.sprite = sprite;
            image.type = HasSlicedBorder(sprite) ? Image.Type.Sliced : defaultType;
        }

        private static bool HasSlicedBorder(Sprite sprite)
        {
            return sprite != null && sprite.border.sqrMagnitude > 0.001f;
        }

        private string FormatEffectLabel(StatusEffect effect)
        {
            string shortName = effect.EffectName.Length > 7 ? effect.EffectName.Substring(0, 7) : effect.EffectName;
            return effect.IsPermanent ? shortName : $"{shortName} {effect.TurnsRemaining}";
        }

        private void UpdateIntent(EnemyIntent intent)
        {
            if (!IsValid(_intentText))
            {
                return;
            }

            if (_enemyUnit == null || intent.ActionType == EnemyIntentType.Unknown)
            {
                _intentText.text = "";
                return;
            }

            if (intent.DealsDamage)
            {
                _intentText.text = intent.HitCount > 1
                    ? $"Intent: {intent.DamageAmount}x{intent.HitCount}"
                    : $"Intent: {intent.DamageAmount}";
            }
            else
            {
                _intentText.text = string.IsNullOrWhiteSpace(intent.Description)
                    ? $"Intent: {intent.ActionType}"
                    : $"Intent: {intent.Description}";
            }
        }

        private void ClearStatusChips()
        {
            foreach (GameObject chip in _statusChips)
            {
                if (IsValid(chip))
                {
                    if (Application.isPlaying)
                    {
                        Destroy(chip);
                    }
                    else
                    {
                        DestroyImmediate(chip);
                    }
                }
            }
            _statusChips.Clear();
        }

        private void HandleHealthChanged(int current, int max)
        {
            UpdateHealth(current, max);
        }

        private void HandleEffectChanged(StatusEffect effect)
        {
            UpdateStatusChips();
        }

        private void HandleEffectsCleared()
        {
            ClearStatusChips();
        }

        private void HandleIntentChanged(EnemyIntent intent)
        {
            UpdateIntent(intent);
        }

        private void HandleDeath(CombatUnit unit)
        {
            SetVisible(false);
        }

        private void SetVisible(bool visible)
        {
            if (!IsValid(_canvasGroup))
            {
                _canvasGroup = GetOrAddComponent<CanvasGroup>(gameObject);
            }

            _canvasGroup.alpha = visible ? 1f : 0f;
            _canvasGroup.interactable = visible;
            _canvasGroup.blocksRaycasts = false;
        }

        private void Unsubscribe()
        {
            if (_unit != null)
            {
                _unit.Stats.OnHealthChanged -= HandleHealthChanged;
                _unit.OnEffectAdded -= HandleEffectChanged;
                _unit.OnEffectRemoved -= HandleEffectChanged;
                _unit.OnEffectsCleared -= HandleEffectsCleared;
                _unit.OnDeath -= HandleDeath;
            }

            if (_enemyUnit != null)
            {
                _enemyUnit.OnIntentChanged -= HandleIntentChanged;
            }
        }
    }
}
