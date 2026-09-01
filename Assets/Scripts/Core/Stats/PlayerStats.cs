using UnityEngine;
using System;

namespace KAM.Core.Stats
{
    /// <summary>
    /// Player-specific stats extending UnitStats.
    /// Handles Kam's unique attributes: Ultimate meter, Spirit Fragments, Action Points.
    /// Based on GDD combat mechanics.
    /// </summary>
    [Serializable]
    public class PlayerStats : UnitStats
    {
        [Header("Ultimate System")]
        [SerializeField] private int ultimateMeter = 0;
        [SerializeField] private int ultimateMaxMeter = 50;  // 50 successful hits to fill

        [Header("Currency")]
        [SerializeField] private int spiritFragments = 0;

        [Header("Action Points")]
        [SerializeField] private int maxActions = 2;        // Max AP that can be stored
        [SerializeField] private int apPerTurn = 1;         // AP gained each turn
        [SerializeField] private int startingActions = 0;   // AP at combat start (BeginTurn adds 1 on first turn)
        [SerializeField] private int maxBonusActions = 1;
        [SerializeField] private int maxReactions = 1;

        private int _currentActions;
        private int _currentBonusActions;
        private int _currentReactions;

        // Ultimate Properties
        public int UltimateMeter => ultimateMeter;
        public int UltimateMaxMeter => ultimateMaxMeter;
        public float UltimatePercentage => ultimateMaxMeter > 0 ? (float)ultimateMeter / ultimateMaxMeter : 0f;
        public bool UltimateReady => ultimateMeter >= ultimateMaxMeter;

        // Currency Properties
        public int SpiritFragments => spiritFragments;

        // Action Point Properties
        public int CurrentActions => _currentActions;
        public int CurrentBonusActions => _currentBonusActions;
        public int CurrentReactions => _currentReactions;
        public int MaxActions => maxActions;
        public int APPerTurn => apPerTurn;
        public int MaxBonusActions => maxBonusActions;
        public int MaxReactions => maxReactions;

        public bool HasAction => _currentActions > 0;
        public bool HasBonusAction => _currentBonusActions > 0;
        public bool HasReaction => _currentReactions > 0;

        // Events
        public event Action<int, int> OnUltimateMeterChanged;    // current, max
        public event Action OnUltimateReady;
        public event Action<int> OnSpiritFragmentsChanged;
        public event Action OnActionPointsChanged;

        /// <summary>
        /// Initialize player stats for combat start.
        /// Sets AP to starting amount (1 by default).
        /// </summary>
        public new void Initialize()
        {
            base.Initialize();

            InitializeCombatResources();
            // Note: Ultimate meter and spirit fragments persist between combats
        }

        /// <summary>
        /// Prepare player-only combat resources while preserving current health.
        /// Use this when entering combat so healing items and bonfires keep their value.
        /// </summary>
        public void PrepareForCombat()
        {
            if (CurrentHealth <= 0)
                SetHealth(MaxHealth);

            ResetModifiers();
            InitializeCombatResources();
            // Note: Health, ultimate meter, and spirit fragments persist between combats
        }

        private void InitializeCombatResources()
        {
            // Force AP defaults in case Inspector has stale serialized values
            maxActions = 2;
            apPerTurn = 1;
            startingActions = 0;

            _currentActions = startingActions;
            _currentBonusActions = maxBonusActions;
            _currentReactions = maxReactions;
            OnActionPointsChanged?.Invoke();
        }

        /// <summary>
        /// Gain action points at the start of a new turn.
        /// Adds apPerTurn (1) to current AP, capped at maxActions (2).
        /// Defending on the previous turn preserves unspent AP.
        /// </summary>
        public void GainTurnActionPoints()
        {
            _currentActions = Mathf.Min(maxActions, _currentActions + apPerTurn);
            _currentBonusActions = Mathf.Min(maxBonusActions, _currentBonusActions + 1);
            _currentReactions = maxReactions;
            OnActionPointsChanged?.Invoke();
        }

        /// <summary>
        /// Grant action points immediately, capped by MaxActions.
        /// Used by consumables and temporary combat rewards.
        /// </summary>
        public int GainActionPoints(int count)
        {
            if (count <= 0) return 0;

            int previousActions = _currentActions;
            _currentActions = Mathf.Min(maxActions, _currentActions + count);
            int gained = _currentActions - previousActions;

            if (gained > 0)
                OnActionPointsChanged?.Invoke();

            return gained;
        }

        /// <summary>
        /// Legacy method - now calls GainTurnActionPoints.
        /// </summary>
        public void ResetActionPoints()
        {
            GainTurnActionPoints();
        }

        #region Action Point Methods

        /// <summary>
        /// Use an action point.
        /// </summary>
        public bool UseAction()
        {
            return UseActions(1);
        }

        /// <summary>
        /// Use multiple action points at once.
        /// </summary>
        public bool UseActions(int count)
        {
            if (_currentActions < count) return false;

            _currentActions -= count;
            OnActionPointsChanged?.Invoke();
            return true;
        }

