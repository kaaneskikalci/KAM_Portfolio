using UnityEngine;
using System;
using System.Collections.Generic;
using KAM.Core.Stats;
using KAM.Progression;

namespace KAM.Core.Combat
{
    /// <summary>
    /// Base component for any GameObject that participates in combat.
    /// Attach to player, enemies, summons, etc.
    /// </summary>
    public class CombatUnit : MonoBehaviour
    {
        [Header("Unit Info")]
        [SerializeField] private string unitName = "Unit";
        [SerializeField] private UnitTeam team = UnitTeam.Enemy;
        [SerializeField] private Sprite portrait;

        [Header("Stats")]
        [SerializeField] protected UnitStats stats = new UnitStats();

        [Header("Combat Position")]
        [SerializeField] private Transform combatAnchor;

        // Properties
        public string UnitName => unitName;
        public UnitTeam Team => team;
        public Sprite Portrait => portrait;
        public virtual UnitStats Stats => stats;
        public Transform CombatAnchor => combatAnchor != null ? combatAnchor : transform;

        public bool IsAlive => Stats.IsAlive;
        public bool IsPlayer => team == UnitTeam.Player;
        public bool IsEnemy => team == UnitTeam.Enemy;

        // Combat State
        private bool _isInCombat = false;
        private bool _hasActedThisTurn = false;

        public bool IsInCombat => _isInCombat;
        public bool HasActedThisTurn => _hasActedThisTurn;

        // Status Effects
        private List<StatusEffect> _activeEffects = new List<StatusEffect>();
        public IReadOnlyList<StatusEffect> ActiveEffects => _activeEffects;

        // Events
        public event Action<CombatUnit> OnEnterCombat;
        public event Action<CombatUnit> OnExitCombat;
        public event Action<CombatUnit, DamageResult> OnDamageDealt;
        public event Action<CombatUnit, DamageResult> OnDamageTaken;
        public event Action<CombatUnit, int> OnHealed;
        public event Action<CombatUnit> OnTurnStart;
        public event Action<CombatUnit> OnTurnEnd;
        public event Action<CombatUnit> OnDeath;

        // Status Effect Events
        public event Action<StatusEffect> OnEffectAdded;
        public event Action<StatusEffect> OnEffectRemoved;
        public event Action OnEffectsCleared;

        protected virtual void Awake()
        {
            if (combatAnchor == null)
            {
                combatAnchor = transform;
            }
        }

        protected virtual void Start()
        {
            // Subscribe to stats events (use Stats property for proper inheritance)
            Stats.OnDeath += HandleDeath;
        }

        protected virtual void OnDestroy()
        {
            if (Stats != null)
            {
                Stats.OnDeath -= HandleDeath;
            }
        }

        #region Combat Lifecycle

        /// <summary>
        /// Initialize this unit for combat.
        /// </summary>
        public virtual void InitializeForCombat()
        {
            Stats.Initialize();
            _isInCombat = true;
            _hasActedThisTurn = false;
            OnEnterCombat?.Invoke(this);
        }

        /// <summary>
        /// Called when this unit exits combat.
        /// </summary>
        public virtual void ExitCombat()
        {
            _isInCombat = false;
            _hasActedThisTurn = false;
            OnExitCombat?.Invoke(this);
        }

        /// <summary>
        /// Called at the start of this unit's turn.
        /// </summary>
        public virtual void BeginTurn()
        {
            _hasActedThisTurn = false;

            // Process status effects at turn start
            ProcessEffectsOnTurnStart();

            OnTurnStart?.Invoke(this);
        }

        /// <summary>
        /// Called at the end of this unit's turn.
        /// </summary>
        public virtual void EndTurn()
        {
            _hasActedThisTurn = true;

            // Process status effects at turn end
            ProcessEffectsOnTurnEnd();

            OnTurnEnd?.Invoke(this);
        }

        #endregion

        #region Status Effects

        /// <summary>
        /// Apply a status effect to this unit.
        /// </summary>
        public void ApplyEffect(StatusEffect effect)
        {
            if (effect == null) return;

            // Check if effect already exists (stack or refresh)
            StatusEffect existing = _activeEffects.Find(e => e.Category == effect.Category);
            if (existing != null)
            {
                // Refresh duration instead of stacking
                existing.RefreshDuration();
                Debug.Log($"[CombatUnit] Refreshed {effect.EffectName} on {unitName}");
                return;
            }

            effect.Apply(this);
            _activeEffects.Add(effect);
            effect.OnEffectExpired += HandleEffectExpired;

            OnEffectAdded?.Invoke(effect);
            Debug.Log($"[CombatUnit] Applied {effect.EffectName} to {unitName}");
        }

