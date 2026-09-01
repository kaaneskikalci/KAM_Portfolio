using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

namespace KAM.Inventory.UI
{
    /// <summary>
    /// HUD element showing 4 quick slots for D-Pad item access.
    /// Supports drag-drop to return items to inventory.
    /// </summary>
    public class QuickSlotsUI : MonoBehaviour
    {
        // Static drag state for quick slots
        private static int _draggedQuickSlotIndex = -1;
        private static GameObject _dragIcon;
        private static Canvas _parentCanvas;

        public static int DraggedQuickSlotIndex => _draggedQuickSlotIndex;
        public static bool IsDraggingFromQuickSlot => _draggedQuickSlotIndex >= 0;
        [Header("Slot References")]
        [SerializeField] private QuickSlotElement[] slots = new QuickSlotElement[4];

        [Header("Input")]
        [SerializeField] private KeyCode slot1Key = KeyCode.Alpha1;  // Or D-Pad Up
        [SerializeField] private KeyCode slot2Key = KeyCode.Alpha2;  // Or D-Pad Right
        [SerializeField] private KeyCode slot3Key = KeyCode.Alpha3;  // Or D-Pad Down
        [SerializeField] private KeyCode slot4Key = KeyCode.Alpha4;  // Or D-Pad Left

        [Header("Visual")]
        [SerializeField] private Color normalColor = new Color(0.2f, 0.2f, 0.2f, 0.8f);
        [SerializeField] private Color selectedColor = new Color(0.4f, 0.4f, 0.2f, 1f);
        [SerializeField] private Color emptyColor = new Color(0.1f, 0.1f, 0.1f, 0.5f);
        [SerializeField] private Color cooldownColor = new Color(0.3f, 0.3f, 0.3f, 0.9f);
        [SerializeField] private bool preserveBackgroundArt = true;
        [SerializeField] private Color decoratedOccupiedColor = Color.white;
        [SerializeField] private Color decoratedEmptyColor = new Color(1f, 1f, 1f, 0.65f);
        [SerializeField] private float selectedScaleMultiplier = 1.08f;
        [SerializeField, Min(12f)] private float diamondIconSize = 28f;

        [Header("Tooltip")]
        [SerializeField] private ItemTooltipUI tooltip;
        [SerializeField] private bool showTooltipOnHover = true;

        private InventoryManager _inventory;
        private InventoryUI _inventoryUI;
        private int _selectedIndex = -1;
        private int _hoveredSlotIndex = -1;

        [System.Serializable]
        public class QuickSlotElement
        {
            public RectTransform container;
            public Image background;
            public Image itemIcon;
            public Image rarityBorder;
            public TextMeshProUGUI quantityText;
            public TextMeshProUGUI keyHintText;
            public TextMeshProUGUI itemNameText;
            public Image cooldownOverlay;
            public Button clickButton;

            [HideInInspector] public ItemData currentItem;
        }

        private void Start()
        {
            _inventory = InventoryManager.Instance;
            _inventoryUI = FindFirstObjectByType<InventoryUI>();

            if (_inventory != null)
            {
                _inventory.OnQuickSlotChanged += OnQuickSlotChanged;
                _inventory.OnInventoryChanged += RefreshAll;
            }

            // Set key hints and click handlers
            SetKeyHints();
            SetupClickHandlers();
            EnsureTooltip();

            // Initial refresh
            RefreshAll();
        }

