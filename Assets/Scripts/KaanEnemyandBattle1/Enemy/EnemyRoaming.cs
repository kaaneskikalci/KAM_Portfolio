using UnityEngine;
using System.Collections.Generic;


[RequireComponent(typeof(EnemyMovement))]
public class EnemyRoaming : MonoBehaviour
{
    public enum RoamingMode
    {
        Patrol,     
        Wander,     
        PingPong   
    }
    
    [Header("Roaming Mode")]
    [SerializeField] private RoamingMode roamingMode = RoamingMode.Patrol;
    
    [Header("Patrol Settings")]
    [SerializeField] private List<EnemyWaypoint> waypoints = new List<EnemyWaypoint>();
    [SerializeField] private bool startAtClosestWaypoint = true;
    
    [Header("Wander Settings")]
    [SerializeField] private float wanderRadius = 8f;
    [SerializeField] private Vector3 wanderCenter; 
    [SerializeField] private bool useStartPositionAsCenter = true;
    
    [Header("Timing")]
    [SerializeField] private float minIdleTime = 1f;
    [SerializeField] private float maxIdleTime = 3f;
    
    
    private EnemyMovement _movement;
    
   
    private int _currentWaypointIndex;
    private int _patrolDirection = 1; 
    private float _idleTimer;
    private float _currentIdleTime;
    private bool _isIdle;
    private bool _isRoaming;
    private Vector3 _startPosition;
    
    
    public bool IsRoaming => _isRoaming;
    public bool IsIdle => _isIdle;
    public RoamingMode CurrentMode => roamingMode;
    public Vector3 CurrentDestination { get; private set; }
    
    private void Awake()
    {
        _movement = GetComponent<EnemyMovement>();
        _startPosition = transform.position;
        
        if (useStartPositionAsCenter && wanderCenter == Vector3.zero)
        {
            wanderCenter = _startPosition;
        }
    }
    
    private void Start()
    {
        _movement.OnDestinationReached += HandleDestinationReached;
        
        if (startAtClosestWaypoint && roamingMode != RoamingMode.Wander)
        {
            FindClosestWaypoint();
        }
    }
    
    private void OnDestroy()
    {
        if (_movement != null)
        {
            _movement.OnDestinationReached -= HandleDestinationReached;
        }
    }
    
    private void Update()
    {
        if (!_isRoaming) return;
        
        if (_isIdle)
        {
            UpdateIdleState();
        }
    }
    
    
    public void StartRoaming()
    {
        _isRoaming = true;
        MoveToNextDestination();
    }
    

    public void StopRoaming()
    {
        _isRoaming = false;
        _isIdle = false;
        _movement.Stop();
    }
    

    public void PauseRoaming()
    {
        _isRoaming = false;
        _movement.Pause();
    }
    
  
    public void ResumeRoaming()
    {
        _isRoaming = true;
        _movement.Resume();
    }
    
    private void UpdateIdleState()
    {
        _idleTimer += Time.deltaTime;
        
        if (_idleTimer >= _currentIdleTime)
        {
            _isIdle = false;
            MoveToNextDestination();
        }
    }
    
    private void HandleDestinationReached()
    {
        if (!_isRoaming) return;
        
        float waitTime = GetCurrentWaypointWaitTime();
        
        StartIdle(waitTime);
        OnWaypointReached?.Invoke(_currentWaypointIndex);
    }
    
    private void StartIdle(float? overrideTime = null)
    {
        _isIdle = true;
        _idleTimer = 0f;
        _currentIdleTime = overrideTime ?? Random.Range(minIdleTime, maxIdleTime);
    }
    
    private void MoveToNextDestination()
    {
        Vector3 destination;
        
        switch (roamingMode)
        {
            case RoamingMode.Patrol:
                destination = GetNextPatrolDestination();
                break;
            case RoamingMode.PingPong:
                destination = GetNextPingPongDestination();
                break;
            case RoamingMode.Wander:
            default:
                destination = GetNextWanderDestination();
                break;
        }
        
        CurrentDestination = destination;
        _movement.MoveTo(destination);
    }
    
    private Vector3 GetNextPatrolDestination()
    {
        if (waypoints == null || waypoints.Count == 0)
        {
            Debug.LogWarning($"{gameObject.name}: No waypoints assigned for patrol mode!");
            return transform.position;
        }
        
        _currentWaypointIndex = (_currentWaypointIndex + 1) % waypoints.Count;
        
        if (waypoints[_currentWaypointIndex] != null)
        {
            return waypoints[_currentWaypointIndex].transform.position;
        }
        
        return transform.position;
    }
    
