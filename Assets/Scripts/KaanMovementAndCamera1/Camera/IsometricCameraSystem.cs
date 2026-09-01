using UnityEngine;
using Unity.Cinemachine; 

public class IsometricCameraSystem : MonoBehaviour
{
    [Header("Targeting")]
    [Tooltip("Reference to your Player object")]
    [SerializeField] private PlayerMovement player;

    [Header("Isometric Settings")]
    [Tooltip("The size of the orthographic camera (Zoom level)")]
    [SerializeField] private float orthographicSize = 8f;
    [Tooltip("Standard Isometric angle is (30, 45, 0) or (45, 45, 0)")]
    [SerializeField] private Vector3 isometricAngle = new Vector3(30f, 45f, 0f);
    
    [Header("Follow Offset")]
    [Tooltip("Distance from player (affects how far back camera sits)")]
    [SerializeField] private float cameraDistance = 20f;
    [Tooltip("Additional offset to fine-tune centering")]
    [SerializeField] private Vector3 centeringOffset = Vector3.zero;

    [Header("Behavior")]
    [Tooltip("How far the camera looks ahead in the direction of movement (0 = perfectly centered)")]
    [SerializeField] private float lookAheadAmount = 0f;
    [Tooltip("How quickly the camera moves to the look-ahead point")]
    [SerializeField] private float lookAheadSpeed = 2f;
    [Tooltip("Damping for the follow smoothness")]
    [SerializeField] private Vector3 followDamping = new Vector3(0.5f, 0.5f, 0.5f);

    private CinemachineCamera _cmCamera;
    private CinemachineFollow _cmFollow;
    private GameObject _focusTarget; 
    private Vector3 _currentLookAheadOffset;

    private void Start()
    {
        InitializeSystem();
    }

    private void LateUpdate()
    {
        if (player == null || _focusTarget == null) return;

        HandleCameraLogic();
    }

  
    private void HandleCameraLogic()
    {
        Vector3 playerVelocity = player.Velocity;
        
        Vector3 targetOffset = Vector3.zero;
        
        if (player.IsMoving)
        {
            Vector3 moveDir = playerVelocity.normalized;
            targetOffset = moveDir * lookAheadAmount;
        }

        _currentLookAheadOffset = Vector3.Lerp(_currentLookAheadOffset, targetOffset, Time.deltaTime * lookAheadSpeed);

        
        _focusTarget.transform.position = player.transform.position + _currentLookAheadOffset;
    }

    
    [ContextMenu("Auto-Setup Camera")] 
    public void InitializeSystem()
    {
        if (player == null)
        {
            player = FindFirstObjectByType<PlayerMovement>();
            if (player == null)
            {
                Debug.LogError("IsometricCameraSystem: No PlayerMovement script found in scene!");
                return;
            }
        }

        if (_focusTarget == null)
        {
            _focusTarget = new GameObject("Camera_Focus_Target");
            _focusTarget.transform.position = player.transform.position;
        }

        var mainCam = Camera.main;
        if (mainCam == null)
        {
            Debug.LogError("IsometricCameraSystem: No MainCamera found tagged 'MainCamera'.");
            return;
        }

        
        mainCam.orthographic = true;

        var brain = mainCam.GetComponent<CinemachineBrain>();
        if (brain == null)
        {
            brain = mainCam.gameObject.AddComponent<CinemachineBrain>();
            brain.DefaultBlend.Time = 0.5f; 
        }

      
        _cmCamera = FindFirstObjectByType<CinemachineCamera>();
        if (_cmCamera == null)
        {
            GameObject camObj = new GameObject("CM_Isometric_Camera");
            _cmCamera = camObj.AddComponent<CinemachineCamera>();
        }

        _cmCamera.transform.position = player.transform.position + (-isometricAngle.normalized * 20f); 
        _cmCamera.transform.rotation = Quaternion.Euler(isometricAngle);

        
        LensSettings lens = _cmCamera.Lens;
        lens.OrthographicSize = orthographicSize;
        lens.NearClipPlane = -50f; 
        lens.FarClipPlane = 500f;
        
        
        _cmCamera.Lens = lens;

        
        var rotationComposer = _cmCamera.GetComponent<CinemachineRotationComposer>();
        if (rotationComposer != null)
        {
            if (Application.isPlaying) Destroy(rotationComposer);
            else DestroyImmediate(rotationComposer);
        }
        
        _cmFollow = _cmCamera.GetComponent<CinemachineFollow>();
        if (_cmFollow == null)
        {
            _cmFollow = _cmCamera.gameObject.AddComponent<CinemachineFollow>();
        }

        _cmCamera.Follow = _focusTarget.transform;
        _cmFollow.TrackerSettings.PositionDamping = followDamping;

        // Calculate the follow offset based on isometric angle
        // This positions the camera behind and above the target
        Vector3 followOffset = CalculateFollowOffset();
        _cmFollow.FollowOffset = followOffset + centeringOffset;

        Debug.Log($"Isometric Camera System Initialized. Follow Offset: {_cmFollow.FollowOffset}");
    }

    /// <summary>
    /// Calculate the follow offset based on the isometric angle.
    /// This ensures the camera is positioned correctly behind/above the target.
    /// </summary>
    private Vector3 CalculateFollowOffset()
    {
        // Convert isometric angles to radians
        float pitchRad = isometricAngle.x * Mathf.Deg2Rad;
        float yawRad = isometricAngle.y * Mathf.Deg2Rad;

        // Calculate offset based on spherical coordinates
        // The camera should be positioned at 'cameraDistance' away from target
        float y = Mathf.Sin(pitchRad) * cameraDistance;
        float horizontalDist = Mathf.Cos(pitchRad) * cameraDistance;
        float x = -Mathf.Sin(yawRad) * horizontalDist;
        float z = -Mathf.Cos(yawRad) * horizontalDist;

        return new Vector3(x, y, z);
    }

    /// <summary>
    /// Call this to recenter the camera on the player immediately.
    /// </summary>
    public void RecenterCamera()
    {
        if (_focusTarget != null && player != null)
        {
            _currentLookAheadOffset = Vector3.zero;
            _focusTarget.transform.position = player.transform.position;
        }
    }

    /// <summary>
    /// Update settings at runtime (useful for testing in inspector).
    /// </summary>
    [ContextMenu("Apply Settings")]
    public void ApplySettings()
    {
        if (_cmFollow != null)
        {
            _cmFollow.FollowOffset = CalculateFollowOffset() + centeringOffset;
        }

        if (_cmCamera != null)
        {
            _cmCamera.transform.rotation = Quaternion.Euler(isometricAngle);

            LensSettings lens = _cmCamera.Lens;
            lens.OrthographicSize = orthographicSize;
            _cmCamera.Lens = lens;
        }
    }

    private void OnValidate()
    {
        // Apply changes when values are modified in inspector during play mode
        if (Application.isPlaying && _cmFollow != null)
        {
            ApplySettings();
        }
    }

    private void OnDrawGizmos()
    {
        if (_focusTarget != null && player != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(_focusTarget.transform.position, 0.5f);
            Gizmos.DrawLine(player.transform.position, _focusTarget.transform.position);
        }
    }
}