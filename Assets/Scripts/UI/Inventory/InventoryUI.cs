using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace KAM.Inventory.UI
{
    /// <summary>
    /// Main inventory panel UI. Opens with TAB key.
    /// Displays all inventory slots in a grid layout.
    /// </summary>
    public class InventoryUI : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private GameObject inventoryPanel;
        [SerializeField] private Transform slotContainer;
        [SerializeField] private InventorySlotUI slotPrefab;
        [SerializeField] private ItemTooltipUI tooltip;
        [SerializeField] private TextMeshProUGUI titleText;

        [Header("Grid Settings")]
        [SerializeField] private int columns = 6;
        [SerializeField] private Vector2 slotSize = new Vector2(60, 60);
        [SerializeField] private Vector2 slotSpacing = new Vector2(5, 5);

        [Header("Scroll Settings")]
        [SerializeField] private ScrollRect scrollRect;
        [SerializeField] private float scrollSpeed = 50f;

        [Header("Category Tabs")]
        [SerializeField] private Button tabAll;
        [SerializeField] private Button tabMaterials;
        [SerializeField] private Button tabPotions;
        [SerializeField] private Button tabTalismans;

        [Header("Input")]
        [SerializeField] private KeyCode toggleKey = KeyCode.Tab;
        [SerializeField] private KeyCode useSelectedItemKey = KeyCode.E;
        [SerializeField] private KeyCode quickSlot1Key = KeyCode.Alpha1;
        [SerializeField] private KeyCode quickSlot2Key = KeyCode.Alpha2;
        [SerializeField] private KeyCode quickSlot3Key = KeyCode.Alpha3;
        [SerializeField] private KeyCode quickSlot4Key = KeyCode.Alpha4;

        [Header("Quick Slot Assignment")]
        [SerializeField] private TextMeshProUGUI assignHintText;

        [Header("Audio")]
        [SerializeField] private AudioClip openSound;
        [SerializeField] private AudioClip closeSound;
        [SerializeField] private AudioClip tabMoveSound;
        [SerializeField] private AudioSource audioSource;
        [SerializeField, Range(0f, 1f)] private float uiSoundVolume = 1f;

        private List<InventorySlotUI> _slotUIs = new List<InventorySlotUI>();
        private InventoryManager _inventory;
        private InventorySlotUI _selectedSlot;
        private ItemType? _currentFilter = null;
        private bool _isOpen;

#if UNITY_EDITOR
        private const string DefaultOpenSoundPath = "Assets/Assets/Sfx/ui opening.mp3";
        private const string DefaultTabMoveSoundPath = "Assets/Assets/Sfx/tab sound.mp3";
#endif

        public bool IsOpen => _isOpen;

#if UNITY_EDITOR
        private void OnValidate()
        {
            AssignDefaultAudioClipsInEditor();
        }
#endif

        private void Start()
        {
#if UNITY_EDITOR
            AssignDefaultAudioClipsInEditor();
#endif
            EnsureAudioSource();

            _inventory = InventoryManager.Instance;

            if (_inventory == null)
            {
                Debug.LogError("[InventoryUI] InventoryManager not found!");
                return;
            }

            // Subscribe to events
            _inventory.OnInventoryChanged += RefreshDisplay;

            // Setup tabs
            SetupTabs();

            // Create slot UIs
            CreateSlotUIs();

            // Hide by default
            if (inventoryPanel != null)
                inventoryPanel.SetActive(false);

            // Create tooltip if not assigned
            if (tooltip == null)
            {
                CreateTooltip();
            }
        }

        private void OnDestroy()
        {
            if (_inventory != null)
            {
                _inventory.OnInventoryChanged -= RefreshDisplay;
            }
        }

        private void Update()
        {
            // Toggle inventory
            if (Input.GetKeyDown(toggleKey))
            {
                Toggle();
            }

            // When inventory is open and item is selected
            if (_isOpen && _selectedSlot != null && !_selectedSlot.IsEmpty)
            {
                if (Input.GetKeyDown(useSelectedItemKey))
                {
                    UseSelectedItem();
                }
                // Assign to quick slots with 1-4
                else if (Input.GetKeyDown(quickSlot1Key))
                {
                    AssignToQuickSlot(0);
                }
                else if (Input.GetKeyDown(quickSlot2Key))
                {
                    AssignToQuickSlot(1);
                }
                else if (Input.GetKeyDown(quickSlot3Key))
                {
                    AssignToQuickSlot(2);
                }
                else if (Input.GetKeyDown(quickSlot4Key))
                {
                    AssignToQuickSlot(3);
                }
            }

            // Mouse scroll when inventory is open
            if (_isOpen && scrollRect != null)
            {
                float scroll = Input.GetAxis("Mouse ScrollWheel");
                if (scroll != 0)
                {
                    scrollRect.verticalNormalizedPosition += scroll * scrollSpeed * Time.unscaledDeltaTime;
                    scrollRect.verticalNormalizedPosition = Mathf.Clamp01(scrollRect.verticalNormalizedPosition);
                }
            }

            // BACKUP CLICK DETECTION: Manual click detection for slots that don't receive events
            // This bypasses Unity's event system which may not work for some slots
            if (_isOpen)
            {
                HandleManualClickDetection();
            }
        }

        /// <summary>
        /// Manual click and drag detection as a backup for slots that don't receive Unity events.
        /// </summary>
        private void HandleManualClickDetection()
        {
            // Skip manual detection if Unity's drag system is active OR if it just completed a swap
            // This prevents both systems from handling the same drag operation
            if (InventorySlotUI.IsUnityDragActive || InventorySlotUI.DidUnitySwapThisFrame)
            {
                // Reset manual drag state if Unity took over or just finished
                _manualDragSlot = null;
                _isManualDragging = false;
                if (_manualDragIcon != null)
                {
                    Destroy(_manualDragIcon);
                    _manualDragIcon = null;
                }
                return;
            }

            Vector2 mousePos = Input.mousePosition;

            // Handle manual drag
            if (_isManualDragging)
            {
                // Update drag icon position
                if (_manualDragIcon != null)
                {
                    _manualDragIcon.transform.position = mousePos;
                }

                // Check for mouse up to end drag
                if (Input.GetMouseButtonUp(0))
                {
                    EndManualDrag(mousePos);
                }
                return; // Don't process other clicks while dragging
            }

            // Check for left click down - start potential drag
            if (Input.GetMouseButtonDown(0))
            {
                InventorySlotUI clickedSlot = FindSlotAtScreenPosition(mousePos);

                if (clickedSlot != null && !clickedSlot.IsEmpty)
                {
                    // Start tracking for potential drag
                    _manualDragSlot = clickedSlot;
                    _manualDragStartPos = mousePos;
                }
            }

            // Check if we should start dragging (mouse held and moved beyond threshold)
            if (Input.GetMouseButton(0) && _manualDragSlot != null && !_isManualDragging)
            {
                float distance = Vector2.Distance(mousePos, _manualDragStartPos);
                if (distance > DRAG_THRESHOLD)
                {
                    StartManualDrag(_manualDragSlot);
                }
            }

            // Check for left click up - either click or drag end
            if (Input.GetMouseButtonUp(0))
            {
                if (_manualDragSlot != null && !_isManualDragging)
                {
                    // This was a click, not a drag
                    InventorySlotUI clickedSlot = _manualDragSlot;
                    _manualDragSlot = null;

                    Debug.Log($"[InventoryUI] Manual click detected on slot {clickedSlot.SlotIndex}");

                    // Check for double-click
                    float timeSinceLastClick = Time.unscaledTime - _lastManualClickTime;
                    bool isDoubleClick = (timeSinceLastClick <= 0.3f) && (_lastManualClickSlot == clickedSlot);
                    _lastManualClickTime = Time.unscaledTime;
                    _lastManualClickSlot = clickedSlot;

                    if (isDoubleClick && !clickedSlot.IsEmpty)
                    {
                        OnSlotDoubleClicked(clickedSlot);
                    }
                    else
                    {
                        OnSlotClicked(clickedSlot);
                    }
                }
            }

            // Check for right click
            if (Input.GetMouseButtonDown(1))
            {
                InventorySlotUI clickedSlot = FindSlotAtScreenPosition(mousePos);

                if (clickedSlot != null && !clickedSlot.IsEmpty)
                {
                    Debug.Log($"[InventoryUI] Manual right-click detected on slot {clickedSlot.SlotIndex}");
                    OnSlotRightClicked(clickedSlot);
                }
            }

            // Manual hover detection for tooltip (backup for slots where OnPointerEnter doesn't fire)
            InventorySlotUI hoveredSlot = FindSlotAtScreenPosition(mousePos);
            if (hoveredSlot != _manualHoveredSlot)
            {
                // Hover changed
                _manualHoveredSlot = hoveredSlot;

                if (hoveredSlot != null && !hoveredSlot.IsEmpty)
                {
                    // Show tooltip for this slot
                    tooltip?.Show(hoveredSlot.Slot.Item);
                }
                else
                {
                    // Hide tooltip
                    tooltip?.Hide();
                }
            }
        }

        private void StartManualDrag(InventorySlotUI slot)
        {
            _isManualDragging = true;
            Debug.Log($"[InventoryUI] Manual DRAG started from slot {slot.SlotIndex}");

            // Create drag icon
            if (_manualDragIcon == null && slot.Slot?.Item != null)
            {
                Canvas canvas = GetComponentInParent<Canvas>();
                if (canvas == null) canvas = GetComponent<Canvas>();

                _manualDragIcon = new GameObject("ManualDragIcon");
                _manualDragIcon.transform.SetParent(canvas.transform, false);

                RectTransform iconRect = _manualDragIcon.AddComponent<RectTransform>();
                iconRect.sizeDelta = new Vector2(50, 50);

                Image iconImage = _manualDragIcon.AddComponent<Image>();
                iconImage.sprite = slot.Slot.Item.Icon;
                iconImage.color = slot.Slot.Item.Icon != null ? Color.white : slot.Slot.Item.GetRarityColor();
                iconImage.raycastTarget = false;
                iconImage.preserveAspect = true;

                Canvas iconCanvas = _manualDragIcon.AddComponent<Canvas>();
                iconCanvas.overrideSorting = true;
                iconCanvas.sortingOrder = 200;
            }
        }

        private void EndManualDrag(Vector2 mousePos)
        {
            Debug.Log($"[InventoryUI] Manual DRAG ended at position {mousePos}");

            // Find target slot
            InventorySlotUI targetSlot = FindSlotAtScreenPosition(mousePos);

            if (targetSlot != null && targetSlot != _manualDragSlot && _manualDragSlot != null)
            {
                // Perform swap
                _inventory.SwapSlots(_manualDragSlot.SlotIndex, targetSlot.SlotIndex);
                PlayTabMoveSound();
                Debug.Log($"[InventoryUI] Manual swap: slot {_manualDragSlot.SlotIndex} <-> slot {targetSlot.SlotIndex}");
                RefreshDisplay();
            }

            // Check for quick slot drop
            QuickSlotsUI quickSlotsUI = FindFirstObjectByType<QuickSlotsUI>();
            if (quickSlotsUI != null && _manualDragSlot != null && !_manualDragSlot.IsEmpty)
            {
                int quickSlotIndex = quickSlotsUI.GetQuickSlotAtPosition(mousePos);
                if (quickSlotIndex >= 0)
                {
                    quickSlotsUI.OnItemDropped(quickSlotIndex, _manualDragSlot.Slot.Item);
                    PlayTabMoveSound();
                    Debug.Log($"[InventoryUI] Manual drop onto Quick Slot {quickSlotIndex + 1}");
                    RefreshDisplay();
                }
            }

            // Cleanup
            if (_manualDragIcon != null)
            {
                Destroy(_manualDragIcon);
                _manualDragIcon = null;
            }
            _manualDragSlot = null;
            _isManualDragging = false;
        }

        /// <summary>
        /// Find an inventory slot at the given screen position.
        /// </summary>
        private InventorySlotUI FindSlotAtScreenPosition(Vector2 screenPosition)
        {
            foreach (var slotUI in _slotUIs)
            {
                if (slotUI == null) continue;

                RectTransform rectTransform = slotUI.GetComponent<RectTransform>();
                if (rectTransform != null)
                {
                    if (RectTransformUtility.RectangleContainsScreenPoint(rectTransform, screenPosition, null))
                    {
                        return slotUI;
                    }
                }
            }
            return null;
        }

        // For manual double-click detection
        private float _lastManualClickTime;
        private InventorySlotUI _lastManualClickSlot;

        // For manual drag detection
        private InventorySlotUI _manualDragSlot;
        private Vector2 _manualDragStartPos;
        private bool _isManualDragging;
        private GameObject _manualDragIcon;
        private const float DRAG_THRESHOLD = 10f;

        // For manual hover detection (tooltip)
        private InventorySlotUI _manualHoveredSlot;

        /// <summary>
        /// Toggle inventory open/closed.
        /// </summary>
        public void Toggle()
        {
            if (_isOpen)
                Close();
            else
                Open();
        }

        /// <summary>
        /// Open the inventory.
        /// </summary>
        public void Open()
        {
            if (_isOpen) return;

            _isOpen = true;

            if (inventoryPanel != null)
                inventoryPanel.SetActive(true);

            RefreshDisplay();

            // Play sound
            PlayUISound(openSound);

            // Pause game (optional)
            // Time.timeScale = 0f;

            Debug.Log("[InventoryUI] Opened");
        }

        /// <summary>
        /// Close the inventory.
        /// </summary>
        public void Close()
        {
            if (!_isOpen) return;

            _isOpen = false;

            if (inventoryPanel != null)
                inventoryPanel.SetActive(false);

            if (tooltip != null)
                tooltip.Hide();

            if (assignHintText != null)
                assignHintText.enabled = false;

            // Reset manual hover tracking
            _manualHoveredSlot = null;

            // Deselect
            SelectSlot(null);

            // Play sound
            PlayUISound(closeSound);

            // Resume game
            // Time.timeScale = 1f;

            Debug.Log("[InventoryUI] Closed");
        }

        private void CreateSlotUIs()
        {
            if (slotContainer == null)
            {
                Debug.LogError("[InventoryUI] slotContainer is NULL! Please reassign in Inspector or run KAM > Inventory > Setup Complete Inventory System");
                return;
            }

            if (_inventory == null)
            {
                Debug.LogError("[InventoryUI] InventoryManager is NULL!");
                return;
            }

            // Clear existing
            foreach (var slot in _slotUIs)
            {
                if (slot != null)
                    Destroy(slot.gameObject);
            }
            _slotUIs.Clear();

            // CRITICAL FIX: Disable raycastTarget on ALL Graphic components in the entire panel
            // This prevents ANY element from blocking clicks on inventory slots
            if (inventoryPanel != null)
            {
                // Disable raycast on EVERY Graphic component (Image, Text, RawImage, etc.)
                var allGraphics = inventoryPanel.GetComponentsInChildren<Graphic>(true);
                foreach (var graphic in allGraphics)
                {
                    graphic.raycastTarget = false;
                }
                Debug.Log($"[InventoryUI] Disabled raycastTarget on {allGraphics.Length} graphics in panel");
            }

            // Also disable on container if it has an image
            Image containerBg = slotContainer.GetComponent<Image>();
            if (containerBg != null)
            {
                containerBg.raycastTarget = false;
            }

            // Create grid layout if not present
            GridLayoutGroup grid = slotContainer.GetComponent<GridLayoutGroup>();
            if (grid == null)
            {
                grid = slotContainer.gameObject.AddComponent<GridLayoutGroup>();
            }
            grid.cellSize = slotSize;
            grid.spacing = slotSpacing;
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = columns;
            grid.childAlignment = TextAnchor.UpperCenter; // Center horizontally
            grid.padding = new RectOffset(10, 10, 10, 10);

            // Create slots
            for (int i = 0; i < _inventory.MaxSlots; i++)
            {
                InventorySlotUI slotUI = CreateSlotUI(i);
                _slotUIs.Add(slotUI);
            }

            // VERIFY: Ensure all slot backgrounds can receive raycasts
            int enabledCount = 0;
            foreach (var slotUI in _slotUIs)
            {
                // Ensure background Image can receive raycasts
                Image bg = slotUI.GetComponent<Image>();
                if (bg != null)
                {
                    bg.raycastTarget = true;
                    enabledCount++;
                }

                // Ensure CanvasGroup allows raycasts
                CanvasGroup cg = slotUI.GetComponent<CanvasGroup>();
                if (cg != null)
                {
                    cg.blocksRaycasts = true;
                    cg.interactable = true;
                }

                // Disable raycast on all child graphics (icon, text, border, etc.)
                var childGraphics = slotUI.GetComponentsInChildren<Graphic>(true);
                foreach (var childGraphic in childGraphics)
                {
                    // Skip the slot's own background
                    if (childGraphic.gameObject == slotUI.gameObject)
                        continue;
                    childGraphic.raycastTarget = false;
                }
            }

            Debug.Log($"[InventoryUI] Created {_slotUIs.Count} inventory slots, enabled raycast on {enabledCount} slot backgrounds");
        }

        private InventorySlotUI CreateSlotUI(int index)
        {
            GameObject slotObj;

            if (slotPrefab != null)
            {
                slotObj = Instantiate(slotPrefab.gameObject, slotContainer);
            }
            else
            {
                // Create slot programmatically
                slotObj = new GameObject($"Slot_{index}");
                slotObj.transform.SetParent(slotContainer, false);

                RectTransform rect = slotObj.AddComponent<RectTransform>();
                rect.sizeDelta = slotSize;

                InventorySlotUI slot = slotObj.AddComponent<InventorySlotUI>();
                slot.CreateUIElements();
            }

            InventorySlotUI slotUI = slotObj.GetComponent<InventorySlotUI>();
            slotUI.Initialize(index);

            // Subscribe to events
            slotUI.OnSlotClicked += OnSlotClicked;
            slotUI.OnSlotRightClicked += OnSlotRightClicked;
            slotUI.OnSlotDoubleClicked += OnSlotDoubleClicked;
            slotUI.OnSlotHoverEnter += OnSlotHover;
            slotUI.OnSlotHoverExit += OnSlotHoverExit;
            slotUI.OnSlotSwap += OnSlotSwap;

            return slotUI;
        }

        private void OnSlotSwap(InventorySlotUI fromSlot, InventorySlotUI toSlot)
        {
            if (_inventory == null) return;

            _inventory.SwapSlots(fromSlot.SlotIndex, toSlot.SlotIndex);
            PlayTabMoveSound();
            RefreshDisplay();

            Debug.Log($"[InventoryUI] Swapped slot {fromSlot.SlotIndex} with slot {toSlot.SlotIndex}");
        }

        /// <summary>
        /// Refresh the display to match inventory data.
        /// </summary>
        public void RefreshDisplay()
        {
            if (_inventory == null) return;

            var slots = _inventory.Slots;

            for (int i = 0; i < _slotUIs.Count && i < slots.Count; i++)
            {
                var slot = slots[i];

                // Apply filter
                if (_currentFilter.HasValue && !slot.IsEmpty && slot.Item.Type != _currentFilter.Value)
                {
                    _slotUIs[i].Clear();
                }
                else
                {
                    _slotUIs[i].UpdateDisplay(slot);
                }
            }
        }

        private void SetupTabs()
        {
            if (tabAll != null)
                tabAll.onClick.AddListener(() => SetFilter(null));

            if (tabMaterials != null)
                tabMaterials.onClick.AddListener(() => SetFilter(ItemType.Material));

            if (tabPotions != null)
                tabPotions.onClick.AddListener(() => SetFilter(ItemType.Potion));

            if (tabTalismans != null)
                tabTalismans.onClick.AddListener(() => SetFilter(ItemType.Talisman));
        }

        /// <summary>
        /// Set category filter.
        /// </summary>
        public void SetFilter(ItemType? type)
        {
            _currentFilter = type;

            // Clear selection when changing filters
            SelectSlot(null);

            RefreshDisplay();

            if (titleText != null)
            {
                titleText.text = type.HasValue ? $"Inventory - {type.Value}s" : "Inventory";
            }
        }

        private void SelectSlot(InventorySlotUI slot)
        {
            // Deselect previous
            if (_selectedSlot != null)
            {
                _selectedSlot.SetSelected(false);
            }

            _selectedSlot = slot;

            // Select new
            if (_selectedSlot != null)
            {
                _selectedSlot.SetSelected(true);
            }

            // Update hint text
            UpdateAssignHint();
        }

        private void UpdateAssignHint()
        {
            if (assignHintText == null) return;

            if (_selectedSlot != null && !_selectedSlot.IsEmpty)
            {
                assignHintText.enabled = true;
                assignHintText.text = "[1-4] Quick Slot  [E] Use";
            }
            else
            {
                assignHintText.enabled = false;
            }
        }

        private void UseSelectedItem()
        {
            if (_selectedSlot == null || _selectedSlot.IsEmpty) return;

            var item = _selectedSlot.Slot.Item;
            bool wasUsed = item.Use(GameObject.FindGameObjectWithTag("Player"));

            if (wasUsed)
            {
                // Always remove from inventory when used from TAB UI
                _inventory.RemoveItem(item, 1);
                Debug.Log($"[InventoryUI] Used {item.ItemName}");
            }

            RefreshDisplay();

            // Hide tooltip if item was consumed
            if (_selectedSlot.IsEmpty)
            {
                tooltip?.Hide();
                SelectSlot(null);
            }
        }

        /// <summary>
        /// Assign selected item to a quick slot. Item will be MOVED from inventory to quick slot.
        /// </summary>
        private void AssignToQuickSlot(int slotIndex)
        {
            if (_selectedSlot == null || _selectedSlot.IsEmpty) return;

            var item = _selectedSlot.Slot.Item;
            _inventory.SetQuickSlot(slotIndex, item);
            PlayTabMoveSound();

            Debug.Log($"[InventoryUI] Moved {item.ItemName} to Quick Slot {slotIndex + 1}");

            // Item was moved, so clear selection and refresh
            SelectSlot(null);
            RefreshDisplay();
        }

        #region Slot Events

        private void OnSlotClicked(InventorySlotUI slot)
        {
            // Only select non-empty slots
            if (slot.IsEmpty)
            {
                SelectSlot(null);
            }
            else
            {
                SelectSlot(slot);
            }
        }

        private void OnSlotRightClicked(InventorySlotUI slot)
        {
            // Quick use on right-click - always consume item from TAB UI
            if (!slot.IsEmpty)
            {
                var item = slot.Slot.Item;
                bool wasUsed = item.Use(GameObject.FindGameObjectWithTag("Player"));

                if (wasUsed)
                {
                    // Always remove from inventory when used from TAB UI
                    _inventory.RemoveItem(item, 1);
                    Debug.Log($"[InventoryUI] Used {item.ItemName} (right-click)");
                }

                RefreshDisplay();

                // Update selection if slot is now empty
                if (slot.IsEmpty)
                {
                    tooltip?.Hide();
                    SelectSlot(null);
                }
            }
        }

        private void OnSlotDoubleClicked(InventorySlotUI slot)
        {
            // Use item on double-click - always consume item from TAB UI
            if (!slot.IsEmpty)
            {
                var item = slot.Slot.Item;
                bool wasUsed = item.Use(GameObject.FindGameObjectWithTag("Player"));

                if (wasUsed)
                {
                    // Always remove from inventory when used from TAB UI
                    _inventory.RemoveItem(item, 1);
                    Debug.Log($"[InventoryUI] Used {item.ItemName} (double-click)");
                }

                RefreshDisplay();

                // Update selection if slot is now empty
                if (slot.IsEmpty)
                {
                    tooltip?.Hide();
                    SelectSlot(null);
                }
            }
        }

        private void OnSlotHover(InventorySlotUI slot)
        {
            if (slot.IsEmpty)
            {
                tooltip?.Hide();
            }
            else
            {
                tooltip?.Show(slot.Slot.Item);
            }
        }

        private void OnSlotHoverExit(InventorySlotUI slot)
        {
            tooltip?.Hide();
        }

        #endregion

        private void CreateTooltip()
        {
            GameObject tooltipObj = new GameObject("ItemTooltip");
            tooltipObj.transform.SetParent(transform.root, false);

            // Make sure it's on top
            Canvas tooltipCanvas = tooltipObj.AddComponent<Canvas>();
            tooltipCanvas.overrideSorting = true;
            tooltipCanvas.sortingOrder = 100;

            tooltipObj.AddComponent<GraphicRaycaster>();

            tooltip = tooltipObj.AddComponent<ItemTooltipUI>();
            tooltip.CreateUIElements();
        }

        public void PlayTabMoveSound()
        {
            PlayUISound(tabMoveSound);
        }

        private void PlayUISound(AudioClip clip)
        {
            if (clip == null) return;

            EnsureAudioSource();

            if (audioSource == null) return;

            audioSource.PlayOneShot(clip, uiSoundVolume);
        }

        private void EnsureAudioSource()
        {
            if (audioSource == null)
            {
                audioSource = GetComponent<AudioSource>();
            }

            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
            }

            audioSource.playOnAwake = false;
            audioSource.loop = false;
            audioSource.spatialBlend = 0f;
        }

#if UNITY_EDITOR
        private void AssignDefaultAudioClipsInEditor()
        {
            if (openSound == null)
            {
                openSound = AssetDatabase.LoadAssetAtPath<AudioClip>(DefaultOpenSoundPath);
            }

            if (tabMoveSound == null)
            {
                tabMoveSound = AssetDatabase.LoadAssetAtPath<AudioClip>(DefaultTabMoveSoundPath);
            }
        }
#endif
    }
}