        /// <summary>
        /// Remove a specific status effect.
        /// </summary>
        public void RemoveEffect(StatusEffect effect)
        {
            if (effect == null || !_activeEffects.Contains(effect)) return;

            effect.OnEffectExpired -= HandleEffectExpired;
            _activeEffects.Remove(effect);

            OnEffectRemoved?.Invoke(effect);
            Debug.Log($"[CombatUnit] Removed {effect.EffectName} from {unitName}");
        }

        /// <summary>
        /// Remove all effects of a specific type.
        /// </summary>
        public void RemoveEffectsOfType(StatusEffectType type)
        {
            var toRemove = _activeEffects.FindAll(e => e.EffectType == type);
            foreach (var effect in toRemove)
            {
                RemoveEffect(effect);
            }
        }

        /// <summary>
        /// Remove all effects of a specific category.
        /// </summary>
        public void RemoveEffectsOfCategory(StatusEffectCategory category)
        {
            var toRemove = _activeEffects.FindAll(e => e.Category == category);
            foreach (var effect in toRemove)
            {
                RemoveEffect(effect);
            }
        }

        /// <summary>
        /// Clear all status effects.
        /// </summary>
        public void ClearAllEffects()
        {
            foreach (var effect in _activeEffects)
            {
                effect.OnEffectExpired -= HandleEffectExpired;
            }
            _activeEffects.Clear();

            OnEffectsCleared?.Invoke();
            Debug.Log($"[CombatUnit] Cleared all effects from {unitName}");
        }

        /// <summary>
        /// Get all active effects as a list.
        /// </summary>
        public List<StatusEffect> GetActiveEffects()
        {
            return new List<StatusEffect>(_activeEffects);
        }

        /// <summary>
        /// Check if unit has a specific effect category.
        /// </summary>
        public bool HasEffect(StatusEffectCategory category)
        {
            return _activeEffects.Exists(e => e.Category == category);
        }

        /// <summary>
        /// Get the total stat modifier from all effects.
        /// </summary>
        public float GetStatModifierFromEffects(StatusEffectCategory category)
        {
            float modifier = 0f;
            foreach (var effect in _activeEffects)
            {
                if (effect.Category == category)
                    modifier += effect.StatModifier;
            }
            return modifier;
        }

        public int GetEffectiveSpeedWithEffects()
        {
            if (Stats == null) return 0;

            float modifier = 1f;
            modifier += GetStatModifierFromEffects(StatusEffectCategory.SpeedUp);
            modifier -= GetStatModifierFromEffects(StatusEffectCategory.SpeedDown);

            return Mathf.Max(0, Mathf.RoundToInt(Stats.EffectiveSpeed * Mathf.Max(0f, modifier)));
        }

        public bool IsActionBlockedByControlEffect()
        {
            return HasEffect(StatusEffectCategory.Stun) || HasEffect(StatusEffectCategory.Freeze);
        }

        private void ProcessEffectsOnTurnStart()
        {
            // Process each effect (damage over time, etc.)
            foreach (var effect in _activeEffects.ToArray())
            {
                effect.OnTurnStart();
            }
        }

        private void ProcessEffectsOnTurnEnd()
        {
            // Tick down durations and remove expired effects
            foreach (var effect in _activeEffects.ToArray())
            {
                effect.OnTurnEnd();
            }
        }

        private void HandleEffectExpired(StatusEffect effect)
        {
            RemoveEffect(effect);
        }

        #endregion

        #region Damage & Healing

        /// <summary>
        /// Deal damage to another unit.
        /// </summary>
        public DamageResult DealDamage(CombatUnit target, int baseDamage, DamageType type = DamageType.Physical)
        {
            if (target == null || !target.IsAlive)
            {
                return DamageResult.Miss(DamageInfo.Create(this, target, baseDamage, type));
            }

            int modifiedDamage = ApplyOutgoingDamageModifiers(baseDamage);

            // Create damage info
            DamageInfo damageInfo = DamageInfo.Create(this, target, modifiedDamage, type);

            if (ShouldMissFromBlind())
            {
                Debug.Log($"[CombatUnit] {UnitName} missed because of Blind.");
                DamageResult miss = DamageResult.Miss(damageInfo);
                OnDamageDealt?.Invoke(this, miss);
                return miss;
            }

            if (target is EnemyCombatUnit enemyTarget && enemyTarget.IsLocked)
            {
                damageInfo.LockedTargetHit = true;
                Debug.Log($"[CombatUnit] {enemyTarget.UnitName} is locked. Key Rune timed hit required.");
                DamageResult blocked = DamageResult.Miss(damageInfo);
                OnDamageDealt?.Invoke(this, blocked);
                return blocked;
            }

            // Check for critical hit
            if (Stats.RollCritical())
            {
                damageInfo.ApplyCritical(Stats.CriticalMultiplier);
            }

            // Apply the damage
            DamageResult result = target.ReceiveDamage(damageInfo);

            // Fire event
            OnDamageDealt?.Invoke(this, result);

            return result;
        }