        private void SetupClickHandlers()
        {
            _parentCanvas = GetComponentInParent<Canvas>();

            for (int i = 0; i < slots.Length; i++)
            {
                int index = i; // Capture for closure
                var slot = slots[i];

                if (slot.container == null) continue;

                // Ensure background has raycastTarget for drag detection
                if (slot.background != null)
                {
                    slot.background.raycastTarget = true;
                }

                // Add EventTrigger for clicks and drag
                EventTrigger trigger = slot.container.GetComponent<EventTrigger>();
                if (trigger == null)
                {
                    trigger = slot.container.gameObject.AddComponent<EventTrigger>();
                }

                trigger.triggers.Clear();

                // Hover - show item tooltip while the cursor is over a filled quick slot
                EventTrigger.Entry hoverEnter = new EventTrigger.Entry();
                hoverEnter.eventID = EventTriggerType.PointerEnter;
                hoverEnter.callback.AddListener((data) => OnPointerEnterQuickSlot(index));
                trigger.triggers.Add(hoverEnter);

                EventTrigger.Entry hoverExit = new EventTrigger.Entry();
                hoverExit.eventID = EventTriggerType.PointerExit;
                hoverExit.callback.AddListener((data) => OnPointerExitQuickSlot(index));
                trigger.triggers.Add(hoverExit);

                // Left click - use item, Right click - return to inventory
                EventTrigger.Entry clickEntry = new EventTrigger.Entry();
                clickEntry.eventID = EventTriggerType.PointerClick;
                clickEntry.callback.AddListener((data) =>
                {
                    PointerEventData pointerData = (PointerEventData)data;
                    if (pointerData.button == PointerEventData.InputButton.Left)
                    {
                        UseQuickSlot(index);
                    }
                    else if (pointerData.button == PointerEventData.InputButton.Right)
                    {
                        ReturnToInventory(index);
                    }
                });
                trigger.triggers.Add(clickEntry);

                // Begin Drag - start dragging item from quick slot
                EventTrigger.Entry beginDrag = new EventTrigger.Entry();
                beginDrag.eventID = EventTriggerType.BeginDrag;
                beginDrag.callback.AddListener((data) => OnBeginDragQuickSlot(index, (PointerEventData)data));
                trigger.triggers.Add(beginDrag);

                // Drag - move drag icon
                EventTrigger.Entry drag = new EventTrigger.Entry();
                drag.eventID = EventTriggerType.Drag;
                drag.callback.AddListener((data) => OnDragQuickSlot((PointerEventData)data));
                trigger.triggers.Add(drag);

                // End Drag - drop item
                EventTrigger.Entry endDrag = new EventTrigger.Entry();
                endDrag.eventID = EventTriggerType.EndDrag;
                endDrag.callback.AddListener((data) => OnEndDragQuickSlot(index, (PointerEventData)data));
                trigger.triggers.Add(endDrag);
            }
        }

        private void OnBeginDragQuickSlot(int index, PointerEventData eventData)
        {
            if (_inventory == null) return;

            var quickSlots = _inventory.QuickSlots;
            if (index >= quickSlots.Length) return;

            var quickSlot = quickSlots[index];
            if (quickSlot == null || quickSlot.IsEmpty) return;

            HideTooltip();
            _draggedQuickSlotIndex = index;

            // Create drag icon
            if (_dragIcon == null && _parentCanvas != null)
            {
                _dragIcon = new GameObject("QuickSlotDragIcon");
                _dragIcon.transform.SetParent(_parentCanvas.transform, false);

                RectTransform iconRect = _dragIcon.AddComponent<RectTransform>();
                iconRect.sizeDelta = new Vector2(50, 50);

                Image iconImage = _dragIcon.AddComponent<Image>();
                iconImage.sprite = quickSlot.Item.Icon;
                iconImage.color = quickSlot.Item.Icon != null ? Color.white : quickSlot.Item.GetRarityColor();
                iconImage.raycastTarget = false;
                iconImage.preserveAspect = true;

                // Make sure it's on top
                Canvas iconCanvas = _dragIcon.AddComponent<Canvas>();
                iconCanvas.overrideSorting = true;
                iconCanvas.sortingOrder = 200;
            }

            Debug.Log($"[QuickSlotsUI] Started dragging {quickSlot.Item.ItemName} from Quick Slot {index + 1}");
        }

        private void OnDragQuickSlot(PointerEventData eventData)
        {
            if (_dragIcon != null)
            {
                _dragIcon.transform.position = eventData.position;
            }
        }

