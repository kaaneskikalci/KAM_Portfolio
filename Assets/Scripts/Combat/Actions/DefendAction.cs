using UnityEngine;
using System;
using KAM.Core.Combat;
using KAM.Core.Stats;

namespace KAM.Combat.Actions
{
    /// <summary>
    /// Defensive action - Raise defenses to reduce incoming damage.
    /// Uses Reaction point and lasts until next turn.
    /// Based on GDD 1.7.4 Defense Rune concept.
    /// </summary>
    [CreateAssetMenu(fileName = "Defend", menuName = "KAM/Combat/Actions/Defend")]
    public class DefendAction : CombatAction
    {
        [Header("Defense Settings")]
        [SerializeField, Range(0f, 1f)] private float damageReductionPercent = 0.3f;  // 30% reduction
        [SerializeField] private int durationTurns = 2; // 2 so it survives player's turn end and lasts through enemy turn

        [Header("Defense Rune (Timed)")]
        [SerializeField] private bool enableDefenseRune = true;
        [SerializeField, Range(0f, 1f)] private float timedDefenseReduction = 0.3f;   // Additional 30% on success
        [SerializeField, Range(0f, 1f)] private float perfectTimingReduction = 0.6f;  // 60% on perfect timing

        // Runtime state
        private bool _isDefending = false;
        private int _turnsRemaining = 0;
        private float _currentReduction = 0f;

        // Properties
        public float DamageReductionPercent => damageReductionPercent;
        public bool IsDefending => _isDefending;
        public int TurnsRemaining => _turnsRemaining;
        public float CurrentReduction => _currentReduction;

        // Events
        public event Action<DefendAction, CombatUnit> OnDefenseStarted;
        public event Action<DefendAction, CombatUnit> OnDefenseEnded;
        public event Action<DefendAction, float> OnDamageBlocked;

        private void OnEnable()
        {
            // Defend is Free — doesn't consume AP, allowing AP to accumulate
            actionName = "Defend";
            description = "Guard to reduce damage and save your action point for next turn.";
            actionPointCost = ActionPointType.Free;
            targetType = TargetType.Self;
            requiresTarget = false;
        }

        /// <summary>
        /// Execute the defend action.
        /// Grants a temporary shield (defense buff) without consuming AP,
        /// so the player can accumulate AP for next turn.
        /// </summary>
        public override ActionResult Execute(CombatUnit user, CombatUnit target)
        {
            if (user == null || !user.IsAlive)
                return ActionResult.Failed("Cannot defend");

            OnBeforeExecute(user, target ?? user);

            // Apply defensive stance
            _isDefending = true;
            _turnsRemaining = durationTurns;
            _currentReduction = damageReductionPercent;

            // Apply shield as a proper status effect so it shows in UI
            var shieldEffect = StatusEffect.CreateDefenseUp(damageReductionPercent, durationTurns);
            user.ApplyEffect(shieldEffect);

            OnDefenseStarted?.Invoke(this, user);

            ActionResult result = new ActionResult
            {
                Success = true,
                Message = $"Raised shield! -{_currentReduction * 100:0}% damage for {_turnsRemaining} turn(s). AP saved!",
                AffectedUnits = new CombatUnit[] { user }
            };

            OnAfterExecute(user, target ?? user, result);

            return result;
        }

        /// <summary>
        /// Apply timed defense result (from Defense Rune).
        /// Called when enemy attacks and player times the defense.
        /// </summary>
        public void ApplyTimedDefense(bool success, bool perfectTiming = false)
        {
            if (!enableDefenseRune) return;

            if (perfectTiming)
            {
                _currentReduction = perfectTimingReduction;
            }
            else if (success)
            {
                _currentReduction = damageReductionPercent + timedDefenseReduction;
            }
        }

        /// <summary>
        /// Called when damage is incoming. Returns the damage after reduction.
        /// </summary>
        public int ProcessIncomingDamage(int incomingDamage, CombatUnit defender)
        {
            if (!_isDefending || !defender.IsAlive)
            {
                return incomingDamage;
            }

            int blockedDamage = Mathf.RoundToInt(incomingDamage * _currentReduction);
            int finalDamage = incomingDamage - blockedDamage;

            OnDamageBlocked?.Invoke(this, blockedDamage);

            Debug.Log($"Defense blocked {blockedDamage} damage ({_currentReduction * 100:0}% reduction)");

            return Mathf.Max(1, finalDamage); // Minimum 1 damage
        }

        /// <summary>
        /// Tick defense duration at end of turn.
        /// </summary>
        public void TickDefense(CombatUnit user)
        {
            if (!_isDefending) return;

            _turnsRemaining--;

            if (_turnsRemaining <= 0)
            {
                EndDefense(user);
            }
        }

        /// <summary>
        /// End the defensive stance.
        /// </summary>
        public void EndDefense(CombatUnit user)
        {
            if (!_isDefending) return;

            _isDefending = false;
            _turnsRemaining = 0;
            _currentReduction = 0f;

            OnDefenseEnded?.Invoke(this, user);
        }

        /// <summary>
        /// Get formatted description.
        /// </summary>
        public override string GetFormattedDescription(CombatUnit user)
        {
            string desc = $"Raise your guard, reducing incoming damage by {damageReductionPercent * 100:0}%.\n";
            desc += $"Lasts for {durationTurns} turn(s).\n";

            if (enableDefenseRune)
            {
                desc += $"\nDefense Rune: Time your input when attacked!\n";
                desc += $"- Success: {(damageReductionPercent + timedDefenseReduction) * 100:0}% reduction\n";
                desc += $"- Perfect: {perfectTimingReduction * 100:0}% reduction";
            }

            return desc;
        }

        /// <summary>
        /// Create default instance.
        /// </summary>
        public static DefendAction CreateDefault()
        {
            var defend = CreateInstance<DefendAction>();
            defend.actionName = "Defend";
            defend.description = "Raise your guard to reduce incoming damage. Time your input for extra protection!";
            defend.damageReductionPercent = 0.3f;
            defend.durationTurns = 2;
            defend.enableDefenseRune = true;
            defend.timedDefenseReduction = 0.3f;
            defend.perfectTimingReduction = 0.6f;
            defend.actionPointCost = ActionPointType.Free;
            defend.targetType = TargetType.Self;
            defend.requiresTarget = false;
            defend.animationTrigger = "Defend";
            defend.actionDuration = 0.5f;
            return defend;
        }
    }
}
