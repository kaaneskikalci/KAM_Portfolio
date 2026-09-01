using System;
using System.Collections;
using UnityEngine;

namespace KAM.Combat.TimedHit
{
    /// <summary>
    /// Core controller for the Timed Hit (Rune) system.
    /// Manages timing windows, validates player input, and calculates damage bonuses.
    /// Based on GDD 1.7.5: Player presses the correct direction within a 0.5s window for +30% damage.
    /// </summary>
    public class TimedHitController : MonoBehaviour
    {
        [Header("Configuration")]
        [SerializeField] private TimedHitConfig config;

        [Header("Debug")]
        [SerializeField] private bool debugMode = false;
        [SerializeField] private bool showDebugOverlay = false;

        // State tracking
        private TimedHitState _currentState = TimedHitState.Idle;
        private RuneDirection _currentDirection;
        private float _windowStartTime;
        private float _windowEndTime;
        private float _perfectStartTime;
        private float _perfectEndTime;
        private int _consecutiveSuccesses;
        private bool _inputReceived;
        private Coroutine _activeSequence;

        // Events
        public event Action<RuneDirection> OnSequenceStarted;
        public event Action OnWarningStarted;
        public event Action OnWindowOpened;
        public event Action<TimedHitEventData> OnTimedHitComplete;
        public event Action OnSequenceCancelled;

        // Properties
        public TimedHitState CurrentState => _currentState;
        public RuneDirection CurrentDirection => _currentDirection;
        public bool IsActive => _currentState == TimedHitState.Warning || _currentState == TimedHitState.WindowActive;
        public int ConsecutiveSuccesses => _consecutiveSuccesses;
        public TimedHitConfig Config => config;

        /// <summary>
        /// Get the current progress through the timing window (0-1)
        /// </summary>
        public float WindowProgress
        {
            get
            {
                if (_currentState != TimedHitState.WindowActive)
                    return 0f;

                float elapsed = Time.time - _windowStartTime;
                float duration = _windowEndTime - _windowStartTime;
                return Mathf.Clamp01(elapsed / duration);
            }
        }

        /// <summary>
        /// Check if currently in the perfect timing zone
        /// </summary>
        public bool IsInPerfectZone
        {
            get
            {
                if (_currentState != TimedHitState.WindowActive)
                    return false;

                float currentTime = Time.time;
                return currentTime >= _perfectStartTime && currentTime <= _perfectEndTime;
            }
        }

        /// <summary>
        /// Time remaining in the current window
        /// </summary>
        public float RemainingTime
        {
            get
            {
                if (_currentState != TimedHitState.WindowActive)
                    return 0f;

                return Mathf.Max(0f, _windowEndTime - Time.time);
            }
        }

        private void Awake()
        {
            if (config == null)
            {
                Debug.LogWarning("[TimedHitController] No config assigned. Creating default runtime config.");
                config = ScriptableObject.CreateInstance<TimedHitConfig>();
            }
        }

        /// <summary>
        /// Start a timed hit sequence for an attack
        /// </summary>
        public void StartTimedHitSequence()
        {
            // Force reset if not idle (allows quick consecutive attacks)
            if (_currentState != TimedHitState.Idle)
            {
                if (debugMode)
                    Debug.Log("[TimedHitController] Forcing reset before new sequence");
                ForceReset();
            }

            if (debugMode)
            {
                Debug.Log($"[TimedHitController] Starting sequence with config: " +
                    $"WindowDuration={config.HitWindowDuration}s, " +
                    $"Delay={config.WindowStartDelay}s, " +
                    $"Warning={config.WarningTime}s");
            }

            _activeSequence = StartCoroutine(TimedHitSequenceCoroutine());
        }

        /// <summary>
        /// Force reset the controller to idle state (for consecutive attacks)
        /// </summary>
        public void ForceReset()
        {
            if (_activeSequence != null)
            {
                StopCoroutine(_activeSequence);
                _activeSequence = null;
            }

            _currentState = TimedHitState.Idle;
            _inputReceived = false;

            if (debugMode)
                Debug.Log("[TimedHitController] Force reset complete");
        }

        /// <summary>
        /// Start a timed hit sequence with a specific direction
        /// </summary>
        public void StartTimedHitSequence(RuneDirection direction)
        {
            if (_currentState != TimedHitState.Idle)
            {
                if (debugMode)
                    Debug.Log("[TimedHitController] Cannot start sequence - already active");
                return;
            }

            _activeSequence = StartCoroutine(TimedHitSequenceCoroutine(direction));
        }

