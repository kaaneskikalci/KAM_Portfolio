using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

namespace KAM.Combat.TimedHit
{
    /// <summary>
    /// Handles input for the Timed Hit (Rune) system.
    /// Supports both New Input System and legacy input.
    /// Uses Q/E/Z/X keys for rune directions (Q=Up, E=Right, Z=Left, X=Down).
    /// </summary>
    public class TimedHitInput : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private TimedHitController controller;

        [Header("Input Settings")]
        [SerializeField] private bool useNewInputSystem = true;
        [FormerlySerializedAs("acceptIJKL")]
        [SerializeField] private bool acceptQEZx = true;       // Q=Up, E=Right, Z=Left, X=Down (recommended)
        [SerializeField] private bool acceptArrowKeys = false; // Disabled - may conflict with UI
        [SerializeField] private bool acceptWASD = false;      // Disabled - conflicts with movement

        [Header("Debug")]
        [SerializeField] private bool debugInput = true;

        [Header("Input Actions (New Input System)")]
        [SerializeField] private InputActionReference upAction;
        [SerializeField] private InputActionReference downAction;
        [SerializeField] private InputActionReference leftAction;
        [SerializeField] private InputActionReference rightAction;

        // Runtime input actions if not assigned
        private InputAction _upInputAction;
        private InputAction _downInputAction;
        private InputAction _leftInputAction;
        private InputAction _rightInputAction;

        // Events
        public event Action<RuneDirection> OnDirectionInput;

        private bool _inputActionsSetup = false;

        private void Awake()
        {
            if (controller == null)
                controller = GetComponent<TimedHitController>();

            // Setup input actions in Awake so they're ready before OnEnable
            if (useNewInputSystem)
            {
                SetupInputActions();
                _inputActionsSetup = true;
            }
        }

        private void Start()
        {
            if (controller == null)
                controller = FindFirstObjectByType<TimedHitController>();

            // If we couldn't set up in Awake, try again
            if (useNewInputSystem && !_inputActionsSetup)
            {
                SetupInputActions();
                _inputActionsSetup = true;
                EnableInputActions(); // Enable since OnEnable already ran
            }

            if (debugInput)
                Debug.Log($"[TimedHitInput] Start complete. Controller: {(controller != null ? controller.name : "NULL")}, InputSystem: {useNewInputSystem}");
        }

        private void OnEnable()
        {
            if (useNewInputSystem && _inputActionsSetup)
            {
                EnableInputActions();
                if (debugInput)
                    Debug.Log("[TimedHitInput] Input actions ENABLED");
            }
        }

        private void OnDisable()
        {
            if (useNewInputSystem)
            {
                DisableInputActions();
                if (debugInput)
                    Debug.Log("[TimedHitInput] Input actions DISABLED");
            }
        }

        private void Update()
        {
            // Always poll legacy input as backup (Q/E/Z/X keys)
            PollLegacyInput();
        }

        #region New Input System

