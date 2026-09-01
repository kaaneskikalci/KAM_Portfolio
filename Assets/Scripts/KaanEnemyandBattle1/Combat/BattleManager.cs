using UnityEngine;
using System.Collections.Generic;
using KAM.Core.Combat;
using KAM.Combat.TurnSystem;

/// <summary>
/// Manages battle encounters and bridges the exploration/combat systems.
/// Now integrates with TurnManager for turn-based combat.
/// </summary>
public class BattleManager : MonoBehaviour
{
    private static BattleManager _instance;
    private static bool _applicationIsQuitting = false;

    public static BattleManager Instance
    {
        get
        {
            if (_applicationIsQuitting)
            {
                return null;
            }

            if (_instance == null)
            {
                _instance = FindFirstObjectByType<BattleManager>();

                if (_instance == null && Application.isPlaying && !_applicationIsQuitting)
                {
                    GameObject managerObj = new GameObject("BattleManager");
                    _instance = managerObj.AddComponent<BattleManager>();
                }
            }
            return _instance;
        }
    }

    [Header("Combat Settings")]
    [SerializeField] private float combatStartDelay = 0.5f;
    [SerializeField] private bool pauseTimeOnCombat = false;
    [SerializeField] private bool useTurnBasedCombat = true;

    [Header("Combat Facing")]
    [SerializeField] private bool alignUnitsOnCombatStart = true;
    [SerializeField] private bool alignPlayerToEnemyGroup = true;
    [SerializeField] private float facingMinDistance = 0.05f;

    [Header("Encounter Settings")]
    [Tooltip("Radius to search for additional enemies when combat starts")]
    [SerializeField] private float encounterRadius = 10f;
    [SerializeField] private LayerMask enemyLayer;

    [Header("Debug")]
    [SerializeField] private bool showDebugInfo = true;

    // References
    private PlayerMovement _player;
    private PlayerCombatUnit _playerCombatUnit;
    private TurnManager _turnManager;

    // Combat State
    private bool _inCombat;
    private List<EnemyStateMachine> _combatEnemies = new List<EnemyStateMachine>();
    private List<CombatUnit> _combatUnits = new List<CombatUnit>();
    private Transform _combatInitiator;