        /// <summary>
        /// Process player input during timed hit window
        /// </summary>
        public void ProcessInput(RuneDirection inputDirection)
        {
            if (_inputReceived)
            {
                if (debugMode)
                    Debug.Log("[TimedHitController] Input already received this sequence");
                return;
            }

            switch (_currentState)
            {
                case TimedHitState.Idle:
                case TimedHitState.Warning:
                    // Too early - register as miss
                    _inputReceived = true;
                    HandleEarlyInput(inputDirection);
                    break;

                case TimedHitState.WindowActive:
                    _inputReceived = true;
                    HandleWindowInput(inputDirection);
                    break;

                case TimedHitState.Complete:
                    // Sequence already complete
                    break;
            }
        }

        /// <summary>
        /// Cancel the current timed hit sequence
        /// </summary>
        public void CancelSequence()
        {
            if (_activeSequence != null)
            {
                StopCoroutine(_activeSequence);
                _activeSequence = null;
            }

            _currentState = TimedHitState.Idle;
            _inputReceived = false;
            OnSequenceCancelled?.Invoke();

            if (debugMode)
                Debug.Log("[TimedHitController] Sequence cancelled");
        }

        /// <summary>
        /// Reset consecutive success counter
        /// </summary>
        public void ResetConsecutiveSuccesses()
        {
            _consecutiveSuccesses = 0;
        }

        /// <summary>
        /// Set a new configuration at runtime
        /// </summary>
        public void SetConfig(TimedHitConfig newConfig)
        {
            if (newConfig != null)
                config = newConfig;
        }

        private IEnumerator TimedHitSequenceCoroutine(RuneDirection? forcedDirection = null)
        {
            _inputReceived = false;

            // Determine direction
            _currentDirection = forcedDirection ??
                (config.RandomizeDirections ? config.GetRandomDirection() : config.PossibleDirections[0]);

            // Start sequence
            _currentState = TimedHitState.Warning;
            OnSequenceStarted?.Invoke(_currentDirection);

            if (debugMode)
                Debug.Log($"[TimedHitController] Sequence started - Direction: {_currentDirection}");

            // Warning phase
            if (config.WarningTime > 0)
            {
                OnWarningStarted?.Invoke();
                yield return new WaitForSeconds(config.WarningTime);
            }

            // Check if cancelled or input received during warning
            if (_currentState == TimedHitState.Idle || _inputReceived)
                yield break;

            // Delay before window opens
            if (config.WindowStartDelay > 0)
            {
                yield return new WaitForSeconds(config.WindowStartDelay);
            }

            // Check again
            if (_currentState == TimedHitState.Idle || _inputReceived)
                yield break;

            // Open window
            float windowDuration = config.GetAdjustedWindowDuration(_consecutiveSuccesses);
            float perfectDuration = config.GetPerfectWindowDuration(_consecutiveSuccesses);

            _windowStartTime = Time.time;
            _windowEndTime = _windowStartTime + windowDuration;

            // Perfect zone is in the middle of the window
            float perfectOffset = (windowDuration - perfectDuration) / 2f;
            _perfectStartTime = _windowStartTime + perfectOffset;
            _perfectEndTime = _perfectStartTime + perfectDuration;

            _currentState = TimedHitState.WindowActive;
            OnWindowOpened?.Invoke();

            if (debugMode)
                Debug.Log($"[TimedHitController] Window opened - Duration: {windowDuration:F2}s, Perfect: {perfectDuration:F2}s");

            // Wait for window to close or input
            while (Time.time < _windowEndTime && !_inputReceived)
            {
                yield return null;
            }

            // Handle timeout (no input received)
            if (!_inputReceived)
            {
                HandleTimeout();
            }

            _currentState = TimedHitState.Complete;
            _activeSequence = null;

            // Small delay before returning to idle
            yield return new WaitForSeconds(0.1f);
            _currentState = TimedHitState.Idle;
        }

        private void HandleEarlyInput(RuneDirection inputDirection)
        {
            if (debugMode)
                Debug.Log($"[TimedHitController] Early input: {inputDirection} (Expected: {_currentDirection})");

            bool correctDirection = inputDirection == _currentDirection;
            var eventData = TimedHitEventData.CreateMiss(_currentDirection, inputDirection);

            // Reset consecutive on any failure
            _consecutiveSuccesses = 0;

            OnTimedHitComplete?.Invoke(eventData);

            // Cancel the rest of the sequence
            CancelSequence();
        }

