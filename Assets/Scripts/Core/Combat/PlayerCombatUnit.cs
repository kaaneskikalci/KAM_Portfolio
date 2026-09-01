using UnityEngine;
using System;
using KAM.Core.Stats;
using KAM.Progression;

namespace KAM.Core.Combat
{
    /// <summary>
    /// Player-specific combat unit for Kam.
    /// Extends CombatUnit with player-specific functionality.
    /// </summary>
    public class PlayerCombatUnit : CombatUnit
    {
        [Header("Player Stats")]
        [SerializeField] private PlayerStats playerStats = new PlayerStats();

        // Properly override base Stats property so combat system uses playerStats
        public override UnitStats Stats => playerStats;

        // Access player-specific features through this property
        public PlayerStats PlayerSpecificStats => playerStats;

        // Player-specific events
        public event Action OnUltimateReady;
        public event Action<int> OnSpiritFragmentsGained;

        protected override void Awake()
        {
            base.Awake();

            // Subscribe to player-specific events
            playerStats.OnUltimateReady += HandleUltimateReady;
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();

            if (playerStats != null)
            {
                playerStats.OnUltimateReady -= HandleUltimateReady;
            }
        }

        /// <summary>
        /// Initialize player for combat.
        /// </summary>
        public override void InitializeForCombat()
        {
            playerStats.PrepareForCombat();

            var previousHealth = playerStats.CurrentHealth;
            base.InitializeForCombat();
            playerStats.SetHealth(previousHealth);
        }

        /// <summary>
        /// Called at the start of player's turn.
        /// </summary>
        public override void BeginTurn()
        {
            playerStats.ResetActionPoints();
            base.BeginTurn();
        }

        #region Action Points

        /// <summary>
        /// Check if player can perform an action type.
        /// </summary>
        public bool CanPerformAction(ActionPointType type, int count = 1)
        {
            return playerStats.CanPerformAction(type, count);
        }

        /// <summary>
        /// Consume action points.
        /// </summary>
        public bool ConsumeActionPoint(ActionPointType type, int count = 1)
        {
            return playerStats.ConsumeActionPoint(type, count);
        }

        /// <summary>
        /// Check if player has any action points remaining.
        /// </summary>
        public bool HasAnyActionPoints()
        {
            return playerStats.HasAction || playerStats.HasBonusAction;
        }

        #endregion

        #region Ultimate

        /// <summary>
        /// Add to ultimate meter (called on successful attacks).
        /// </summary>
        public void AddUltimateMeter(int amount = 1)
        {
            playerStats.AddUltimateMeter(amount);
        }

        /// <summary>
        /// Use the ultimate ability.
        /// </summary>
        public bool UseUltimate()
        {
            return playerStats.UseUltimate();
        }

        /// <summary>
        /// Check if ultimate is ready.
        /// </summary>
        public bool IsUltimateReady => playerStats.UltimateReady;

        private void HandleUltimateReady()
        {
            OnUltimateReady?.Invoke();
        }

        #endregion

        #region Spirit Fragments

        /// <summary>
        /// Collect spirit fragments from defeated enemies.
        /// Routes through InventoryManager as the single source of truth.
        /// </summary>
        public void CollectSpiritFragments(int amount)
        {
            if (KAM.Inventory.InventoryManager.Instance != null)
            {
                KAM.Inventory.InventoryManager.Instance.AddSpiritFragments(amount);
            }
            OnSpiritFragmentsGained?.Invoke(amount);
        }

        /// <summary>
        /// Get current spirit fragments from InventoryManager.
        /// </summary>
        public int SpiritFragments => KAM.Inventory.InventoryManager.Instance != null
            ? KAM.Inventory.InventoryManager.Instance.SpiritFragments
            : 0;

        #endregion

        #region Timed Hit Integration

