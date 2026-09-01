using System;
using UnityEngine;

namespace KAM.Inventory
{
    /// <summary>
    /// Represents a single slot in the inventory.
    /// Contains item data and stack count.
    /// </summary>
    [Serializable]
    public class InventorySlot
    {
        [SerializeField] private ItemData item;
        [SerializeField] private int quantity;
        [SerializeField] private int slotIndex;

        // Events
        public event Action<InventorySlot> OnSlotChanged;

        // Properties
        public ItemData Item => item;
        public int Quantity => quantity;
        public int SlotIndex => slotIndex;
        public bool IsEmpty => item == null || quantity <= 0;
        public bool IsFull => item != null && quantity >= item.MaxStackSize;
        public int RemainingSpace => item != null ? item.MaxStackSize - quantity : 0;

        public InventorySlot(int index)
        {
            slotIndex = index;
            item = null;
            quantity = 0;
        }

        public InventorySlot(int index, ItemData itemData, int count = 1)
        {
            slotIndex = index;
            item = itemData;
            quantity = Mathf.Clamp(count, 0, itemData?.MaxStackSize ?? 1);
        }

        /// <summary>
        /// Set the item in this slot.
        /// </summary>
        public void SetItem(ItemData newItem, int count = 1)
        {
            item = newItem;
            quantity = newItem != null ? Mathf.Clamp(count, 1, newItem.MaxStackSize) : 0;
            OnSlotChanged?.Invoke(this);
        }

        /// <summary>
        /// Add quantity to this slot (for stacking).
        /// Returns the amount that couldn't be added (overflow).
        /// </summary>
        public int AddQuantity(int amount)
        {
            if (item == null) return amount;

            int maxAdd = item.MaxStackSize - quantity;
            int actualAdd = Mathf.Min(amount, maxAdd);

            quantity += actualAdd;
            OnSlotChanged?.Invoke(this);

            return amount - actualAdd; // Return overflow
        }

        /// <summary>
        /// Remove quantity from this slot.
        /// Returns the actual amount removed.
        /// </summary>
        public int RemoveQuantity(int amount)
        {
            int actualRemove = Mathf.Min(amount, quantity);
            quantity -= actualRemove;

            if (quantity <= 0)
            {
                Clear();
            }
            else
            {
                OnSlotChanged?.Invoke(this);
            }

            return actualRemove;
        }

        /// <summary>
        /// Clear this slot.
        /// </summary>
        public void Clear()
        {
            item = null;
            quantity = 0;
            OnSlotChanged?.Invoke(this);
        }

        /// <summary>
        /// Check if this slot can accept the given item.
        /// </summary>
        public bool CanAccept(ItemData newItem)
        {
            if (newItem == null) return false;
            if (IsEmpty) return true;
            return item == newItem && !IsFull;
        }

        /// <summary>
        /// Swap contents with another slot.
        /// </summary>
        public void SwapWith(InventorySlot other)
        {
            ItemData tempItem = item;
            int tempQty = quantity;

            item = other.item;
            quantity = other.quantity;

            other.item = tempItem;
            other.quantity = tempQty;

            OnSlotChanged?.Invoke(this);
            other.OnSlotChanged?.Invoke(other);
        }

        /// <summary>
        /// Create a copy of this slot's data for saving.
        /// </summary>
        public InventorySlotSaveData ToSaveData()
        {
            return new InventorySlotSaveData
            {
                itemId = item?.ItemId ?? "",
                quantity = quantity,
                slotIndex = slotIndex
            };
        }

        public override string ToString()
        {
            if (IsEmpty) return $"Slot {slotIndex}: [Empty]";
            return $"Slot {slotIndex}: {item.ItemName} x{quantity}";
        }
    }

    /// <summary>
    /// Serializable save data for an inventory slot.
    /// </summary>
    [Serializable]
    public class InventorySlotSaveData
    {
        public string itemId;
        public int quantity;
        public int slotIndex;
    }
}