        /// <summary>
        /// Use a bonus action point.
        /// </summary>
        public bool UseBonusAction()
        {
            if (_currentBonusActions <= 0) return false;

            _currentBonusActions--;
            OnActionPointsChanged?.Invoke();
            return true;
        }

        /// <summary>
        /// Use a reaction point.
        /// </summary>
        public bool UseReaction()
        {
            if (_currentReactions <= 0) return false;

            _currentReactions--;
            OnActionPointsChanged?.Invoke();
            return true;
        }

        /// <summary>
        /// Check if player can perform an action of a specific type.
        /// </summary>
        public bool CanPerformAction(ActionPointType type, int count = 1)
        {
            return type switch
            {
                ActionPointType.Action => _currentActions >= count,
                ActionPointType.BonusAction => _currentBonusActions >= count,
                ActionPointType.Reaction => _currentReactions >= count,
                ActionPointType.Free => true,
                _ => false
            };
        }

        /// <summary>
        /// Consume action points of a specific type.
        /// </summary>
        public bool ConsumeActionPoint(ActionPointType type, int count = 1)
        {
            return type switch
            {
                ActionPointType.Action => UseActions(count),
                ActionPointType.BonusAction => UseBonusAction(),
                ActionPointType.Reaction => UseReaction(),
                ActionPointType.Free => true,
                _ => false
            };
        }

        #endregion

        #region Ultimate Methods

        /// <summary>
        /// Add to ultimate meter (called on successful hits).
        /// </summary>
        /// <param name="amount">Amount to add (default 1 for standard hit)</param>
        public void AddUltimateMeter(int amount = 1)
        {
            int previousMeter = ultimateMeter;
            ultimateMeter = Mathf.Min(ultimateMaxMeter, ultimateMeter + amount);

            OnUltimateMeterChanged?.Invoke(ultimateMeter, ultimateMaxMeter);

            // Check if ultimate just became ready
            if (previousMeter < ultimateMaxMeter && ultimateMeter >= ultimateMaxMeter)
            {
                OnUltimateReady?.Invoke();
            }
        }

        /// <summary>
        /// Consume ultimate meter when using ultimate ability.
        /// </summary>
        public bool UseUltimate()
        {
            if (!UltimateReady) return false;

            ultimateMeter = 0;
            OnUltimateMeterChanged?.Invoke(ultimateMeter, ultimateMaxMeter);
            return true;
        }

        /// <summary>
        /// Reset ultimate meter (for new runs or special effects).
        /// </summary>
        public void ResetUltimateMeter()
        {
            ultimateMeter = 0;
            OnUltimateMeterChanged?.Invoke(ultimateMeter, ultimateMaxMeter);
        }

        #endregion

        #region Spirit Fragment Methods

        /// <summary>
        /// Add spirit fragments (collected from enemies/exploration).
        /// </summary>
        [System.Obsolete("Use InventoryManager.Instance.AddSpiritFragments() instead. InventoryManager is the single source of truth for spirit fragments.")]
        public void AddSpiritFragments(int amount)
        {
            if (amount <= 0) return;

            spiritFragments += amount;
            OnSpiritFragmentsChanged?.Invoke(spiritFragments);
        }

        /// <summary>
        /// Spend spirit fragments (for skill tree).
        /// </summary>
        [System.Obsolete("Use InventoryManager.Instance.SpendSpiritFragments() instead. InventoryManager is the single source of truth for spirit fragments.")]
        public bool SpendSpiritFragments(int amount)
        {
            if (amount <= 0 || spiritFragments < amount) return false;

            spiritFragments -= amount;
            OnSpiritFragmentsChanged?.Invoke(spiritFragments);
            return true;
        }

        /// <summary>
        /// Check if player can afford a cost.
        /// </summary>
        [System.Obsolete("Use InventoryManager.Instance.CanAfford() instead. InventoryManager is the single source of truth for spirit fragments.")]
        public bool CanAfford(int cost)
        {
            return spiritFragments >= cost;
        }

        #endregion

        /// <summary>
        /// Create a copy of player stats.
        /// </summary>
        public new PlayerStats Clone()
        {
            var clone = new PlayerStats();

            // Copy base stats
            clone.Initialize();
            clone.SetMaxHealth(this.MaxHealth, false);
            clone.SetHealth(this.CurrentHealth);

            // Copy player-specific stats
            clone.ultimateMeter = this.ultimateMeter;
            clone.ultimateMaxMeter = this.ultimateMaxMeter;
            clone.spiritFragments = this.spiritFragments;
            clone.maxActions = this.maxActions;
            clone.apPerTurn = this.apPerTurn;
            clone.startingActions = this.startingActions;
            clone.maxBonusActions = this.maxBonusActions;
            clone.maxReactions = this.maxReactions;

            return clone;
        }
    }

    /// <summary>
    /// Types of action points that can be spent during combat.
    /// Based on GDD 1.7.8 Action System.
    /// </summary>
    public enum ActionPointType
    {
        /// <summary>Main action - attacks, skills, movement</summary>
        Action,

        /// <summary>Bonus action - potions, rhythm skills</summary>
        BonusAction,

        /// <summary>Reaction - defensive abilities during enemy turn</summary>
        Reaction,

        /// <summary>Free action - no cost</summary>
        Free
    }
}
