using UnityEngine;
using UnityEngine.InputSystem;


[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float walkSpeed = 6f;
    [SerializeField] private float crouchSpeed = 3f;
    [SerializeField] private float acceleration = 10f;
    [SerializeField] private float deceleration = 12f;
    [SerializeField] private float rotationSpeed = 720f;
    
    [Header("Crouch Settings")]
    [SerializeField] private float standingHeight = 2f;
    [SerializeField] private float crouchingHeight = 1f;
    [SerializeField] private float crouchTransitionSpeed = 8f;
    
    [Header("Gravity")]
    [SerializeField] private float gravity = -20f;
    [SerializeField] private float groundedGravity = -2f;
    
    [Header("References")]
    [SerializeField] private Transform cameraTransform;
    
    private CharacterController _controller;
    private PlayerInputActions _inputActions;
    
    private Vector2 _inputVector;
    private Vector3 _currentVelocity;
    private Vector3 _verticalVelocity;
    private float _currentSpeed;
    private float _targetHeight;
    private bool _isCrouching;
    private bool _movementEnabled = true;
    
    public bool IsCrouching => _isCrouching;
    public bool IsMoving => _inputVector.sqrMagnitude > 0.01f && _movementEnabled;
    public Vector3 Velocity => _currentVelocity;
    public float CurrentSpeed => _currentSpeed;
    public bool MovementEnabled => _movementEnabled;
    
    private void Awake()
    {
        _controller = GetComponent<CharacterController>();
        _inputActions = new PlayerInputActions();
        _targetHeight = standingHeight;
        
        if (cameraTransform == null)
        {
            cameraTransform = Camera.main?.transform;
        }
    }
    
    private void OnEnable()
    {
        _inputActions.Enable();
        
        _inputActions.Player.Move.performed += OnMovePerformed;
        _inputActions.Player.Move.canceled += OnMoveCanceled;
        _inputActions.Player.Crouch.performed += OnCrouchPerformed;
    }
    
    private void OnDisable()
    {
        _inputActions.Disable();
        
        _inputActions.Player.Move.performed -= OnMovePerformed;
        _inputActions.Player.Move.canceled -= OnMoveCanceled;
        _inputActions.Player.Crouch.performed -= OnCrouchPerformed;
    }
    
    private void Update()
    {
        HandleMovement();
        HandleCrouch();
        ApplyGravity();
    }
    
    private void HandleMovement()
    {
        if (!_movementEnabled)
        {
            _currentSpeed = Mathf.MoveTowards(_currentSpeed, 0f, deceleration * Time.deltaTime);
            _currentVelocity = Vector3.zero;
            return;
        }
        
        Vector3 moveDirection = CalculateIsometricDirection(_inputVector);
        
        float targetSpeed = _isCrouching ? crouchSpeed : walkSpeed;
        
        if (moveDirection.sqrMagnitude > 0.01f)
        {
            _currentSpeed = Mathf.MoveTowards(_currentSpeed, targetSpeed, acceleration * Time.deltaTime);
            
            RotateTowardsMovement(moveDirection);
        }
        else
        {
            _currentSpeed = Mathf.MoveTowards(_currentSpeed, 0f, deceleration * Time.deltaTime);
        }
        
        _currentVelocity = moveDirection.normalized * _currentSpeed;
        
        Vector3 finalMovement = _currentVelocity + _verticalVelocity;
        if (_controller != null && _controller.enabled && _controller.gameObject.activeInHierarchy)
            _controller.Move(finalMovement * Time.deltaTime);
    }
    
   
    private Vector3 CalculateIsometricDirection(Vector2 input)
    {
        if (cameraTransform == null)
        {
            return new Vector3(input.x, 0f, input.y).normalized;
        }
        
        Vector3 cameraForward = cameraTransform.forward;
        Vector3 cameraRight = cameraTransform.right;
        
        cameraForward.y = 0f;
        cameraRight.y = 0f;
        cameraForward.Normalize();
        cameraRight.Normalize();
        
        Vector3 moveDirection = (cameraForward * input.y) + (cameraRight * input.x);
        
        return moveDirection;
    }
    
    private void RotateTowardsMovement(Vector3 direction)
    {
        if (direction.sqrMagnitude < 0.01f) return;
        
        Quaternion targetRotation = Quaternion.LookRotation(direction, Vector3.up);
        transform.rotation = Quaternion.RotateTowards(
            transform.rotation,
            targetRotation,
            rotationSpeed * Time.deltaTime
        );
    }
    
    private void HandleCrouch()
    {
        float currentHeight = _controller.height;
        
        if (Mathf.Abs(currentHeight - _targetHeight) > 0.01f)
        {
            float newHeight = Mathf.MoveTowards(currentHeight, _targetHeight, crouchTransitionSpeed * Time.deltaTime);
            
            float heightDifference = newHeight - currentHeight;
            Vector3 center = _controller.center;
            center.y += heightDifference * 0.5f;
            
            _controller.height = newHeight;
            _controller.center = center;
        }
    }
    
    private void ApplyGravity()
    {
        if (_controller.isGrounded)
        {
            _verticalVelocity.y = groundedGravity;
        }
        else
        {
            _verticalVelocity.y += gravity * Time.deltaTime;
        }
    }
    
    #region Input Callbacks
    
    private void OnMovePerformed(InputAction.CallbackContext context)
    {
        _inputVector = context.ReadValue<Vector2>();
    }
    
    private void OnMoveCanceled(InputAction.CallbackContext context)
    {
        _inputVector = Vector2.zero;
    }
    
    private void OnCrouchPerformed(InputAction.CallbackContext context)
    {
        // Toggle crouch
        _isCrouching = !_isCrouching;
        _targetHeight = _isCrouching ? crouchingHeight : standingHeight;
        
        // Check for ceiling when trying to stand up
        if (!_isCrouching && !CanStandUp())
        {
            _isCrouching = true;
            _targetHeight = crouchingHeight;
        }
    }
    
    #endregion
    
    
    private bool CanStandUp()
    {
        float checkDistance = standingHeight - crouchingHeight;
        Vector3 checkPosition = transform.position + Vector3.up * crouchingHeight;
        float checkRadius = _controller.radius * 0.9f;
        
        return !Physics.SphereCast(checkPosition, checkRadius, Vector3.up, out _, checkDistance);
    }
    
    
    public void SetCrouchState(bool crouching)
    {
        if (crouching == _isCrouching) return;
        
        if (!crouching && !CanStandUp()) return;
        
        _isCrouching = crouching;
        _targetHeight = _isCrouching ? crouchingHeight : standingHeight;
    }
    
    
    public void SetMovementEnabled(bool enabled)
    {
        _movementEnabled = enabled;
        
        if (!enabled)
        {
            _inputVector = Vector2.zero;
            _currentVelocity = Vector3.zero;
            _currentSpeed = 0f;
        }
    }
}
