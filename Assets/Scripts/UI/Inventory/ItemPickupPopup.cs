using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace KAM.Inventory.UI
{
    /// <summary>
    /// Shows brief popup notifications when items are collected.
    /// Stacks multiple pickups and animates them in sequence.
    /// </summary>
    public class ItemPickupPopup : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private RectTransform popupContainer;
        [SerializeField] private GameObject popupPrefab;

        [Header("Settings")]
        [SerializeField] private float displayDuration = 2.5f;
        [SerializeField] private float slideInDuration = 0.3f;
        [SerializeField] private float fadeOutDuration = 0.5f;
        [SerializeField] private int maxVisiblePopups = 5;
        [SerializeField] private float popupSpacing = 45f;

        [Header("Animation")]
        [SerializeField] private float slideDistance = 200f;

        private Queue<PopupData> _pendingPopups = new Queue<PopupData>();
        private List<ActivePopup> _activePopups = new List<ActivePopup>();
        private InventoryManager _inventory;

        private struct PopupData
        {
            public ItemData item;
            public int quantity;
        }

        private class ActivePopup
        {
            public GameObject gameObject;
            public RectTransform rectTransform;
            public CanvasGroup canvasGroup;
            public Image icon;
            public TextMeshProUGUI nameText;
            public TextMeshProUGUI quantityText;
            public float timeRemaining;
            public int targetIndex;
        }

        private void Start()
        {
            _inventory = InventoryManager.Instance;

            if (_inventory != null)
            {
                _inventory.OnItemAdded += OnItemAdded;
            }

            // Also listen for pickups
            ItemPickup.OnItemPickedUp += OnItemPickedUp;
        }

        private void OnDestroy()
        {
            if (_inventory != null)
            {
                _inventory.OnItemAdded -= OnItemAdded;
            }

            ItemPickup.OnItemPickedUp -= OnItemPickedUp;
        }

        private void Update()
        {
            // Update active popups
            for (int i = _activePopups.Count - 1; i >= 0; i--)
            {
                var popup = _activePopups[i];
                popup.timeRemaining -= Time.deltaTime;

                // Fade out when time is low
                if (popup.timeRemaining < fadeOutDuration)
                {
                    float alpha = popup.timeRemaining / fadeOutDuration;
                    if (popup.canvasGroup != null)
                        popup.canvasGroup.alpha = alpha;
                }

                // Remove when done
                if (popup.timeRemaining <= 0)
                {
                    Destroy(popup.gameObject);
                    _activePopups.RemoveAt(i);
                }
            }

            // Move popups to their target positions
            for (int i = 0; i < _activePopups.Count; i++)
            {
                var popup = _activePopups[i];
                popup.targetIndex = i;

                Vector2 targetPos = new Vector2(0, -i * popupSpacing);
                popup.rectTransform.anchoredPosition = Vector2.Lerp(
                    popup.rectTransform.anchoredPosition,
                    targetPos,
                    Time.deltaTime * 10f
                );
            }

            // Process pending popups
            if (_pendingPopups.Count > 0 && _activePopups.Count < maxVisiblePopups)
            {
                var data = _pendingPopups.Dequeue();
                ShowPopup(data.item, data.quantity);
            }
        }

        private void OnItemAdded(ItemData item, int quantity)
        {
            QueuePopup(item, quantity);
        }

        private void OnItemPickedUp(ItemPickup pickup)
        {
            // Already handled by OnItemAdded, but could add extra effects here
        }

        /// <summary>
        /// Queue a popup to be shown.
        /// </summary>
        public void QueuePopup(ItemData item, int quantity)
        {
            // Check if we can merge with an existing popup
            foreach (var popup in _activePopups)
            {
                if (popup.nameText != null && popup.nameText.text.Contains(item.ItemName))
                {
                    // Update quantity
                    // (This is a simple approach - could be improved)
                    popup.timeRemaining = displayDuration;
                    return;
                }
            }

            _pendingPopups.Enqueue(new PopupData { item = item, quantity = quantity });
        }

        private void ShowPopup(ItemData item, int quantity)
        {
            GameObject popupObj;

            if (popupPrefab != null)
            {
                popupObj = Instantiate(popupPrefab, popupContainer);
            }
            else
            {
                popupObj = CreatePopupObject();
            }

            // Setup popup
            ActivePopup popup = new ActivePopup
            {
                gameObject = popupObj,
                rectTransform = popupObj.GetComponent<RectTransform>(),
                canvasGroup = popupObj.GetComponent<CanvasGroup>(),
                timeRemaining = displayDuration,
                targetIndex = _activePopups.Count
            };

            // Find components
            popup.icon = popupObj.transform.Find("Icon")?.GetComponent<Image>();
            popup.nameText = popupObj.transform.Find("ItemName")?.GetComponent<TextMeshProUGUI>();
            popup.quantityText = popupObj.transform.Find("Quantity")?.GetComponent<TextMeshProUGUI>();

            // Set content
            if (popup.icon != null)
            {
                if (item.Icon != null)
                {
                    popup.icon.sprite = item.Icon;
                    popup.icon.color = Color.white;
                }
                else
                {
                    popup.icon.color = item.GetRarityColor();
                }
            }

            if (popup.nameText != null)
            {
                popup.nameText.text = item.ItemName;
                popup.nameText.color = item.GetRarityColor();
            }

            if (popup.quantityText != null)
            {
                popup.quantityText.text = quantity > 1 ? $"x{quantity}" : "";
            }

            // Start off-screen
            popup.rectTransform.anchoredPosition = new Vector2(slideDistance, -_activePopups.Count * popupSpacing);

            // Add to list
            _activePopups.Add(popup);

            // Animate in
            StartCoroutine(SlideIn(popup));
        }

        private IEnumerator SlideIn(ActivePopup popup)
        {
            if (popup.canvasGroup != null)
                popup.canvasGroup.alpha = 0f;

            float elapsed = 0f;
            Vector2 startPos = popup.rectTransform.anchoredPosition;
            Vector2 endPos = new Vector2(0, startPos.y);

            while (elapsed < slideInDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / slideInDuration;

                // Ease out
                t = 1f - Mathf.Pow(1f - t, 3f);

                popup.rectTransform.anchoredPosition = Vector2.Lerp(startPos, endPos, t);

                if (popup.canvasGroup != null)
                    popup.canvasGroup.alpha = t;

                yield return null;
            }

            popup.rectTransform.anchoredPosition = endPos;
            if (popup.canvasGroup != null)
                popup.canvasGroup.alpha = 1f;
        }

        private GameObject CreatePopupObject()
        {
            GameObject popup = new GameObject("Popup");
            popup.transform.SetParent(popupContainer, false);

            RectTransform rect = popup.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(200, 40);
            rect.anchorMin = new Vector2(1, 1);
            rect.anchorMax = new Vector2(1, 1);
            rect.pivot = new Vector2(1, 1);

            CanvasGroup cg = popup.AddComponent<CanvasGroup>();

            // Background
            Image bg = popup.AddComponent<Image>();
            bg.color = new Color(0.1f, 0.1f, 0.1f, 0.85f);

            // Horizontal layout
            HorizontalLayoutGroup layout = popup.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 8;
            layout.padding = new RectOffset(8, 8, 4, 4);
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = false;
            layout.childControlHeight = false;

            // Icon
            GameObject iconObj = new GameObject("Icon");
            iconObj.transform.SetParent(popup.transform, false);

            RectTransform iconRect = iconObj.AddComponent<RectTransform>();
            iconRect.sizeDelta = new Vector2(32, 32);

            Image icon = iconObj.AddComponent<Image>();
            icon.preserveAspect = true;

            // Item name
            GameObject nameObj = new GameObject("ItemName");
            nameObj.transform.SetParent(popup.transform, false);

            RectTransform nameRect = nameObj.AddComponent<RectTransform>();
            nameRect.sizeDelta = new Vector2(120, 32);

            TextMeshProUGUI nameText = nameObj.AddComponent<TextMeshProUGUI>();
            nameText.fontSize = 14;
            nameText.alignment = TextAlignmentOptions.Left;

            // Quantity
            GameObject qtyObj = new GameObject("Quantity");
            qtyObj.transform.SetParent(popup.transform, false);

            RectTransform qtyRect = qtyObj.AddComponent<RectTransform>();
            qtyRect.sizeDelta = new Vector2(30, 32);

            TextMeshProUGUI qtyText = qtyObj.AddComponent<TextMeshProUGUI>();
            qtyText.fontSize = 14;
            qtyText.alignment = TextAlignmentOptions.Right;
            qtyText.color = Color.gray;

            // Content size fitter
            ContentSizeFitter fitter = popup.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;

            return popup;
        }

        /// <summary>
        /// Create the popup container and this component's UI.
        /// </summary>
        [ContextMenu("Create UI Elements")]
        public void CreateUIElements()
        {
            if (popupContainer == null)
            {
                popupContainer = GetComponent<RectTransform>();
            }

            // Position in top-right corner
            popupContainer.anchorMin = new Vector2(1, 1);
            popupContainer.anchorMax = new Vector2(1, 1);
            popupContainer.pivot = new Vector2(1, 1);
            popupContainer.anchoredPosition = new Vector2(-20, -20);
            popupContainer.sizeDelta = new Vector2(220, 300);
        }
    }
}
