using UnityEngine;


[RequireComponent(typeof(Animator))]
public class PlayerAnimator : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerMovement playerMovement;
    
    [Header("Animation Parameters")]
    [SerializeField] private string speedParameter = "Speed";
    [SerializeField] private string crouchParameter = "IsCrouching";
    [SerializeField] private string groundedParameter = "IsGrounded";
    [SerializeField] private string moveXParameter = "MoveX";
    [SerializeField] private string moveZParameter = "MoveZ";
    
    [Header("Animation Smoothing")]
    [SerializeField] private float animationSmoothTime = 0.1f;
    
    private Animator _animator;
    private CharacterController _characterController;
    
    private int _speedHash;
    private int _crouchHash;
    private int _groundedHash;
    private int _moveXHash;
    private int _moveZHash;
    
    private float _smoothSpeed;
    private float _smoothMoveX;
    private float _smoothMoveZ;
    private float _speedVelocity;
    private float _moveXVelocity;
    private float _moveZVelocity;
    
    private void Awake()
    {
        _animator = GetComponent<Animator>();
        _characterController = GetComponent<CharacterController>();
        
        if (playerMovement == null)
        {
            playerMovement = GetComponent<PlayerMovement>();
            if (playerMovement == null)
            {
                playerMovement = GetComponentInParent<PlayerMovement>();
            }
        }
        
        _speedHash = Animator.StringToHash(speedParameter);
        _crouchHash = Animator.StringToHash(crouchParameter);
        _groundedHash = Animator.StringToHash(groundedParameter);
        _moveXHash = Animator.StringToHash(moveXParameter);
        _moveZHash = Animator.StringToHash(moveZParameter);
    }
    
    private void Update()
    {
        if (playerMovement == null || _animator == null) return;
        
        UpdateAnimationParameters();
    }
    
    private void UpdateAnimationParameters()
    {
        float targetSpeed = playerMovement.CurrentSpeed;
        Vector3 velocity = playerMovement.Velocity;
        bool isCrouching = playerMovement.IsCrouching;
        bool isGrounded = _characterController != null && _characterController.isGrounded;
        
        _smoothSpeed = Mathf.SmoothDamp(_smoothSpeed, targetSpeed, ref _speedVelocity, animationSmoothTime);
        
        Vector3 localVelocity = transform.InverseTransformDirection(velocity);
        float normalizedMoveX = velocity.magnitude > 0.01f ? localVelocity.x / playerMovement.CurrentSpeed : 0f;
        float normalizedMoveZ = velocity.magnitude > 0.01f ? localVelocity.z / playerMovement.CurrentSpeed : 0f;
        
        _smoothMoveX = Mathf.SmoothDamp(_smoothMoveX, normalizedMoveX, ref _moveXVelocity, animationSmoothTime);
        _smoothMoveZ = Mathf.SmoothDamp(_smoothMoveZ, normalizedMoveZ, ref _moveZVelocity, animationSmoothTime);
        
        SetFloatSafe(_speedHash, _smoothSpeed);
        SetFloatSafe(_moveXHash, _smoothMoveX);
        SetFloatSafe(_moveZHash, _smoothMoveZ);
        SetBoolSafe(_crouchHash, isCrouching);
        SetBoolSafe(_groundedHash, isGrounded);
    }
    
    
    private void SetFloatSafe(int hash, float value)
    {
        if (HasParameter(hash))
        {
            _animator.SetFloat(hash, value);
        }
    }
    
    
    private void SetBoolSafe(int hash, bool value)
    {
        if (HasParameter(hash))
        {
            _animator.SetBool(hash, value);
        }
    }
    
    
    private bool HasParameter(int hash)
    {
        foreach (var param in _animator.parameters)
        {
            if (param.nameHash == hash) return true;
        }
        return false;
    }
    
   
    public void PlayTrigger(string triggerName)
    {
        _animator.SetTrigger(triggerName);
    }
    
    
    public void SetAnimationSpeed(float speed)
    {
        _animator.speed = speed;
    }
    
   
    public float GetCurrentAnimationProgress(int layerIndex = 0)
    {
        var stateInfo = _animator.GetCurrentAnimatorStateInfo(layerIndex);
        return stateInfo.normalizedTime % 1f;
    }
}
