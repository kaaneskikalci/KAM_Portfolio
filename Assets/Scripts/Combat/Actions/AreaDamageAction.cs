using UnityEngine;
using KAM.Core.Combat;

namespace KAM.Combat.Actions
{
    /// <summary>
    /// Skill 3: Area Damage - Staff slam that hits all enemies for 50% base damage.
    /// Has a 1 turn cooldown. Based on GDD 1.7.6 Skill Tree.
    /// </summary>
    [CreateAssetMenu(fileName = "AreaDamage", menuName = "KAM/Combat/Actions/Area Damage")]
    public class AreaDamageAction : CombatAction
    {
        [Header("Area Damage Settings")]
        [SerializeField] private float damagePercent = 0.5f; // 50% of base damage
        [SerializeField] private DamageType damageType = DamageType.Spirit;

        public float DamagePercent => damagePercent;

        private void OnEnable()
        {
            actionName = "Area Damage";
            description = "Staff slam that hits all enemies for 50% base damage.";
            actionPointCost = Core.Stats.ActionPointType.Action;
            actionPointCount = 2;
            targetType = TargetType.AllEnemies;
            requiresTarget = false;
            cooldownTurns = 1;
            animationTrigger = "AreaAttack";
            actionDuration = 1.2f;
        }

        public override ActionResult Execute(CombatUnit user, CombatUnit target)
        {
            // For AoE, we don't need a specific target
            if (user == null || !user.IsAlive)
                return ActionResult.Failed("Cannot execute area damage");

            // Check cooldown and action points
            if (IsOnCooldown)
                return ActionResult.Failed($"On cooldown ({CurrentCooldown} turns remaining)");

            if (user is PlayerCombatUnit playerUnit && !playerUnit.CanPerformAction(ActionPointCost, ActionPointCount))
                return ActionResult.Failed("Not enough action points");

            OnBeforeExecute(user, null);

            // Calculate damage: 50% of user's effective damage
            int baseDmg = Mathf.RoundToInt(user.Stats.EffectiveDamage * damagePercent);

            // Find all alive enemies
            var allUnits = Object.FindObjectsByType<CombatUnit>(FindObjectsSortMode.None);
            int totalDamage = 0;
            int enemiesHit = 0;
            CombatUnit[] affectedUnits = new CombatUnit[allUnits.Length];
            int affectedCount = 0;

            foreach (var unit in allUnits)
            {
                if (unit != null && unit.IsAlive && unit.Team != user.Team)
                {
                    DamageResult result = user.DealDamage(unit, baseDmg, damageType);
                    totalDamage += result.DamageDealt;
                    enemiesHit++;
                    affectedUnits[affectedCount++] = unit;
                }
            }

            // Trim affected array
            var trimmed = new CombatUnit[affectedCount];
            System.Array.Copy(affectedUnits, trimmed, affectedCount);

            ActionResult actionResult = new ActionResult
            {
                Success = true,
                DamageDealt = totalDamage,
                AffectedUnits = trimmed,
                Message = $"Staff Slam hit {enemiesHit} enemies for {totalDamage} total damage!"
            };

            OnAfterExecute(user, null, actionResult);

            return actionResult;
        }

        public override string GetFormattedDescription(CombatUnit user)
        {
            int damage = user != null ? Mathf.RoundToInt(user.Stats.EffectiveDamage * damagePercent) : 0;
            string desc = $"Slam your staff into the ground, dealing {damage} Spirit damage to ALL enemies.\n";
            desc += $"({damagePercent * 100:0}% of base damage)\n";
            desc += $"Cooldown: {cooldownTurns} turn";
            return desc;
        }

        public static AreaDamageAction CreateDefault()
        {
            var action = CreateInstance<AreaDamageAction>();
            action.actionName = "Area Damage";
            action.description = "Staff slam that hits all enemies for 50% base damage.";
            action.damagePercent = 0.5f;
            action.damageType = DamageType.Spirit;
            action.actionPointCost = Core.Stats.ActionPointType.Action;
            action.actionPointCount = 2;
            action.targetType = TargetType.AllEnemies;
            action.requiresTarget = false;
            action.cooldownTurns = 1;
            action.animationTrigger = "AreaAttack";
            action.actionDuration = 1.2f;
            return action;
        }
    }
}
