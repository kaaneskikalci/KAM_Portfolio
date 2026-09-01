using UnityEngine;
using System;

namespace KAM.Core.Stats
{
    /// <summary>
    /// Base stats for all combat participants (player and enemies).
    /// Handles health, damage, speed, and other core combat attributes.
    /// </summary>
    [Serializable]
    public class UnitStats
    {
        [Header("Health")]
        [SerializeField] private int maxHealth = 100;
        [SerializeField] private int currentHealth;

        [Header("Combat Stats")]
        [SerializeField] private int baseDamage = 10;
        [SerializeField] private int defense = 0;
        [SerializeField] private int speed = 10;

        [Header("Critical Hit")]
        [SerializeField, Range(0f, 1f)] private float criticalChance = 0.05f;
        [SerializeField] private float criticalMultiplier = 1.5f;

        // Properties
        public int MaxHealth => maxHealth;
        public int CurrentHealth => currentHealth;
        public int BaseDamage => baseDamage;
        public int Defense => defense;
        public int Speed => speed;
        public float CriticalChance => criticalChance;
        public float CriticalMultiplier => criticalMultiplier;

        public bool IsAlive => currentHealth > 0;
        public float HealthPercentage => maxHealth > 0 ? (float)currentHealth / maxHealth : 0f;

        // Modifiers (for buffs/debuffs)
        private float _damageModifier = 1f;
        private float _defenseModifier = 1f;
        private float _speedModifier = 1f;

        public float DamageModifier
        {
            get => _damageModifier;
            set => _damageModifier = Mathf.Max(0f, value);
        }

        public float DefenseModifier
        {
            get => _defenseModifier;
            set => _defenseModifier = Mathf.Max(0f, value);
        }

        public float SpeedModifier
        {
            get => _speedModifier;
            set => _speedModifier = Mathf.Max(0f, value);
        }

        // Calculated stats (base * modifier)
        public int EffectiveDamage => Mathf.RoundToInt(baseDamage * _damageModifier);
        public int EffectiveDefense => Mathf.RoundToInt(defense * _defenseModifier);
        public int EffectiveSpeed => Mathf.RoundToInt(speed * _speedModifier);

        // Events
        public event Action<int, int> OnHealthChanged;           // current, max
        public event Action<int> OnDamageTaken;                  // damage amount
        public event Action<int> OnHealed;                       // heal amount
        public event Action OnDeath;

        /// <summary>
        /// Initialize stats at the start of combat or game.
        /// </summary>
        public void Initialize()
        {
            currentHealth = maxHealth;
            ResetModifiers();
        }

        /// <summary>
        /// Initialize with custom max health.
        /// </summary>
        public void Initialize(int customMaxHealth)
        {
            maxHealth = customMaxHealth;
            currentHealth = maxHealth;
            ResetModifiers();
        }

        /// <summary>
        /// Apply damage to the unit, accounting for defense.
        /// </summary>
        /// <param name="rawDamage">Incoming damage before defense calculation</param>
        /// <returns>Actual damage dealt after defense</returns>
        public int TakeDamage(int rawDamage)
        {
            if (!IsAlive) return 0;

            // Calculate damage reduction from defense
            int damageReduction = EffectiveDefense;
            int actualDamage = Mathf.Max(1, rawDamage - damageReduction); // Minimum 1 damage

            currentHealth = Mathf.Max(0, currentHealth - actualDamage);

            OnDamageTaken?.Invoke(actualDamage);
            OnHealthChanged?.Invoke(currentHealth, maxHealth);

            if (currentHealth <= 0)
            {
                OnDeath?.Invoke();
            }

            return actualDamage;
        }

        /// <summary>
        /// Apply raw damage without defense calculation (for effects like bleed).
        /// </summary>
        public int TakeRawDamage(int damage)
        {
            if (!IsAlive) return 0;

            int actualDamage = Mathf.Max(0, damage);
            currentHealth = Mathf.Max(0, currentHealth - actualDamage);

            OnDamageTaken?.Invoke(actualDamage);
            OnHealthChanged?.Invoke(currentHealth, maxHealth);

            if (currentHealth <= 0)
            {
                OnDeath?.Invoke();
            }

            return actualDamage;
        }

        /// <summary>
        /// Heal the unit.
        /// </summary>
        /// <param name="amount">Amount to heal</param>
        /// <returns>Actual amount healed (may be less if near max)</returns>
        public int Heal(int amount)
        {
            if (!IsAlive) return 0;

            int previousHealth = currentHealth;
            currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
            int actualHeal = currentHealth - previousHealth;

            if (actualHeal > 0)
            {
                OnHealed?.Invoke(actualHeal);
                OnHealthChanged?.Invoke(currentHealth, maxHealth);
            }

            return actualHeal;
        }

        /// <summary>
        /// Set health directly (for initialization or special effects).
        /// </summary>
        public void SetHealth(int health)
        {
            currentHealth = Mathf.Clamp(health, 0, maxHealth);
            OnHealthChanged?.Invoke(currentHealth, maxHealth);

            if (currentHealth <= 0)
            {
                OnDeath?.Invoke();
            }
        }

        /// <summary>
        /// Modify max health (for buffs or level ups).
        /// </summary>
        public void SetMaxHealth(int newMaxHealth, bool healToFull = false)
        {
            maxHealth = Mathf.Max(1, newMaxHealth);

            if (healToFull)
            {
                currentHealth = maxHealth;
            }
            else
            {
                currentHealth = Mathf.Min(currentHealth, maxHealth);
            }

            OnHealthChanged?.Invoke(currentHealth, maxHealth);
        }

        /// <summary>
        /// Check if an attack is a critical hit.
        /// </summary>
        public bool RollCritical()
        {
            return UnityEngine.Random.value <= criticalChance;
        }

        /// <summary>
        /// Calculate damage with potential critical hit.
        /// </summary>
        public int CalculateDamage(bool isCritical = false)
        {
            int damage = EffectiveDamage;

            if (isCritical)
            {
                damage = Mathf.RoundToInt(damage * criticalMultiplier);
            }

            return damage;
        }

        /// <summary>
        /// Reset all stat modifiers to default.
        /// </summary>
        public void ResetModifiers()
        {
            _damageModifier = 1f;
            _defenseModifier = 1f;
            _speedModifier = 1f;
        }

        /// <summary>
        /// Create a copy of these stats.
        /// </summary>
        public UnitStats Clone()
        {
            return new UnitStats
            {
                maxHealth = this.maxHealth,
                currentHealth = this.currentHealth,
                baseDamage = this.baseDamage,
                defense = this.defense,
                speed = this.speed,
                criticalChance = this.criticalChance,
                criticalMultiplier = this.criticalMultiplier,
                _damageModifier = this._damageModifier,
                _defenseModifier = this._defenseModifier,
                _speedModifier = this._speedModifier
            };
        }
    }
}
