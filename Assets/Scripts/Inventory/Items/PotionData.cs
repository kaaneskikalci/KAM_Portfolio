using System;
using UnityEngine;

namespace KAM.Inventory
{
    /// <summary>
    /// Types of potion effects
    /// </summary>
    public enum PotionEffectType
    {
        Heal,           // Restore health
        HealPercent,    // Restore % of max health
        BuffDamage,     // Increase damage
        BuffDefense,    // Increase defense
        BuffSpeed,      // Increase speed (turn order)
        BuffCritical,   // Increase crit chance
        Ultimate,       // Fill ultimate meter
        Cleanse         // Remove debuffs
    }

    /// <summary>
    /// A single ingredient requirement for crafting
    /// </summary>
    [Serializable]
    public class CraftingIngredient
    {
        public MaterialData material;
        public int quantity = 1;
    }

    /// <summary>
    /// ScriptableObject for consumable potions.
    /// Potions are crafted from materials and used during combat (costs Bonus Action).
    /// </summary>
    [CreateAssetMenu(fileName = "New Potion", menuName = "KAM/Inventory/Potion")]
    public class PotionData : ItemData
    {
        [Header("Potion Effect")]
        [SerializeField] private PotionEffectType effectType = PotionEffectType.Heal;

        [Tooltip("Primary effect value (heal amount, buff percentage, etc.)")]
        [SerializeField] private float effectValue = 20f;

        [Tooltip("Duration in turns (0 = instant)")]
        [SerializeField] private int effectDuration = 0;

        [Header("Combat Usage")]
        [Tooltip("Does using this potion cost a Bonus Action?")]
        [SerializeField] private bool requiresBonusAction = true;

        [Tooltip("Cooldown in turns before can use another potion")]
        [SerializeField] private int cooldownTurns = 0;

        [Header("Crafting Recipe")]
        [Tooltip("Materials required to craft this potion")]
        [SerializeField] private CraftingIngredient[] recipe;

        [Tooltip("Is the recipe discovered? (can be unlocked through gameplay)")]
        [SerializeField] private bool recipeKnown = true;

        [Header("Visual Effects")]
        [SerializeField] private Color potionColor = Color.red;
        [SerializeField] private ParticleSystem useEffect;

        // Public Properties
        public PotionEffectType EffectType => effectType;
        public float EffectValue => effectValue;
        public int EffectDuration => effectDuration;
        public bool RequiresBonusAction => requiresBonusAction;
        public int CooldownTurns => cooldownTurns;
        public CraftingIngredient[] Recipe => recipe;
        public bool RecipeKnown => recipeKnown;
        public Color PotionColor => potionColor;

        // Events
        public static event Action<PotionData, GameObject> OnPotionUsed;

        /// <summary>
        /// Use the potion on the target.
        /// </summary>
        public override bool Use(GameObject user)
        {
            Debug.Log($"[PotionData] Using {ItemName} - Effect: {effectType} ({effectValue})");

            // Apply effect based on type
            ApplyEffect(user);

            // Fire event for combat system to handle
            OnPotionUsed?.Invoke(this, user);

            return true;
        }

        private void ApplyEffect(GameObject target)
        {
            // This will integrate with your combat system
            // For now, just log the effect
            string targetName = target != null ? target.name : "Player";

            switch (effectType)
            {
                case PotionEffectType.Heal:
                    Debug.Log($"[PotionData] Healing {targetName} for {effectValue} HP");
                    // TODO: target.GetComponent<CombatUnit>()?.Heal((int)effectValue);
                    break;

                case PotionEffectType.HealPercent:
                    Debug.Log($"[PotionData] Healing {targetName} for {effectValue}% of max HP");
                    break;

                case PotionEffectType.BuffDamage:
                    Debug.Log($"[PotionData] Buffing {targetName} damage by {effectValue}% for {effectDuration} turns");
                    break;

                case PotionEffectType.BuffDefense:
                    Debug.Log($"[PotionData] Buffing {targetName} defense by {effectValue}% for {effectDuration} turns");
                    break;

                case PotionEffectType.BuffSpeed:
                    Debug.Log($"[PotionData] Buffing {targetName} speed by {effectValue}% for {effectDuration} turns");
                    break;

                case PotionEffectType.BuffCritical:
                    Debug.Log($"[PotionData] Buffing {targetName} crit chance by {effectValue}% for {effectDuration} turns");
                    break;

                case PotionEffectType.Ultimate:
                    Debug.Log($"[PotionData] Filling {targetName} ultimate meter by {effectValue}");
                    break;

                case PotionEffectType.Cleanse:
                    Debug.Log($"[PotionData] Cleansing {targetName} of debuffs");
                    break;
            }
        }

        /// <summary>
        /// Check if player has materials to craft this potion.
        /// </summary>
        public bool CanCraft(InventoryManager inventory)
        {
            if (!recipeKnown) return false;
            if (recipe == null || recipe.Length == 0) return false;

            foreach (var ingredient in recipe)
            {
                if (ingredient.material == null) continue;

                int owned = inventory.GetItemCount(ingredient.material);
                if (owned < ingredient.quantity)
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Get effect description for tooltip.
        /// </summary>
        public string GetEffectDescription()
        {
            string effect = effectType switch
            {
                PotionEffectType.Heal => $"Restores {effectValue} HP",
                PotionEffectType.HealPercent => $"Restores {effectValue}% of max HP",
                PotionEffectType.BuffDamage => $"+{effectValue}% damage for {effectDuration} turns",
                PotionEffectType.BuffDefense => $"+{effectValue}% defense for {effectDuration} turns",
                PotionEffectType.BuffSpeed => $"+{effectValue}% speed for {effectDuration} turns",
                PotionEffectType.BuffCritical => $"+{effectValue}% crit chance for {effectDuration} turns",
                PotionEffectType.Ultimate => $"Fills ultimate meter by {effectValue}",
                PotionEffectType.Cleanse => "Removes all debuffs",
                _ => "Unknown effect"
            };

            if (requiresBonusAction)
                effect += "\n<color=#FFAA00>Requires Bonus Action</color>";

            return effect;
        }

        /// <summary>
        /// Get recipe as formatted string.
        /// </summary>
        public string GetRecipeText()
        {
            if (!recipeKnown)
                return "<color=#888888>Recipe not discovered</color>";

            if (recipe == null || recipe.Length == 0)
                return "No recipe (found in world)";

            string text = "Recipe:\n";
            foreach (var ingredient in recipe)
            {
                if (ingredient.material != null)
                {
                    text += $"  - {ingredient.material.ItemName} x{ingredient.quantity}\n";
                }
            }

            return text.TrimEnd('\n');
        }
    }
}