        private void OnEndDragQuickSlot(int index, PointerEventData eventData)
        {
            if (_draggedQuickSlotIndex < 0)
            {
                CleanupDrag();
                return;
            }

            // Check if dropped over the inventory panel (return to inventory)
            if (_inventoryUI != null && _inventoryUI.IsOpen)
            {
                // If inventory is open, return the item to inventory
                ReturnToInventory(_draggedQuickSlotIndex);
                Debug.Log($"[QuickSlotsUI] Dropped Quick Slot {_draggedQuickSlotIndex + 1} item back to inventory");
            }
            else
            {
                // Check if dropped onto another quick slot (swap)
                int targetSlot = GetQuickSlotAtPosition(eventData.position);
                if (targetSlot >= 0 && targetSlot != _draggedQuickSlotIndex)
                {
                    SwapQuickSlots(_draggedQuickSlotIndex, targetSlot);
                }
            }

            CleanupDrag();
        }

        private void CleanupDrag()
        {
            _draggedQuickSlotIndex = -1;

            if (_dragIcon != null)
            {
                Destroy(_dragIcon);
                _dragIcon = null;
            }
        }

        /// <summary>
        /// Swap items between two quick slots.
        /// </summary>
        private void SwapQuickSlots(int fromIndex, int toIndex)
        {
            if (_inventory == null) return;

            var quickSlots = _inventory.QuickSlots;
            if (fromIndex >= quickSlots.Length || toIndex >= quickSlots.Length) return;

            var fromSlot = quickSlots[fromIndex];
            var toSlot = quickSlots[toIndex];

            // Store from data
            ItemData fromItem = fromSlot?.Item;
            int fromQty = fromSlot?.Quantity ?? 0;

            // Store to data
            ItemData toItem = toSlot?.Item;
            int toQty = toSlot?.Quantity ?? 0;

            // Clear both
            fromSlot?.Clear();
            toSlot?.Clear();

            // Swap
            if (toItem != null)
                fromSlot?.SetItem(toItem, toQty);
            if (fromItem != null)
                toSlot?.SetItem(fromItem, fromQty);

            RefreshAll();
            Debug.Log($"[QuickSlotsUI] Swapped Quick Slot {fromIndex + 1} with Quick Slot {toIndex + 1}");
        }

        /// <summary>
        /// Return item from quick slot back to inventory.
        /// </summary>
        public void ReturnToInventory(int index)
        {
            if (index < 0 || index >= slots.Length) return;
            if (_inventory == null) return;

            var quickSlots = _inventory.QuickSlots;
            if (index >= quickSlots.Length) return;

            var quickSlot = quickSlots[index];
            if (quickSlot == null || quickSlot.IsEmpty) return;

            string itemName = quickSlot.Item.ItemName;
            _inventory.MoveQuickSlotToInventory(index);

            Debug.Log($"[QuickSlotsUI] Returned {itemName} from Quick Slot {index + 1} to inventory");
        }

        private void OnDestroy()
        {
            if (_inventory != null)
            {
                _inventory.OnQuickSlotChanged -= OnQuickSlotChanged;
                _inventory.OnInventoryChanged -= RefreshAll;
            }

            HideTooltip();
        }

        private void Update()
        {
            // Only handle input when inventory is CLOSED
            // When inventory is open, InventoryUI uses 1-4 for assigning items
            bool inventoryOpen = _inventoryUI != null && _inventoryUI.IsOpen;
            if (inventoryOpen) return;

            // Handle input
            if (Input.GetKeyDown(slot1Key))
                UseQuickSlot(0);
            else if (Input.GetKeyDown(slot2Key))
                UseQuickSlot(1);
            else if (Input.GetKeyDown(slot3Key))
                UseQuickSlot(2);
            else if (Input.GetKeyDown(slot4Key))
                UseQuickSlot(3);

            RefreshHoveredTooltip();

            // D-Pad input (for controller)
            // You can add controller support here using Input System
        }

        private void SetKeyHints()
        {
            if (slots.Length > 0 && slots[0].keyHintText != null)
                slots[0].keyHintText.text = "1";
            if (slots.Length > 1 && slots[1].keyHintText != null)
                slots[1].keyHintText.text = "2";
            if (slots.Length > 2 && slots[2].keyHintText != null)
                slots[2].keyHintText.text = "3";
            if (slots.Length > 3 && slots[3].keyHintText != null)
                slots[3].keyHintText.text = "4";
        }

