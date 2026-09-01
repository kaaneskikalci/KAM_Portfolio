using UnityEngine;
using System;
using System.Collections.Generic;
using KAM.Core.Combat;

namespace KAM.Combat.TurnSystem
{
    /// <summary>
    /// Manages the turn-based combat flow.
    /// Controls turn order, current turn, and combat phases.
    /// Based on GDD 1.7.2 Turn Order System.
    /// </summary>
    public class TurnManager : MonoBehaviour
    {
        private static TurnManager _instance;
        public static TurnManager Instance => _instance;

        [Header("Turn Settings")]
        [SerializeField] private float turnTransitionDelay = 0.5f;
        [SerializeField] private bool autoAdvanceTurns = false;

        [Header("Debug")]
        [SerializeField] private bool showDebugInfo = true;

        // Combat State
        private CombatState _currentState = CombatState.NotInCombat;
        private List<CombatUnit> _combatUnits = new List<CombatUnit>();
        private Queue<CombatUnit> _turnQueue = new Queue<CombatUnit>();
        private CombatUnit _currentUnit;
        private int _currentRound = 0;

        // References
        private TurnOrderCalculator _turnOrderCalculator;

        // Properties
        public CombatState CurrentState => _currentState;
        public CombatUnit CurrentUnit => _currentUnit;
        public int CurrentRound => _currentRound;
        public bool IsPlayerTurn => _currentUnit != null && _currentUnit.IsPlayer;
        public bool IsInCombat => _currentState != CombatState.NotInCombat;
        public IReadOnlyList<CombatUnit> CombatUnits => _combatUnits;

        // Events
        public event Action OnCombatStart;
        public event Action OnCombatEnd;
        public event Action<CombatResult> OnCombatEndWithResult;
        public event Action<int> OnRoundStart;
        public event Action<int> OnRoundEnd;
        public event Action<CombatUnit> OnTurnStart;
        public event Action<CombatUnit> OnTurnEnd;
        public event Action<List<CombatUnit>> OnTurnOrderChanged;
        public event Action<CombatState> OnStateChanged;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;

