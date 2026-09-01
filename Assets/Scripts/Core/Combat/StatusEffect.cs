using UnityEngine;
using System;

namespace KAM.Core.Combat
{
    /// <summary>
    /// Type of status effect.
    /// </summary>
    public enum StatusEffectType
    {
        Buff,       // Positive effect
        Debuff,     // Negative effect
        Neutral     // Neither positive nor negative
    }

    /// <summary>
    /// Categories of status effects.
    /// </summary>
    public enum StatusEffectCategory
    {
        // Damage over time
        Poison,
        Burn,
        Bleed,

        // Stat modifiers
        StrengthUp,
        StrengthDown,
        DefenseUp,
        DefenseDown,
        SpeedUp,
        SpeedDown,

        // Control effects
        Stun,
        Freeze,
        Silence,

        // Special
        Regeneration,
        Shield,
        Vulnerable,
        Weak,
        Custom,
        Blind,
        Decay,
        Acid,
        Evasion
    }

    /// <summary>
    /// Base class for all status effects that can be applied to combat units.
    /// Status effects can modify stats, deal damage over time, or provide other effects.
    /// </summary>
    [System.Serializable]
    public class StatusEffect
    {
        [Header("Effect Info")]
        [SerializeField] private string effectName = "Status Effect";
        [SerializeField] private string description = "";
        [SerializeField] private Sprite icon;
        [SerializeField] private StatusEffectType effectType = StatusEffectType.Debuff;
        [SerializeField] private StatusEffectCategory category = StatusEffectCategory.Custom;

        [Header("Duration")]
        [SerializeField] private int duration = 3; // Turns remaining
        [SerializeField] private bool isPermanent = false;

        [Header("Effect Values")]
        [SerializeField] private int damagePerTurn = 0;
        [SerializeField] private int healPerTurn = 0;
        [SerializeField] private float statModifier = 0f; // +/- percentage

        [Header("Visual")]
        [SerializeField] private Color effectColor = Color.white;

        // Runtime
        private int _turnsRemaining;
        private CombatUnit _target;
        private CombatUnit _source;

        // Events
        public event Action<StatusEffect> OnEffectExpired;
        public event Action<StatusEffect, int> OnEffectTick; // effect, damage/heal amount

        // Properties
        public string EffectName => effectName;
        public string Description => description;
        public Sprite Icon => icon;
        public StatusEffectType EffectType => effectType;
        public StatusEffectCategory Category => category;
        public int TurnsRemaining => _turnsRemaining;
        public bool IsExpired => !isPermanent && _turnsRemaining <= 0;
        public bool IsPermanent => isPermanent;
        public Color EffectColor => effectColor;
        public float StatModifier => statModifier;
        public CombatUnit Target => _target;
        public CombatUnit Source => _source;

        /// <summary>
        /// Create a new status effect instance.
        /// </summary>
        public StatusEffect()
        {
            _turnsRemaining = duration;
        }

        /// <summary>
        /// Create a status effect with parameters.
        /// </summary>
        public StatusEffect(string name, StatusEffectType type, StatusEffectCategory cat, int turns, Sprite effectIcon = null)
        {
            effectName = name;
            effectType = type;
            category = cat;
            duration = turns;
            _turnsRemaining = turns;
            icon = effectIcon;

            // Set default color based on type
            switch (type)
            {
                case StatusEffectType.Buff:
                    effectColor = new Color(0.3f, 1f, 0.3f); // Green
                    break;
                case StatusEffectType.Debuff:
                    effectColor = new Color(1f, 0.3f, 0.3f); // Red
                    break;
                default:
                    effectColor = Color.white;
                    break;
            }
        }

        /// <summary>
        /// Apply this effect to a target.
        /// </summary>
        public virtual void Apply(CombatUnit target, CombatUnit source = null)
        {
            _target = target;
            _source = source;
            _turnsRemaining = duration;

            Debug.Log($"[StatusEffect] Applied {effectName} to {target.UnitName} for {duration} turns");
        }

        /// <summary>
        /// Called at the start of the affected unit's turn.
        /// </summary>
        public virtual void OnTurnStart()
        {
            // Apply damage over time (DOT ignores defense - uses raw damage)
            if (damagePerTurn > 0 && _target != null)
            {
                _target.Stats.TakeRawDamage(damagePerTurn);
                OnEffectTick?.Invoke(this, damagePerTurn);
                Debug.Log($"[StatusEffect] {effectName} dealt {damagePerTurn} damage to {_target.UnitName}");
            }

            // Apply healing over time
            if (healPerTurn > 0 && _target != null)
            {
                _target.Stats.Heal(healPerTurn);
                OnEffectTick?.Invoke(this, -healPerTurn); // Negative = healing
                Debug.Log($"[StatusEffect] {effectName} healed {_target.UnitName} for {healPerTurn}");
            }
        }

