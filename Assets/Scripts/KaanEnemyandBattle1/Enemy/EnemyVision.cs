using UnityEngine;


public class EnemyVision : MonoBehaviour
{
    [Header("Vision Settings")]
    [SerializeField] private float visionRange = 10f;
    [SerializeField] private float visionAngle = 120f;
    [SerializeField] private float detectionHeight = 2f;

    [Header("Detection Settings")]
    [SerializeField] private float detectionDelay = 0.5f;
    [SerializeField] private float lostPlayerDelay = 2f;
    [SerializeField] private LayerMask obstacleMask; 
    [SerializeField] private LayerMask playerMask; 

    [Header("Detection Point")]
    [SerializeField] private Transform eyePoint; 
    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = true;
    [SerializeField] private bool useSimpleDetection = true; 

   
    private float _visionRangeMultiplier = 1f;

    private Transform _detectedPlayer;
    private float _detectionTimer;
    private float _lostPlayerTimer;
    private bool _playerInSight;
    private bool _playerConfirmed;

    public bool PlayerInSight => _playerInSight;
    public bool PlayerConfirmed => _playerConfirmed;
    public Transform DetectedPlayer => _detectedPlayer;
    public float EffectiveVisionRange => visionRange * _visionRangeMultiplier;

    private Transform _playerTransform;
    private PlayerMovement _playerMovement;

    private void Start()
    {
        _playerMovement = FindFirstObjectByType<PlayerMovement>();
        if (_playerMovement != null)
        {
            _playerTransform = _playerMovement.transform;
            Debug.Log($"{gameObject.name}: Found player '{_playerTransform.name}'");
        }
        else
        {
            Debug.LogError($"{gameObject.name}: NO PLAYER FOUND! Make sure PlayerMovement component exists in scene.");
        }

        if (eyePoint == null)
        {
            eyePoint = transform;
        }

        ValidateSetup();
    }

    private void ValidateSetup()
    {
        if (playerMask == 0)
        {
            Debug.LogError($"{gameObject.name}: PLAYER MASK NOT SET! " +
                "Select the enemy, find EnemyVision component, and set 'Player Mask' to the Player layer.");
        }

        if (_playerTransform != null)
        {
            int playerLayer = _playerTransform.gameObject.layer;
            if ((playerMask.value & (1 << playerLayer)) == 0)
            {
                Debug.LogError($"{gameObject.name}: Player is on layer '{LayerMask.LayerToName(playerLayer)}' " +
                    $"but Player Mask doesn't include this layer! " +
                    $"Either change Player's layer or update the Player Mask.");
            }

            if (_playerTransform.GetComponent<Collider>() == null &&
                _playerTransform.GetComponent<CharacterController>() == null)
            {
                Debug.LogWarning($"{gameObject.name}: Player has no Collider! Vision raycast may not detect it.");
            }
        }
    }

    private void Update()
    {
        if (_playerTransform == null) return;

        CheckVision();
        UpdateDetectionTimers();
    }

    private void CheckVision()
    {
        _playerInSight = false;

        Vector3 directionToPlayer = _playerTransform.position - eyePoint.position;
        directionToPlayer.y = 0; 

        float distanceToPlayer = directionToPlayer.magnitude;

        if (distanceToPlayer > EffectiveVisionRange)
        {
            return; 
        }

        float angle = Vector3.Angle(transform.forward, directionToPlayer);

        if (angle > visionAngle * 0.5f)
        {
            return; 
        }

        if (useSimpleDetection)
        {
            _playerInSight = true;
            _detectedPlayer = _playerTransform;

            if (showDebugLogs && !_playerConfirmed)
                Debug.Log($"<color=green>{gameObject.name}: PLAYER DETECTED!</color> Distance: {distanceToPlayer:F1}m, Angle: {angle:F1}°");
            return;
        }

        Vector3 eyePosition = eyePoint.position + Vector3.up * (detectionHeight * 0.5f);
        Vector3 playerCenter = _playerTransform.position + Vector3.up;
        Vector3 rayDirection = (playerCenter - eyePosition).normalized;
        float rayDistance = Vector3.Distance(eyePosition, playerCenter);

        bool obstacleBlocking = false;
        if (obstacleMask != 0)
        {
            if (Physics.Raycast(eyePosition, rayDirection, out RaycastHit obstacleHit, rayDistance, obstacleMask))
            {
                if (obstacleHit.distance < rayDistance - 0.1f)
                {
                    obstacleBlocking = true;
                }
            }
        }

        if (obstacleBlocking)
        {
            return;
        }

        if (playerMask != 0)
        {
            if (Physics.Raycast(eyePosition, rayDirection, out RaycastHit playerHit, rayDistance, playerMask))
            {
                _playerInSight = true;
                _detectedPlayer = _playerTransform;

                if (showDebugLogs && !_playerConfirmed)
                    Debug.Log($"<color=green>{gameObject.name}: PLAYER DETECTED!</color> Distance: {distanceToPlayer:F1}m");
            }
        }
        else
        {
            _playerInSight = true;
            _detectedPlayer = _playerTransform;
        }
    }

