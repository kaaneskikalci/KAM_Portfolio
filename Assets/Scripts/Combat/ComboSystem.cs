using UnityEngine;
using System;
using KAM.Core.Combat;
using KAM.Combat.TurnSystem;

namespace KAM.Combat
{
    /// <summary>
    /// Tracks consecutive player attacks and provides damage multipliers.
    /// Combo breaks when player takes damage, defends, or uses non-attack action.
    /// </summary>
    public class ComboSystem : MonoBehaviour
    {
        public static ComboSystem Instance { get; private set; }

        [Header("Combo Settings")]
        [SerializeField] private int maxCombo = 10;
        [SerializeField] private float baseMultiplierPerCombo = 0.1f; // +10% per combo
        [SerializeField] private float maxMultiplier = 2f; // Cap at 2x damage
        [SerializeField] private bool breakOnDefend = true;
        [SerializeField] private bool breakOnDamage = true;

        [Header("Debug")]
        [SerializeField] private bool debugMode = true;

        // Current combo state
        private int _comboCount = 0;
        private int _highestCombo = 0;

        // Events
        public event Action<int> OnComboChanged; // New combo count
        public event Action<int> OnComboIncreased; // Combo increased
        public event Action<int> OnComboReset; // Combo was reset (old count)
        public event Action<int> OnNewHighScore; // New highest combo

        // Properties
        public int ComboCount => _comboCount;
        public int HighestCombo => _highestCombo;
        public bool HasCombo => _comboCount > 0;

        /// <summary>
        /// Current damage multiplier based on combo count.
        /// </summary>
        public float ComboMultiplier
        {
            get
            {
                if (_comboCount <= 0) return 1f;
                float multiplier = 1f + (_comboCount * baseMultiplierPerCombo);
                return Mathf.Min(multiplier, maxMultiplier);
            }
        }

        /// <summary>
        /// Percentage bonus as integer (for display).
        /// </summary>
        public int ComboPercentageBonus => Mathf.RoundToInt((_comboCount * baseMultiplierPerCombo) * 100f);

        /// <summary>
        /// Apply settings from CombatUIController before Start() runs.
        /// </summary>
        public void SetSettings(float multiplierPerHit, float maxMult, int maxCount)
        {
            baseMultiplierPerCombo = multiplierPerHit;
            maxMultiplier = maxMult;
            maxCombo = maxCount;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void Start()
        {
            // Subscribe to combat events
            TurnManager turnManager = TurnManager.Instance;
            if (turnManager != null)
            {
                turnManager.OnCombatStart += HandleCombatStart;
                turnManager.OnCombatEnd += HandleCombatEnd;
            }

            // Subscribe to player damage events
            PlayerCombatUnit player = FindFirstObjectByType<PlayerCombatUnit>();
            if (player != null)
            {
                player.OnDamageTaken += HandlePlayerDamageTaken;
            }
        }

        private void OnDestroy()
        {
            TurnManager turnManager = TurnManager.Instance;
            if (turnManager != null)
            {
                turnManager.OnCombatStart -= HandleCombatStart;
                turnManager.OnCombatEnd -= HandleCombatEnd;
            }

            PlayerCombatUnit player = FindFirstObjectByType<PlayerCombatUnit>();
            if (player != null)
            {
                player.OnDamageTaken -= HandlePlayerDamageTaken;
            }

            if (Instance == this)
                Instance = null;
        }

        #region Combo Management

        /// <summary>
        /// Register an attack to increase the combo.
        /// Called when player successfully deals damage.
        /// </summary>
        public void RegisterAttack()
        {
            int oldCombo = _comboCount;
            _comboCount = Mathf.Min(_comboCount + 1, maxCombo);

            if (_comboCount > oldCombo)
            {
                OnComboIncreased?.Invoke(_comboCount);
                OnComboChanged?.Invoke(_comboCount);

                // Check for new high score
                if (_comboCount > _highestCombo)
                {
                    _highestCombo = _comboCount;
                    OnNewHighScore?.Invoke(_highestCombo);
                }

                if (debugMode)
                    Debug.Log($"[ComboSystem] Combo increased to {_comboCount} (x{ComboMultiplier:F2})");
            }
        }

        /// <summary>
        /// Reset the combo counter.
        /// </summary>
        public void ResetCombo(string reason = "")
        {
            if (_comboCount <= 0) return;

            int oldCombo = _comboCount;
            _comboCount = 0;

            OnComboReset?.Invoke(oldCombo);
            OnComboChanged?.Invoke(0);

            if (debugMode)
                Debug.Log($"[ComboSystem] Combo reset from {oldCombo}. Reason: {reason}");
        }

        /// <summary>
        /// Called when player chooses to defend.
        /// </summary>
        public void OnPlayerDefend()
        {
            if (breakOnDefend)
            {
                ResetCombo("Player defended");
            }
        }

        /// <summary>
        /// Called when player uses a non-attack action.
        /// </summary>
        public void OnPlayerNonAttackAction()
        {
            ResetCombo("Non-attack action");
        }

        /// <summary>
        /// Apply the combo multiplier to damage.
        /// </summary>
        public int ApplyComboMultiplier(int baseDamage)
        {
            return Mathf.RoundToInt(baseDamage * ComboMultiplier);
        }

        #endregion

        #region Event Handlers

        private void HandleCombatStart()
        {
            // Reset combo at combat start
            _comboCount = 0;
            OnComboChanged?.Invoke(0);

            if (debugMode)
                Debug.Log("[ComboSystem] Combat started - combo reset");
        }

        private void HandleCombatEnd()
        {
            // Report final stats
            if (debugMode && _highestCombo > 0)
            {
                Debug.Log($"[ComboSystem] Combat ended. Highest combo: {_highestCombo}");
            }

            // Don't reset highest combo - track across battles
        }

        private void HandlePlayerDamageTaken(CombatUnit player, DamageResult result)
        {
            if (result.HitSuccessful && breakOnDamage)
            {
                ResetCombo($"Player took {result.DamageInfo.FinalDamage} damage");
            }
        }

        #endregion

        #region Static Helpers

        /// <summary>
        /// Get the current combo multiplier (safe to call even if system doesn't exist).
        /// </summary>
        public static float GetMultiplier()
        {
            return Instance != null ? Instance.ComboMultiplier : 1f;
        }

        /// <summary>
        /// Register an attack (safe to call even if system doesn't exist).
        /// </summary>
        public static void Attack()
        {
            Instance?.RegisterAttack();
        }

        /// <summary>
        /// Notify defend action (safe to call even if system doesn't exist).
        /// </summary>
        public static void Defend()
        {
            Instance?.OnPlayerDefend();
        }

        #endregion
    }
}
