using UnityEngine;

namespace KAM.Inventory
{
    /// <summary>
    /// Categories of crafting materials
    /// </summary>
    public enum MaterialCategory
    {
        Herb,       // Plants, flowers - for healing potions
        Mineral,    // Stones, gems - for buff potions
        Essence,    // Spirit essences - for powerful effects
        Spirit      // Spirit fragments and rare materials
    }

    /// <summary>
    /// ScriptableObject for crafting materials.
    /// Materials are found in the Spirit Realm and dropped by enemies.
    /// Used to craft potions at the Oba.
    /// </summary>
    [CreateAssetMenu(fileName = "New Material", menuName = "KAM/Inventory/Material")]
    public class MaterialData : ItemData
    {
        [Header("Material Properties")]
        [SerializeField] private MaterialCategory category = MaterialCategory.Herb;

        [Tooltip("Biomes/areas where this material can be found")]
        [SerializeField] private string[] foundInAreas;

        [Tooltip("Enemy types that can drop this material")]
        [SerializeField] private string[] droppedByEnemies;

        [Header("Crafting")]
        [Tooltip("Base crafting value - higher = more potent in recipes")]
        [SerializeField, Range(1, 10)] private int craftingPotency = 1;

        // Public Properties
        public MaterialCategory Category => category;
        public string[] FoundInAreas => foundInAreas;
        public string[] DroppedByEnemies => droppedByEnemies;
        public int CraftingPotency => craftingPotency;

        /// <summary>
        /// Materials cannot be directly used - they are for crafting.
        /// </summary>
        public override bool Use(GameObject user)
        {
            Debug.Log($"[MaterialData] {ItemName} cannot be used directly. Use it for crafting!");
            return false;
        }

        /// <summary>
        /// Get a description including material info.
        /// </summary>
        public string GetFullDescription()
        {
            string desc = Description;
            desc += $"\n\nCategory: {category}";
            desc += $"\nCrafting Potency: {craftingPotency}";

            if (foundInAreas != null && foundInAreas.Length > 0)
            {
                desc += $"\n\nFound in: {string.Join(", ", foundInAreas)}";
            }

            return desc;
        }
    }
}