        private void HandleWindowInput(RuneDirection inputDirection)
        {
            float currentTime = Time.time;
            float centerTime = (_perfectStartTime + _perfectEndTime) / 2f;
            float timingOffset = currentTime - centerTime;

            bool correctDirection = inputDirection == _currentDirection;

            if (debugMode)
                Debug.Log($"[TimedHitController] Window input: {inputDirection} (Expected: {_currentDirection}), Offset: {timingOffset:F3}s");

            if (!correctDirection)
            {
                // Wrong direction
                var missData = TimedHitEventData.CreateMiss(_currentDirection, inputDirection);
                _consecutiveSuccesses = 0;
                OnTimedHitComplete?.Invoke(missData);
            }
            else if (IsInPerfectZone)
            {
                // Perfect timing!
                _consecutiveSuccesses++;
                var perfectData = TimedHitEventData.CreatePerfect(_currentDirection, timingOffset, config.PerfectMultiplier);
                OnTimedHitComplete?.Invoke(perfectData);

                if (debugMode)
                    Debug.Log($"[TimedHitController] PERFECT HIT! Consecutive: {_consecutiveSuccesses}");
            }
            else
            {
                // Success (in window but not perfect)
                _consecutiveSuccesses++;
                var successData = TimedHitEventData.CreateSuccess(_currentDirection, timingOffset, config.SuccessMultiplier);
                OnTimedHitComplete?.Invoke(successData);

                if (debugMode)
                    Debug.Log($"[TimedHitController] SUCCESS! Consecutive: {_consecutiveSuccesses}");
            }

            // Stop the sequence coroutine since we got input
            if (_activeSequence != null)
            {
                StopCoroutine(_activeSequence);
                _activeSequence = null;
            }

            _currentState = TimedHitState.Complete;
            StartCoroutine(ResetToIdleAfterDelay(0.1f));
        }

        private void HandleTimeout()
        {
            if (debugMode)
                Debug.Log("[TimedHitController] Timeout - no input received");

            // No input = failure
            _consecutiveSuccesses = 0;

            var lateData = TimedHitEventData.CreateLate(_currentDirection, config.HitWindowDuration);
            OnTimedHitComplete?.Invoke(lateData);
        }

        private IEnumerator ResetToIdleAfterDelay(float delay)
        {
            yield return new WaitForSeconds(delay);
            _currentState = TimedHitState.Idle;
        }

        /// <summary>
        /// Get the current timing for external progress visualization
        /// </summary>
        public TimingInfo GetCurrentTimingInfo()
        {
            return new TimingInfo
            {
                State = _currentState,
                Direction = _currentDirection,
                WindowProgress = WindowProgress,
                IsInPerfectZone = IsInPerfectZone,
                RemainingTime = RemainingTime,
                ConsecutiveSuccesses = _consecutiveSuccesses
            };
        }

#if UNITY_EDITOR
        private void OnGUI()
        {
            if (!debugMode || !showDebugOverlay || _currentState == TimedHitState.Idle)
                return;

            GUILayout.BeginArea(new Rect(10, 10, 300, 200));
            GUILayout.Label($"State: {_currentState}");
            GUILayout.Label($"Direction: {_currentDirection}");
            GUILayout.Label($"Progress: {WindowProgress:P0}");
            GUILayout.Label($"In Perfect Zone: {IsInPerfectZone}");
            GUILayout.Label($"Remaining: {RemainingTime:F2}s");
            GUILayout.Label($"Consecutive: {_consecutiveSuccesses}");
            GUILayout.EndArea();
        }
#endif
    }

    /// <summary>
    /// State machine for timed hit sequence
    /// </summary>
    public enum TimedHitState
    {
        Idle,           // Waiting for next attack
        Warning,        // Visual warning before window opens
        WindowActive,   // Timing window is open
        Complete        // Result determined, transitioning back to idle
    }

    /// <summary>
    /// Snapshot of current timing information for UI
    /// </summary>
    public struct TimingInfo
    {
        public TimedHitState State;
        public RuneDirection Direction;
        public float WindowProgress;
        public bool IsInPerfectZone;
        public float RemainingTime;
        public int ConsecutiveSuccesses;
    }
}
