using UnityEngine;
using System;
using KAM.Core.Combat;
using KAM.Core.Stats;

namespace KAM.Combat.Actions
{
    /// <summary>
    /// Base class for all combat actions.
    /// Defines the structure for attacks, skills, items, and defensive actions.
    /// Based on GDD 1.7.8 Action System.
    /// </summary>
    public abstract class CombatAction : ScriptableObject
    {
        [Header("Action Info")]
        [SerializeField] protected string actionName = "Action";
        [SerializeField, TextArea] protected string description = "";
        [SerializeField] protected Sprite icon;

        [Header("Action Cost")]
        [SerializeField] protected ActionPointType actionPointCost = ActionPointType.Action;
        [SerializeField] protected int actionPointCount = 1; // How many AP this action costs

        [Header("Cooldown")]
        [SerializeField] protected int cooldownTurns = 0;
        [SerializeField] protected int currentCooldown = 0;

        [Header("Targeting")]
        [SerializeField] protected TargetType targetType = TargetType.SingleEnemy;
        [SerializeField] protected bool requiresTarget = true;

        [Header("Animation")]
        [SerializeField] protected string animationTrigger = "";
        [SerializeField] protected float actionDuration = 1f;

        // Properties
        public string ActionName => actionName;
        public string Description => description;
        public Sprite Icon => icon;
        public ActionPointType ActionPointCost => actionPointCost;
        public int ActionPointCount => actionPointCount;
        public int CooldownTurns => cooldownTurns;
        public int CurrentCooldown => currentCooldown;
        public bool IsOnCooldown => currentCooldown > 0;
        public TargetType TargetingType => targetType;
        public bool RequiresTarget => requiresTarget;
        public string AnimationTrigger => animationTrigger;
        public float ActionDuration => actionDuration;

        // Events
        public event Action<CombatAction, CombatUnit, CombatUnit> OnActionExecuted;
        public event Action<CombatAction> OnCooldownStarted;
        public event Action<CombatAction> OnCooldownEnded;

        #region Abstract Methods

        /// <summary>
        /// Execute the action. Must be implemented by derived classes.
        /// </summary>
        /// <param name="user">The unit performing the action</param>
        /// <param name="target">The target of the action (can be null for self-targeting)</param>
        /// <returns>Result of the action execution</returns>
        public abstract ActionResult Execute(CombatUnit user, CombatUnit target);

        #endregion

        #region Virtual Methods

        /// <summary>
        /// Check if this action can be executed.
        /// Override in derived classes for additional conditions.
        /// </summary>
        public virtual bool CanExecute(CombatUnit user, CombatUnit target = null)
        {
            // Check if user exists and is alive
            if (user == null || !user.IsAlive)
                return false;

            // Check cooldown
            if (IsOnCooldown)
                return false;

            // Check action points (for player)
            if (user is PlayerCombatUnit playerUnit)
            {
                if (!playerUnit.CanPerformAction(actionPointCost, actionPointCount))
                    return false;
            }

            // Check target requirements
            if (requiresTarget && target == null)
                return false;

            if (target != null && !target.IsAlive && targetType != TargetType.DeadAlly)
                return false;

            // Validate target type
            if (target != null && !IsValidTarget(user, target))
                return false;

            return true;
        }

        /// <summary>
        /// Called before action execution. Override for setup logic.
        /// </summary>
        protected virtual void OnBeforeExecute(CombatUnit user, CombatUnit target)
        {
            // Consume action points
            if (user is PlayerCombatUnit playerUnit)
            {
                playerUnit.ConsumeActionPoint(actionPointCost, actionPointCount);
            }
        }

        /// <summary>
        /// Called after action execution. Override for cleanup logic.
        /// </summary>
        protected virtual void OnAfterExecute(CombatUnit user, CombatUnit target, ActionResult result)
        {
            // Start cooldown
            if (cooldownTurns > 0)
            {
                currentCooldown = cooldownTurns;
                OnCooldownStarted?.Invoke(this);
            }

            // Fire event
            OnActionExecuted?.Invoke(this, user, target);
        }

        /// <summary>
        /// Get a formatted description with current stats.
        /// </summary>
        public virtual string GetFormattedDescription(CombatUnit user)
        {
            return description;
        }

