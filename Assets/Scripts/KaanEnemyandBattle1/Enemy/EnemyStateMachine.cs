using UnityEngine;


[RequireComponent(typeof(EnemyMovement))]
[RequireComponent(typeof(EnemyVision))]
[RequireComponent(typeof(EnemyRoaming))]
public class EnemyStateMachine : MonoBehaviour
{
    public enum EnemyState
    {
        Idle,      
        Roaming,   
        Alert,      
        Combat     
    }
    
    [Header("State Settings")]
    [SerializeField] private EnemyState initialState = EnemyState.Roaming;
    [SerializeField] private float idleToRoamDelay = 2f;
    
    [Header("Alert Settings")]
    [SerializeField] private float alertDuration = 1.5f;
    [SerializeField] private bool chaseOnAlert = true;
    
    [Header("Combat Settings")]
    [SerializeField] private float combatEngageDistance = 2f;
    
    [Header("Debug")]
    [SerializeField] private bool showDebugInfo = true;
    
    private EnemyMovement _movement;
    private EnemyVision _vision;
    private EnemyRoaming _roaming;
    
    private EnemyState _currentState;
    private float _stateTimer;
    private Transform _targetPlayer;
    
    public EnemyState CurrentState => _currentState;
    public Transform TargetPlayer => _targetPlayer;
    public bool IsInCombat => _currentState == EnemyState.Combat;
    
    private void Awake()
    {
        _movement = GetComponent<EnemyMovement>();
        _vision = GetComponent<EnemyVision>();
        _roaming = GetComponent<EnemyRoaming>();
    }
    
    private BattleManager _battleManagerRef;
    
    private void Start()
    {
        _vision.OnPlayerDetected += HandlePlayerDetected;
        _vision.OnPlayerLost += HandlePlayerLost;
        
        _battleManagerRef = BattleManager.Instance;
        if (_battleManagerRef != null)
        {
            _battleManagerRef.OnCombatEnd += HandleCombatEnded;
        }
        
        TransitionToState(initialState);
    }
    
    private void OnDestroy()
    {
        if (_vision != null)
        {
            _vision.OnPlayerDetected -= HandlePlayerDetected;
            _vision.OnPlayerLost -= HandlePlayerLost;
        }
        
        if (_battleManagerRef != null)
        {
            _battleManagerRef.OnCombatEnd -= HandleCombatEnded;
        }
    }
    
    private void Update()
    {
        _stateTimer += Time.deltaTime;
        
        switch (_currentState)
        {
            case EnemyState.Idle:
                UpdateIdleState();
                break;
            case EnemyState.Roaming:
                UpdateRoamingState();
                break;
            case EnemyState.Alert:
                UpdateAlertState();
                break;
            case EnemyState.Combat:
                UpdateCombatState();
                break;
        }
    }
    
    #region State Updates
    
    private void UpdateIdleState()
    {
        // Transition to roaming after delay
        if (_stateTimer >= idleToRoamDelay)
        {
            TransitionToState(EnemyState.Roaming);
        }
    }
    
    private void UpdateRoamingState()
    {
        // Vision system handles player detection via events
        // Roaming component handles movement
    }
    
    private void UpdateAlertState()
    {
        if (_targetPlayer == null)
        {
            TransitionToState(EnemyState.Roaming);
            return;
        }
        
        // Chase player if enabled
        if (chaseOnAlert)
        {
            _movement.MoveToTarget(_targetPlayer);
        }
        
        // Check if close enough to engage combat
        float distanceToPlayer = Vector3.Distance(transform.position, _targetPlayer.position);
        
        if (distanceToPlayer <= combatEngageDistance)
        {
            TransitionToState(EnemyState.Combat);
            return;
        }
        
        // If player is confirmed detected and alert time passed, engage combat
        if (_vision.PlayerConfirmed && _stateTimer >= alertDuration)
        {
            TransitionToState(EnemyState.Combat);
        }
    }
    
    private void UpdateCombatState()
    {
        // Combat is managed by BattleManager
        // This state just maintains the enemy in combat mode
    }
    
    #endregion
    
    #region State Transitions
    
    private void TransitionToState(EnemyState newState)
    {
        if (_currentState == newState) return;
        
        // Exit current state
        ExitState(_currentState);
        
        EnemyState previousState = _currentState;
        _currentState = newState;
        _stateTimer = 0f;
        
        // Enter new state
        EnterState(newState);
        
        // Broadcast state change
        OnStateChanged?.Invoke(previousState, newState);
        
        if (showDebugInfo)
        {
            Debug.Log($"{gameObject.name}: {previousState} -> {newState}");
        }
    }
    