    private void UpdateDetectionTimers()
    {
        if (_playerInSight)
        {
            _lostPlayerTimer = 0f;

            if (!_playerConfirmed)
            {
                _detectionTimer += Time.deltaTime;

                if (_detectionTimer >= detectionDelay)
                {
                    _playerConfirmed = true;
                    OnPlayerDetected?.Invoke(_detectedPlayer);
                }
            }
        }
        else
        {
            _detectionTimer = 0f;

            if (_playerConfirmed)
            {
                _lostPlayerTimer += Time.deltaTime;

                if (_lostPlayerTimer >= lostPlayerDelay)
                {
                    _playerConfirmed = false;
                    _detectedPlayer = null;
                    OnPlayerLost?.Invoke();
                }
            }
        }
    }

    
    public void SetVisionRangeMultiplier(float multiplier)
    {
        _visionRangeMultiplier = Mathf.Clamp(multiplier, 0.1f, 2f);
    }

   
    public void ResetDetection()
    {
        _playerInSight = false;
        _playerConfirmed = false;
        _detectedPlayer = null;
        _detectionTimer = 0f;
        _lostPlayerTimer = 0f;
    }

   
    public void ForceDetectPlayer()
    {
        if (_playerTransform != null)
        {
            _playerInSight = true;
            _playerConfirmed = true;
            _detectedPlayer = _playerTransform;
            OnPlayerDetected?.Invoke(_detectedPlayer);
        }
    }

    
    public bool IsPositionInVision(Vector3 position)
    {
        Vector3 directionToPosition = position - eyePoint.position;
        directionToPosition.y = 0;

        if (directionToPosition.magnitude > EffectiveVisionRange)
            return false;

        float angle = Vector3.Angle(transform.forward, directionToPosition);
        return angle <= visionAngle * 0.5f;
    }

    public delegate void PlayerDetectedHandler(Transform player);
    public delegate void PlayerLostHandler();

    public event PlayerDetectedHandler OnPlayerDetected;
    public event PlayerLostHandler OnPlayerLost;

    private void OnDrawGizmos()
    {
        DrawVisionGizmos();
    }

    private void OnDrawGizmosSelected()
    {
        DrawVisionGizmos();
    }

    private void DrawVisionGizmos()
    {
        Vector3 eyePos = eyePoint != null ? eyePoint.position : transform.position;
        eyePos += Vector3.up * (detectionHeight * 0.5f);

        float effectiveRange = Application.isPlaying ? EffectiveVisionRange : visionRange;

        if (_playerConfirmed)
            Gizmos.color = Color.red; 
        else if (_playerInSight)
            Gizmos.color = Color.yellow;
        else
            Gizmos.color = new Color(0f, 1f, 0f, 0.5f); 

        int segments = 20;
        Vector3 prevPoint = eyePos + Quaternion.Euler(0, -visionAngle * 0.5f, 0) * transform.forward * effectiveRange;

        for (int i = 0; i <= segments; i++)
        {
            float currentAngle = -visionAngle * 0.5f + (visionAngle / segments) * i;
            Vector3 point = eyePos + Quaternion.Euler(0, currentAngle, 0) * transform.forward * effectiveRange;
            Gizmos.DrawLine(prevPoint, point);
            prevPoint = point;
        }

        Vector3 leftEdge = Quaternion.Euler(0, -visionAngle * 0.5f, 0) * transform.forward * effectiveRange;
        Vector3 rightEdge = Quaternion.Euler(0, visionAngle * 0.5f, 0) * transform.forward * effectiveRange;

        Gizmos.DrawLine(eyePos, eyePos + leftEdge);
        Gizmos.DrawLine(eyePos, eyePos + rightEdge);

        Gizmos.color = Color.blue;
        Gizmos.DrawRay(eyePos, transform.forward * 2f);

        if (Application.isPlaying && _playerInSight && _detectedPlayer != null)
        {
            Gizmos.color = _playerConfirmed ? Color.red : Color.yellow;
            Gizmos.DrawLine(eyePos, _detectedPlayer.position + Vector3.up);

            Gizmos.DrawWireSphere(_detectedPlayer.position + Vector3.up, 0.5f);
        }
    }
}
