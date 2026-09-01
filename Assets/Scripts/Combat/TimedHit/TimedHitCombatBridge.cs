using System;
using System.Collections;
using UnityEngine;
using KAM.Combat.Actions;
using KAM.Core.Combat;
using KAM.Progression;

namespace KAM.Combat.TimedHit
{
    /// <summary>
    /// Bridge component that connects the TimedHitController with combat actions.
    /// Manages the flow between attack initiation and timed hit window.
    /// </summary>
    public class TimedHitCombatBridge : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private TimedHitController controller;
        [SerializeField] private TimedHitInput input;

        [Header("Settings")]
        [SerializeField] private bool autoFindReferences = true;
        [SerializeField] private float attackToWindowDelay = 0.3f;

        // State
        private BasicAttackAction _currentAction;
        private CombatUnit _currentUser;
        private CombatUnit _currentTarget;
        private bool _isProcessingAttack;
        private TimedHitEventData _lastResult;

        // Events
        public event Action<CombatUnit, CombatUnit> OnAttackStarted;
        public event Action<TimedHitEventData> OnTimedHitResolved;
        public event Action<ActionResult> OnAttackCompleted;

        // Properties
        public bool IsProcessingAttack => _isProcessingAttack;
        public TimedHitEventData LastResult => _lastResult;

        private void Awake()
        {
            if (autoFindReferences)
            {
                FindReferences();
            }
        }

        private void Start()
        {
            if (controller != null)
            {
                controller.OnTimedHitComplete += HandleTimedHitComplete;
            }
        }

        private void OnDestroy()
        {
            if (controller != null)
            {
                controller.OnTimedHitComplete -= HandleTimedHitComplete;
            }
        }

        private void FindReferences()
        {
            if (controller == null)
                controller = GetComponent<TimedHitController>() ?? FindFirstObjectByType<TimedHitController>();

            if (input == null)
                input = GetComponent<TimedHitInput>() ?? FindFirstObjectByType<TimedHitInput>();
        }

        /// <summary>
        /// Start an attack with timed hit support.
        /// </summary>
        public void StartTimedHitAttack(BasicAttackAction action, CombatUnit user, CombatUnit target)
        {
            if (_isProcessingAttack)
            {
                Debug.LogWarning("[TimedHitCombatBridge] Already processing an attack");
                return;
            }

            _currentAction = action;
            _currentUser = user;
            _currentTarget = target;
            _isProcessingAttack = true;

            OnAttackStarted?.Invoke(user, target);

            // Start the attack sequence
            StartCoroutine(AttackSequence());
        }

        /// <summary>
        /// Start a quick attack without waiting for animation.
        /// Immediately opens the timed hit window.
        /// </summary>
        public void StartQuickTimedHitAttack(BasicAttackAction action, CombatUnit user, CombatUnit target)
        {
            // Force reset if still processing (allows consecutive attacks)
            if (_isProcessingAttack)
            {
                Debug.Log("[TimedHitCombatBridge] Force resetting previous attack for new one");
                ForceResetAttack();
            }

            _currentAction = action;
            _currentUser = user;
            _currentTarget = target;
            _isProcessingAttack = true;

            OnAttackStarted?.Invoke(user, target);

            // Directly start timed hit window
            if (controller != null && action.EnableTimedHit)
            {
                Debug.Log("[TimedHitCombatBridge] Starting timed hit sequence...");
                controller.StartTimedHitSequence();
            }
            else
            {
                // No timed hit, execute immediately
                Debug.Log("[TimedHitCombatBridge] No timed hit, executing basic attack");
                CompleteAttack(false, 1f);
            }
        }

        /// <summary>
        /// Force reset the attack state (for consecutive attacks)
        /// </summary>
        public void ForceResetAttack()
        {
            if (controller != null)
            {
                controller.ForceReset();
            }

            StopAllCoroutines();

            _currentAction = null;
            _currentUser = null;
            _currentTarget = null;
            _isProcessingAttack = false;
        }

        private IEnumerator AttackSequence()
        {
            // Phase 1: Animation startup
            if (attackToWindowDelay > 0)
            {
                yield return new WaitForSeconds(attackToWindowDelay);
            }

            // Phase 2: Open timed hit window if enabled
            if (_currentAction.EnableTimedHit && controller != null)
            {
                controller.StartTimedHitSequence();

                // Wait for timed hit to complete
                while (controller.IsActive)
                {
                    yield return null;
                }

                // Small delay for feedback display
                yield return new WaitForSeconds(0.2f);
            }
            else
            {
                // No timed hit, proceed immediately
                CompleteAttack(false, 1f);
            }
        }

        private void HandleTimedHitComplete(TimedHitEventData eventData)
        {
            if (!_isProcessingAttack || _currentAction == null)
                return;

            _lastResult = eventData;
            OnTimedHitResolved?.Invoke(eventData);

            // Determine success and multiplier
            bool success = eventData.Result == TimedHitResult.Success || eventData.Result == TimedHitResult.Perfect;
            float multiplier = eventData.DamageMultiplier;
            if (success && SkillEffectApplier.Instance != null)
            {
                multiplier += SkillEffectApplier.Instance.GetTimedHitMultiplierBonus();
            }

            CompleteAttack(success, multiplier);
        }

        private void CompleteAttack(bool timedHitSuccess, float multiplier)
        {
            if (_currentAction == null || _currentUser == null || _currentTarget == null)
            {
                _isProcessingAttack = false;
                return;
            }

            // Set the timed hit result on the action
            _currentAction.SetTimedHitResult(timedHitSuccess, multiplier);

            // Execute the action
            ActionResult result = _currentAction.Execute(_currentUser, _currentTarget);

            // Fire completion event
            OnAttackCompleted?.Invoke(result);

            // Cleanup
            _currentAction = null;
            _currentUser = null;
            _currentTarget = null;
            _isProcessingAttack = false;
        }

        /// <summary>
        /// Cancel the current attack in progress.
        /// </summary>
        public void CancelAttack()
        {
            if (!_isProcessingAttack)
                return;

            if (controller != null)
            {
                controller.CancelSequence();
            }

            StopAllCoroutines();

            _currentAction = null;
            _currentUser = null;
            _currentTarget = null;
            _isProcessingAttack = false;
        }

        /// <summary>
        /// Set references manually.
        /// </summary>
        public void SetReferences(TimedHitController timedHitController, TimedHitInput timedHitInput)
        {
            // Unsubscribe from old controller
            if (controller != null)
            {
                controller.OnTimedHitComplete -= HandleTimedHitComplete;
            }

            controller = timedHitController;
            input = timedHitInput;

            // Subscribe to new controller
            if (controller != null)
            {
                controller.OnTimedHitComplete += HandleTimedHitComplete;
            }
        }

        /// <summary>
        /// Get the controller reference.
        /// </summary>
        public TimedHitController GetController()
        {
            return controller;
        }
    }
}
