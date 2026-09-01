using UnityEngine;
using UnityEngine.AI;


[RequireComponent(typeof(NavMeshAgent))]
public class EnemyMovement : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 3.5f;
    [SerializeField] private float rotationSpeed = 540f;
    [SerializeField] private float stoppingDistance = 0.5f;
    [SerializeField] private float acceleration = 8f;
    
    [Header("NavMesh Settings")]
    [SerializeField] private float pathUpdateInterval = 0.2f;
    [SerializeField] private bool autoRotate = false; 
    private NavMeshAgent _agent;
    
    private Vector3 _targetPosition;
    private bool _isMoving;
    private float _pathUpdateTimer;
    
    public bool IsMoving => _isMoving && _agent.remainingDistance > stoppingDistance;
    public Vector3 Velocity => _agent.velocity;
    public float CurrentSpeed => _agent.velocity.magnitude;
    public bool HasReachedDestination => !_agent.pathPending && _agent.remainingDistance <= stoppingDistance;
    public NavMeshAgent Agent => _agent;
    
    private void Awake()
    {
        _agent = GetComponent<NavMeshAgent>();
        ConfigureAgent();
    }
    
    private void ConfigureAgent()
    {
        _agent.speed = moveSpeed;
        _agent.angularSpeed = autoRotate ? rotationSpeed : 0f;
        _agent.acceleration = acceleration;
        _agent.stoppingDistance = stoppingDistance;
        _agent.updateRotation = autoRotate;
        _agent.autoBraking = true;
    }
    
    private void Update()
    {
        if (_isMoving)
        {
            HandleRotation();
            CheckDestinationReached();
        }
    }
    
    
    private void HandleRotation()
    {
        if (autoRotate || _agent.velocity.sqrMagnitude < 0.01f) return;
        
        Vector3 direction = _agent.velocity.normalized;
        direction.y = 0f;
        
        if (direction.sqrMagnitude > 0.01f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(direction, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation,
                targetRotation,
                rotationSpeed * Time.deltaTime
            );
        }
    }
    
    private void CheckDestinationReached()
    {
        if (HasReachedDestination)
        {
            _isMoving = false;
            OnDestinationReached?.Invoke();
        }
    }
    
    
    public bool MoveTo(Vector3 position)
    {
        if (!_agent.isOnNavMesh)
        {
            Debug.LogWarning($"{gameObject.name}: Cannot move - not on NavMesh!");
            return false;
        }
        
        _targetPosition = position;
        
        if (_agent.SetDestination(position))
        {
            _isMoving = true;
            _agent.isStopped = false;
            return true;
        }
        
        return false;
    }
    
    
    public void MoveToTarget(Transform target)
    {
        if (target == null) return;
        MoveTo(target.position);
    }
    
   
    public void Stop()
    {
        if (_agent.isOnNavMesh)
        {
            _agent.isStopped = true;
            _agent.ResetPath();
        }
        _isMoving = false;
    }
    

    public void Pause()
    {
        if (_agent.isOnNavMesh)
        {
            _agent.isStopped = true;
        }
    }
    

    public void Resume()
    {
        if (_agent.isOnNavMesh && _isMoving)
        {
            _agent.isStopped = false;
        }
    }
    
   
    public void SetSpeed(float speed)
    {
        moveSpeed = speed;
        _agent.speed = speed;
    }
    
    
    public bool CanReachPosition(Vector3 position)
    {
        NavMeshPath path = new NavMeshPath();
        return _agent.CalculatePath(position, path) && path.status == NavMeshPathStatus.PathComplete;
    }
    
    
    public bool GetRandomPointOnNavMesh(Vector3 center, float radius, out Vector3 result)
    {
        for (int i = 0; i < 30; i++) // Try up to 30 times
        {
            Vector3 randomPoint = center + Random.insideUnitSphere * radius;
            
            if (NavMesh.SamplePosition(randomPoint, out NavMeshHit hit, radius, NavMesh.AllAreas))
            {
                result = hit.position;
                return true;
            }
        }
        
        result = center;
        return false;
    }
    
    public delegate void DestinationReachedHandler();
    public event DestinationReachedHandler OnDestinationReached;
    
    private void OnDrawGizmosSelected()
    {
        if (!Application.isPlaying) return;
        
        if (_agent != null && _agent.hasPath)
        {
            Gizmos.color = Color.green;
            Vector3[] corners = _agent.path.corners;
            
            for (int i = 0; i < corners.Length - 1; i++)
            {
                Gizmos.DrawLine(corners[i], corners[i + 1]);
            }
            
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(_agent.destination, 0.3f);
        }
    }
}