        private void SetupInputActions()
        {
            // Create runtime input actions if not assigned via inspector
            if (upAction == null)
            {
                _upInputAction = new InputAction("TimedHit_Up", InputActionType.Button);
                if (acceptQEZx) _upInputAction.AddBinding("<Keyboard>/q");
                if (acceptWASD) _upInputAction.AddBinding("<Keyboard>/w");
                if (acceptArrowKeys) _upInputAction.AddBinding("<Keyboard>/upArrow");
                _upInputAction.performed += ctx => HandleDirectionInput(RuneDirection.Up);
            }

            if (downAction == null)
            {
                _downInputAction = new InputAction("TimedHit_Down", InputActionType.Button);
                if (acceptQEZx) _downInputAction.AddBinding("<Keyboard>/x");
                if (acceptWASD) _downInputAction.AddBinding("<Keyboard>/s");
                if (acceptArrowKeys) _downInputAction.AddBinding("<Keyboard>/downArrow");
                _downInputAction.performed += ctx => HandleDirectionInput(RuneDirection.Down);
            }

            if (leftAction == null)
            {
                _leftInputAction = new InputAction("TimedHit_Left", InputActionType.Button);
                if (acceptQEZx) _leftInputAction.AddBinding("<Keyboard>/z");
                if (acceptWASD) _leftInputAction.AddBinding("<Keyboard>/a");
                if (acceptArrowKeys) _leftInputAction.AddBinding("<Keyboard>/leftArrow");
                _leftInputAction.performed += ctx => HandleDirectionInput(RuneDirection.Left);
            }

            if (rightAction == null)
            {
                _rightInputAction = new InputAction("TimedHit_Right", InputActionType.Button);
                if (acceptQEZx) _rightInputAction.AddBinding("<Keyboard>/e");
                if (acceptWASD) _rightInputAction.AddBinding("<Keyboard>/d");
                if (acceptArrowKeys) _rightInputAction.AddBinding("<Keyboard>/rightArrow");
                _rightInputAction.performed += ctx => HandleDirectionInput(RuneDirection.Right);
            }

            // Subscribe to assigned input actions
            if (upAction != null)
                upAction.action.performed += ctx => HandleDirectionInput(RuneDirection.Up);
            if (downAction != null)
                downAction.action.performed += ctx => HandleDirectionInput(RuneDirection.Down);
            if (leftAction != null)
                leftAction.action.performed += ctx => HandleDirectionInput(RuneDirection.Left);
            if (rightAction != null)
                rightAction.action.performed += ctx => HandleDirectionInput(RuneDirection.Right);

            if (debugInput)
                Debug.Log($"[TimedHitInput] Input setup complete - Q/E/Z/X:{acceptQEZx}, Arrows:{acceptArrowKeys}, WASD:{acceptWASD}");
        }

        private void EnableInputActions()
        {
            _upInputAction?.Enable();
            _downInputAction?.Enable();
            _leftInputAction?.Enable();
            _rightInputAction?.Enable();

            upAction?.action.Enable();
            downAction?.action.Enable();
            leftAction?.action.Enable();
            rightAction?.action.Enable();
        }

        private void DisableInputActions()
        {
            _upInputAction?.Disable();
            _downInputAction?.Disable();
            _leftInputAction?.Disable();
            _rightInputAction?.Disable();

            upAction?.action.Disable();
            downAction?.action.Disable();
            leftAction?.action.Disable();
            rightAction?.action.Disable();
        }

        #endregion

        #region Legacy Input

        private void PollLegacyInput()
        {
            // Check Q/E/Z/X keys FIRST (always detect key press for debugging)
            if (acceptQEZx)
            {
                RuneDirection? pressedDirection = null;

                if (Input.GetKeyDown(KeyCode.Q))
                    pressedDirection = RuneDirection.Up;
                else if (Input.GetKeyDown(KeyCode.X))
                    pressedDirection = RuneDirection.Down;
                else if (Input.GetKeyDown(KeyCode.Z))
                    pressedDirection = RuneDirection.Left;
                else if (Input.GetKeyDown(KeyCode.E))
                    pressedDirection = RuneDirection.Right;

                if (pressedDirection.HasValue)
                {
                    if (debugInput)
                        Debug.Log($"[TimedHitInput] KEY PRESSED: {pressedDirection.Value} | Controller: {(controller != null ? "found" : "NULL")} | IsActive: {controller?.IsActive} | State: {controller?.CurrentState}");

                    // Check if we can process this input
                    if (controller == null)
                    {
                        Debug.LogError("[TimedHitInput] CONTROLLER IS NULL! Cannot process input.");
                        return;
                    }

                    if (!controller.IsActive)
                    {
                        Debug.LogWarning($"[TimedHitInput] Controller not active (State: {controller.CurrentState}). Input ignored.");
                        return;
                    }

                    // Process the input!
                    HandleDirectionInput(pressedDirection.Value);
                    return;
                }
            }

            // Check WASD (disabled by default - conflicts with movement)
            if (acceptWASD)
            {
                if (Input.GetKeyDown(KeyCode.W))
                {
                    HandleDirectionInput(RuneDirection.Up);
                    return;
                }
                if (Input.GetKeyDown(KeyCode.S))
                {
                    HandleDirectionInput(RuneDirection.Down);
                    return;
                }
                if (Input.GetKeyDown(KeyCode.A))
                {
                    HandleDirectionInput(RuneDirection.Left);
                    return;
                }
                if (Input.GetKeyDown(KeyCode.D))
                {
                    HandleDirectionInput(RuneDirection.Right);
                    return;
                }
            }

            // Check Arrow keys (disabled by default - may conflict with UI)
            if (acceptArrowKeys)
            {
                if (Input.GetKeyDown(KeyCode.UpArrow))
                {
                    HandleDirectionInput(RuneDirection.Up);
                    return;
                }
                if (Input.GetKeyDown(KeyCode.DownArrow))
                {
                    HandleDirectionInput(RuneDirection.Down);
                    return;
                }
                if (Input.GetKeyDown(KeyCode.LeftArrow))
                {
                    HandleDirectionInput(RuneDirection.Left);
                    return;
                }
                if (Input.GetKeyDown(KeyCode.RightArrow))
                {
                    HandleDirectionInput(RuneDirection.Right);
                    return;
                }
            }
        }