        /// <summary>
        /// Receive damage from an attack.
        /// </summary>
        public DamageResult ReceiveDamage(DamageInfo damageInfo)
        {
            if (this is EnemyCombatUnit enemyUnit && enemyUnit.IsLocked)
            {
                damageInfo.LockedTargetHit = true;
                damageInfo.FinalDamage = 0;
                Debug.Log($"[CombatUnit] {enemyUnit.UnitName} is locked and ignored incoming damage.");
                return DamageResult.Miss(damageInfo);
            }

            if (ShouldDodgeFromEvasion(damageInfo.Type))
            {
                Debug.Log($"[CombatUnit] {UnitName} dodged the attack with Evasion.");
                return DamageResult.Miss(damageInfo);
            }

            // Apply defense (unless True damage)
            if (damageInfo.Type != DamageType.True)
            {
                damageInfo.ApplyDefense(Stats.EffectiveDefense);
            }

            // Apply percentage-based damage reduction from DefenseUp status effects
            if (damageInfo.Type != DamageType.True && HasEffect(StatusEffectCategory.DefenseUp))
            {
                float reductionPercent = GetStatModifierFromEffects(StatusEffectCategory.DefenseUp);
                if (reductionPercent > 0f)
                {
                    int reduced = Mathf.RoundToInt(damageInfo.FinalDamage * reductionPercent);
                    damageInfo.FinalDamage = Mathf.Max(1, damageInfo.FinalDamage - reduced);
                    damageInfo.DamageBlocked += reduced;
                    Debug.Log($"[CombatUnit] DefenseUp blocked {reduced} damage ({reductionPercent * 100:0}% reduction)");
                }
            }

            ApplyIncomingDamageModifiers(ref damageInfo);

            // Apply the damage
            int actualDamage = Stats.TakeDamage(damageInfo.FinalDamage);
            damageInfo.FinalDamage = actualDamage;

            bool killed = !Stats.IsAlive;

            DamageResult result = DamageResult.Success(
                damageInfo,
                actualDamage,
                Stats.CurrentHealth,
                killed
            );

            // Fire event
            OnDamageTaken?.Invoke(this, result);

            return result;
        }

        /// <summary>
        /// Heal this unit.
        /// </summary>
        public int Heal(int amount)
        {
            int healed = Stats.Heal(amount);

            if (healed > 0)
            {
                OnHealed?.Invoke(this, healed);
            }

            return healed;
        }

        /// <summary>
        /// Apply damage with timed hit bonus.
        /// </summary>
        public DamageResult DealTimedHitDamage(CombatUnit target, int baseDamage, float timedHitMultiplier = 1.3f, DamageType type = DamageType.Physical)
        {
            if (target == null || !target.IsAlive)
            {
                return DamageResult.Miss(DamageInfo.Create(this, target, baseDamage, type));
            }

            int modifiedDamage = ApplyOutgoingDamageModifiers(baseDamage);
            DamageInfo damageInfo = DamageInfo.Create(this, target, modifiedDamage, type);

            if (ShouldMissFromBlind())
            {
                Debug.Log($"[CombatUnit] {UnitName} missed timed hit attack because of Blind.");
                DamageResult miss = DamageResult.Miss(damageInfo);
                OnDamageDealt?.Invoke(this, miss);
                return miss;
            }

            // Apply timed hit bonus
            damageInfo.ApplyTimedHitBonus(timedHitMultiplier);

            if (target is EnemyCombatUnit enemyTarget && enemyTarget.IsLocked)
            {
                bool canBreakLock = IsPlayer &&
                    timedHitMultiplier > 1f &&
                    SkillEffectApplier.Instance != null &&
                    SkillEffectApplier.Instance.HasKeyRuneAbility();

                if (canBreakLock && enemyTarget.BreakLock())
                {
                    damageInfo.LockedTargetHit = true;
                    Debug.Log($"[CombatUnit] {enemyTarget.UnitName}'s damage lock was broken by Key Rune.");
                }
                else
                {
                    damageInfo.LockedTargetHit = true;
                    Debug.Log($"[CombatUnit] {enemyTarget.UnitName} is locked. Key Rune timed hit required.");
                    DamageResult blocked = DamageResult.Miss(damageInfo);
                    OnDamageDealt?.Invoke(this, blocked);
                    return blocked;
                }
            }

            // Check for critical (can stack with timed hit!)
            if (Stats.RollCritical())
            {
                damageInfo.ApplyCritical(Stats.CriticalMultiplier);
            }

            DamageResult result = target.ReceiveDamage(damageInfo);
            OnDamageDealt?.Invoke(this, result);

            return result;
        }