        /// <summary>
        /// Use item from quick slot.
        /// </summary>
        public void UseQuickSlot(int index)
        {
            if (index < 0 || index >= slots.Length) return;

            var slot = slots[index];
            if (slot == null || slot.currentItem == null) return;

            // Store item name before use (it will be cleared after)
            string itemName = slot.currentItem.ItemName;

            // Flash selection
            SelectSlot(index);

            // Use the item from quick slot (consumes from quick slot directly)
            if (_inventory != null && _inventory.UseFromQuickSlot(index))
            {
                Debug.Log($"[QuickSlotsUI] Used {itemName} from quick slot {index + 1}");
                RefreshSlot(index);
                RefreshHoveredTooltip();
            }
        }

        /// <summary>
        /// Assign an item to a quick slot.
        /// </summary>
        public void AssignToQuickSlot(int index, ItemData item)
        {
            if (_inventory != null)
            {
                _inventory.SetQuickSlot(index, item);
            }
        }

        /// <summary>
        /// Handle item dropped onto a quick slot from inventory.
        /// </summary>
        public void OnItemDropped(int quickSlotIndex, ItemData item)
        {
            if (_inventory == null || item == null) return;

            _inventory.SetQuickSlot(quickSlotIndex, item);
            Debug.Log($"[QuickSlotsUI] Dropped {item.ItemName} onto Quick Slot {quickSlotIndex + 1}");
        }

        /// <summary>
        /// Get quick slot index at screen position (for drag-drop).
        /// Returns -1 if no slot at position.
        /// </summary>
        public int GetQuickSlotAtPosition(Vector2 screenPosition)
        {
            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i].container == null) continue;