    private Vector3 GetNextPingPongDestination()
    {
        if (waypoints == null || waypoints.Count == 0)
        {
            return transform.position;
        }
        
        _currentWaypointIndex += _patrolDirection;
        
        if (_currentWaypointIndex >= waypoints.Count)
        {
            _currentWaypointIndex = waypoints.Count - 2;
            _patrolDirection = -1;
        }
        else if (_currentWaypointIndex < 0)
        {
            _currentWaypointIndex = 1;
            _patrolDirection = 1;
        }
        
        _currentWaypointIndex = Mathf.Clamp(_currentWaypointIndex, 0, waypoints.Count - 1);
        
        if (waypoints[_currentWaypointIndex] != null)
        {
            return waypoints[_currentWaypointIndex].transform.position;
        }
        
        return transform.position;
    }
    
    private Vector3 GetNextWanderDestination()
    {
        Vector3 center = useStartPositionAsCenter ? _startPosition : wanderCenter;
        
        if (_movement.GetRandomPointOnNavMesh(center, wanderRadius, out Vector3 randomPoint))
        {
            return randomPoint;
        }
        
        return _startPosition;
    }
    
    private float GetCurrentWaypointWaitTime()
    {
        if (roamingMode == RoamingMode.Wander) 
            return Random.Range(minIdleTime, maxIdleTime);
        
        if (waypoints != null && 
            _currentWaypointIndex >= 0 && 
            _currentWaypointIndex < waypoints.Count &&
            waypoints[_currentWaypointIndex] != null)
        {
            float waypointWait = waypoints[_currentWaypointIndex].WaitTime;
            if (waypointWait > 0) return waypointWait;
        }
        
        return Random.Range(minIdleTime, maxIdleTime);
    }
    
    private void FindClosestWaypoint()
    {
        if (waypoints == null || waypoints.Count == 0) return;
        
        float closestDistance = float.MaxValue;
        int closestIndex = 0;
        
        for (int i = 0; i < waypoints.Count; i++)
        {
            if (waypoints[i] == null) continue;
            
            float distance = Vector3.Distance(transform.position, waypoints[i].transform.position);
            if (distance < closestDistance)
            {
                closestDistance = distance;
                closestIndex = i;
            }
        }
        
        _currentWaypointIndex = closestIndex - 1; 
    }
    
   
    public void SetWaypoints(List<EnemyWaypoint> newWaypoints)
    {
        waypoints = newWaypoints;
        _currentWaypointIndex = -1;
    }
    
    
    public void SetWanderArea(Vector3 center, float radius)
    {
        wanderCenter = center;
        wanderRadius = radius;
        useStartPositionAsCenter = false;
    }
    
    
    public void SetRoamingMode(RoamingMode mode)
    {
        roamingMode = mode;
        _currentWaypointIndex = -1;
        _patrolDirection = 1;
    }
    
    public delegate void WaypointReachedHandler(int waypointIndex);
    public event WaypointReachedHandler OnWaypointReached;
    
    private void OnDrawGizmosSelected()
    {
      
        if (roamingMode == RoamingMode.Wander)
        {
            Gizmos.color = new Color(0f, 1f, 0f, 0.3f);
            Vector3 center = Application.isPlaying ? 
                (useStartPositionAsCenter ? _startPosition : wanderCenter) : 
                (useStartPositionAsCenter ? transform.position : wanderCenter);
            Gizmos.DrawWireSphere(center, wanderRadius);
        }
        
        if ((roamingMode == RoamingMode.Patrol || roamingMode == RoamingMode.PingPong) && 
            waypoints != null && waypoints.Count > 1)
        {
            Gizmos.color = Color.cyan;
            
            for (int i = 0; i < waypoints.Count - 1; i++)
            {
                if (waypoints[i] != null && waypoints[i + 1] != null)
                {
                    Gizmos.DrawLine(
                        waypoints[i].transform.position, 
                        waypoints[i + 1].transform.position
                    );
                }
            }
            
            if (roamingMode == RoamingMode.Patrol && waypoints.Count > 2)
            {
                if (waypoints[0] != null && waypoints[waypoints.Count - 1] != null)
                {
                    Gizmos.color = new Color(0f, 1f, 1f, 0.5f);
                    Gizmos.DrawLine(
                        waypoints[waypoints.Count - 1].transform.position,
                        waypoints[0].transform.position
                    );
                }
            }
        }
        
        if (Application.isPlaying && _isRoaming)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(CurrentDestination, 0.5f);
        }
    }
}

