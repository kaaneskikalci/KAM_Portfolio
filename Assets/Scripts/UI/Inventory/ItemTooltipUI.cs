using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace KAM.Inventory.UI
{
    /// <summary>
    /// Tooltip popup that shows item details on hover.
    /// </summary>
    public class ItemTooltipUI : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private RectTransform tooltipPanel;
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private TextMeshProUGUI itemNameText;
        [SerializeField] private TextMeshProUGUI itemTypeText;
        [SerializeField] private TextMeshProUGUI descriptionText;
        [SerializeField] private TextMeshProUGUI effectText;
        [SerializeField] private TextMeshProUGUI hintText;
        [SerializeField] private Image itemIcon;

        [Header("Settings")]
        [SerializeField] private Vector2 offset = new Vector2(10, -10);
        [SerializeField] private float fadeSpeed = 10f;
        [SerializeField] private bool followMouse = true;

        private Canvas _canvas;
        private RectTransform _canvasRect;
        private bool _isVisible;
        private float _targetAlpha;

        private void Awake()
        {
            _canvas = GetComponentInParent<Canvas>();
            if (_canvas != null)
                _canvasRect = _canvas.GetComponent<RectTransform>();

            if (tooltipPanel == null)
                tooltipPanel = GetComponent<RectTransform>();

            if (canvasGroup == null)
                canvasGroup = GetComponent<CanvasGroup>();

            ConfigureAsNonBlockingOverlay();
            Hide();
        }

        private void OnEnable()
        {
            ConfigureAsNonBlockingOverlay();
        }

        private void Update()
        {
            // Fade animation
            if (canvasGroup != null)
            {
                canvasGroup.alpha = Mathf.Lerp(canvasGroup.alpha, _targetAlpha, Time.unscaledDeltaTime * fadeSpeed);
            }

            // Follow mouse
            if (_isVisible && followMouse)
            {
                UpdatePosition();
            }
        }

        /// <summary>
        /// Show tooltip for an item.
        /// </summary>
        public void Show(ItemData item)
        {
            if (item == null)
            {
                Hide();
                return;
            }

            _isVisible = true;
            _targetAlpha = 1f;
            gameObject.SetActive(true);
            ConfigureAsNonBlockingOverlay();

            // Set item name with rarity color
            if (itemNameText != null)
            {
                itemNameText.text = item.GetColoredName();
            }

            // Set item type
            if (itemTypeText != null)
            {
                itemTypeText.text = $"{item.Rarity} {item.Type}";
                itemTypeText.color = item.GetRarityColor();
            }

            // Set icon
            if (itemIcon != null)
            {
                if (item.Icon != null)
                {
                    itemIcon.sprite = item.Icon;
                    itemIcon.color = Color.white;
                    itemIcon.enabled = true;
                }
                else
                {
                    itemIcon.enabled = false;
                }
            }

            // Set description
            if (descriptionText != null)
            {
                descriptionText.text = item.Description;
            }

            // Set effect text (for potions/talismans)
            if (effectText != null)
            {
                string effect = GetEffectText(item);
                if (!string.IsNullOrEmpty(effect))
                {
                    effectText.gameObject.SetActive(true);
                    effectText.text = effect;
                }
                else
                {
                    effectText.gameObject.SetActive(false);
                }
            }

            // Set hint text
            if (hintText != null)
            {
                string hint = GetHintText(item);
                if (!string.IsNullOrEmpty(hint))
                {
                    hintText.gameObject.SetActive(true);
                    hintText.text = hint;
                }
                else
                {
                    hintText.gameObject.SetActive(false);
                }
            }

            // Force layout rebuild
            LayoutRebuilder.ForceRebuildLayoutImmediate(tooltipPanel);

            UpdatePosition();
        }

        /// <summary>
        /// Hide the tooltip.
        /// </summary>
        public void Hide()
        {
            _isVisible = false;
            _targetAlpha = 0f;

            if (canvasGroup != null && canvasGroup.alpha <= 0.01f)
            {
                gameObject.SetActive(false);
            }
        }

        private void ConfigureAsNonBlockingOverlay()
        {
            if (canvasGroup == null)
            {
                canvasGroup = GetComponent<CanvasGroup>();
                if (canvasGroup == null)
                    canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }

            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;

            GraphicRaycaster raycaster = GetComponent<GraphicRaycaster>();
            if (raycaster != null)
                raycaster.enabled = false;

            Graphic[] graphics = GetComponentsInChildren<Graphic>(true);
            for (int i = 0; i < graphics.Length; i++)
            {
                graphics[i].raycastTarget = false;
            }
        }

        private void UpdatePosition()
        {
            if (tooltipPanel == null) return;

            Vector2 mousePos = Input.mousePosition;
            Vector2 tooltipPos = mousePos + offset;

            // Keep tooltip on screen
            Vector2 tooltipSize = tooltipPanel.sizeDelta;

            // Right edge
            if (tooltipPos.x + tooltipSize.x > Screen.width)
            {
                tooltipPos.x = mousePos.x - tooltipSize.x - offset.x;
            }

            // Top edge
            if (tooltipPos.y > Screen.height)
            {
                tooltipPos.y = Screen.height;
            }

            // Bottom edge
            if (tooltipPos.y - tooltipSize.y < 0)
            {
                tooltipPos.y = tooltipSize.y;
            }

            // Left edge
            if (tooltipPos.x < 0)
            {
                tooltipPos.x = 0;
            }

            tooltipPanel.position = tooltipPos;
        }

        private string GetEffectText(ItemData item)
        {
            if (item is PotionData potion)
            {
                return potion.GetEffectDescription();
            }
            else if (item is TalismanData talisman)
            {
                return talisman.GetStatsDescription();
            }
            else if (item is MaterialData material)
            {
                return $"Crafting Potency: {material.CraftingPotency}";
            }

            return "";
        }

        private string GetHintText(ItemData item)
        {
            if (item.IsConsumable)
            {
                return "<color=#AAAAAA>[E] Use</color>";
            }
            else if (item is TalismanData)
            {
                return "<color=#AAAAAA>[E] Equip</color>";
            }
            else if (item is MaterialData)
            {
                return "<color=#AAAAAA>Crafting Material</color>";
            }

            return "";
        }

        /// <summary>
        /// Create tooltip UI elements programmatically.
        /// </summary>
        public void CreateUIElements()
        {
            // Main panel
            if (tooltipPanel == null)
            {
                tooltipPanel = gameObject.GetComponent<RectTransform>();
                if (tooltipPanel == null)
                    tooltipPanel = gameObject.AddComponent<RectTransform>();
            }

            tooltipPanel.pivot = new Vector2(0, 1);
            tooltipPanel.sizeDelta = new Vector2(250, 150);

            // Canvas group for fading
            if (canvasGroup == null)
            {
                canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;

            // Background
            Image bg = gameObject.GetComponent<Image>();
            if (bg == null)
            {
                bg = gameObject.AddComponent<Image>();
            }
            bg.color = new Color(0.1f, 0.1f, 0.1f, 0.95f);

            // Vertical layout
            VerticalLayoutGroup layout = gameObject.GetComponent<VerticalLayoutGroup>();
            if (layout == null)
            {
                layout = gameObject.AddComponent<VerticalLayoutGroup>();
            }
            layout.padding = new RectOffset(10, 10, 10, 10);
            layout.spacing = 5;
            layout.childControlHeight = false;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;

            // Content size fitter
            ContentSizeFitter fitter = gameObject.GetComponent<ContentSizeFitter>();
            if (fitter == null)
            {
                fitter = gameObject.AddComponent<ContentSizeFitter>();
            }
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // Item name
            if (itemNameText == null)
            {
                GameObject nameObj = new GameObject("ItemName");
                nameObj.transform.SetParent(transform, false);

                itemNameText = nameObj.AddComponent<TextMeshProUGUI>();
                itemNameText.fontSize = 18;
                itemNameText.fontStyle = FontStyles.Bold;
                itemNameText.richText = true;

                LayoutElement nameLayout = nameObj.AddComponent<LayoutElement>();
                nameLayout.preferredHeight = 25;
            }

            // Item type
            if (itemTypeText == null)
            {
                GameObject typeObj = new GameObject("ItemType");
                typeObj.transform.SetParent(transform, false);

                itemTypeText = typeObj.AddComponent<TextMeshProUGUI>();
                itemTypeText.fontSize = 12;
                itemTypeText.fontStyle = FontStyles.Italic;

                LayoutElement typeLayout = typeObj.AddComponent<LayoutElement>();
                typeLayout.preferredHeight = 18;
            }

            // Description
            if (descriptionText == null)
            {
                GameObject descObj = new GameObject("Description");
                descObj.transform.SetParent(transform, false);

                descriptionText = descObj.AddComponent<TextMeshProUGUI>();
                descriptionText.fontSize = 14;
                descriptionText.color = new Color(0.8f, 0.8f, 0.8f);

                LayoutElement descLayout = descObj.AddComponent<LayoutElement>();
                descLayout.preferredHeight = 40;
            }

            // Effect text
            if (effectText == null)
            {
                GameObject effectObj = new GameObject("Effect");
                effectObj.transform.SetParent(transform, false);

                effectText = effectObj.AddComponent<TextMeshProUGUI>();
                effectText.fontSize = 13;
                effectText.color = new Color(0.6f, 1f, 0.6f);
                effectText.richText = true;

                LayoutElement effectLayout = effectObj.AddComponent<LayoutElement>();
                effectLayout.preferredHeight = 30;
            }

            // Hint text
            if (hintText == null)
            {
                GameObject hintObj = new GameObject("Hint");
                hintObj.transform.SetParent(transform, false);

                hintText = hintObj.AddComponent<TextMeshProUGUI>();
                hintText.fontSize = 12;
                hintText.alignment = TextAlignmentOptions.Right;
                hintText.richText = true;

                LayoutElement hintLayout = hintObj.AddComponent<LayoutElement>();
                hintLayout.preferredHeight = 20;
            }

            ConfigureAsNonBlockingOverlay();
        }
    }
}
