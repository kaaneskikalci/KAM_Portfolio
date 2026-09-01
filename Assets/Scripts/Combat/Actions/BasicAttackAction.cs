using UnityEngine;
using System;
using KAM.Core.Combat;

namespace KAM.Combat.Actions
{
    /// <summary>
    /// Kam's basic attack - Soul Ray.
    /// Raises staff and shoots a soul ray at a single enemy.
    /// Deals 10 base damage, can trigger timed hit for +30% damage.
    /// Based on GDD 1.7.3 Basic Attack and 1.7.5 Timed Hit.
    /// </summary>
    [CreateAssetMenu(fileName = "BasicAttack", menuName = "KAM/Combat/Actions/Basic Attack")]
    public class BasicAttackAction : CombatAction
    {
        [Header("Attack Settings")]
        [SerializeField] private int baseDamage = 10;
        [SerializeField] private DamageType damageType = DamageType.Spirit;

        [Header("Timed Hit Settings")]
        [SerializeField] private bool enableTimedHit = true;
        [SerializeField] private float timedHitBonusMultiplier = 1.3f;  // +30% damage

        // Runtime state (set by timed hit system)
        private bool _wasTimedHit = false;
        private float _currentTimedHitMultiplier = 1f;

        // Properties
        public int BaseDamage => baseDamage;
        public bool EnableTimedHit => enableTimedHit;
        public float TimedHitBonusMultiplier => timedHitBonusMultiplier;

        // Events for timed hit integration
        public event Action<BasicAttackAction> OnTimedHitWindowStart;
        public event Action<BasicAttackAction, bool> OnTimedHitWindowEnd;

        /// <summary>
        /// Execute the basic attack.
        /// </summary>
        public override ActionResult Execute(CombatUnit user, CombatUnit target)
        {
            if (!CanExecute(user, target))
            {
                return ActionResult.Failed("Cannot execute attack");
            }

            OnBeforeExecute(user, target);

            // Calculate damage
            int damage = CalculateDamage(user);

            // Deal damage
            DamageResult damageResult;
            if (_wasTimedHit && user is PlayerCombatUnit playerUnit)
            {
                damageResult = playerUnit.DealTimedHitDamage(target, damage, _currentTimedHitMultiplier, damageType);
            }
            else
            {
                damageResult = user.DealDamage(target, damage, damageType);
            }

            bool isCritical = damageResult.DamageInfo.IsCritical;

            // Create result
            ActionResult result = new ActionResult
            {
                Success = damageResult.HitSuccessful,
                DamageDealt = damageResult.DamageDealt,
                WasCritical = isCritical,
                WasTimedHit = _wasTimedHit,
                AffectedUnits = new CombatUnit[] { target },
                Message = BuildResultMessage(damageResult, isCritical, _wasTimedHit)
            };

            // Reset timed hit state
            _wasTimedHit = false;
            _currentTimedHitMultiplier = 1f;

            OnAfterExecute(user, target, result);

            return result;
        }

        /// <summary>
        /// Calculate the damage for this attack.
        /// </summary>
        private int CalculateDamage(CombatUnit user)
        {
            // Start with base damage
            int damage = baseDamage;

            // Add user's effective damage modifier
            if (user.Stats != null)
            {
                damage = Mathf.RoundToInt(damage * user.Stats.DamageModifier);
            }

            return damage;
        }

        /// <summary>
        /// Set the result of the timed hit window.
        /// Called by the TimedHitController.
        /// </summary>
        public void SetTimedHitResult(bool success, float multiplier = 1.3f)
        {
            _wasTimedHit = success;
            _currentTimedHitMultiplier = success ? multiplier : 1f;
        }

        /// <summary>
        /// Request timed hit window to open.
        /// Called during attack animation.
        /// </summary>
        public void RequestTimedHitWindow()
        {
            if (enableTimedHit)
            {
                OnTimedHitWindowStart?.Invoke(this);
            }
        }

        /// <summary>
        /// Notify that timed hit window has ended.
        /// </summary>
        public void NotifyTimedHitWindowEnd(bool success)
        {
            OnTimedHitWindowEnd?.Invoke(this, success);
        }

        /// <summary>
        /// Build the result message for the combat log.
        /// </summary>
        private string BuildResultMessage(DamageResult damageResult, bool critical, bool timedHit)
        {
            if (!damageResult.HitSuccessful)
            {
                if (damageResult.DamageInfo.LockedTargetHit)
                    return "Soul Ray hit a damage lock. Use Key Rune timed hit to break it.";

                return "Soul Ray missed.";
            }

            int damage = damageResult.DamageDealt;
            string message = $"Soul Ray dealt {damage} damage";

            if (timedHit && critical)
                message += " (Timed Hit + Critical!)";
            else if (timedHit)
                message += " (Timed Hit!)";
            else if (critical)
                message += " (Critical!)";

            if (damageResult.DamageInfo.LockedTargetHit)
                message += " - Damage lock broken!";

            if (damageResult.KilledTarget)
                message += " - Target defeated!";

            return message;
        }

        /// <summary>
        /// Get formatted description with actual damage values.
        /// </summary>
        public override string GetFormattedDescription(CombatUnit user)
        {
            int damage = CalculateDamage(user);
            int timedHitDamage = Mathf.RoundToInt(damage * timedHitBonusMultiplier);

            return $"Shoot a soul ray at an enemy.\n" +
                   $"Deals {damage} Spirit damage.\n" +
                   $"Timed Hit: {timedHitDamage} damage (+{(timedHitBonusMultiplier - 1f) * 100:0}%)";
        }

        /// <summary>
        /// Create default instance of basic attack.
        /// </summary>
        public static BasicAttackAction CreateDefault()
        {
            var attack = CreateInstance<BasicAttackAction>();
            attack.actionName = "Soul Ray";
            attack.description = "Shoot a soul ray at an enemy. Time your input for bonus damage!";
            attack.baseDamage = 10;
            attack.damageType = DamageType.Spirit;
            attack.enableTimedHit = true;
            attack.timedHitBonusMultiplier = 1.3f;
            attack.actionPointCost = Core.Stats.ActionPointType.Action;
            attack.targetType = TargetType.SingleEnemy;
            attack.cooldownTurns = 0;
            attack.animationTrigger = "Attack";
            attack.actionDuration = 1f;
            return attack;
        }
    }
}