        /// <summary>
        /// Called at the end of the affected unit's turn.
        /// </summary>
        public virtual void OnTurnEnd()
        {
            if (!isPermanent)
            {
                _turnsRemaining--;

                if (_turnsRemaining <= 0)
                {
                    Expire();
                }
            }
        }

        /// <summary>
        /// Remove this effect.
        /// </summary>
        public virtual void Expire()
        {
            Debug.Log($"[StatusEffect] {effectName} expired on {_target?.UnitName}");
            OnEffectExpired?.Invoke(this);
        }

        /// <summary>
        /// Refresh the duration (for stacking effects).
        /// </summary>
        public void RefreshDuration()
        {
            _turnsRemaining = duration;
        }

        /// <summary>
        /// Add turns to the duration.
        /// </summary>
        public void ExtendDuration(int turns)
        {
            _turnsRemaining += turns;
        }

        #region Factory Methods

        /// <summary>
        /// Create a poison effect.
        /// </summary>
        public static StatusEffect CreatePoison(int damage, int duration)
        {
            var effect = new StatusEffect("Poison", StatusEffectType.Debuff, StatusEffectCategory.Poison, duration);
            effect.damagePerTurn = damage;
            effect.effectColor = new Color(0.5f, 0.8f, 0.2f); // Toxic green
            effect.description = $"Takes {damage} damage per turn";
            return effect;
        }

        /// <summary>
        /// Create a burn effect.
        /// </summary>
        public static StatusEffect CreateBurn(int damage, int duration)
        {
            var effect = new StatusEffect("Burn", StatusEffectType.Debuff, StatusEffectCategory.Burn, duration);
            effect.damagePerTurn = damage;
            effect.effectColor = new Color(1f, 0.5f, 0f); // Orange
            effect.description = $"Takes {damage} fire damage per turn";
            return effect;
        }

        /// <summary>
        /// Create a regeneration effect.
        /// </summary>
        public static StatusEffect CreateRegeneration(int healing, int duration)
        {
            var effect = new StatusEffect("Regeneration", StatusEffectType.Buff, StatusEffectCategory.Regeneration, duration);
            effect.healPerTurn = healing;
            effect.effectColor = new Color(0.3f, 1f, 0.5f); // Light green
            effect.description = $"Heals {healing} HP per turn";
            return effect;
        }

        /// <summary>
        /// Create a defense up effect.
        /// </summary>
        public static StatusEffect CreateDefenseUp(float modifier, int duration)
        {
            var effect = new StatusEffect("Defense Up", StatusEffectType.Buff, StatusEffectCategory.DefenseUp, duration);
            effect.statModifier = modifier;
            effect.effectColor = new Color(0.3f, 0.5f, 1f); // Blue
            effect.description = $"+{modifier * 100:F0}% Defense";
            return effect;
        }

        /// <summary>
        /// Create a strength up effect.
        /// </summary>
        public static StatusEffect CreateStrengthUp(float modifier, int duration)
        {
            var effect = new StatusEffect("Strength Up", StatusEffectType.Buff, StatusEffectCategory.StrengthUp, duration);
            effect.statModifier = modifier;
            effect.effectColor = new Color(1f, 0.3f, 0.3f); // Red
            effect.description = $"+{modifier * 100:F0}% Damage";
            return effect;
        }

        /// <summary>
        /// Create a vulnerable effect (takes more damage).
        /// </summary>
        public static StatusEffect CreateVulnerable(float modifier, int duration)
        {
            var effect = new StatusEffect("Vulnerable", StatusEffectType.Debuff, StatusEffectCategory.Vulnerable, duration);
            effect.statModifier = modifier;
            effect.effectColor = new Color(0.8f, 0.3f, 0.8f); // Purple
            effect.description = $"Takes {modifier * 100:F0}% more damage";
            return effect;
        }

        /// <summary>
        /// Create a weak effect (deals less damage).
        /// </summary>
        public static StatusEffect CreateWeak(float modifier, int duration)
        {
            var effect = new StatusEffect("Weak", StatusEffectType.Debuff, StatusEffectCategory.Weak, duration);
            effect.statModifier = modifier;
            effect.effectColor = new Color(0.6f, 0.6f, 0.6f); // Gray
            effect.description = $"Deals {modifier * 100:F0}% less damage";
            return effect;
        }