    // Properties
    public bool InCombat => _inCombat;
    public IReadOnlyList<EnemyStateMachine> CombatEnemies => _combatEnemies;
    public PlayerMovement Player => _player;
    public PlayerCombatUnit PlayerCombat => _playerCombatUnit;
    public TurnManager TurnManager => _turnManager;
    public bool UseTurnBasedCombat => useTurnBasedCombat;

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        _applicationIsQuitting = false;
    }

    private void OnApplicationQuit()
    {
        _applicationIsQuitting = true;
    }

    private void OnDestroy()
    {
        if (_instance == this)
        {
            _instance = null;
        }

        // Unsubscribe from TurnManager events
        if (_turnManager != null)
        {
            _turnManager.OnCombatEnd -= HandleTurnManagerCombatEnd;
            _turnManager.OnTurnStart -= HandleTurnStart;
            _turnManager.OnTurnEnd -= HandleTurnEnd;
        }
    }

    private void Start()
    {
        // Find player references
        _player = FindFirstObjectByType<PlayerMovement>();
        _playerCombatUnit = FindFirstObjectByType<PlayerCombatUnit>();

        if (_player == null)
        {
            Debug.LogWarning("BattleManager: No PlayerMovement found in scene!");
        }

        // Setup TurnManager
        SetupTurnManager();
    }

    /// <summary>
    /// Setup or find the TurnManager.
    /// </summary>
    private void SetupTurnManager()
    {
        _turnManager = TurnManager.Instance;

        if (_turnManager == null && useTurnBasedCombat)
        {
            // Create TurnManager if it doesn't exist
            GameObject turnManagerObj = new GameObject("TurnManager");
            _turnManager = turnManagerObj.AddComponent<TurnManager>();
        }

        if (_turnManager != null)
        {
            _turnManager.OnCombatEnd += HandleTurnManagerCombatEnd;
            _turnManager.OnTurnStart += HandleTurnStart;
            _turnManager.OnTurnEnd += HandleTurnEnd;
        }
    }

    #region Combat Start/End

    /// <summary>
    /// Start combat with an enemy initiator.
    /// </summary>
    public void StartCombat(EnemyStateMachine initiator, Transform playerTarget)
    {
        if (_inCombat)
        {
            // Add enemy to existing combat
            if (!_combatEnemies.Contains(initiator))
            {
                _combatEnemies.Add(initiator);

                // Add to turn system if using turn-based combat
                if (useTurnBasedCombat && _turnManager != null)
                {
                    EnemyCombatUnit enemyCombat = initiator.GetComponent<EnemyCombatUnit>();
                    if (enemyCombat != null)
                    {
                        _turnManager.AddUnitToCombat(enemyCombat);
                    }
                }

                AlignJoinedEnemyFacing(initiator);
                OnEnemyJoinedCombat?.Invoke(initiator);

                if (showDebugInfo)
                {
                    Debug.Log($"BattleManager: {initiator.name} joined existing combat!");
                }
            }
            return;
        }

        _inCombat = true;
        _combatInitiator = initiator.transform;
        _combatEnemies.Clear();
        _combatUnits.Clear();
        _combatEnemies.Add(initiator);

        // Get player reference
        if (_player == null)
        {
            _player = playerTarget.GetComponent<PlayerMovement>();
        }

        if (_playerCombatUnit == null && _player != null)
        {
            _playerCombatUnit = _player.GetComponent<PlayerCombatUnit>();
        }

        // Disable player exploration movement
        if (_player != null)
        {
            _player.SetMovementEnabled(false);
        }

        // Gather nearby enemies
        GatherNearbyEnemies(initiator.transform.position);

        AlignCombatFacing();

        // Pause time if configured
        if (pauseTimeOnCombat)
        {
            Time.timeScale = 0f;
        }

        // Fire event
        OnCombatStart?.Invoke();

        if (showDebugInfo)
        {
            Debug.Log($"BattleManager: Combat started with {_combatEnemies.Count} enemies!");
        }

        // Start turn-based combat
        if (useTurnBasedCombat)
        {
            StartTurnBasedCombat();
        }
    }

    /// <summary>
    /// Initialize and start turn-based combat.
    /// </summary>
    private void StartTurnBasedCombat()
    {
        if (_turnManager == null)
        {
            Debug.LogError("BattleManager: TurnManager not found!");
            return;
        }

        // Build combat unit list
        _combatUnits.Clear();

        // Add player
        if (_playerCombatUnit != null)
        {
            _combatUnits.Add(_playerCombatUnit);
        }
        else
        {
            Debug.LogWarning("BattleManager: No PlayerCombatUnit found!");
        }

        // Add enemies
        foreach (var enemy in _combatEnemies)
        {
            EnemyCombatUnit enemyCombat = enemy.GetComponent<EnemyCombatUnit>();
            if (enemyCombat != null)
            {
                _combatUnits.Add(enemyCombat);
            }
            else
            {
                Debug.LogWarning($"BattleManager: {enemy.name} has no EnemyCombatUnit component!");
            }
        }

        // Start the turn system
        _turnManager.StartCombat(_combatUnits);
    }

    /// <summary>
    /// End the current combat.
    /// </summary>
    public void EndCombat()
    {
        if (!_inCombat) return;

        _inCombat = false;

        // Re-enable player movement
        if (_player != null)
        {
            _player.SetMovementEnabled(true);
        }

        // Restore time scale
        if (pauseTimeOnCombat)
        {
            Time.timeScale = 1f;
        }

        _combatEnemies.Clear();
        _combatUnits.Clear();
        _combatInitiator = null;

        OnCombatEnd?.Invoke();

        if (showDebugInfo)
        {
            Debug.Log("BattleManager: Combat ended!");
        }
    }

    /// <summary>
    /// Handle TurnManager signaling combat end.
    /// </summary>
    private void HandleTurnManagerCombatEnd()
    {
        EndCombat();
    }

    #endregion

    #region Enemy Management

    /// <summary>
    /// Remove an enemy from combat.
    /// </summary>
    public void RemoveEnemyFromCombat(EnemyStateMachine enemy)
    {
        if (_combatEnemies.Contains(enemy))
        {
            _combatEnemies.Remove(enemy);

            // Remove from turn system
            if (useTurnBasedCombat && _turnManager != null)
            {
                EnemyCombatUnit enemyCombat = enemy.GetComponent<EnemyCombatUnit>();
                if (enemyCombat != null)
                {
                    _turnManager.RemoveUnitFromCombat(enemyCombat);
                }
            }

            OnEnemyLeftCombat?.Invoke(enemy);

            if (showDebugInfo)
            {
                Debug.Log($"BattleManager: {enemy.name} left combat. {_combatEnemies.Count} enemies remaining.");
            }

            // End combat if no enemies remain
            if (_combatEnemies.Count == 0)
            {
                if (useTurnBasedCombat && _turnManager != null)
                {
                    _turnManager.EndCombat(CombatResult.Victory);
                }
                else
                {
                    EndCombat();
                }
            }
        }
    }

    /// <summary>
    /// Gather nearby enemies to join the combat.
    /// </summary>
    private void GatherNearbyEnemies(Vector3 combatCenter)
    {
        Collider[] nearbyColliders = Physics.OverlapSphere(combatCenter, encounterRadius, enemyLayer);

        foreach (Collider col in nearbyColliders)
        {
            EnemyStateMachine enemy = col.GetComponent<EnemyStateMachine>();

            if (enemy != null && !_combatEnemies.Contains(enemy))
            {
                enemy.AlertToPlayer(_player.transform);

                if (showDebugInfo)
                {
                    Debug.Log($"BattleManager: {enemy.name} alerted to combat!");
                }
            }
        }
    }

    private void AlignCombatFacing()
    {
        if (!alignUnitsOnCombatStart || _player == null || _combatEnemies.Count == 0)
        {
            return;
        }

        Vector3 playerPosition = _player.transform.position;
        Vector3 playerLookTarget = alignPlayerToEnemyGroup
            ? GetEnemyGroupCenter()
            : _combatEnemies[0].transform.position;

        FaceTowards(_player.transform, playerLookTarget);

        foreach (EnemyStateMachine enemy in _combatEnemies)
        {
            if (enemy == null) continue;
            FaceTowards(enemy.transform, playerPosition);
        }
    }

    private void AlignJoinedEnemyFacing(EnemyStateMachine enemy)
    {
        if (!alignUnitsOnCombatStart || enemy == null || _player == null)
        {
            return;
        }

        FaceTowards(enemy.transform, _player.transform.position);
    }

    private Vector3 GetEnemyGroupCenter()
    {
        Vector3 center = Vector3.zero;
        int validEnemyCount = 0;

        foreach (EnemyStateMachine enemy in _combatEnemies)
        {
            if (enemy == null) continue;

            center += enemy.transform.position;
            validEnemyCount++;
        }

        if (validEnemyCount > 0)
        {
            return center / validEnemyCount;
        }

        return _combatInitiator != null ? _combatInitiator.position : _player.transform.position + _player.transform.forward;
    }

    private void FaceTowards(Transform unit, Vector3 targetPosition)
    {
        if (unit == null)
        {
            return;
        }

        Vector3 direction = targetPosition - unit.position;
        direction.y = 0f;

        if (direction.sqrMagnitude <= facingMinDistance * facingMinDistance)
        {
            return;
        }

        unit.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
    }

    /// <summary>
    /// Check if an enemy is in combat.
    /// </summary>
    public bool IsEnemyInCombat(EnemyStateMachine enemy)
    {
        return _combatEnemies.Contains(enemy);
    }

    /// <summary>
    /// Get all enemies in combat.
    /// </summary>
    public EnemyStateMachine[] GetAllCombatEnemies()
    {
        return _combatEnemies.ToArray();
    }

    /// <summary>
    /// Force start combat with specified enemies.
    /// </summary>
    public void ForceStartCombat(params EnemyStateMachine[] enemies)
    {
        if (_inCombat) return;
        if (enemies == null || enemies.Length == 0) return;

        if (_player == null)
        {
            _player = FindFirstObjectByType<PlayerMovement>();
        }

        if (_player == null)
        {
            Debug.LogError("BattleManager: Cannot start combat - no player found!");
            return;
        }

        enemies[0].ForceEnterCombat(_player.transform);

        for (int i = 1; i < enemies.Length; i++)
        {
            if (enemies[i] != null)
            {
                enemies[i].ForceEnterCombat(_player.transform);
            }
        }
    }

    #endregion

    #region Turn-Based Combat Integration

    /// <summary>
    /// Handle turn start from TurnManager.
    /// </summary>
    private void HandleTurnStart(CombatUnit unit)
    {
        if (unit == null) return;

        if (unit.IsPlayer)
        {
            OnPlayerTurnStart?.Invoke();

            if (showDebugInfo)
            {
                Debug.Log("BattleManager: Player's turn!");
            }
        }
        else
        {
            // Find the corresponding EnemyStateMachine
            EnemyCombatUnit enemyCombat = unit as EnemyCombatUnit;
            if (enemyCombat != null)
            {
                EnemyStateMachine enemySM = enemyCombat.GetComponent<EnemyStateMachine>();
                if (enemySM != null)
                {
                    OnEnemyTurnStart?.Invoke(enemySM);

                    if (showDebugInfo)
                    {
                        Debug.Log($"BattleManager: {enemySM.name}'s turn!");
                    }
                }
            }
        }
    }

    /// <summary>
    /// Handle turn end from TurnManager.
    /// </summary>
    private void HandleTurnEnd(CombatUnit unit)
    {
        OnTurnEnd?.Invoke();
    }

    /// <summary>
    /// Called when it's the player's turn (legacy support).
    /// </summary>
    public void BeginPlayerTurn()
    {
        OnPlayerTurnStart?.Invoke();

        if (showDebugInfo)
        {
            Debug.Log("BattleManager: Player's turn!");
        }
    }

    /// <summary>
    /// Called when it's an enemy's turn (legacy support).
    /// </summary>
    public void BeginEnemyTurn(EnemyStateMachine enemy)
    {
        OnEnemyTurnStart?.Invoke(enemy);

        if (showDebugInfo)
        {
            Debug.Log($"BattleManager: {enemy.name}'s turn!");
        }
    }

    /// <summary>
    /// Call when a turn ends (legacy support).
    /// </summary>
    public void EndTurn()
    {
        if (useTurnBasedCombat && _turnManager != null)
        {
            _turnManager.EndCurrentTurn();
        }

        OnTurnEnd?.Invoke();
    }

    /// <summary>
    /// Advance to next turn manually.
    /// </summary>
    public void AdvanceToNextTurn()
    {
        if (useTurnBasedCombat && _turnManager != null)
        {
            _turnManager.AdvanceToNextTurn();
        }
    }

    /// <summary>
    /// Get the current unit in combat.
    /// </summary>
    public CombatUnit GetCurrentUnit()
    {
        if (_turnManager != null)
        {
            return _turnManager.CurrentUnit;
        }
        return null;
    }

    /// <summary>
    /// Check if it's the player's turn.
    /// </summary>
    public bool IsPlayerTurn()
    {
        if (_turnManager != null)
        {
            return _turnManager.IsPlayerTurn;
        }
        return false;
    }

    #endregion

    #region Events

    public delegate void CombatHandler();
    public delegate void EnemyHandler(EnemyStateMachine enemy);

    /// <summary>Fired when combat begins.</summary>
    public event CombatHandler OnCombatStart;

    /// <summary>Fired when combat ends.</summary>
    public event CombatHandler OnCombatEnd;

    /// <summary>Fired when an enemy joins an existing combat.</summary>
    public event EnemyHandler OnEnemyJoinedCombat;

    /// <summary>Fired when an enemy leaves combat (death, flee).</summary>
    public event EnemyHandler OnEnemyLeftCombat;

    /// <summary>Fired when player's turn starts (turn-based).</summary>
    public event CombatHandler OnPlayerTurnStart;

    /// <summary>Fired when an enemy's turn starts (turn-based).</summary>
    public event EnemyHandler OnEnemyTurnStart;

    /// <summary>Fired when any turn ends (turn-based).</summary>
    public event CombatHandler OnTurnEnd;

    #endregion

    private void OnDrawGizmosSelected()
    {
        // Draw encounter radius
        Gizmos.color = new Color(1f, 0f, 0f, 0.2f);

        if (_combatInitiator != null)
        {
            Gizmos.DrawWireSphere(_combatInitiator.position, encounterRadius);
        }
        else
        {
            Gizmos.DrawWireSphere(transform.position, encounterRadius);
        }
    }
}