            _turnOrderCalculator = new TurnOrderCalculator();
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }
        }

        #region Combat Lifecycle

        /// <summary>
        /// Start a new combat encounter.
        /// </summary>
        public void StartCombat(List<CombatUnit> units)
        {
            if (_currentState != CombatState.NotInCombat)
            {
                Debug.LogWarning("TurnManager: Already in combat!");
                return;
            }

            if (units == null || units.Count == 0)
            {
                Debug.LogError("TurnManager: Cannot start combat with no units!");
                return;
            }

            _combatUnits.Clear();
            _combatUnits.AddRange(units);

            // Initialize all units for combat
            foreach (var unit in _combatUnits)
            {
                unit.InitializeForCombat();
                unit.OnDeath += HandleUnitDeath;
            }

            _currentRound = 0;
            SetState(CombatState.Starting);
            OnCombatStart?.Invoke();

            if (showDebugInfo)
            {
                Debug.Log($"TurnManager: Combat started with {_combatUnits.Count} units");
            }

            // Start first round
            StartNewRound();
        }

        /// <summary>
        /// End the current combat.
        /// </summary>
        public void EndCombat(CombatResult result = CombatResult.None)
        {
            if (_currentState == CombatState.NotInCombat) return;

            // Cleanup
            foreach (var unit in _combatUnits)
            {
                unit.OnDeath -= HandleUnitDeath;
                unit.ExitCombat();
            }

            _combatUnits.Clear();
            _turnQueue.Clear();
            _currentUnit = null;
            _currentRound = 0;

            SetState(CombatState.NotInCombat);
            OnCombatEnd?.Invoke();
            OnCombatEndWithResult?.Invoke(result);

            if (showDebugInfo)
            {
                Debug.Log($"TurnManager: Combat ended - {result}");
            }
        }

        /// <summary>
        /// Start a new round of combat.
        /// </summary>
        private void StartNewRound()
        {
            _currentRound++;

            // Calculate turn order for this round
            List<CombatUnit> aliveUnits = GetAliveUnits();
            List<CombatUnit> orderedUnits = _turnOrderCalculator.CalculateTurnOrder(aliveUnits);

            // Fill the turn queue
            _turnQueue.Clear();
            foreach (var unit in orderedUnits)
            {
                _turnQueue.Enqueue(unit);
            }

            OnRoundStart?.Invoke(_currentRound);
            OnTurnOrderChanged?.Invoke(orderedUnits);

            if (showDebugInfo)
            {
                Debug.Log($"TurnManager: Round {_currentRound} started");
                LogTurnOrder(orderedUnits);
            }

            // Start first turn
            StartNextTurn();
        }

        /// <summary>
        /// Start the next unit's turn.
        /// </summary>
        public void StartNextTurn()
        {
            // Check for end of round
            if (_turnQueue.Count == 0)
            {
                EndRound();
                return;
            }

            // Check for combat end conditions
            if (CheckCombatEnd())
            {
                return;
            }

            // Get next unit
            _currentUnit = _turnQueue.Dequeue();

            // Skip dead units
            if (_currentUnit == null || !_currentUnit.IsAlive)
            {
                StartNextTurn();
                return;
            }

            // Check if enemy can act this turn (turn frequency)
            if (_currentUnit is EnemyCombatUnit enemyUnit && !enemyUnit.CanActThisTurn)
            {
                if (showDebugInfo)
                {
                    Debug.Log($"TurnManager: {_currentUnit.UnitName} skips turn (charging)");
                }
                enemyUnit.BeginTurn();
                enemyUnit.EndTurn();
                StartNextTurn();
                return;
            }

            // Begin the turn
            SetState(_currentUnit.IsPlayer ? CombatState.PlayerTurn : CombatState.EnemyTurn);
            _currentUnit.BeginTurn();

            if (_currentUnit.IsActionBlockedByControlEffect())
            {
                if (showDebugInfo)
                {
                    Debug.Log($"TurnManager: {_currentUnit.UnitName} skips turn due to control effect");
                }

                _currentUnit.EndTurn();
                OnTurnEnd?.Invoke(_currentUnit);
                _currentUnit = null;
                StartNextTurn();
                return;
            }

            OnTurnStart?.Invoke(_currentUnit);

            if (showDebugInfo)
            {
                Debug.Log($"TurnManager: {_currentUnit.UnitName}'s turn");
            }
        }

        /// <summary>
        /// End the current unit's turn.
        /// </summary>
        public void EndCurrentTurn()
        {
            if (_currentUnit == null) return;

            _currentUnit.EndTurn();
            OnTurnEnd?.Invoke(_currentUnit);

            if (showDebugInfo)
            {
                Debug.Log($"TurnManager: {_currentUnit.UnitName}'s turn ended");
            }

            _currentUnit = null;

            // Auto advance or wait for input
            if (autoAdvanceTurns)
            {
                Invoke(nameof(StartNextTurn), turnTransitionDelay);
            }
        }

        /// <summary>
        /// Manually advance to next turn (for UI button).
        /// </summary>
        public void AdvanceToNextTurn()
        {
            if (_currentUnit != null)
            {
                EndCurrentTurn();
            }
            StartNextTurn();
        }

        /// <summary>
        /// End the current round.
        /// </summary>
        private void EndRound()
        {
            OnRoundEnd?.Invoke(_currentRound);

            if (showDebugInfo)
            {
                Debug.Log($"TurnManager: Round {_currentRound} ended");
            }

            // Start new round if combat continues
            if (!CheckCombatEnd())
            {
                StartNewRound();
            }
        }

        #endregion

        #region Combat Checks

        /// <summary>
        /// Check if combat should end.
        /// </summary>
        private bool CheckCombatEnd()
        {
            bool playerAlive = false;
            bool enemyAlive = false;

            foreach (var unit in _combatUnits)
            {
                if (!unit.IsAlive) continue;

                if (unit.IsPlayer)
                    playerAlive = true;
                else if (unit.IsEnemy)
                    enemyAlive = true;
            }

            if (!playerAlive)
            {
                EndCombat(CombatResult.Defeat);
                return true;
            }

            if (!enemyAlive)
            {
                EndCombat(CombatResult.Victory);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Handle a unit dying mid-combat.
        /// </summary>
        private void HandleUnitDeath(CombatUnit unit)
        {
            if (showDebugInfo)
            {
                Debug.Log($"TurnManager: {unit.UnitName} died!");
            }

            // Remove from turn queue if pending
            var newQueue = new Queue<CombatUnit>();
            while (_turnQueue.Count > 0)
            {
                var queuedUnit = _turnQueue.Dequeue();
                if (queuedUnit != unit && queuedUnit.IsAlive)
                {
                    newQueue.Enqueue(queuedUnit);
                }
            }
            _turnQueue = newQueue;

            // Update turn order display
            OnTurnOrderChanged?.Invoke(GetUpcomingTurns());

            // If it was the current unit's turn, advance
            if (_currentUnit == unit)
            {
                _currentUnit = null;
                StartNextTurn();
            }

            // Check for combat end
            CheckCombatEnd();
        }

        #endregion

        #region Unit Management

        /// <summary>
        /// Add a unit mid-combat (for summons).
        /// </summary>
        public void AddUnitToCombat(CombatUnit unit)
        {
            if (!_combatUnits.Contains(unit))
            {
                _combatUnits.Add(unit);
                unit.InitializeForCombat();
                unit.OnDeath += HandleUnitDeath;

                // Insert into turn queue based on speed
                _turnOrderCalculator.InsertIntoQueue(_turnQueue, unit, GetAliveUnits());

                OnTurnOrderChanged?.Invoke(GetUpcomingTurns());

                if (showDebugInfo)
                {
                    Debug.Log($"TurnManager: {unit.UnitName} joined combat!");
                }
            }
        }

        /// <summary>
        /// Remove a unit from combat (for fleeing).
        /// </summary>
        public void RemoveUnitFromCombat(CombatUnit unit)
        {
            if (_combatUnits.Contains(unit))
            {
                unit.OnDeath -= HandleUnitDeath;
                unit.ExitCombat();
                _combatUnits.Remove(unit);

                // Remove from queue
                var newQueue = new Queue<CombatUnit>();
                while (_turnQueue.Count > 0)
                {
                    var queuedUnit = _turnQueue.Dequeue();
                    if (queuedUnit != unit)
                    {
                        newQueue.Enqueue(queuedUnit);
                    }
                }
                _turnQueue = newQueue;

                OnTurnOrderChanged?.Invoke(GetUpcomingTurns());

                if (_currentUnit == unit)
                {
                    _currentUnit = null;
                    StartNextTurn();
                }

                CheckCombatEnd();
            }
        }

        /// <summary>
        /// Get all alive units.
        /// </summary>
        public List<CombatUnit> GetAliveUnits()
        {
            return _combatUnits.FindAll(u => u.IsAlive);
        }

        /// <summary>
        /// Get all alive enemy units.
        /// </summary>
        public List<CombatUnit> GetAliveEnemies()
        {
            return _combatUnits.FindAll(u => u.IsAlive && u.IsEnemy);
        }

        /// <summary>
        /// Get the player unit.
        /// </summary>
        public CombatUnit GetPlayer()
        {
            return _combatUnits.Find(u => u.IsPlayer);
        }

        /// <summary>
        /// Get upcoming turns for UI display.
        /// </summary>
        public List<CombatUnit> GetUpcomingTurns()
        {
            List<CombatUnit> upcoming = new List<CombatUnit>();

            if (_currentUnit != null && _currentUnit.IsAlive)
            {
                upcoming.Add(_currentUnit);
            }

            foreach (var unit in _turnQueue)
            {
                if (unit.IsAlive)
                {
                    upcoming.Add(unit);
                }
            }

            return upcoming;
        }

        #endregion

        #region State Management

        private void SetState(CombatState newState)
        {
            if (_currentState == newState) return;

            _currentState = newState;
            OnStateChanged?.Invoke(newState);
        }

        #endregion

        #region Debug

        private void LogTurnOrder(List<CombatUnit> units)
        {
            string order = "Turn Order: ";
            for (int i = 0; i < units.Count; i++)
            {
                order += $"{i + 1}.{units[i].UnitName} ";
            }
            Debug.Log(order);
        }

        #endregion
    }

    /// <summary>
    /// States of the combat system.
    /// </summary>
    public enum CombatState
    {
        NotInCombat,
        Starting,
        PlayerTurn,
        EnemyTurn,
        Animating,
        Ending
    }

    /// <summary>
    /// Result of a combat encounter.
    /// </summary>
    public enum CombatResult
    {
        None,
        Victory,
        Defeat,
        Fled
    }
}
