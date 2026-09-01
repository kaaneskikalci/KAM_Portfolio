using UnityEngine;
using KAM.Core.Combat;

namespace KAM.Combat.Actions
{
    /// <summary>
    /// Skill 8: Echo of Ancestors - Skip action to apply a damage echo (bleed)
    /// to all enemies for 3 turns at 15% of base damage per turn.
    /// Based on GDD 1.7.6 Skill Tree.
    /// </summary>
    [CreateAssetMenu(fileName = "EchoOfAncestors", menuName = "KAM/Combat/Actions/Echo of Ancestors")]
    public class EchoOfAncestorsAction : CombatAction
    {
        [Header("Echo Settings")]
        [SerializeField] private float echoDamagePercent = 0.15f; // 15% of base damage per turn
        [SerializeField] private int echoDuration = 3; // 3 turns of bleed

        public float EchoDamagePercent => echoDamagePercent;
        public int EchoDuration => echoDuration;

        private void OnEnable()
        {
            actionName = "Echo of Ancestors";
            description = "Channel ancestral power. Applies bleed to all enemies for 3 turns.";
            actionPointCost = Core.Stats.ActionPointType.Action;
            actionPointCount = 2;
            targetType = TargetType.AllEnemies;
            requiresTarget = false;
            cooldownTurns = 0;
            animationTrigger = "Echo";
            actionDuration = 1f;
        }

        public override ActionResult Execute(CombatUnit user, CombatUnit target)
        {
            if (user == null || !user.IsAlive)
                return ActionResult.Failed("Cannot execute Echo of Ancestors");

            if (user is PlayerCombatUnit playerUnit && !playerUnit.CanPerformAction(ActionPointCost, ActionPointCount))
                return ActionResult.Failed("Not enough action points");

            OnBeforeExecute(user, null);

            // Calculate bleed damage per turn: 15% of base damage
            int bleedDamage = Mathf.Max(1, Mathf.RoundToInt(user.Stats.EffectiveDamage * echoDamagePercent));

            // Apply bleed to all alive enemies
            var allUnits = Object.FindObjectsByType<CombatUnit>(FindObjectsSortMode.None);
            int enemiesAffected = 0;
            CombatUnit[] affectedUnits = new CombatUnit[allUnits.Length];
            int affectedCount = 0;

            foreach (var unit in allUnits)
            {
                if (unit != null && unit.IsAlive && unit.Team != user.Team)
                {
                    var echoBleed = StatusEffect.CreateBleed(bleedDamage, echoDuration);
                    unit.ApplyEffect(echoBleed);

                    enemiesAffected++;
                    affectedUnits[affectedCount++] = unit;
                }
            }

            var trimmed = new CombatUnit[affectedCount];
            System.Array.Copy(affectedUnits, trimmed, affectedCount);

            ActionResult actionResult = new ActionResult
            {
                Success = true,
                AffectedUnits = trimmed,
                Message = $"Echo of Ancestors! {enemiesAffected} enemies bleed for {bleedDamage}/turn for {echoDuration} turns!"
            };

            OnAfterExecute(user, null, actionResult);

            return actionResult;
        }

        public override string GetFormattedDescription(CombatUnit user)
        {
            int bleedDmg = user != null ? Mathf.Max(1, Mathf.RoundToInt(user.Stats.EffectiveDamage * echoDamagePercent)) : 0;
            string desc = $"Channel the power of your ancestors.\n";
            desc += $"All enemies bleed for {bleedDmg} damage per turn for {echoDuration} turns.\n";
            desc += $"({echoDamagePercent * 100:0}% of base damage per turn)";
            return desc;
        }

        public static EchoOfAncestorsAction CreateDefault()
        {
            var action = CreateInstance<EchoOfAncestorsAction>();
            action.actionName = "Echo of Ancestors";
            action.description = "Channel ancestral power. Applies bleed to all enemies for 3 turns.";
            action.echoDamagePercent = 0.15f;
            action.echoDuration = 3;
            action.actionPointCost = Core.Stats.ActionPointType.Action;
            action.actionPointCount = 2;
            action.targetType = TargetType.AllEnemies;
            action.requiresTarget = false;
            action.cooldownTurns = 0;
            action.animationTrigger = "Echo";
            action.actionDuration = 1f;
            return action;
        }
    }
}