        #endregion

        private int ApplyOutgoingDamageModifiers(int baseDamage)
        {
            float modifier = 1f;

            modifier += GetStatModifierFromEffects(StatusEffectCategory.StrengthUp);
            modifier -= GetStatModifierFromEffects(StatusEffectCategory.StrengthDown);
            modifier -= GetStatModifierFromEffects(StatusEffectCategory.Weak);

            if (IsPlayer &&
                Stats != null &&
                Stats.HealthPercentage <= 0.5f &&
                SkillEffectApplier.Instance != null)
            {
                modifier += SkillEffectApplier.Instance.GetLowHPDamageBonus();
            }

            return Mathf.Max(0, Mathf.RoundToInt(baseDamage * Mathf.Max(0f, modifier)));
        }

        private void ApplyIncomingDamageModifiers(ref DamageInfo damageInfo)
        {
            float modifier = 1f;
            modifier += GetStatModifierFromEffects(StatusEffectCategory.Vulnerable);
            modifier += GetStatModifierFromEffects(StatusEffectCategory.DefenseDown);

            if (!Mathf.Approximately(modifier, 1f))
            {
                int previousDamage = damageInfo.FinalDamage;
                damageInfo.FinalDamage = Mathf.Max(1, Mathf.RoundToInt(damageInfo.FinalDamage * Mathf.Max(0f, modifier)));
                damageInfo.BonusDamage += Mathf.Max(0, damageInfo.FinalDamage - previousDamage);
            }
        }

        private bool ShouldMissFromBlind()
        {
            if (!HasEffect(StatusEffectCategory.Blind))
                return false;

            float missChance = Mathf.Clamp01(GetStatModifierFromEffects(StatusEffectCategory.Blind));
            return UnityEngine.Random.value < missChance;
        }

        private bool ShouldDodgeFromEvasion(DamageType damageType)
        {
            if (damageType == DamageType.DoT || damageType == DamageType.True)
                return false;

            if (!HasEffect(StatusEffectCategory.Evasion))
                return false;

            float dodgeChance = Mathf.Clamp01(GetStatModifierFromEffects(StatusEffectCategory.Evasion));
            return UnityEngine.Random.value < dodgeChance;
        }

        #region Utility

        /// <summary>
        /// Check if this unit can target another unit.
        /// </summary>
        public bool CanTarget(CombatUnit target)
        {
            if (target == null || !target.IsAlive) return false;
            if (target == this) return false;

            // By default, can only target enemies
            return target.Team != this.Team;
        }

        /// <summary>
        /// Check if this unit can act (alive and in combat).
        /// </summary>
        public bool CanAct()
        {
            return IsAlive && _isInCombat && !_hasActedThisTurn;
        }

        /// <summary>
        /// Get display name for combat log.
        /// </summary>
        public string GetDisplayName()
        {
            return string.IsNullOrEmpty(unitName) ? gameObject.name : unitName;
        }

        private void HandleDeath()
        {
            OnDeath?.Invoke(this);
        }

        #endregion

        #region Editor Helpers

        protected virtual void OnDrawGizmosSelected()
        {
            // Draw combat anchor
            if (combatAnchor != null && combatAnchor != transform)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(combatAnchor.position, 0.3f);
                Gizmos.DrawLine(transform.position, combatAnchor.position);
            }

            // Draw health indicator
            if (Application.isPlaying && Stats != null)
            {
                Gizmos.color = Color.Lerp(Color.red, Color.green, Stats.HealthPercentage);
                Gizmos.DrawWireCube(transform.position + Vector3.up * 2f, new Vector3(Stats.HealthPercentage, 0.1f, 0.1f));
            }
        }

        #endregion
    }

    /// <summary>
    /// Team affiliation for combat targeting.
    /// </summary>
    public enum UnitTeam
    {
        /// <summary>Player and allies</summary>
        Player,

        /// <summary>Enemies</summary>
        Enemy,

        /// <summary>Neutral units (can be attacked by either)</summary>
        Neutral
    }
}
