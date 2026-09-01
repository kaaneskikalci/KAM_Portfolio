using System;
using UnityEngine;

namespace KAM.Combat.TimedHit
{
    /// <summary>
    /// ScriptableObject configuration for the Timed Hit (Rune) system.
    /// Based on GDD 1.7.5: Kam strikes with precise timing to deal +30% bonus damage.
    /// </summary>
    [CreateAssetMenu(fileName = "TimedHitConfig", menuName = "KAM/Combat/Timed Hit Config")]
    public class TimedHitConfig : ScriptableObject
    {
        [Header("Timing Settings")]
        [Tooltip("Time window in seconds for successful input (default: 1.0s)")]
        [SerializeField, Range(0.1f, 3f)] private float hitWindowDuration = 1.0f;

        [Tooltip("Delay before the hit window opens after attack starts (0 = immediate)")]
        [SerializeField, Range(0f, 1f)] private float windowStartDelay = 0f;

        [Tooltip("Visual warning time before window opens (0 = immediate)")]
        [SerializeField, Range(0f, 0.5f)] private float warningTime = 0.1f;

        [Header("Damage Modifiers")]
        [Tooltip("Damage multiplier for successful timed hit (1.3 = +30%)")]
        [SerializeField, Range(1f, 3f)] private float successMultiplier = 1.3f;

        [Tooltip("Damage multiplier for perfect timing (optional)")]
        [SerializeField, Range(1f, 4f)] private float perfectMultiplier = 1.5f;

        [Tooltip("Perfect timing window as percentage of hit window")]
        [SerializeField, Range(0.1f, 0.5f)] private float perfectWindowPercent = 0.2f;

        [Header("Difficulty Scaling")]
        [Tooltip("How quickly the window shrinks with consecutive hits")]
        [SerializeField, Range(0f, 0.1f)] private float windowShrinkPerHit = 0.02f;

        [Tooltip("Minimum window duration after shrinking")]
        [SerializeField, Range(0.1f, 0.5f)] private float minimumWindowDuration = 0.2f;

        [Tooltip("Number of consecutive hits before window starts shrinking")]
        [SerializeField, Range(0, 10)] private int shrinkStartThreshold = 3;

        [Header("Rune Directions")]
        [Tooltip("Possible rune directions for input")]
        [SerializeField] private RuneDirection[] possibleDirections = new RuneDirection[]
        {
            RuneDirection.Up,
            RuneDirection.Down,
            RuneDirection.Left,
            RuneDirection.Right
        };

        [Tooltip("Use random directions or sequential pattern")]
        [SerializeField] private bool randomizeDirections = true;

        [Header("Visual Feedback")]
        [Tooltip("Color when waiting for input")]
        [SerializeField] private Color waitingColor = new Color(1f, 1f, 1f, 0.5f);

        [Tooltip("Color when window is active")]
        [SerializeField] private Color activeColor = Color.yellow;

        [Tooltip("Color for perfect timing zone")]
        [SerializeField] private Color perfectColor = Color.cyan;

        [Tooltip("Color on successful hit")]
        [SerializeField] private Color successColor = Color.green;

        [Tooltip("Color on failed hit")]
        [SerializeField] private Color failColor = Color.red;

        [Header("Audio Cues")]
        [Tooltip("Sound when window opens")]
        [SerializeField] private AudioClip windowOpenSound;

        [Tooltip("Sound on successful timed hit")]
        [SerializeField] private AudioClip successSound;

        [Tooltip("Sound on perfect timed hit")]
        [SerializeField] private AudioClip perfectSound;

        [Tooltip("Sound on failed timed hit")]
        [SerializeField] private AudioClip failSound;

        // Public Properties
        public float HitWindowDuration => hitWindowDuration;
        public float WindowStartDelay => windowStartDelay;
        public float WarningTime => warningTime;
        public float SuccessMultiplier => successMultiplier;
        public float PerfectMultiplier => perfectMultiplier;
        public float PerfectWindowPercent => perfectWindowPercent;
        public float WindowShrinkPerHit => windowShrinkPerHit;
        public float MinimumWindowDuration => minimumWindowDuration;
        public int ShrinkStartThreshold => shrinkStartThreshold;
        public RuneDirection[] PossibleDirections => possibleDirections;
        public bool RandomizeDirections => randomizeDirections;
        public Color WaitingColor => waitingColor;
        public Color ActiveColor => activeColor;
        public Color PerfectColor => perfectColor;
        public Color SuccessColor => successColor;
        public Color FailColor => failColor;
        public AudioClip WindowOpenSound => windowOpenSound;
        public AudioClip SuccessSound => successSound;
        public AudioClip PerfectSound => perfectSound;
        public AudioClip FailSound => failSound;

