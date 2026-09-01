using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

namespace KAM.Inventory.UI
{
    /// <summary>
    /// Visual representation of a single inventory slot.
    /// Handles display, hover, selection, click, and drag-drop events.
    /// </summary>
    public class InventorySlotUI : MonoBehaviour,
        IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler,
        IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler
    {
        [Header("UI References")]
        [SerializeField] private Image background;
        [SerializeField] private Image itemIcon;
        [SerializeField] private TextMeshProUGUI quantityText;
        [SerializeField] private Image rarityBorder;
        [SerializeField] private Image selectionHighlight;

        [Header("Colors")]
        [SerializeField] private Color normalColor = new Color(0.2f, 0.2f, 0.2f, 0.8f);
        [SerializeField] private Color hoverColor = new Color(0.3f, 0.3f, 0.3f, 0.9f);
        [SerializeField] private Color selectedColor = new Color(0.4f, 0.4f, 0.2f, 1f);
        [SerializeField] private Color emptySlotColor = new Color(0.15f, 0.15f, 0.15f, 0.5f);

        [Header("Layout")]
        [SerializeField, Min(0f)] private float itemIconPadding = 8f;

        // Data
        private InventorySlot _slot;
        private int _slotIndex;
        private bool _isSelected;
        private bool _isHovered;

        // Double-click detection
        private float _lastClickTime;
        private const float DOUBLE_CLICK_TIME = 0.3f;

        // Events
        public event System.Action<InventorySlotUI> OnSlotClicked;
        public event System.Action<InventorySlotUI> OnSlotRightClicked;
        public event System.Action<InventorySlotUI> OnSlotDoubleClicked;
        public event System.Action<InventorySlotUI> OnSlotHoverEnter;
        public event System.Action<InventorySlotUI> OnSlotHoverExit;
        public event System.Action<InventorySlotUI, InventorySlotUI> OnSlotSwap; // from, to

        // Drag and drop
        private static InventorySlotUI _draggedSlot;
        private static GameObject _dragIcon;

        /// <summary>
        /// Returns true if Unity's drag system is currently active (to prevent manual drag from also firing)
        /// </summary>
        public static bool IsUnityDragActive => _draggedSlot != null;

        /// <summary>
        /// Frame number when Unity's drag system last completed a swap.
        /// Used to prevent manual drag from also swapping in the same frame.
        /// </summary>
        private static int _lastUnitySwapFrame = -1;

        /// <summary>
        /// Returns true if Unity's drag system completed a swap this frame.
        /// </summary>
        public static bool DidUnitySwapThisFrame => Time.frameCount == _lastUnitySwapFrame;
        private Canvas _parentCanvas;
        private CanvasGroup _canvasGroup;

        public InventorySlot Slot => _slot;
        public int SlotIndex => _slotIndex;
        public bool IsEmpty => _slot == null || _slot.IsEmpty;
        public static InventorySlotUI DraggedSlot => _draggedSlot;

        private void Awake()
        {
            if (selectionHighlight != null)
                selectionHighlight.enabled = false;

            if (rarityBorder != null)
                rarityBorder.enabled = false;

            // Get canvas for drag operations
            _parentCanvas = GetComponentInParent<Canvas>();

            // Add canvas group for raycast blocking during drag
            _canvasGroup = GetComponent<CanvasGroup>();
            if (_canvasGroup == null)
            {
                _canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }
        }

        /// <summary>
        /// Initialize this slot UI with data.
        /// </summary>
        public void Initialize(int index)
        {
            _slotIndex = index;

            // If no background image exists, create one for raycast detection
            if (background == null)
            {
                background = GetComponent<Image>();
                if (background == null)
                {
                    background = gameObject.AddComponent<Image>();
                    background.color = emptySlotColor;
                }
            }

            // CRITICAL: Background must receive raycasts for clicks and drops
            background.raycastTarget = true;

            // CRITICAL: Child elements must NOT block raycasts
            if (itemIcon != null) itemIcon.raycastTarget = false;
            if (rarityBorder != null) rarityBorder.raycastTarget = false;
            if (selectionHighlight != null) selectionHighlight.raycastTarget = false;
            if (quantityText != null) quantityText.raycastTarget = false;
            EnsureQuantityTextReadable();

            // Ensure canvas group allows raycasts when not dragging
            if (_canvasGroup != null)
            {
                _canvasGroup.blocksRaycasts = true;
                _canvasGroup.interactable = true;
            }

            Clear();
        }

        /// <summary>
        /// Update the slot display with inventory data.
        /// </summary>
        public void UpdateDisplay(InventorySlot slot)
        {
            _slot = slot;

            // ALWAYS ensure this slot can receive interactions after update
            if (background != null)
            {
                background.raycastTarget = true;
            }

            // Ensure CanvasGroup exists and is configured for interaction
            if (_canvasGroup == null)
            {
                _canvasGroup = GetComponent<CanvasGroup>();
                if (_canvasGroup == null)
                {
                    _canvasGroup = gameObject.AddComponent<CanvasGroup>();
                }
            }
            _canvasGroup.blocksRaycasts = true;
            _canvasGroup.interactable = true;
            _canvasGroup.alpha = 1f;

            if (slot == null || slot.IsEmpty)
            {
                Clear();
                return;
            }

            // Show item icon
            if (itemIcon != null)
            {
                itemIcon.enabled = true;
                itemIcon.sprite = slot.Item.Icon;
                itemIcon.raycastTarget = false; // Ensure icon doesn't block slot clicks
                itemIcon.preserveAspect = true;

                // If no icon, show a colored placeholder
                if (slot.Item.Icon == null)
                {
                    itemIcon.color = slot.Item.GetRarityColor();
                }
                else
                {
                    itemIcon.color = Color.white;
                }

                RectTransform iconRect = itemIcon.rectTransform;
                iconRect.anchorMin = Vector2.zero;
                iconRect.anchorMax = Vector2.one;
                iconRect.offsetMin = new Vector2(itemIconPadding, itemIconPadding);
                iconRect.offsetMax = new Vector2(-itemIconPadding, -itemIconPadding);
            }

            // Show quantity (only if > 1)
            if (quantityText != null)
            {
                if (slot.Quantity > 1)
                {
                    quantityText.enabled = true;
                    quantityText.text = slot.Quantity.ToString();
                    quantityText.raycastTarget = false; // Ensure text doesn't block slot clicks
                }
                else
                {
                    quantityText.enabled = false;
                }

                EnsureQuantityTextReadable();
            }

            // Show rarity border
            if (rarityBorder != null)
            {
                // A plain Image fills the slot and hides the icon. Keep rarity visuals out
                // of this layer unless a real sliced frame sprite is assigned later.
                rarityBorder.enabled = false;
                rarityBorder.raycastTarget = false; // Ensure border doesn't block slot clicks
            }

            // Update background
            UpdateBackgroundColor();
        }

        /// <summary>
        /// Clear the slot display.
        /// </summary>
        public void Clear()
        {
            _slot = null;

            // ALWAYS ensure this slot can receive interactions even when empty
            if (background != null)
            {
                background.raycastTarget = true;
            }

            // Ensure CanvasGroup exists and is configured for interaction
            if (_canvasGroup == null)
            {
                _canvasGroup = GetComponent<CanvasGroup>();
                if (_canvasGroup == null)
                {
                    _canvasGroup = gameObject.AddComponent<CanvasGroup>();
                }
            }
            _canvasGroup.blocksRaycasts = true;
            _canvasGroup.interactable = true;
            _canvasGroup.alpha = 1f;

            if (itemIcon != null)
            {
                itemIcon.enabled = false;
                itemIcon.sprite = null;
            }

            if (quantityText != null)
            {
                quantityText.enabled = false;
                quantityText.text = "";
                EnsureQuantityTextReadable();
            }

            if (rarityBorder != null)
            {
                rarityBorder.enabled = false;
            }

            if (background != null)
            {
                background.color = emptySlotColor;
            }
        }

        /// <summary>
        /// Set selected state.
        /// </summary>
        public void SetSelected(bool selected)
        {
            _isSelected = selected;

            if (selectionHighlight != null)
            {
                // Selection uses background tint; a full highlight image covers item art.
                selectionHighlight.enabled = false;
            }

            UpdateBackgroundColor();
        }

        private void UpdateBackgroundColor()
        {
            if (background == null) return;

            if (IsEmpty)
            {
                background.color = emptySlotColor;
            }
            else if (_isSelected)
            {
                background.color = selectedColor;
            }
            else if (_isHovered)
            {
                background.color = hoverColor;
            }
            else
            {
                background.color = normalColor;
            }
        }

        private void EnsureQuantityTextReadable()
        {
            if (quantityText == null)
                return;

            RectTransform quantityRect = quantityText.rectTransform;
            quantityRect.anchorMin = new Vector2(1f, 0f);
            quantityRect.anchorMax = new Vector2(1f, 0f);
            quantityRect.pivot = new Vector2(1f, 0f);
            quantityRect.anchoredPosition = new Vector2(-4f, 3f);
            quantityRect.sizeDelta = new Vector2(32f, 20f);

            quantityText.alignment = TextAlignmentOptions.BottomRight;
            quantityText.fontStyle = FontStyles.Bold;
            quantityText.color = Color.white;
            quantityText.raycastTarget = false;
            quantityText.transform.SetAsLastSibling();
        }

        #region Pointer Events

        public void OnPointerEnter(PointerEventData eventData)
        {
            _isHovered = true;
            UpdateBackgroundColor();
            OnSlotHoverEnter?.Invoke(this);
            Debug.Log($"[InventorySlotUI] Hover ENTER slot {_slotIndex}, isEmpty={IsEmpty}");
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _isHovered = false;
            UpdateBackgroundColor();
            OnSlotHoverExit?.Invoke(this);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            Debug.Log($"[InventorySlotUI] CLICK on slot {_slotIndex}, button={eventData.button}, isEmpty={IsEmpty}");

            if (eventData.button == PointerEventData.InputButton.Left)
            {
                // Check for double-click
                float timeSinceLastClick = Time.unscaledTime - _lastClickTime;
                _lastClickTime = Time.unscaledTime;

                if (timeSinceLastClick <= DOUBLE_CLICK_TIME && !IsEmpty)
                {
                    // Double-click detected - use item
                    OnSlotDoubleClicked?.Invoke(this);
                }
                else
                {
                    // Single click - select
                    OnSlotClicked?.Invoke(this);
                }
            }
            else if (eventData.button == PointerEventData.InputButton.Right)
            {
                OnSlotRightClicked?.Invoke(this);
            }
        }

        #endregion

        #region Drag and Drop

        public void OnBeginDrag(PointerEventData eventData)
        {
            Debug.Log($"[InventorySlotUI] BEGIN DRAG attempt on slot {_slotIndex}, isEmpty={IsEmpty}, item={(_slot?.Item?.ItemName ?? "null")}");

            if (IsEmpty) return;

            _draggedSlot = this;

            // Create drag icon
            if (_dragIcon == null)
            {
                _dragIcon = new GameObject("DragIcon");
                _dragIcon.transform.SetParent(_parentCanvas.transform, false);

                RectTransform iconRect = _dragIcon.AddComponent<RectTransform>();
                iconRect.sizeDelta = new Vector2(50, 50);

                Image iconImage = _dragIcon.AddComponent<Image>();
                iconImage.sprite = _slot.Item.Icon;
                iconImage.color = _slot.Item.Icon != null ? Color.white : _slot.Item.GetRarityColor();
                iconImage.raycastTarget = false;
                iconImage.preserveAspect = true;

                // Make sure it's on top
                Canvas iconCanvas = _dragIcon.AddComponent<Canvas>();
                iconCanvas.overrideSorting = true;
                iconCanvas.sortingOrder = 200;
            }

            // Make this slot semi-transparent
            _canvasGroup.alpha = 0.6f;
            _canvasGroup.blocksRaycasts = false;

            Debug.Log($"[InventorySlotUI] Started dragging {_slot.Item.ItemName}");
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (_dragIcon != null)
            {
                _dragIcon.transform.position = eventData.position;
            }
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            // Restore visuals FIRST so target slot can receive raycast
            _canvasGroup.alpha = 1f;
            _canvasGroup.blocksRaycasts = true;

            // Check if dropped onto a quick slot
            QuickSlotsUI quickSlotsUI = FindFirstObjectByType<QuickSlotsUI>();
            if (quickSlotsUI != null && _draggedSlot != null && !_draggedSlot.IsEmpty)
            {
                int quickSlotIndex = quickSlotsUI.GetQuickSlotAtPosition(eventData.position);
                if (quickSlotIndex >= 0)
                {
                    quickSlotsUI.OnItemDropped(quickSlotIndex, _draggedSlot.Slot.Item);
                    FindFirstObjectByType<InventoryUI>()?.PlayTabMoveSound();
                    Debug.Log($"[InventorySlotUI] Dropped onto Quick Slot {quickSlotIndex + 1}");

                    // Cleanup and exit
                    if (_dragIcon != null)
                    {
                        Destroy(_dragIcon);
                        _dragIcon = null;
                    }
                    _draggedSlot = null;
                    return;
                }
            }

            // MANUAL DROP DETECTION: Use GraphicRaycaster to find target slot
            // This bypasses Unity's IDropHandler which may not work reliably
            if (_draggedSlot != null && !_draggedSlot.IsEmpty && _parentCanvas != null)
            {
                GraphicRaycaster raycaster = _parentCanvas.GetComponent<GraphicRaycaster>();
                if (raycaster == null)
                {
                    raycaster = _parentCanvas.GetComponentInParent<GraphicRaycaster>();
                }

                if (raycaster != null)
                {
                    var results = new System.Collections.Generic.List<RaycastResult>();
                    raycaster.Raycast(eventData, results);

                    InventorySlotUI targetSlot = null;

                    foreach (var result in results)
                    {
                        // Try to find InventorySlotUI on the hit object or its parents
                        targetSlot = result.gameObject.GetComponent<InventorySlotUI>();
                        if (targetSlot == null)
                        {
                            targetSlot = result.gameObject.GetComponentInParent<InventorySlotUI>();
                        }

                        if (targetSlot != null && targetSlot != _draggedSlot)
                        {
                            break;
                        }
                        targetSlot = null;
                    }

                    // If raycast didn't find a slot, try position-based detection as fallback
                    if (targetSlot == null)
                    {
                        targetSlot = FindSlotAtPosition(eventData.position);
                    }

                    if (targetSlot != null && targetSlot != _draggedSlot)
                    {
                        // Found a valid target slot - perform swap
                        var inventory = InventoryManager.Instance;
                        if (inventory != null)
                        {
                            inventory.SwapSlots(_draggedSlot.SlotIndex, targetSlot.SlotIndex);
                            Debug.Log($"[InventorySlotUI] Swap: slot {_draggedSlot.SlotIndex} <-> slot {targetSlot.SlotIndex}");

                            // Mark that Unity's drag system performed a swap this frame
                            // This prevents the manual drag system from also swapping
                            _lastUnitySwapFrame = Time.frameCount;

                            // Explicitly refresh UI to ensure all slots are updated
                            var inventoryUI = FindFirstObjectByType<InventoryUI>();
                            if (inventoryUI != null)
                            {
                                inventoryUI.PlayTabMoveSound();
                                inventoryUI.RefreshDisplay();
                            }
                        }
                    }
                }
            }

            // Destroy drag icon
            if (_dragIcon != null)
            {
                Destroy(_dragIcon);
                _dragIcon = null;
            }

            _draggedSlot = null;
        }

        public void OnDrop(PointerEventData eventData)
        {
            // Handle drop from quick slot
            if (QuickSlotsUI.IsDraggingFromQuickSlot)
            {
                // Quick slot item is being dropped onto inventory slot
                // This will be handled by QuickSlotsUI.OnEndDragQuickSlot
                Debug.Log($"[InventorySlotUI] Quick slot item dropped onto slot {_slotIndex}");
                return;
            }

            // Handle drop from another inventory slot
            if (_draggedSlot == null || _draggedSlot == this) return;

            Debug.Log($"[InventorySlotUI] Dropped {_draggedSlot.Slot?.Item?.ItemName} onto slot {_slotIndex}");

            // Fire swap event - let InventoryUI handle the actual swap
            if (OnSlotSwap != null)
            {
                OnSlotSwap.Invoke(_draggedSlot, this);
            }
            else
            {
                // Fallback: directly call swap if no event subscribers
                var inventory = InventoryManager.Instance;
                if (inventory != null)
                {
                    inventory.SwapSlots(_draggedSlot.SlotIndex, _slotIndex);
                    FindFirstObjectByType<InventoryUI>()?.PlayTabMoveSound();
                    Debug.Log($"[InventorySlotUI] Direct swap: slot {_draggedSlot.SlotIndex} <-> slot {_slotIndex}");
                }
            }
        }

        /// <summary>
        /// Find an inventory slot at the given screen position using RectTransform bounds.
        /// This is a fallback when GraphicRaycaster doesn't work.
        /// </summary>
        private static InventorySlotUI FindSlotAtPosition(Vector2 screenPosition)
        {
            // Find all inventory slots in the scene
            var allSlots = FindObjectsByType<InventorySlotUI>(FindObjectsSortMode.None);

            foreach (var slot in allSlots)
            {
                RectTransform rectTransform = slot.GetComponent<RectTransform>();
                if (rectTransform != null)
                {
                    // Check if the screen position is within this slot's rect
                    if (RectTransformUtility.RectangleContainsScreenPoint(rectTransform, screenPosition, null))
                    {
                        return slot;
                    }
                }
            }

            return null;
        }

        #endregion

        /// <summary>
        /// Create slot UI elements programmatically.
        /// </summary>
        public void CreateUIElements()
        {
            // Background - must have raycastTarget enabled to receive clicks
            if (background == null)
            {
                background = gameObject.AddComponent<Image>();
                background.color = emptySlotColor;
                background.raycastTarget = true; // Important for receiving clicks/drops
            }

            // Item icon - must NOT block raycasts
            if (itemIcon == null)
            {
                GameObject iconObj = new GameObject("ItemIcon");
                iconObj.transform.SetParent(transform, false);

                RectTransform iconRect = iconObj.AddComponent<RectTransform>();
                iconRect.anchorMin = Vector2.zero;
                iconRect.anchorMax = Vector2.one;
                iconRect.offsetMin = new Vector2(4, 4);
                iconRect.offsetMax = new Vector2(-4, -4);

                itemIcon = iconObj.AddComponent<Image>();
                itemIcon.enabled = false;
                itemIcon.preserveAspect = true;
                itemIcon.raycastTarget = false; // Don't block clicks on slot
            }

            // Quantity text - must NOT block raycasts
            if (quantityText == null)
            {
                GameObject qtyObj = new GameObject("Quantity");
                qtyObj.transform.SetParent(transform, false);

                RectTransform qtyRect = qtyObj.AddComponent<RectTransform>();
                qtyRect.anchorMin = new Vector2(1, 0);
                qtyRect.anchorMax = new Vector2(1, 0);
                qtyRect.pivot = new Vector2(1, 0);
                qtyRect.anchoredPosition = new Vector2(-2, 2);
                qtyRect.sizeDelta = new Vector2(30, 20);

                quantityText = qtyObj.AddComponent<TextMeshProUGUI>();
                quantityText.fontSize = 14;
                quantityText.fontStyle = FontStyles.Bold;
                quantityText.alignment = TextAlignmentOptions.BottomRight;
                quantityText.enabled = false;
                quantityText.raycastTarget = false; // Don't block clicks on slot
                EnsureQuantityTextReadable();
            }

            // Rarity border
            if (rarityBorder == null)
            {
                GameObject borderObj = new GameObject("RarityBorder");
                borderObj.transform.SetParent(transform, false);

                RectTransform borderRect = borderObj.AddComponent<RectTransform>();
                borderRect.anchorMin = Vector2.zero;
                borderRect.anchorMax = Vector2.one;
                borderRect.offsetMin = Vector2.zero;
                borderRect.offsetMax = Vector2.zero;

                rarityBorder = borderObj.AddComponent<Image>();
                rarityBorder.enabled = false;
                rarityBorder.raycastTarget = false;

                // Make it a border (outline only) - requires a border sprite or use Outline component
            }

            // Selection highlight
            if (selectionHighlight == null)
            {
                GameObject highlightObj = new GameObject("SelectionHighlight");
                highlightObj.transform.SetParent(transform, false);

                RectTransform highlightRect = highlightObj.AddComponent<RectTransform>();
                highlightRect.anchorMin = Vector2.zero;
                highlightRect.anchorMax = Vector2.one;
                highlightRect.offsetMin = Vector2.zero;
                highlightRect.offsetMax = Vector2.zero;

                selectionHighlight = highlightObj.AddComponent<Image>();
                selectionHighlight.color = new Color(1, 1, 0, 0.3f);
                selectionHighlight.enabled = false;
                selectionHighlight.raycastTarget = false;
            }
        }
    }
}