                if (RectTransformUtility.RectangleContainsScreenPoint(
                    slots[i].container, screenPosition, null))
                {
                    return i;
                }
            }
            return -1;
        }

        private void OnQuickSlotChanged(int index)
        {
            RefreshSlot(index);
        }

        private void RefreshAll()
        {
            for (int i = 0; i < slots.Length; i++)
            {
                RefreshSlot(i);
            }
        }

        private void RefreshSlot(int index)
        {
            if (index < 0 || index >= slots.Length) return;
            if (_inventory == null) return;

            var slotUI = slots[index];
            var quickSlots = _inventory.QuickSlots;

            if (index >= quickSlots.Length)
            {
                ClearSlot(slotUI);
                return;
            }

            var inventorySlot = quickSlots[index];

            // Check if slot is empty
            if (inventorySlot == null || inventorySlot.IsEmpty)
            {
                ClearSlot(slotUI);
                if (_hoveredSlotIndex == index)
                    HideTooltip();
                return;
            }

            // Update display with item from quick slot
            slotUI.currentItem = inventorySlot.Item;

            if (slotUI.itemIcon != null)
            {
                slotUI.itemIcon.enabled = true;
                slotUI.itemIcon.sprite = inventorySlot.Item.Icon;
                slotUI.itemIcon.preserveAspect = true;

                // Use rarity color if no icon
                if (inventorySlot.Item.Icon == null)
                {
                    slotUI.itemIcon.color = inventorySlot.Item.GetRarityColor();
                }
                else
                {
                    slotUI.itemIcon.color = Color.white;
                }

                RectTransform iconRect = slotUI.itemIcon.rectTransform;
                iconRect.anchorMin = new Vector2(0.5f, 0.5f);
                iconRect.anchorMax = new Vector2(0.5f, 0.5f);
                iconRect.pivot = new Vector2(0.5f, 0.5f);
                iconRect.anchoredPosition = Vector2.zero;
                iconRect.sizeDelta = new Vector2(diamondIconSize, diamondIconSize);
                slotUI.itemIcon.transform.SetAsLastSibling();
            }

            // Get quantity directly from quick slot (NOT main inventory!)
            int quantity = inventorySlot.Quantity;
            if (slotUI.quantityText != null)
            {
                if (quantity > 1)
                {
                    slotUI.quantityText.enabled = true;
                    slotUI.quantityText.text = quantity.ToString();
                }
                else
                {
                    // Show "1" for single items so user knows it's there
                    slotUI.quantityText.enabled = true;
                    slotUI.quantityText.text = "1";
                }
            }

            // Set background to normal (has item) color
            ApplySlotBackground(slotUI, isEmpty: false);

            // Show rarity border
            if (slotUI.rarityBorder != null)
            {
                // The quick-slot sprite already has a frame. This Image is a filled square
                // with the current prefab art, so it reads as an oversized blue border.
                slotUI.rarityBorder.enabled = false;
            }

            // Show item name below slot
            if (slotUI.itemNameText != null)
            {
                slotUI.itemNameText.enabled = true;
                // Truncate long names
                string name = inventorySlot.Item.ItemName;
                if (name.Length > 8)
                    name = name.Substring(0, 7) + "..";
                slotUI.itemNameText.text = name;
            }

            Debug.Log($"[QuickSlotsUI] Refreshed slot {index}: {inventorySlot.Item.ItemName} x{quantity}");
        }

        private void ClearSlot(QuickSlotElement slotUI)
        {
            slotUI.currentItem = null;

            if (slotUI.itemIcon != null)
            {
                slotUI.itemIcon.enabled = false;
                slotUI.itemIcon.sprite = null;
            }

            if (slotUI.quantityText != null)
            {
                slotUI.quantityText.enabled = false;
                slotUI.quantityText.text = "";
            }

            if (slotUI.background != null)
                ApplySlotBackground(slotUI, isEmpty: true);

            if (slotUI.rarityBorder != null)
            {
                slotUI.rarityBorder.enabled = false;
            }

            if (slotUI.itemNameText != null)
            {
                slotUI.itemNameText.enabled = false;
                slotUI.itemNameText.text = "";
            }

            if (slotUI.cooldownOverlay != null)
            {
                slotUI.cooldownOverlay.enabled = false;
            }
        }

        private void OnPointerEnterQuickSlot(int index)
        {
            _hoveredSlotIndex = index;
            ShowTooltipForSlot(index);
        }

        private void OnPointerExitQuickSlot(int index)
        {
            if (_hoveredSlotIndex == index)
                _hoveredSlotIndex = -1;

            HideTooltip();
        }

        private void ShowTooltipForSlot(int index)
        {
            if (!showTooltipOnHover || _inventory == null || _draggedQuickSlotIndex >= 0)
                return;

            if (index < 0 || index >= _inventory.QuickSlots.Length)
            {
                HideTooltip();
                return;
            }

            InventorySlot quickSlot = _inventory.QuickSlots[index];
            if (quickSlot == null || quickSlot.IsEmpty || quickSlot.Item == null)
            {
                HideTooltip();
                return;
            }

            EnsureTooltip();
            tooltip?.Show(quickSlot.Item);
        }

        private void RefreshHoveredTooltip()
        {
            if (_hoveredSlotIndex >= 0)
                ShowTooltipForSlot(_hoveredSlotIndex);
        }

        private void HideTooltip()
        {
            tooltip?.Hide();
        }

        private void EnsureTooltip()
        {
            if (tooltip != null)
                return;

            tooltip = FindFirstObjectByType<ItemTooltipUI>();
            if (tooltip != null)
                return;

            Transform tooltipParent = transform.root != null ? transform.root : transform;
            GameObject tooltipObj = new GameObject("QuickSlotItemTooltip");
            tooltipObj.transform.SetParent(tooltipParent, false);

            Canvas tooltipCanvas = tooltipObj.AddComponent<Canvas>();
            tooltipCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            tooltipCanvas.overrideSorting = true;
            tooltipCanvas.sortingOrder = 110;

            tooltipObj.AddComponent<GraphicRaycaster>();
            tooltip = tooltipObj.AddComponent<ItemTooltipUI>();
            tooltip.CreateUIElements();
            tooltip.Hide();
        }

        private void SelectSlot(int index)
        {
            // Deselect previous
            if (_selectedIndex >= 0 && _selectedIndex < slots.Length)
            {
                var prevSlot = slots[_selectedIndex];
                ApplySlotSelectionVisual(prevSlot, false);
            }

            _selectedIndex = index;

            // Select new
            if (index >= 0 && index < slots.Length)
            {
                var slot = slots[index];
                ApplySlotSelectionVisual(slot, true);

                // Reset after short delay
                Invoke(nameof(DeselectAll), 0.2f);
            }
        }

        private void DeselectAll()
        {
            _selectedIndex = -1;
            RefreshAll();
        }

        private void ApplySlotBackground(QuickSlotElement slotUI, bool isEmpty)
        {
            if (slotUI.background != null)
            {
                slotUI.background.color = preserveBackgroundArt
                    ? (isEmpty ? decoratedEmptyColor : decoratedOccupiedColor)
                    : (isEmpty ? emptyColor : normalColor);
            }

            if (slotUI.container != null)
                slotUI.container.localScale = Vector3.one;
        }

        private void ApplySlotSelectionVisual(QuickSlotElement slotUI, bool selected)
        {
            if (slotUI == null)
                return;

            if (preserveBackgroundArt)
            {
                if (slotUI.container != null)
                    slotUI.container.localScale = selected ? Vector3.one * selectedScaleMultiplier : Vector3.one;

                if (slotUI.background != null)
                    slotUI.background.color = slotUI.currentItem != null ? decoratedOccupiedColor : decoratedEmptyColor;
            }
            else if (slotUI.background != null)
            {
                slotUI.background.color = selected
                    ? selectedColor
                    : (slotUI.currentItem != null ? normalColor : emptyColor);
            }
        }

        /// <summary>
        /// Show cooldown on a slot.
        /// </summary>
        public void ShowCooldown(int index, float duration)
        {
            if (index < 0 || index >= slots.Length) return;

            var slot = slots[index];
            if (slot.cooldownOverlay != null)
            {
                slot.cooldownOverlay.enabled = true;
                // You could animate this with a fill amount
                Invoke(nameof(HideCooldown), duration);
            }
        }

        private void HideCooldown()
        {
            foreach (var slot in slots)
            {
                if (slot.cooldownOverlay != null)
                    slot.cooldownOverlay.enabled = false;
            }
        }

        /// <summary>
        /// Create quick slot UI elements programmatically.
        /// </summary>
        [ContextMenu("Create Quick Slot Elements")]
        public void CreateUIElements()
        {
            slots = new QuickSlotElement[4];

            HorizontalLayoutGroup layout = GetComponent<HorizontalLayoutGroup>();
            if (layout == null)
            {
                layout = gameObject.AddComponent<HorizontalLayoutGroup>();
            }
            layout.spacing = 10;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = false;
            layout.childControlHeight = false;

            for (int i = 0; i < 4; i++)
            {
                slots[i] = CreateQuickSlotElement(i);
            }
        }

        private QuickSlotElement CreateQuickSlotElement(int index)
        {
            QuickSlotElement element = new QuickSlotElement();

            // Container (larger to fit name below)
            GameObject container = new GameObject($"QuickSlot_{index}");
            container.transform.SetParent(transform, false);

            element.container = container.AddComponent<RectTransform>();
            element.container.sizeDelta = new Vector2(55, 70);

            // Slot background (the actual slot area)
            GameObject slotBg = new GameObject("SlotBg");
            slotBg.transform.SetParent(container.transform, false);

            RectTransform slotRect = slotBg.AddComponent<RectTransform>();
            slotRect.anchorMin = new Vector2(0.5f, 1);
            slotRect.anchorMax = new Vector2(0.5f, 1);
            slotRect.pivot = new Vector2(0.5f, 1);
            slotRect.anchoredPosition = Vector2.zero;
            slotRect.sizeDelta = new Vector2(50, 50);

            element.background = slotBg.AddComponent<Image>();
            element.background.color = emptyColor;

            // Rarity border
            GameObject borderObj = new GameObject("RarityBorder");
            borderObj.transform.SetParent(slotBg.transform, false);

            RectTransform borderRect = borderObj.AddComponent<RectTransform>();
            borderRect.anchorMin = Vector2.zero;
            borderRect.anchorMax = Vector2.one;
            borderRect.offsetMin = new Vector2(-2, -2);
            borderRect.offsetMax = new Vector2(2, 2);

            element.rarityBorder = borderObj.AddComponent<Image>();
            element.rarityBorder.color = Color.white;
            element.rarityBorder.enabled = false;
            element.rarityBorder.raycastTarget = false;
            // Move border behind background
            borderObj.transform.SetAsFirstSibling();

            // Item icon
            GameObject iconObj = new GameObject("Icon");
            iconObj.transform.SetParent(slotBg.transform, false);

            RectTransform iconRect = iconObj.AddComponent<RectTransform>();
            iconRect.anchorMin = new Vector2(0.5f, 0.5f);
            iconRect.anchorMax = new Vector2(0.5f, 0.5f);
            iconRect.pivot = new Vector2(0.5f, 0.5f);
            iconRect.anchoredPosition = Vector2.zero;
            iconRect.sizeDelta = new Vector2(diamondIconSize, diamondIconSize);

            element.itemIcon = iconObj.AddComponent<Image>();
            element.itemIcon.enabled = false;
            element.itemIcon.preserveAspect = true;
            element.itemIcon.raycastTarget = false;

            // Key hint (top-left)
            GameObject keyObj = new GameObject("KeyHint");
            keyObj.transform.SetParent(slotBg.transform, false);

            RectTransform keyRect = keyObj.AddComponent<RectTransform>();
            keyRect.anchorMin = new Vector2(0, 1);
            keyRect.anchorMax = new Vector2(0, 1);
            keyRect.pivot = new Vector2(0, 1);
            keyRect.anchoredPosition = new Vector2(2, -2);
            keyRect.sizeDelta = new Vector2(15, 15);

            element.keyHintText = keyObj.AddComponent<TextMeshProUGUI>();
            element.keyHintText.fontSize = 10;
            element.keyHintText.fontStyle = FontStyles.Bold;
            element.keyHintText.text = (index + 1).ToString();
            element.keyHintText.raycastTarget = false;

            // Quantity (bottom-right)
            GameObject qtyObj = new GameObject("Quantity");
            qtyObj.transform.SetParent(slotBg.transform, false);

            RectTransform qtyRect = qtyObj.AddComponent<RectTransform>();
            qtyRect.anchorMin = new Vector2(1, 0);
            qtyRect.anchorMax = new Vector2(1, 0);
            qtyRect.pivot = new Vector2(1, 0);
            qtyRect.anchoredPosition = new Vector2(-2, 2);
            qtyRect.sizeDelta = new Vector2(25, 18);

            element.quantityText = qtyObj.AddComponent<TextMeshProUGUI>();
            element.quantityText.fontSize = 14;
            element.quantityText.fontStyle = FontStyles.Bold;
            element.quantityText.alignment = TextAlignmentOptions.BottomRight;
            element.quantityText.enabled = false;
            element.quantityText.raycastTarget = false;

            // Item name (below slot)
            GameObject nameObj = new GameObject("ItemName");
            nameObj.transform.SetParent(container.transform, false);

            RectTransform nameRect = nameObj.AddComponent<RectTransform>();
            nameRect.anchorMin = new Vector2(0.5f, 0);
            nameRect.anchorMax = new Vector2(0.5f, 0);
            nameRect.pivot = new Vector2(0.5f, 0);
            nameRect.anchoredPosition = new Vector2(0, 0);
            nameRect.sizeDelta = new Vector2(60, 18);

            element.itemNameText = nameObj.AddComponent<TextMeshProUGUI>();
            element.itemNameText.fontSize = 9;
            element.itemNameText.alignment = TextAlignmentOptions.Top;
            element.itemNameText.enabled = false;
            element.itemNameText.raycastTarget = false;

            return element;
        }
    }
}
