using UnityEngine;

namespace KAM.Core.Combat
{
    /// <summary>
    /// Data structure containing all information about a damage instance.
    /// Used for damage calculation, UI feedback, and combat logging.
    /// </summary>
    public struct DamageInfo
    {
        /// <summary>The unit dealing the damage</summary>
        public CombatUnit Source;

        /// <summary>The unit receiving the damage</summary>
        public CombatUnit Target;

        /// <summary>Base damage before modifiers</summary>
        public int BaseDamage;

        /// <summary>Final damage after all calculations</summary>
        public int FinalDamage;

        /// <summary>Type of damage being dealt</summary>
        public DamageType Type;

        /// <summary>Was this a critical hit?</summary>
        public bool IsCritical;

        /// <summary>Was this damage from a timed hit success?</summary>
        public bool IsTimedHit;

        /// <summary>Timed hit bonus damage multiplier (1.0 = no bonus)</summary>
        public float TimedHitMultiplier;

        /// <summary>Was the damage blocked/reduced by defense?</summary>
        public int DamageBlocked;

        /// <summary>Was the target's damage lock broken for this hit?</summary>
        public bool LockedTargetHit;

        /// <summary>Additional damage from buffs/skills</summary>
        public int BonusDamage;

        /// <summary>
        /// Create a basic damage info structure.
        /// </summary>
        public static DamageInfo Create(CombatUnit source, CombatUnit target, int baseDamage, DamageType type = DamageType.Physical)
        {
            return new DamageInfo
            {
                Source = source,
                Target = target,
                BaseDamage = baseDamage,
                FinalDamage = baseDamage,
                Type = type,
                IsCritical = false,
                IsTimedHit = false,
                TimedHitMultiplier = 1f,
                DamageBlocked = 0,
                LockedTargetHit = false,
                BonusDamage = 0
            };
        }

        /// <summary>
        /// Apply critical hit multiplier.
        /// </summary>
        public void ApplyCritical(float critMultiplier)
        {
            IsCritical = true;
            FinalDamage = Mathf.RoundToInt(FinalDamage * critMultiplier);
        }

        /// <summary>
        /// Apply timed hit bonus (from Rune skill).
        /// </summary>
        public void ApplyTimedHitBonus(float multiplier = 1.3f)
        {
            IsTimedHit = true;
            TimedHitMultiplier = multiplier;
            FinalDamage = Mathf.RoundToInt(FinalDamage * multiplier);
        }

        /// <summary>
        /// Apply defense reduction.
        /// </summary>
        public void ApplyDefense(int defense)
        {
            int reduction = Mathf.Min(defense, FinalDamage - 1); // Always deal at least 1 damage
            DamageBlocked = reduction;
            FinalDamage -= reduction;
        }

        /// <summary>
        /// Add bonus damage from skills/buffs.
        /// </summary>
        public void AddBonusDamage(int bonus)
        {
            BonusDamage += bonus;
            FinalDamage += bonus;
        }

        /// <summary>
        /// Calculate total damage dealt (for logging/UI).
        /// </summary>
        public int GetTotalDamage()
        {
            return FinalDamage;
        }

        /// <summary>
        /// Get a formatted string describing this damage for combat log.
        /// </summary>
        public string GetCombatLogEntry()
        {
            string sourceName = Source != null ? Source.UnitName : "Unknown";
            string targetName = Target != null ? Target.UnitName : "Unknown";

            string message = $"{sourceName} dealt {FinalDamage} {Type} damage to {targetName}";

            if (IsCritical)
                message += " (CRITICAL!)";

            if (IsTimedHit)
                message += " (Timed Hit!)";

            if (DamageBlocked > 0)
                message += $" ({DamageBlocked} blocked)";

            return message;
        }
    }

    /// <summary>
    /// Types of damage that can be dealt.
    /// Different types may have different resistances/weaknesses.
    /// </summary>
    public enum DamageType
    {
        /// <summary>Standard physical damage</summary>
        Physical,

        /// <summary>Spirit/Soul damage from shaman abilities</summary>
        Spirit,

        /// <summary>Damage over time effects (bleed, echo)</summary>
        DoT,

        /// <summary>True damage - ignores defense</summary>
        True,

        /// <summary>Environmental hazard damage</summary>
        Environmental
    }

    /// <summary>
    /// Result of a damage application.
    /// </summary>
    public struct DamageResult
    {
        /// <summary>The damage info that was applied</summary>
        public DamageInfo DamageInfo;

        /// <summary>Actual damage dealt after all reductions</summary>
        public int DamageDealt;

        /// <summary>Did this kill the target?</summary>
        public bool KilledTarget;

        /// <summary>Target's remaining health after damage</summary>
        public int TargetRemainingHealth;

        /// <summary>Was the hit successful (not missed/dodged)?</summary>
        public bool HitSuccessful;

        /// <summary>
        /// Create a successful damage result.
        /// </summary>
        public static DamageResult Success(DamageInfo info, int damageDealt, int remainingHealth, bool killed)
        {
            return new DamageResult
            {
                DamageInfo = info,
                DamageDealt = damageDealt,
                KilledTarget = killed,
                TargetRemainingHealth = remainingHealth,
                HitSuccessful = true
            };
        }

        /// <summary>
        /// Create a missed/blocked damage result.
        /// </summary>
        public static DamageResult Miss(DamageInfo info)
        {
            return new DamageResult
            {
                DamageInfo = info,
                DamageDealt = 0,
                KilledTarget = false,
                TargetRemainingHealth = info.Target?.Stats?.CurrentHealth ?? 0,
                HitSuccessful = false
            };
        }
    }
}
