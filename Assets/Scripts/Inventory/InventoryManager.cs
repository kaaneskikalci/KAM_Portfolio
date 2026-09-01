using System;
using System.Collections.Generic;
using KAM.SaveSystem;
using UnityEngine;

namespace KAM.Inventory
{
    /// <summary>
    /// Central manager for the inventory system.
    /// Handles adding, removing, using items, and quick slots.
    /// </summary>
    public class InventoryManager : MonoBehaviour
    {
        public static InventoryManager Instance { get; private set; }

        [Header("Inventory Settings")]
        [SerializeField] private int maxSlots = 24;
        [SerializeField] private int quickSlotCount = 4;

        [Header("Currency")]
        [SerializeField] private int spiritFragments = 0;

        [Header("Debug")]
        [SerializeField] private bool debugLog = true;

        // Inventory data
        private List<InventorySlot> _slots = new List<InventorySlot>();
        private InventorySlot[] _quickSlots;
        private List<TalismanData> _equippedTalismans = new List<TalismanData>();

        // Events
        public event Action<ItemData, int> OnItemAdded;
        public event Action<ItemData, int> OnItemRemoved;
        public event Action<ItemData> OnItemUsed;
        public event Action<int, int> OnSpiritFragmentsChanged; // old, new
        public event Action OnInventoryChanged;
        public event Action<int> OnQuickSlotChanged;

        // Properties
        public int SpiritFragments => spiritFragments;
        public int MaxSlots => maxSlots;
        public int UsedSlots => _slots.FindAll(s => !s.IsEmpty).Count;
        public int FreeSlots => maxSlots - UsedSlots;
        public IReadOnlyList<InventorySlot> Slots => _slots;
        public InventorySlot[] QuickSlots => _quickSlots;
        public IReadOnlyList<TalismanData> EquippedTalismans => _equippedTalismans;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;

            // DontDestroyOnLoad only works for root GameObjects
            if (transform.parent == null)
            {
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                // If not root, move to root first
                transform.SetParent(null);
                DontDestroyOnLoad(gameObject);
            }

            InitializeInventory();
        }

        private void InitializeInventory()
        {
            _slots.Clear();
            for (int i = 0; i < maxSlots; i++)
            {
                _slots.Add(new InventorySlot(i));
            }

            _quickSlots = new InventorySlot[quickSlotCount];
            for (int i = 0; i < quickSlotCount; i++)
            {
                _quickSlots[i] = new InventorySlot(i);
            }

            if (debugLog) Debug.Log($"[InventoryManager] Initialized with {maxSlots} slots, {quickSlotCount} quick slots");
        }

        #region Add Items

        /// <summary>
        /// Add an item to the inventory. Returns true if successful.
        /// </summary>
        /// <param name="item">The item to add</param>
        /// <param name="quantity">How many to add</param>
        /// <param name="silent">If true, won't trigger OnItemAdded event (for internal transfers)</param>
        public bool AddItem(ItemData item, int quantity = 1, bool silent = false)
        {
            if (item == null || quantity <= 0) return false;

            int remaining = quantity;

            // First, try to stack with existing items
            foreach (var slot in _slots)
            {
                if (slot.Item == item && !slot.IsFull)
                {
                    remaining = slot.AddQuantity(remaining);
                    if (remaining <= 0) break;
                }
            }

            // Then, add to empty slots
            while (remaining > 0)
            {
                InventorySlot emptySlot = FindEmptySlot();
                if (emptySlot == null)
                {
                    if (debugLog) Debug.LogWarning($"[InventoryManager] Inventory full! Could not add {remaining}x {item.ItemName}");
                    return false;
                }

                int toAdd = Mathf.Min(remaining, item.MaxStackSize);
                emptySlot.SetItem(item, toAdd);
                remaining -= toAdd;
            }

            if (debugLog) Debug.Log($"[InventoryManager] Added {quantity}x {item.ItemName}");

            // Only fire OnItemAdded if not silent (silent = internal transfer, not new pickup)
            if (!silent)
            {
                OnItemAdded?.Invoke(item, quantity);
            }
            OnInventoryChanged?.Invoke();

            return true;
        }