    private void ExitState(EnemyState state)
    {
        switch (state)
        {
            case EnemyState.Roaming:
                _roaming.StopRoaming();
                break;
            case EnemyState.Alert:
                // Nothing special
                break;
            case EnemyState.Combat:
                // Combat cleanup handled by BattleManager
                break;
        }
    }
    
    private void EnterState(EnemyState state)
    {
        switch (state)
        {
            case EnemyState.Idle:
                _movement.Stop();
                OnEnterIdle?.Invoke();
                break;
                
            case EnemyState.Roaming:
                _roaming.StartRoaming();
                OnEnterRoaming?.Invoke();
                break;
                
            case EnemyState.Alert:
                _movement.Stop();
                OnEnterAlert?.Invoke(_targetPlayer);
                break;
                
            case EnemyState.Combat:
                _movement.Stop();
                EnterCombat();
                break;
        }
    }
    
    private void EnterCombat()
    {
        OnEnterCombat?.Invoke(_targetPlayer);
        
        // Notify Battle Manager (use cached reference first, then try Instance)
        BattleManager battleManager = _battleManagerRef != null ? _battleManagerRef : BattleManager.Instance;
        
        if (battleManager != null)
        {
            battleManager.StartCombat(this, _targetPlayer);
        }
        else
        {
            Debug.LogWarning($"{gameObject.name}: No BattleManager found! Combat cannot start properly.");
        }
    }
    
    #endregion
    
    #region Event Handlers
    
    private void HandlePlayerDetected(Transform player)
    {
        if (_currentState == EnemyState.Combat) return;
        
        _targetPlayer = player;
        TransitionToState(EnemyState.Alert);
    }
    
    private void HandlePlayerLost()
    {
        if (_currentState == EnemyState.Alert)
        {
            _targetPlayer = null;
            TransitionToState(EnemyState.Roaming);
        }
    }
    
    private void HandleCombatEnded()
    {
        if (_currentState == EnemyState.Combat)
        {
            _targetPlayer = null;
            _vision.ResetDetection();
            TransitionToState(EnemyState.Idle);
        }
    }
    
    #endregion
    
    #region Public Methods
    
    /// <summary>
    /// Forces the enemy into combat state (for scripted encounters).
    /// </summary>
    public void ForceEnterCombat(Transform player)
    {
        _targetPlayer = player;
        TransitionToState(EnemyState.Combat);
    }
    
    /// <summary>
    /// Forces the enemy out of combat (for fleeing, etc.).
    /// </summary>
    public void ForceExitCombat()
    {
        if (_currentState == EnemyState.Combat)
        {
            TransitionToState(EnemyState.Idle);
        }
    }
    
    /// <summary>
    /// Alerts this enemy about the player (for alert chains).
    /// </summary>
    public void AlertToPlayer(Transform player)
    {
        if (_currentState == EnemyState.Combat) return;
        
        _targetPlayer = player;
        _vision.ForceDetectPlayer();
        TransitionToState(EnemyState.Alert);
    }
    
    /// <summary>
    /// Resets the enemy to initial state.
    /// </summary>
    public void ResetEnemy()
    {
        _targetPlayer = null;
        _vision.ResetDetection();
        TransitionToState(initialState);
    }
    
    #endregion
    
    #region Events
    
    public delegate void StateChangedHandler(EnemyState previousState, EnemyState newState);
    public delegate void PlayerTargetHandler(Transform player);
    public delegate void SimpleHandler();
    
    public event StateChangedHandler OnStateChanged;
    public event SimpleHandler OnEnterIdle;
    public event SimpleHandler OnEnterRoaming;
    public event PlayerTargetHandler OnEnterAlert;
    public event PlayerTargetHandler OnEnterCombat;
    
    #endregion
    
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, combatEngageDistance);
        
        if (_targetPlayer != null)
        {
            Gizmos.color = _currentState == EnemyState.Combat ? Color.red : Color.yellow;
            Gizmos.DrawLine(transform.position + Vector3.up, _targetPlayer.position + Vector3.up);
        }
    }
    
    private void OnGUI()
    {
        if (!showDebugInfo || !Application.isPlaying) return;
        
        Vector3 screenPos = Camera.main.WorldToScreenPoint(transform.position + Vector3.up * 2f);
        
        if (screenPos.z > 0)
        {
            GUI.Label(
                new Rect(screenPos.x - 50, Screen.height - screenPos.y, 100, 25),
                _currentState.ToString(),
                new GUIStyle { alignment = TextAnchor.MiddleCenter, normal = { textColor = GetStateColor() } }
            );
        }
    }
    
    private Color GetStateColor()
    {
        return _currentState switch
        {
            EnemyState.Idle => Color.gray,
            EnemyState.Roaming => Color.green,
            EnemyState.Alert => Color.yellow,
            EnemyState.Combat => Color.red,
            _ => Color.white
        };
    }
}