        #endregion

        #region Targeting

        /// <summary>
        /// Check if a target is valid for this action.
        /// </summary>
        public bool IsValidTarget(CombatUnit user, CombatUnit target)
        {
            if (user == null || target == null) return false;

            switch (targetType)
            {
                case TargetType.Self:
                    return target == user;

                case TargetType.SingleEnemy:
                    return target.Team != user.Team && target.IsAlive;

                case TargetType.SingleAlly:
                    return target.Team == user.Team && target != user && target.IsAlive;

                case TargetType.SingleAllyOrSelf:
                    return target.Team == user.Team && target.IsAlive;

                case TargetType.AllEnemies:
                    return target.Team != user.Team && target.IsAlive;

                case TargetType.AllAllies:
                    return target.Team == user.Team && target.IsAlive;

                case TargetType.All:
                    return target.IsAlive;

                case TargetType.DeadAlly:
                    return target.Team == user.Team && !target.IsAlive;

                default:
                    return false;
            }
        }

        /// <summary>
        /// Get all valid targets for this action.
        /// </summary>
        public CombatUnit[] GetValidTargets(CombatUnit user, CombatUnit[] allUnits)
        {
            if (user == null || allUnits == null)
                return new CombatUnit[0];

            return System.Array.FindAll(allUnits, t => IsValidTarget(user, t));
        }

        #endregion

        #region Cooldown

        /// <summary>
        /// Reduce cooldown by 1 turn.
        /// </summary>
        public void TickCooldown()
        {
            if (currentCooldown > 0)
            {
                currentCooldown--;

                if (currentCooldown == 0)
                {
                    OnCooldownEnded?.Invoke(this);
                }
            }
        }

        /// <summary>
        /// Reset cooldown to 0.
        /// </summary>
        public void ResetCooldown()
        {
            currentCooldown = 0;
        }

        /// <summary>
        /// Set cooldown to a specific value.
        /// </summary>
        public void SetCooldown(int turns)
        {
            currentCooldown = Mathf.Max(0, turns);
        }

        #endregion

        #region Utility

        /// <summary>
        /// Create a runtime instance of this action (for modifications).
        /// </summary>
        public CombatAction CreateRuntimeInstance()
        {
            return Instantiate(this);
        }

        #endregion
    }

    /// <summary>
    /// Result of executing a combat action.
    /// </summary>
    public struct ActionResult
    {
        public bool Success;
        public bool WasBlocked;
        public bool WasCritical;
        public bool WasTimedHit;
        public int DamageDealt;
        public int HealingDone;
        public string Message;
        public CombatUnit[] AffectedUnits;

        public static ActionResult Successful(string message = "")
        {
            return new ActionResult
            {
                Success = true,
                Message = message
            };
        }

        public static ActionResult Failed(string message = "Action failed")
        {
            return new ActionResult
            {
                Success = false,
                Message = message
            };
        }

        public static ActionResult DamageResult(int damage, bool critical = false, bool timedHit = false)
        {
            return new ActionResult
            {
                Success = true,
                DamageDealt = damage,
                WasCritical = critical,
                WasTimedHit = timedHit,
                Message = $"Dealt {damage} damage" + (critical ? " (Critical!)" : "") + (timedHit ? " (Timed Hit!)" : "")
            };
        }

        public static ActionResult HealResult(int healing)
        {
            return new ActionResult
            {
                Success = true,
                HealingDone = healing,
                Message = $"Healed {healing} HP"
            };
        }
    }

    /// <summary>
    /// Types of targeting for combat actions.
    /// </summary>
    public enum TargetType
    {
        /// <summary>Targets the user only</summary>
        Self,

        /// <summary>Targets a single enemy</summary>
        SingleEnemy,

        /// <summary>Targets a single ally (not self)</summary>
        SingleAlly,

        /// <summary>Targets a single ally or self</summary>
        SingleAllyOrSelf,

        /// <summary>Targets all enemies</summary>
        AllEnemies,

        /// <summary>Targets all allies (including self)</summary>
        AllAllies,

        /// <summary>Targets all units in combat</summary>
        All,

        /// <summary>Targets a dead ally (for revival)</summary>
        DeadAlly,

        /// <summary>No target required</summary>
        None
    }
}
