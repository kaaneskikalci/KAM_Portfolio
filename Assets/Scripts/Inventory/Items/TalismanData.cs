using System;
using UnityEngine;

namespace KAM.Inventory
{
    /// <summary>
    /// Stat modifiers that a talisman can provide
    /// </summary>
    [Serializable]
    public class StatModifier
    {
        public StatType statType;
        public ModifierType modifierType;
        public float value;

        public enum StatType
        {
            Health,         // Max HP
            Damage,         // Attack damage
            Defense,        // Damage reduction
            Speed,          // Turn order priority
            CritChance,     // Critical hit chance
            CritDamage,     // Critical hit multiplier
            LifeSteal,      // % of damage healed
            DodgeChance     // Chance to avoid attacks
        }

        public enum ModifierType
        {
            Flat,           // +10 damage
            Percentage      // +10% damage
        }

        public string GetDescription()
        {
            string sign = value >= 0 ? "+" : "";
            string suffix = modifierType == ModifierType.Percentage ? "%" : "";
            return $"{sign}{value}{suffix} {statType}";
        }
    }

    /// <summary>
    /// Special effects that talismans can grant
    /// </summary>
    public enum TalismanEffect
    {
        None,
        ReflectDamage,      // Reflect % damage back to attacker
        PoisonImmunity,     // Immune to poison effects
        BurnImmunity,       // Immune to burn effects
        SlowImmunity,       // Immune to slow effects
        ReviveOnce,         // Revive with X% HP once per run
        BonusLoot,          // Increased loot drop chance
        SpiritVision,       // See hidden objects (Eye for Nature skill)
        QuickDraw           // Chance for free action
    }

    /// <summary>
    /// ScriptableObject for passive equipment talismans.
    /// Talismans provide stat bonuses and special effects while equipped.
    /// </summary>
    [CreateAssetMenu(fileName = "New Talisman", menuName = "KAM/Inventory/Talisman")]
    public class TalismanData : ItemData
    {
        [Header("Stat Modifiers")]
        [SerializeField] private StatModifier[] statModifiers;

        [Header("Special Effects")]
        [SerializeField] private TalismanEffect specialEffect = TalismanEffect.None;

        [Tooltip("Value for special effect (e.g., reflect %, revive HP %)")]
        [SerializeField] private float specialEffectValue = 0f;

        [Header("Equipment")]
        [Tooltip("Which slot this talisman occupies (0 = any)")]
        [SerializeField, Range(0, 3)] private int equipSlot = 0;

        [Tooltip("Can only have one of this talisman equipped?")]
        [SerializeField] private bool isUnique = false;

        [Header("Requirements")]
        [Tooltip("Minimum level to equip")]
        [SerializeField] private int requiredLevel = 1;

        [Tooltip("Required skill to be unlocked")]
        [SerializeField] private string requiredSkillId;

        // Public Properties
        public StatModifier[] StatModifiers => statModifiers;
        public TalismanEffect SpecialEffect => specialEffect;
        public float SpecialEffectValue => specialEffectValue;
        public int EquipSlot => equipSlot;
        public bool IsUnique => isUnique;
        public int RequiredLevel => requiredLevel;
        public string RequiredSkillId => requiredSkillId;

        // Events
        public static event Action<TalismanData, GameObject> OnTalismanEquipped;
        public static event Action<TalismanData, GameObject> OnTalismanUnequipped;

        /// <summary>
        /// Equip the talisman to the target.
        /// </summary>
        public override bool Use(GameObject user)
        {
            string userName = user != null ? user.name : "Player";
            Debug.Log($"[TalismanData] Equipping {ItemName} to {userName}");
            OnTalismanEquipped?.Invoke(this, user);
            return true;
        }

        /// <summary>
        /// Unequip the talisman from the target.
        /// </summary>
        public void Unequip(GameObject user)
        {
            string userName = user != null ? user.name : "Player";
            Debug.Log($"[TalismanData] Unequipping {ItemName} from {userName}");
            OnTalismanUnequipped?.Invoke(this, user);
        }

        /// <summary>
        /// Check if the user meets requirements to equip this talisman.
        /// </summary>
        public bool CanEquip(GameObject user, int currentLevel)
        {
            if (currentLevel < requiredLevel)
            {
                Debug.Log($"[TalismanData] Cannot equip {ItemName} - requires level {requiredLevel}");
                return false;
            }

            // TODO: Check requiredSkillId against skill tree
            if (!string.IsNullOrEmpty(requiredSkillId))
            {
                // Check if skill is unlocked
                Debug.Log($"[TalismanData] Requires skill: {requiredSkillId}");
            }

            return true;
        }

        /// <summary>
        /// Get total flat modifier for a stat type.
        /// </summary>
        public float GetFlatModifier(StatModifier.StatType statType)
        {
            float total = 0f;
            if (statModifiers == null) return total;

            foreach (var mod in statModifiers)
            {
                if (mod.statType == statType && mod.modifierType == StatModifier.ModifierType.Flat)
                {
                    total += mod.value;
                }
            }
            return total;
        }

        /// <summary>
        /// Get total percentage modifier for a stat type.
        /// </summary>
        public float GetPercentageModifier(StatModifier.StatType statType)
        {
            float total = 0f;
            if (statModifiers == null) return total;

            foreach (var mod in statModifiers)
            {
                if (mod.statType == statType && mod.modifierType == StatModifier.ModifierType.Percentage)
                {
                    total += mod.value;
                }
            }
            return total;
        }

        /// <summary>
        /// Get formatted description of all stat bonuses.
        /// </summary>
        public string GetStatsDescription()
        {
            if (statModifiers == null || statModifiers.Length == 0)
                return "No stat bonuses";

            string desc = "";
            foreach (var mod in statModifiers)
            {
                desc += mod.GetDescription() + "\n";
            }

            if (specialEffect != TalismanEffect.None)
            {
                desc += $"\n<color=#FFD700>Special: {GetSpecialEffectDescription()}</color>";
            }

            return desc.TrimEnd('\n');
        }

        /// <summary>
        /// Get description of the special effect.
        /// </summary>
        public string GetSpecialEffectDescription()
        {
            return specialEffect switch
            {
                TalismanEffect.ReflectDamage => $"Reflect {specialEffectValue}% damage to attackers",
                TalismanEffect.PoisonImmunity => "Immune to poison",
                TalismanEffect.BurnImmunity => "Immune to burn",
                TalismanEffect.SlowImmunity => "Immune to slow",
                TalismanEffect.ReviveOnce => $"Revive with {specialEffectValue}% HP once per run",
                TalismanEffect.BonusLoot => $"+{specialEffectValue}% loot drop chance",
                TalismanEffect.SpiritVision => "Reveal hidden objects",
                TalismanEffect.QuickDraw => $"{specialEffectValue}% chance for free action",
                _ => "None"
            };
        }
    }
}
