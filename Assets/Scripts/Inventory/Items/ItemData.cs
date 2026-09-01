using UnityEngine;

namespace KAM.Inventory
{
    /// <summary>
    /// Item type categories
    /// </summary>
    public enum ItemType
    {
        Material,   // Crafting ingredients
        Potion,     // Consumables (uses Bonus Action)
        Talisman,   // Passive equipment
        KeyItem     // Quest/story items
    }

    /// <summary>
    /// Item rarity levels - affects drop rates and visuals
    /// </summary>
    public enum ItemRarity
    {
        Common,     // White
        Uncommon,   // Green
        Rare,       // Blue
        Epic,       // Purple
        Legendary   // Orange/Gold
    }

    /// <summary>
    /// Base ScriptableObject for all items in the game.
    /// Create specific item types by extending this class or using it directly.
    /// </summary>
    [CreateAssetMenu(fileName = "New Item", menuName = "KAM/Inventory/Item Data")]
    public class ItemData : ScriptableObject
    {
        [Header("Basic Info")]
        [Tooltip("Unique identifier for this item")]
        [SerializeField] private string itemId;

        [Tooltip("Display name shown to player")]
        [SerializeField] private string itemName;

        [Tooltip("Description shown in tooltip")]
        [SerializeField, TextArea(2, 5)] private string description;

        [Tooltip("Icon displayed in inventory")]
        [SerializeField] private Sprite icon;

        [Header("Classification")]
        [SerializeField] private ItemType itemType = ItemType.Material;
        [SerializeField] private ItemRarity rarity = ItemRarity.Common;

        [Header("Stacking")]
        [Tooltip("Maximum number that can stack in one slot")]
        [SerializeField, Range(1, 999)] private int maxStackSize = 99;

        [Header("Usage")]
        [Tooltip("Can this item be consumed/used?")]
        [SerializeField] private bool isConsumable = false;

        [Tooltip("Can this item be dropped?")]
        [SerializeField] private bool isDroppable = true;

        [Tooltip("Can this item be sold?")]
        [SerializeField] private bool isSellable = true;

        [Tooltip("Base sell value in Spirit Fragments")]
        [SerializeField] private int sellValue = 1;

        [Header("Audio/Visual")]
        [SerializeField] private AudioClip pickupSound;
        [SerializeField] private AudioClip useSound;

        // Public Properties
        public string ItemId => string.IsNullOrEmpty(itemId) ? name : itemId;
        public string ItemName => itemName;
        public string Description => description;
        public Sprite Icon => icon;
        public ItemType Type => itemType;
        public ItemRarity Rarity => rarity;
        public int MaxStackSize => maxStackSize;
        public bool IsConsumable => isConsumable;
        public bool IsDroppable => isDroppable;
        public bool IsSellable => isSellable;
        public int SellValue => sellValue;
        public AudioClip PickupSound => pickupSound;
        public AudioClip UseSound => useSound;

        /// <summary>
        /// Called when the item is used. Override in derived classes for specific behavior.
        /// </summary>
        /// <param name="user">The GameObject using the item (usually player)</param>
        /// <returns>True if the item was successfully used</returns>
        public virtual bool Use(GameObject user)
        {
            if (!isConsumable)
            {
                Debug.Log($"[ItemData] {itemName} is not consumable");
                return false;
            }

            Debug.Log($"[ItemData] {itemName} used by {user.name}");
            return true;
        }

        /// <summary>
        /// Get the color associated with this item's rarity.
        /// </summary>
        public Color GetRarityColor()
        {
            return rarity switch
            {
                ItemRarity.Common => Color.white,
                ItemRarity.Uncommon => new Color(0.2f, 0.8f, 0.2f),    // Green
                ItemRarity.Rare => new Color(0.2f, 0.4f, 1f),          // Blue
                ItemRarity.Epic => new Color(0.6f, 0.2f, 0.8f),        // Purple
                ItemRarity.Legendary => new Color(1f, 0.6f, 0f),       // Orange
                _ => Color.white
            };
        }

        /// <summary>
        /// Get formatted name with rarity color (for rich text).
        /// </summary>
        public string GetColoredName()
        {
            Color color = GetRarityColor();
            string hex = ColorUtility.ToHtmlStringRGB(color);
            return $"<color=#{hex}>{itemName}</color>";
        }

        private void OnValidate()
        {
            // Auto-generate ID from asset name if empty
            if (string.IsNullOrEmpty(itemId))
            {
                itemId = name.ToLower().Replace(" ", "_");
            }
        }
    }
}