        #endregion

        #region Input Processing

        private void HandleDirectionInput(RuneDirection direction)
        {
            if (debugInput)
                Debug.Log($"[TimedHitInput] Direction input received: {direction}, Controller active: {controller?.IsActive ?? false}");

            // Fire event for any listeners
            OnDirectionInput?.Invoke(direction);

            // Forward to controller if active
            if (controller != null && controller.IsActive)
            {
                if (debugInput)
                    Debug.Log($"[TimedHitInput] Forwarding {direction} to controller");
                controller.ProcessInput(direction);
            }
            else if (debugInput)
            {
                Debug.LogWarning($"[TimedHitInput] Controller not active, input ignored! Controller: {(controller != null ? "found" : "NULL")}, IsActive: {controller?.IsActive}");
            }
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Set the controller reference
        /// </summary>
        public void SetController(TimedHitController newController)
        {
            controller = newController;
        }

        /// <summary>
        /// Manually simulate a direction input (useful for testing or touch controls)
        /// </summary>
        public void SimulateInput(RuneDirection direction)
        {
            HandleDirectionInput(direction);
        }

        /// <summary>
        /// Check if the system is ready to receive input
        /// </summary>
        public bool IsReadyForInput()
        {
            return controller != null && controller.IsActive;
        }

        #endregion

        #region Gamepad Support

        /// <summary>
        /// Setup gamepad support with custom bindings
        /// </summary>
        public void SetupGamepadBindings()
        {
            if (!useNewInputSystem)
            {
                Debug.LogWarning("[TimedHitInput] Gamepad support requires New Input System");
                return;
            }

            // Add D-pad bindings
            _upInputAction?.AddBinding("<Gamepad>/dpad/up");
            _downInputAction?.AddBinding("<Gamepad>/dpad/down");
            _leftInputAction?.AddBinding("<Gamepad>/dpad/left");
            _rightInputAction?.AddBinding("<Gamepad>/dpad/right");

            // Add left stick bindings (using stick as discrete directions)
            _upInputAction?.AddBinding("<Gamepad>/leftStick/up");
            _downInputAction?.AddBinding("<Gamepad>/leftStick/down");
            _leftInputAction?.AddBinding("<Gamepad>/leftStick/left");
            _rightInputAction?.AddBinding("<Gamepad>/leftStick/right");

            Debug.Log("[TimedHitInput] Gamepad bindings configured");
        }

        #endregion

        private void OnDestroy()
        {
            // Cleanup runtime input actions
            _upInputAction?.Dispose();
            _downInputAction?.Dispose();
            _leftInputAction?.Dispose();
            _rightInputAction?.Dispose();
        }
    }
}
