using UnityEngine;
using System;
using KAM.Core.Stats;

namespace KAM.Core.Combat
{
    /// <summary>
    /// Enemy-specific combat unit.
    /// Extends CombatUnit with enemy-specific functionality.
    /// Works with or without EnemyStateMachine for flexibility.
    /// </summary>
    public class EnemyCombatUnit : CombatUnit
    {
        [Header("Enemy Stats")]
        [SerializeField] private EnemyStats enemyStats = new EnemyStats();

        // Properly override base Stats property so combat system uses enemyStats
        public override UnitStats Stats => enemyStats;

        // Access enemy-specific features through this property
        public EnemyStats EnemySpecificStats => enemyStats;

        // References
        private EnemyStateMachine _stateMachine;
        private EnemySkillController _skillController;

        // Enemy-specific events
        public event Action<int> OnLootDropped;
        public event Action OnLockBroken;
        public event Action OnLockRestored;
        public event Action<EnemyIntent> OnIntentChanged;

        // Intent system - shows what enemy will do next turn
        private EnemyIntent _nextIntent = new EnemyIntent();
        public EnemyIntent NextIntent => _nextIntent;

        protected override void Awake()
        {
            base.Awake();

            _stateMachine = GetComponent<EnemyStateMachine>();
            _skillController = GetComponent<EnemySkillController>();
            if (_skillController == null)
            {
                _skillController = gameObject.AddComponent<EnemySkillController>();
            }

            // Subscribe to enemy-specific events
            enemyStats.OnSpiritFragmentsDropped += HandleLootDropped;
            enemyStats.OnLockBroken += HandleLockBroken;
            enemyStats.OnLockRestored += HandleLockRestored;
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();

            if (enemyStats != null)
            {
                enemyStats.OnSpiritFragmentsDropped -= HandleLootDropped;
                enemyStats.OnLockBroken -= HandleLockBroken;
                enemyStats.OnLockRestored -= HandleLockRestored;
            }
        }

        /// <summary>
        /// Initialize enemy for combat.
        /// </summary>
        public override void InitializeForCombat()
        {
            enemyStats.Initialize();
            base.InitializeForCombat();
        }

        /// <summary>
        /// Called at the start of this enemy's turn.
        /// </summary>
        public override void BeginTurn()
        {
            enemyStats.OnTurnStart();
            base.BeginTurn();
        }

        #region Turn Frequency

        /// <summary>
        /// Check if this enemy can act this turn (based on turn frequency).
        /// </summary>
        public bool CanActThisTurn => enemyStats.CanActThisTurn;

        /// <summary>
        /// Get turns until this enemy acts again.
        /// </summary>
        public int TurnsUntilNextAction => enemyStats.GetTurnsUntilNextAction();

        /// <summary>
        /// Call when enemy performs an action.
        /// </summary>
        public void OnActionPerformed()
        {
            enemyStats.OnActionPerformed();
        }

        #endregion

        #region Lock System

        /// <summary>
        /// Check if this enemy has an active damage lock.
        /// </summary>
        public bool IsLocked => enemyStats.IsLocked;

        /// <summary>
        /// Check if this enemy has the lock mechanic.
        /// </summary>
        public bool HasDamageLock => enemyStats.HasDamageLock;

        /// <summary>
        /// Break the damage lock (requires Key Rune skill).
        /// </summary>
        public bool BreakLock()
        {
            return enemyStats.BreakLock();
        }

        private void HandleLockBroken()
        {
            OnLockBroken?.Invoke();
            Debug.Log($"{enemyStats.EnemyName}'s lock has been broken!");
        }

        private void HandleLockRestored()
        {
            OnLockRestored?.Invoke();
            Debug.Log($"{enemyStats.EnemyName}'s lock has been restored!");
        }

        #endregion

        #region Loot

        /// <summary>
        /// Drop loot when killed.
        /// </summary>
        public int DropLoot()
        {
            return enemyStats.RollLootDrop();
        }

        /// <summary>
        /// Drop guaranteed loot (for bosses).
        /// </summary>
        public int DropGuaranteedLoot()
        {
            return enemyStats.GetGuaranteedLoot();
        }

        private void HandleLootDropped(int amount)
        {
            OnLootDropped?.Invoke(amount);
        }

        #endregion

        #region Enemy Info

        /// <summary>
        /// Get the enemy type.
        /// </summary>
        public EnemyType EnemyType => enemyStats.Type;

        /// <summary>
        /// Check if this enemy is aggressive on sight.
        /// </summary>
        public bool IsAggressive => enemyStats.AggressiveOnSight;

        /// <summary>
        /// Check if this is a boss enemy.
        /// </summary>
        public bool IsBoss => enemyStats.Type == EnemyType.Boss || enemyStats.Type == EnemyType.MiniBoss;

        #endregion

        #region Death Handling

        /// <summary>
        /// Handle enemy death - drop loot and notify systems.
        /// </summary>
        public void HandleDeathAndDropLoot(PlayerCombatUnit player)
        {
            int loot = IsBoss ? DropGuaranteedLoot() : DropLoot();

            if (loot > 0 && player != null)
            {
                player.CollectSpiritFragments(loot);
            }
        }

        #endregion

        #region Intent System

        /// <summary>
        /// Decide what action this enemy will take on its next turn.
        /// Called at the start of each round to show intent to player.
        /// </summary>
        public void DecideNextIntent()
        {
            if (_skillController != null)
            {
                _nextIntent = _skillController.PreviewIntent();
                OnIntentChanged?.Invoke(_nextIntent);
                return;
            }

            // Simple AI: mostly attack, sometimes defend if low health
            float healthPercent = Stats.HealthPercentage;

            if (healthPercent < 0.3f && UnityEngine.Random.value < 0.3f)
            {
                // Low health - might defend
                _nextIntent = new EnemyIntent
                {
                    ActionType = EnemyIntentType.Defend,
                    DamageAmount = 0,
                    Description = "Defending"
                };
            }
            else
            {
                // Attack
                int damage = Stats.EffectiveDamage;
                _nextIntent = new EnemyIntent
                {
                    ActionType = EnemyIntentType.Attack,
                    DamageAmount = damage,
                    Description = $"Attack for {damage}"
                };
            }

            OnIntentChanged?.Invoke(_nextIntent);
        }

        /// <summary>
        /// Clear the intent display (after action is taken).
        /// </summary>
        public void ClearIntent()
        {
            _nextIntent = new EnemyIntent
            {
                ActionType = EnemyIntentType.Unknown,
                DamageAmount = 0,
                Description = ""
            };
            OnIntentChanged?.Invoke(_nextIntent);
        }

        #endregion

        protected override void OnDrawGizmosSelected()
        {
            base.OnDrawGizmosSelected();

            // Draw lock indicator
            if (Application.isPlaying && enemyStats != null && enemyStats.HasDamageLock)
            {
                Gizmos.color = enemyStats.IsLocked ? Color.magenta : Color.green;
                Gizmos.DrawWireCube(transform.position + Vector3.up * 2.5f, Vector3.one * 0.5f);
            }
        }
    }
}
