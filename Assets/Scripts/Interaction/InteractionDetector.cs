using System;
using System.Collections.Generic;
using UnityEngine;

namespace KAM.Interaction
{
    /// <summary>
    /// Attach to player to detect and interact with nearby interactable objects.
    /// Uses a sphere trigger to detect objects implementing IInteractable.
    /// </summary>
    public class InteractionDetector : MonoBehaviour
    {
        [Header("Detection Settings")]
        [SerializeField] private float detectionRadius = 2.5f;
        [SerializeField] private LayerMask interactableLayers = ~0; // All layers by default
        [SerializeField] private bool useSphereCast = true;

        [Header("Input")]
        [SerializeField] private KeyCode interactKey = KeyCode.E;

        [Header("Debug")]
        [SerializeField] private bool showDebugGizmos = true;
        [SerializeField] private bool debugLog = false;

        // Events
        public event Action<IInteractable> OnInteractableFound;
        public event Action<IInteractable> OnInteractableLost;
        public event Action<IInteractable> OnInteractionStarted;
        public event Action<IInteractable> OnInteractionCompleted;

        // Current state
        private IInteractable _currentInteractable;
        private readonly List<IInteractable> _interactablesInRange = new List<IInteractable>();
        private bool _isInteracting;

        public IInteractable CurrentInteractable => _currentInteractable;
        public bool HasInteractable => _currentInteractable != null;
        public bool IsInteracting => _isInteracting;
        public float DetectionRadius => detectionRadius;

        private void Update()
        {
            // Detect interactables
            DetectInteractables();

            // Handle input
            if (Input.GetKeyDown(interactKey) && !_isInteracting)
            {
                TryInteract();
            }
        }

        private void DetectInteractables()
        {
            _interactablesInRange.Clear();

            if (useSphereCast)
            {
                // Use OverlapSphere to find all colliders in range
                Collider[] colliders = Physics.OverlapSphere(transform.position, detectionRadius, interactableLayers);

                foreach (Collider col in colliders)
                {
                    // Check if this object has an IInteractable component
                    IInteractable interactable = col.GetComponent<IInteractable>();
                    if (interactable == null)
                        interactable = col.GetComponentInParent<IInteractable>();

                    if (interactable != null && interactable.CanInteract())
                    {
                        _interactablesInRange.Add(interactable);
                    }
                }
            }

            // Find the best interactable (closest with highest priority)
            IInteractable bestInteractable = FindBestInteractable();

            // Update current interactable
            if (bestInteractable != _currentInteractable)
            {
                if (_currentInteractable != null)
                {
                    if (debugLog) Debug.Log($"[InteractionDetector] Lost interactable: {(_currentInteractable as MonoBehaviour)?.gameObject.name}");
                    OnInteractableLost?.Invoke(_currentInteractable);
                }

                _currentInteractable = bestInteractable;

                if (_currentInteractable != null)
                {
                    if (debugLog) Debug.Log($"[InteractionDetector] Found interactable: {(_currentInteractable as MonoBehaviour)?.gameObject.name}");
                    OnInteractableFound?.Invoke(_currentInteractable);
                }
            }
        }

        private IInteractable FindBestInteractable()
        {
            if (_interactablesInRange.Count == 0)
                return null;

            IInteractable best = null;
            float bestScore = float.MinValue;

            foreach (IInteractable interactable in _interactablesInRange)
            {
                MonoBehaviour mb = interactable as MonoBehaviour;
                if (mb == null) continue;

                float distance = Vector3.Distance(transform.position, mb.transform.position);
                // Score = priority * 100 - distance (higher priority wins, then closer wins)
                float score = interactable.InteractionPriority * 100f - distance;

                if (score > bestScore)
                {
                    bestScore = score;
                    best = interactable;
                }
            }

            return best;
        }

        public void TryInteract()
        {
            if (_currentInteractable == null)
            {
                if (debugLog) Debug.Log("[InteractionDetector] No interactable in range");
                return;
            }

            if (!_currentInteractable.CanInteract())
            {
                if (debugLog) Debug.Log("[InteractionDetector] Current interactable cannot be interacted with");
                return;
            }

            if (_isInteracting)
            {
                if (debugLog) Debug.Log("[InteractionDetector] Already interacting");
                return;
            }

            // Start interaction
            _isInteracting = true;
            OnInteractionStarted?.Invoke(_currentInteractable);

            if (debugLog) Debug.Log($"[InteractionDetector] Interacting with: {(_currentInteractable as MonoBehaviour)?.gameObject.name}");

            // Execute the interaction
            _currentInteractable.Interact(gameObject);

            // Complete interaction (for simple interactions - complex ones should call CompleteInteraction manually)
            CompleteInteraction();
        }

        /// <summary>
        /// Call this to signal that a complex interaction has completed.
        /// For simple interactions, this is called automatically.
        /// </summary>
        public void CompleteInteraction()
        {
            if (!_isInteracting) return;

            _isInteracting = false;
            OnInteractionCompleted?.Invoke(_currentInteractable);
        }

        /// <summary>
        /// Manually set the detection radius at runtime.
        /// </summary>
        public void SetDetectionRadius(float radius)
        {
            detectionRadius = Mathf.Max(0.1f, radius);
        }

        private void OnDrawGizmosSelected()
        {
            if (!showDebugGizmos) return;

            // Draw detection radius
            Gizmos.color = _currentInteractable != null ? Color.green : Color.yellow;
            Gizmos.DrawWireSphere(transform.position, detectionRadius);

            // Draw line to current interactable
            if (_currentInteractable != null)
            {
                MonoBehaviour mb = _currentInteractable as MonoBehaviour;
                if (mb != null)
                {
                    Gizmos.color = Color.green;
                    Gizmos.DrawLine(transform.position, mb.transform.position);
                }
            }
        }
    }
}