        /// <summary>
        /// Calculate adjusted window duration based on consecutive hits
        /// </summary>
        public float GetAdjustedWindowDuration(int consecutiveHits)
        {
            if (consecutiveHits < shrinkStartThreshold)
                return hitWindowDuration;

            int shrinkSteps = consecutiveHits - shrinkStartThreshold;
            float adjustedDuration = hitWindowDuration - (shrinkSteps * windowShrinkPerHit);

            return Mathf.Max(adjustedDuration, minimumWindowDuration);
        }

        /// <summary>
        /// Get the perfect timing window duration
        /// </summary>
        public float GetPerfectWindowDuration(int consecutiveHits = 0)
        {
            return GetAdjustedWindowDuration(consecutiveHits) * perfectWindowPercent;
        }

        /// <summary>
        /// Get a random rune direction from available directions
        /// </summary>
        public RuneDirection GetRandomDirection()
        {
            if (possibleDirections == null || possibleDirections.Length == 0)
                return RuneDirection.Up;

            return possibleDirections[UnityEngine.Random.Range(0, possibleDirections.Length)];
        }

        /// <summary>
        /// Validate configuration in editor
        /// </summary>
        private void OnValidate()
        {
            if (minimumWindowDuration > hitWindowDuration)
                minimumWindowDuration = hitWindowDuration;

            if (possibleDirections == null || possibleDirections.Length == 0)
            {
                possibleDirections = new RuneDirection[] { RuneDirection.Up };
            }
        }
    }

    /// <summary>
    /// Rune direction enum matching Arrow key inputs (WASD disabled for movement)
    /// </summary>
    public enum RuneDirection
    {
        Up,     // Up Arrow (↑)
        Down,   // Down Arrow (↓)
        Left,   // Left Arrow (←)
        Right   // Right Arrow (→)
    }

    /// <summary>
    /// Result of a timed hit attempt
    /// </summary>
    public enum TimedHitResult
    {
        None,           // No attempt made
        Miss,           // Wrong direction or too early
        Late,           // Correct direction but too late
        Success,        // Within the hit window
        Perfect         // Within the perfect timing zone
    }

    /// <summary>
    /// Data structure for timed hit event information
    /// </summary>
    [Serializable]
    public struct TimedHitEventData
    {
        public TimedHitResult Result;
        public RuneDirection ExpectedDirection;
        public RuneDirection InputDirection;
        public float TimingOffset;      // How far from perfect (negative = early, positive = late)
        public float DamageMultiplier;
        public bool WasCorrectDirection;

        public static TimedHitEventData CreateSuccess(RuneDirection direction, float offset, float multiplier)
        {
            return new TimedHitEventData
            {
                Result = TimedHitResult.Success,
                ExpectedDirection = direction,
                InputDirection = direction,
                TimingOffset = offset,
                DamageMultiplier = multiplier,
                WasCorrectDirection = true
            };
        }

        public static TimedHitEventData CreatePerfect(RuneDirection direction, float offset, float multiplier)
        {
            return new TimedHitEventData
            {
                Result = TimedHitResult.Perfect,
                ExpectedDirection = direction,
                InputDirection = direction,
                TimingOffset = offset,
                DamageMultiplier = multiplier,
                WasCorrectDirection = true
            };
        }

        public static TimedHitEventData CreateMiss(RuneDirection expected, RuneDirection input)
        {
            return new TimedHitEventData
            {
                Result = TimedHitResult.Miss,
                ExpectedDirection = expected,
                InputDirection = input,
                TimingOffset = 0f,
                DamageMultiplier = 1f,
                WasCorrectDirection = expected == input
            };
        }

        public static TimedHitEventData CreateLate(RuneDirection direction, float offset)
        {
            return new TimedHitEventData
            {
                Result = TimedHitResult.Late,
                ExpectedDirection = direction,
                InputDirection = direction,
                TimingOffset = offset,
                DamageMultiplier = 1f,
                WasCorrectDirection = true
            };
        }
    }
}