        /// <summary>
        /// Add spirit fragments (permanent currency).
        /// </summary>
        public void AddSpiritFragments(int amount)
        {
            if (amount <= 0) return;

            int oldValue = spiritFragments;
            spiritFragments += amount;

            if (debugLog) Debug.Log($"[InventoryManager] +{amount} Spirit Fragments (Total: {spiritFragments})");

            OnSpiritFragmentsChanged?.Invoke(oldValue, spiritFragments);
        }

        public InventorySaveData GetSaveData()
        {
            return new InventorySaveData
            {
                spiritFragments = spiritFragments
            };
        }

        public void LoadSaveData(InventorySaveData data)
        {
            if (data == null)
                return;

            int oldValue = spiritFragments;
            spiritFragments = Mathf.Max(0, data.spiritFragments);

            if (oldValue != spiritFragments)
                OnSpiritFragmentsChanged?.Invoke(oldValue, spiritFragments);

            OnInventoryChanged?.Invoke();
        }

        #endregion

        #region Remove Items

        /// <summary>
        /// Remove an item from the inventory. Returns true if successful.
        /// </summary>
        public bool RemoveItem(ItemData item, int quantity = 1)
        {
            if (item == null || quantity <= 0) return false;

            int totalOwned = GetItemCount(item);
            if (totalOwned < quantity)
            {
                if (debugLog) Debug.LogWarning($"[InventoryManager] Not enough {item.ItemName} (have {totalOwned}, need {quantity})");
                return false;
            }

            int remaining = quantity;

            // Remove from slots (prefer partial stacks first)
            var slotsWithItem = _slots.FindAll(s => s.Item == item);
            slotsWithItem.Sort((a, b) => a.Quantity.CompareTo(b.Quantity));

            foreach (var slot in slotsWithItem)
            {
                if (remaining <= 0) break;
                remaining -= slot.RemoveQuantity(remaining);
            }

            if (debugLog) Debug.Log($"[InventoryManager] Removed {quantity}x {item.ItemName}");

            OnItemRemoved?.Invoke(item, quantity);
            OnInventoryChanged?.Invoke();

            return true;
        }

        /// <summary>
        /// Remove item from a specific slot.
        /// </summary>
        public bool RemoveFromSlot(int slotIndex, int quantity = 1)
        {
            if (slotIndex < 0 || slotIndex >= _slots.Count) return false;

            var slot = _slots[slotIndex];
            if (slot.IsEmpty) return false;

            ItemData item = slot.Item;
            int removed = slot.RemoveQuantity(quantity);

            if (removed > 0)
            {
                OnItemRemoved?.Invoke(item, removed);
                OnInventoryChanged?.Invoke();
                return true;
            }

            return false;
        }

        /// <summary>
        /// Spend spirit fragments. Returns true if successful.
        /// </summary>
        public bool SpendSpiritFragments(int amount)
        {
            if (amount <= 0) return true;
            if (spiritFragments < amount)
            {
                if (debugLog) Debug.LogWarning($"[InventoryManager] Not enough Spirit Fragments (have {spiritFragments}, need {amount})");
                return false;
            }

            int oldValue = spiritFragments;
            spiritFragments -= amount;

            if (debugLog) Debug.Log($"[InventoryManager] Spent {amount} Spirit Fragments (Remaining: {spiritFragments})");

            OnSpiritFragmentsChanged?.Invoke(oldValue, spiritFragments);
            return true;
        }

        public int LoseSpiritFragmentsPercent(float percent)
        {
            if (spiritFragments <= 0 || percent <= 0f)
                return 0;

            int amountToLose = Mathf.CeilToInt(spiritFragments * Mathf.Clamp01(percent));
            int oldValue = spiritFragments;
            spiritFragments = Mathf.Max(0, spiritFragments - amountToLose);

            if (debugLog)
                Debug.Log($"[InventoryManager] Lost {amountToLose} Spirit Fragments (Remaining: {spiritFragments})");

            OnSpiritFragmentsChanged?.Invoke(oldValue, spiritFragments);
            return amountToLose;
        }

