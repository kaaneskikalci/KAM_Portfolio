using UnityEngine;

namespace KAM.Core.Combat
{
    /// <summary>
    /// Types of actions enemies can intend to do.
    /// </summary>
    public enum EnemyIntentType
    {
        Unknown,    // Intent not yet decided
        Attack,     // Will attack the player
        Defend,     // Will defend/buff self
        Buff,       // Will buff self or allies
        Debuff,     // Will debuff player
        Special,    // Special ability
        Heal,       // Will heal self or allies
        Summon,     // Will summon minions
        Flee        // Will try to flee
    }

    /// <summary>
    /// Represents what an enemy intends to do on their next turn.
    /// Displayed above enemy health bar to help player make tactical decisions.
    /// </summary>
    [System.Serializable]
    public struct EnemyIntent
    {
        /// <summary>
        /// Type of action the enemy will take.
        /// </summary>
        public EnemyIntentType ActionType;

        /// <summary>
        /// Damage amount if attacking.
        /// </summary>
        public int DamageAmount;

        /// <summary>
        /// Text description of the intent.
        /// </summary>
        public string Description;

        /// <summary>
        /// Number of times the action will be performed (for multi-hit attacks).
        /// </summary>
        public int HitCount;

        /// <summary>
        /// Optional target type (for special abilities).
        /// </summary>
        public string TargetDescription;

        /// <summary>
        /// Returns true if this intent involves dealing damage.
        /// </summary>
        public bool DealsDamage => ActionType == EnemyIntentType.Attack && DamageAmount > 0;

        /// <summary>
        /// Returns true if this intent is defensive.
        /// </summary>
        public bool IsDefensive => ActionType == EnemyIntentType.Defend ||
                                   ActionType == EnemyIntentType.Heal ||
                                   ActionType == EnemyIntentType.Buff;

        /// <summary>
        /// Get the total potential damage (damage * hit count).
        /// </summary>
        public int TotalDamage => DamageAmount * Mathf.Max(1, HitCount);

        /// <summary>
        /// Create an attack intent.
        /// </summary>
        public static EnemyIntent Attack(int damage, int hits = 1)
        {
            return new EnemyIntent
            {
                ActionType = EnemyIntentType.Attack,
                DamageAmount = damage,
                HitCount = hits,
                Description = hits > 1 ? $"Attack x{hits}" : "Attack"
            };
        }

        /// <summary>
        /// Create a defend intent.
        /// </summary>
        public static EnemyIntent Defend()
        {
            return new EnemyIntent
            {
                ActionType = EnemyIntentType.Defend,
                DamageAmount = 0,
                HitCount = 0,
                Description = "Defend"
            };
        }

        /// <summary>
        /// Create an unknown/hidden intent.
        /// </summary>
        public static EnemyIntent Unknown()
        {
            return new EnemyIntent
            {
                ActionType = EnemyIntentType.Unknown,
                DamageAmount = 0,
                HitCount = 0,
                Description = "?"
            };
        }
    }
}