        /// <summary>
        /// Create a blind effect with a miss chance.
        /// </summary>
        public static StatusEffect CreateBlind(float missChance, int duration)
        {
            var effect = new StatusEffect("Blind", StatusEffectType.Debuff, StatusEffectCategory.Blind, duration);
            effect.statModifier = Mathf.Clamp01(missChance);
            effect.effectColor = new Color(0.9f, 0.9f, 0.2f); // Yellow
            effect.description = $"{missChance * 100:F0}% chance to miss attacks";
            return effect;
        }

        /// <summary>
        /// Create a speed down effect.
        /// </summary>
        public static StatusEffect CreateSpeedDown(float modifier, int duration)
        {
            var effect = new StatusEffect("Speed Down", StatusEffectType.Debuff, StatusEffectCategory.SpeedDown, duration);
            effect.statModifier = Mathf.Clamp01(modifier);
            effect.effectColor = new Color(0.3f, 0.8f, 1f); // Cyan
            effect.description = $"-{modifier * 100:F0}% Speed";
            return effect;
        }

        /// <summary>
        /// Create a strength down effect.
        /// </summary>
        public static StatusEffect CreateStrengthDown(float modifier, int duration)
        {
            var effect = new StatusEffect("Strength Down", StatusEffectType.Debuff, StatusEffectCategory.StrengthDown, duration);
            effect.statModifier = Mathf.Clamp01(modifier);
            effect.effectColor = new Color(0.5f, 0.5f, 0.5f); // Gray
            effect.description = $"-{modifier * 100:F0}% Damage";
            return effect;
        }

        /// <summary>
        /// Create a Bleed effect (damage over time, Bleed category).
        /// Used by Echo of Ancestors skill.
        /// </summary>
        public static StatusEffect CreateBleed(int damage, int duration)
        {
            var effect = new StatusEffect("Bleed", StatusEffectType.Debuff, StatusEffectCategory.Bleed, duration);
            effect.damagePerTurn = damage;
            effect.effectColor = new Color(0.8f, 0.2f, 0.2f); // Dark red
            effect.description = $"Bleeding for {damage} damage per turn";
            return effect;
        }

        /// <summary>
        /// Create a decay effect used by spirit enemies.
        /// </summary>
        public static StatusEffect CreateDecay(int damage, int duration)
        {
            var effect = new StatusEffect("Decay", StatusEffectType.Debuff, StatusEffectCategory.Decay, duration);
            effect.damagePerTurn = damage;
            effect.effectColor = new Color(0.45f, 0.65f, 0.35f);
            effect.description = $"Decays for {damage} damage per turn";
            return effect;
        }

        /// <summary>
        /// Create an acid effect used by snake-like enemies.
        /// </summary>
        public static StatusEffect CreateAcid(int damage, int duration)
        {
            var effect = new StatusEffect("Acid", StatusEffectType.Debuff, StatusEffectCategory.Acid, duration);
            effect.damagePerTurn = damage;
            effect.effectColor = new Color(0.55f, 1f, 0.2f);
            effect.description = $"Acid burns for {damage} damage per turn";
            return effect;
        }

        /// <summary>
        /// Create a stun effect that blocks the target's next action.
        /// </summary>
        public static StatusEffect CreateStun(int duration)
        {
            var effect = new StatusEffect("Stun", StatusEffectType.Debuff, StatusEffectCategory.Stun, duration);
            effect.effectColor = new Color(1f, 0.9f, 0.2f);
            effect.description = "Cannot act";
            return effect;
        }

        /// <summary>
        /// Create a speed up effect.
        /// </summary>
        public static StatusEffect CreateSpeedUp(float modifier, int duration)
        {
            var effect = new StatusEffect("Speed Up", StatusEffectType.Buff, StatusEffectCategory.SpeedUp, duration);
            effect.statModifier = Mathf.Clamp01(modifier);
            effect.effectColor = new Color(0.2f, 1f, 0.7f);
            effect.description = $"+{modifier * 100:F0}% Speed";
            return effect;
        }

        /// <summary>
        /// Create an evasion effect with a dodge chance.
        /// </summary>
        public static StatusEffect CreateEvasion(float dodgeChance, int duration)
        {
            var effect = new StatusEffect("Evasion", StatusEffectType.Buff, StatusEffectCategory.Evasion, duration);
            effect.statModifier = Mathf.Clamp01(dodgeChance);
            effect.effectColor = new Color(0.7f, 0.7f, 1f);
            effect.description = $"{dodgeChance * 100:F0}% chance to dodge attacks";
            return effect;
        }

        #endregion
    }
}