        /// <summary>
        /// Check if the player can afford a cost in spirit fragments.
        /// </summary>
        public bool CanAfford(int cost)
        {
            return spiritFragments >= cost;
        }

        #endregion

        #region Use Items

        /// <summary>
        /// Use an item from the inventory.
        /// </summary>
        public bool UseItem(ItemData item, GameObject user = null)
        {
            if (item == null) return false;
            if (!HasItem(item))
            {
                if (debugLog) Debug.LogWarning($"[InventoryManager] Cannot use {item.ItemName} - not in inventory");
                return false;
            }

            // Try to find player if user not specified
            if (user == null)
            {
                user = GameObject.FindGameObjectWithTag("Player");

                // Fallback: try to find by name
                if (user == null)
                {
                    user = GameObject.Find("Player");
                }

                // Still null? Try to find any PlayerMovement component
                if (user == null)
                {
                    var playerMovement = FindFirstObjectByType<PlayerMovement>();
                    if (playerMovement != null)
                        user = playerMovement.gameObject;
                }
            }

            if (user == null)
            {
                Debug.LogWarning("[InventoryManager] Could not find Player! Make sure your player has the 'Player' tag.");
                // Still try to use the item without a user
            }

            if (item.Use(user))
            {
                if (item.IsConsumable)
                {
                    RemoveItem(item, 1);
                }

                OnItemUsed?.Invoke(item);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Use item from a specific slot.
        /// </summary>
        public bool UseItemFromSlot(int slotIndex, GameObject user = null)
        {
            if (slotIndex < 0 || slotIndex >= _slots.Count) return false;

            var slot = _slots[slotIndex];
            if (slot.IsEmpty) return false;

            return UseItem(slot.Item, user);
        }

        /// <summary>
        /// Use item from a quick slot (0-3 for D-Pad).
        /// </summary>
        public bool UseQuickSlot(int quickSlotIndex, GameObject user = null)
        {
            if (quickSlotIndex < 0 || quickSlotIndex >= _quickSlots.Length) return false;

            var slot = _quickSlots[quickSlotIndex];
            if (slot.IsEmpty) return false;

            return UseItem(slot.Item, user);
        }

        #endregion

        #region Quick Slots

        /// <summary>
        /// Assign an item to a quick slot. MOVES the item from inventory to quick slot.
        /// </summary>
        public void SetQuickSlot(int quickSlotIndex, ItemData item)
        {
            if (quickSlotIndex < 0 || quickSlotIndex >= _quickSlots.Length) return;
            if (item == null) return;

            // Check if we have this item in inventory
            var inventorySlot = _slots.Find(s => s.Item == item);
            if (inventorySlot == null)
            {
                if (debugLog) Debug.LogWarning($"[InventoryManager] Cannot assign {item.ItemName} to quick slot - not in inventory");
                return;
            }

            // If quick slot already has an item, move it back to inventory first
            if (!_quickSlots[quickSlotIndex].IsEmpty)
            {
                var oldItem = _quickSlots[quickSlotIndex].Item;
                var oldQty = _quickSlots[quickSlotIndex].Quantity;
                AddItem(oldItem, oldQty, silent: true); // Silent - internal transfer
                _quickSlots[quickSlotIndex].Clear();
            }

            // Move item from inventory to quick slot
            int qtyToMove = inventorySlot.Quantity;
            _quickSlots[quickSlotIndex].SetItem(item, qtyToMove);

            // Remove from inventory
            inventorySlot.Clear();

            if (debugLog) Debug.Log($"[InventoryManager] Moved {item.ItemName} x{qtyToMove} to Quick Slot {quickSlotIndex + 1}");

            OnQuickSlotChanged?.Invoke(quickSlotIndex);
            OnInventoryChanged?.Invoke();
        }

        /// <summary>
        /// Move item from quick slot back to inventory.
        /// </summary>
        public void MoveQuickSlotToInventory(int quickSlotIndex)
        {
            if (quickSlotIndex < 0 || quickSlotIndex >= _quickSlots.Length) return;

            var quickSlot = _quickSlots[quickSlotIndex];
            if (quickSlot.IsEmpty) return;

            // Add back to inventory (silent = true, no pickup notification)
            AddItem(quickSlot.Item, quickSlot.Quantity, silent: true);

            // Clear quick slot
            quickSlot.Clear();

            if (debugLog) Debug.Log($"[InventoryManager] Moved item from Quick Slot {quickSlotIndex + 1} back to inventory");

            OnQuickSlotChanged?.Invoke(quickSlotIndex);
        }

        /// <summary>
        /// Clear a quick slot (moves item back to inventory).
        /// </summary>
        public void ClearQuickSlot(int quickSlotIndex)
        {
            MoveQuickSlotToInventory(quickSlotIndex);
        }

        /// <summary>
        /// Use item directly from quick slot. Always removes/consumes the item.
        /// </summary>
        public bool UseFromQuickSlot(int quickSlotIndex, GameObject user = null)
        {
            if (quickSlotIndex < 0 || quickSlotIndex >= _quickSlots.Length) return false;

            var quickSlot = _quickSlots[quickSlotIndex];
            if (quickSlot.IsEmpty) return false;

            var item = quickSlot.Item;

            // Try to find player if user not specified
            if (user == null)
            {
                user = GameObject.FindGameObjectWithTag("Player");
                if (user == null) user = GameObject.Find("Player");
                if (user == null)
                {
                    var playerMovement = FindFirstObjectByType<PlayerMovement>();
                    if (playerMovement != null) user = playerMovement.gameObject;
                }
            }

            if (item.Use(user))
            {
                // Always remove item from quick slot after use (consumable or not)
                int remaining = quickSlot.RemoveQuantity(1);

                if (quickSlot.IsEmpty)
                {
                    if (debugLog) Debug.Log($"[InventoryManager] Quick Slot {quickSlotIndex + 1} is now empty after using {item.ItemName}");
                }
                else
                {
                    if (debugLog) Debug.Log($"[InventoryManager] Used {item.ItemName}, {quickSlot.Quantity} remaining in Quick Slot {quickSlotIndex + 1}");
                }

                OnItemUsed?.Invoke(item);
                OnQuickSlotChanged?.Invoke(quickSlotIndex);
                return true;
            }

            return false;
        }

        #endregion

        #region Query Methods

        /// <summary>
        /// Check if the inventory contains an item.
        /// </summary>
        public bool HasItem(ItemData item)
        {
            return GetItemCount(item) > 0;
        }

        /// <summary>
        /// Get total count of an item across all slots.
        /// </summary>
        public int GetItemCount(ItemData item)
        {
            if (item == null) return 0;

            int count = 0;
            foreach (var slot in _slots)
            {
                if (slot.Item == item)
                {
                    count += slot.Quantity;
                }
            }
            return count;
        }

        /// <summary>
        /// Find the first slot containing an item.
        /// </summary>
        public InventorySlot FindSlotWithItem(ItemData item)
        {
            return _slots.Find(s => s.Item == item);
        }

        /// <summary>
        /// Find an empty slot.
        /// </summary>
        public InventorySlot FindEmptySlot()
        {
            return _slots.Find(s => s.IsEmpty);
        }

        /// <summary>
        /// Get all items of a specific type.
        /// </summary>
        public List<InventorySlot> GetItemsByType(ItemType type)
        {
            return _slots.FindAll(s => !s.IsEmpty && s.Item.Type == type);
        }

        /// <summary>
        /// Get slot at index.
        /// </summary>
        public InventorySlot GetSlot(int index)
        {
            if (index < 0 || index >= _slots.Count) return null;
            return _slots[index];
        }

        /// <summary>
        /// Swap items between two slots.
        /// </summary>
        public void SwapSlots(int fromIndex, int toIndex)
        {
            if (fromIndex < 0 || fromIndex >= _slots.Count) return;
            if (toIndex < 0 || toIndex >= _slots.Count) return;
            if (fromIndex == toIndex) return;

            var fromSlot = _slots[fromIndex];
            var toSlot = _slots[toIndex];

            // Store from slot data
            ItemData fromItem = fromSlot.Item;
            int fromQty = fromSlot.Quantity;

            // Store to slot data
            ItemData toItem = toSlot.Item;
            int toQty = toSlot.Quantity;

            // Clear both slots
            fromSlot.Clear();
            toSlot.Clear();

            // Swap: put to's item in from's slot
            if (toItem != null)
            {
                fromSlot.SetItem(toItem, toQty);
            }

            // Put from's item in to's slot
            if (fromItem != null)
            {
                toSlot.SetItem(fromItem, fromQty);
            }

            if (debugLog) Debug.Log($"[InventoryManager] Swapped slot {fromIndex} with slot {toIndex}");

            OnInventoryChanged?.Invoke();
        }

        #endregion

        #region Talisman Equipment

        /// <summary>
        /// Equip a talisman.
        /// </summary>
        public bool EquipTalisman(TalismanData talisman)
        {
            if (talisman == null) return false;
            if (!HasItem(talisman)) return false;

            if (talisman.IsUnique && _equippedTalismans.Contains(talisman))
            {
                if (debugLog) Debug.LogWarning($"[InventoryManager] {talisman.ItemName} is already equipped");
                return false;
            }

            _equippedTalismans.Add(talisman);
            talisman.Use(GameObject.FindGameObjectWithTag("Player"));

            if (debugLog) Debug.Log($"[InventoryManager] Equipped {talisman.ItemName}");
            return true;
        }

        /// <summary>
        /// Unequip a talisman.
        /// </summary>
        public bool UnequipTalisman(TalismanData talisman)
        {
            if (talisman == null) return false;

            if (_equippedTalismans.Remove(talisman))
            {
                talisman.Unequip(GameObject.FindGameObjectWithTag("Player"));
                if (debugLog) Debug.Log($"[InventoryManager] Unequipped {talisman.ItemName}");
                return true;
            }

            return false;
        }

        #endregion

        #region Clear / Reset

        /// <summary>
        /// Clear all temporary items (called on death).
        /// Keeps spirit fragments (permanent).
        /// </summary>
        public void ClearTemporaryItems()
        {
            foreach (var slot in _slots)
            {
                // Keep key items, clear everything else
                if (!slot.IsEmpty && slot.Item.Type != ItemType.KeyItem)
                {
                    slot.Clear();
                }
            }

            foreach (var quickSlot in _quickSlots)
            {
                quickSlot.Clear();
            }

            _equippedTalismans.Clear();

            if (debugLog) Debug.Log("[InventoryManager] Cleared temporary items (run ended)");
            OnInventoryChanged?.Invoke();
        }

        /// <summary>
        /// Clear the entire inventory (for testing/new game).
        /// </summary>
        public void ClearAll()
        {
            foreach (var slot in _slots)
            {
                slot.Clear();
            }

            foreach (var quickSlot in _quickSlots)
            {
                quickSlot.Clear();
            }

            _equippedTalismans.Clear();
            spiritFragments = 0;

            if (debugLog) Debug.Log("[InventoryManager] Cleared all inventory data");
            OnInventoryChanged?.Invoke();
            OnSpiritFragmentsChanged?.Invoke(spiritFragments, 0);
        }

        #endregion

        #region Debug

        [ContextMenu("Debug: Print Inventory")]
        public void DebugPrintInventory()
        {
            Debug.Log("=== INVENTORY ===");
            Debug.Log($"Spirit Fragments: {spiritFragments}");
            Debug.Log($"Slots Used: {UsedSlots}/{maxSlots}");

            foreach (var slot in _slots)
            {
                if (!slot.IsEmpty)
                {
                    Debug.Log(slot.ToString());
                }
            }

            Debug.Log("=== QUICK SLOTS ===");
            for (int i = 0; i < _quickSlots.Length; i++)
            {
                Debug.Log($"Quick {i}: {(_quickSlots[i].IsEmpty ? "[Empty]" : _quickSlots[i].Item.ItemName)}");
            }
        }

        #endregion
    }
}
