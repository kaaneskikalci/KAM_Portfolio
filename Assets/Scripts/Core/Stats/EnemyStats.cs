using UnityEngine;
using System;

namespace KAM.Core.Stats
{
    /// <summary>
    /// Enemy-specific stats extending UnitStats.
    /// Handles enemy combat attributes, loot drops, and special behaviors.
    /// Based on GDD enemy behavior specifications.
    /// </summary>
    [Serializable]
    public class EnemyStats : UnitStats
    {
        [Header("Enemy Identity")]
        [SerializeField] private string enemyName = "Spirit";
        [SerializeField] private EnemyType enemyType = EnemyType.Normal;

        [Header("Combat Behavior")]
        [Tooltip("How many turns between this enemy's actions (1 = every turn, 2 = every other turn)")]
        [SerializeField, Min(1)] private int turnFrequency = 1;

        [Tooltip("Does this enemy attack on sight or only when provoked?")]
        [SerializeField] private bool aggressiveOnSight = true;

        [Header("Loot")]
        [SerializeField] private int spiritFragmentDrop = 5;
        [SerializeField, Range(0f, 1f)] private float dropChance = 1f;

        [Header("Lock System (For Bosses)")]
        [Tooltip("Requires Key Rune Power-up skill to damage")]
        [SerializeField] private bool hasDamageLock = false;
        [SerializeField] private int lockDuration = 3;  // Turns lock stays broken

        private bool _isLocked = true;
        private int _lockBreakTurnsRemaining = 0;

        // Properties
        public string EnemyName => enemyName;
        public EnemyType Type => enemyType;
        public int TurnFrequency => turnFrequency;
        public bool AggressiveOnSight => aggressiveOnSight;
        public int SpiritFragmentDrop => spiritFragmentDrop;
        public float DropChance => dropChance;

        // Lock System Properties
        public bool HasDamageLock => hasDamageLock;
        public bool IsLocked => hasDamageLock && _isLocked;
        public int LockBreakTurnsRemaining => _lockBreakTurnsRemaining;

        // Turn Tracking
        private int _turnsSinceLastAction = 0;
        public bool CanActThisTurn => _turnsSinceLastAction >= turnFrequency - 1;

        // Events
        public event Action<int> OnSpiritFragmentsDropped;
        public event Action OnLockBroken;
        public event Action OnLockRestored;

        /// <summary>
        /// Initialize enemy stats for combat.
        /// </summary>
        public new void Initialize()
        {
            base.Initialize();
            _turnsSinceLastAction = turnFrequency - 1; // Ready to act on first turn

            if (hasDamageLock)
            {
                _isLocked = true;
                _lockBreakTurnsRemaining = 0;
            }
        }

        #region Turn Management

        /// <summary>
        /// Called at the start of this enemy's turn.
        /// </summary>
        public void OnTurnStart()
        {
            _turnsSinceLastAction++;

            // Handle lock restoration
            if (hasDamageLock && !_isLocked)
            {
                _lockBreakTurnsRemaining--;

                if (_lockBreakTurnsRemaining <= 0)
                {
                    RestoreLock();
                }
            }
        }

        /// <summary>
        /// Called when the enemy performs an action.
        /// </summary>
        public void OnActionPerformed()
        {
            _turnsSinceLastAction = 0;
        }

        /// <summary>
        /// Get turns until this enemy can act again.
        /// </summary>
        public int GetTurnsUntilNextAction()
        {
            return Mathf.Max(0, turnFrequency - 1 - _turnsSinceLastAction);
        }

        #endregion

        #region Lock System

        /// <summary>
        /// Break the damage lock (requires Key Rune Power-up skill).
        /// </summary>
        public bool BreakLock()
        {
            if (!hasDamageLock || !_isLocked) return false;

            _isLocked = false;
            _lockBreakTurnsRemaining = lockDuration;
            OnLockBroken?.Invoke();
            return true;
        }

        /// <summary>
        /// Restore the damage lock.
        /// </summary>
        private void RestoreLock()
        {
            _isLocked = true;
            OnLockRestored?.Invoke();
        }

        /// <summary>
        /// Override TakeDamage to check for lock.
        /// </summary>
        public new int TakeDamage(int rawDamage)
        {
            if (IsLocked)
            {
                // Locked enemies take no damage
                Debug.Log($"{enemyName} is locked! Break the lock first with Key Rune.");
                return 0;
            }

            return base.TakeDamage(rawDamage);
        }

        #endregion

        #region Loot

        /// <summary>
        /// Roll for loot drops when enemy dies.
        /// </summary>
        /// <returns>Spirit fragments dropped (0 if roll failed)</returns>
        public int RollLootDrop()
        {
            if (UnityEngine.Random.value <= dropChance)
            {
                OnSpiritFragmentsDropped?.Invoke(spiritFragmentDrop);
                return spiritFragmentDrop;
            }
            return 0;
        }

        /// <summary>
        /// Get guaranteed loot (for bosses or scripted drops).
        /// </summary>
        public int GetGuaranteedLoot()
        {
            OnSpiritFragmentsDropped?.Invoke(spiritFragmentDrop);
            return spiritFragmentDrop;
        }

        #endregion

        /// <summary>
        /// Create a copy of enemy stats.
        /// </summary>
        public new EnemyStats Clone()
        {
            var clone = new EnemyStats();

            // Copy base stats
            clone.Initialize();
            clone.SetMaxHealth(this.MaxHealth, false);
            clone.SetHealth(this.CurrentHealth);

            // Copy enemy-specific stats
            clone.enemyName = this.enemyName;
            clone.enemyType = this.enemyType;
            clone.turnFrequency = this.turnFrequency;
            clone.aggressiveOnSight = this.aggressiveOnSight;
            clone.spiritFragmentDrop = this.spiritFragmentDrop;
            clone.dropChance = this.dropChance;
            clone.hasDamageLock = this.hasDamageLock;
            clone.lockDuration = this.lockDuration;

            return clone;
        }
    }

    /// <summary>
    /// Types of enemies based on GDD specifications.
    /// </summary>
    public enum EnemyType
    {
        /// <summary>Standard enemy</summary>
        Normal,

        /// <summary>Stronger enemy with more health/damage</summary>
        Elite,

        /// <summary>Boss enemy - may have damage lock, special mechanics</summary>
        Boss,

        /// <summary>Mini-boss - stronger than elite but not full boss</summary>
        MiniBoss,

        /// <summary>Passive enemy - only attacks when provoked</summary>
        Neutral
    }
}