        /// <summary>
        /// Deal damage with timed hit bonus and add to ultimate meter.
        /// </summary>
        public new DamageResult DealTimedHitDamage(CombatUnit target, int baseDamage, float timedHitMultiplier = 1.3f, DamageType type = DamageType.Physical)
        {
            DamageResult result = base.DealTimedHitDamage(target, baseDamage, timedHitMultiplier, type);

            // Add to ultimate meter on successful hit
            if (result.HitSuccessful)
            {
                AddUltimateMeter(1);

                if (timedHitMultiplier > 1f && SkillEffectApplier.Instance != null)
                {
                    ApplyTimedHitSkillEffects(target, result.DamageDealt);
                }

                TryApplyUmaySecondBeam(target);
            }

            return result;
        }

        /// <summary>
        /// Deal normal damage and add to ultimate meter.
        /// </summary>
        public new DamageResult DealDamage(CombatUnit target, int baseDamage, DamageType type = DamageType.Physical)
        {
            DamageResult result = base.DealDamage(target, baseDamage, type);

            // Add to ultimate meter on successful hit
            if (result.HitSuccessful)
            {
                AddUltimateMeter(1);
                TryApplyUmaySecondBeam(target);
            }

            return result;
        }

        #endregion

        private void ApplyTimedHitSkillEffects(CombatUnit target, int damageDealt)
        {
            if (target == null) return;

            float blindChance = SkillEffectApplier.Instance.GetTimedHitBlindChance();
            if (blindChance > 0f)
            {
                target.ApplyEffect(StatusEffect.CreateBlind(blindChance, 1));
                Debug.Log($"[PlayerCombatUnit] Rune Power-up blinded {target.UnitName} ({blindChance * 100:0}% miss chance).");
            }

            float lifeStealPercent = SkillEffectApplier.Instance.GetTimedHitLifeStealPercent();
            if (lifeStealPercent > 0f && damageDealt > 0)
            {
                int healAmount = Mathf.Max(1, Mathf.RoundToInt(damageDealt * lifeStealPercent));
                int healed = Heal(healAmount);
                if (healed > 0)
                {
                    Debug.Log($"[PlayerCombatUnit] Rune Power-up life steal healed {healed} HP.");
                }
            }
        }

        private void TryApplyUmaySecondBeam(CombatUnit originalTarget)
        {
            if (SkillEffectApplier.Instance == null)
                return;

            float chance = SkillEffectApplier.Instance.GetDoubleBeamChance();
            if (chance <= 0f || UnityEngine.Random.value > chance)
                return;

            CombatUnit secondTarget = ResolveUmaySecondBeamTarget(originalTarget);
            if (secondTarget == null || !secondTarget.IsAlive)
                return;

            float damagePercent = SkillEffectApplier.Instance.GetDoubleBeamDamagePercent();
            int secondDamage = Mathf.Max(1, Mathf.RoundToInt(Stats.EffectiveDamage * damagePercent));
            DamageResult result = base.DealDamage(secondTarget, secondDamage, DamageType.Spirit);

            float slowPercent = SkillEffectApplier.Instance.GetDoubleBeamSlowPercent();
            if (result.HitSuccessful && slowPercent > 0f)
            {
                secondTarget.ApplyEffect(StatusEffect.CreateSpeedDown(slowPercent, 1));
            }

            if (result.HitSuccessful)
            {
                Debug.Log($"[PlayerCombatUnit] Umay Ana's Favorite fired a second beam at {secondTarget.UnitName} for {result.DamageDealt} damage.");
            }
        }

        private CombatUnit ResolveUmaySecondBeamTarget(CombatUnit originalTarget)
        {
            if (originalTarget != null && originalTarget.IsAlive)
                return originalTarget;

            var units = FindObjectsByType<CombatUnit>(FindObjectsSortMode.None);
            foreach (var unit in units)
            {
                if (unit != null && unit.IsAlive && unit.Team != Team)
                    return unit;
            }

            return null;
        }
    }
}
