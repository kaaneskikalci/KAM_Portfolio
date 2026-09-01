using UnityEngine;

namespace KAM.Interaction
{
    /// <summary>
    /// Base class for all interactable objects.
    /// Provides common functionality and a starting point for specific interactables.
    /// </summary>
    public abstract class BaseInteractable : MonoBehaviour, IInteractable
    {
        [Header("Interaction Settings")]
        [SerializeField] protected string interactionPrompt = "Interact";
        [SerializeField] protected int priority = 0;
        [SerializeField] protected Transform interactionPoint;
        [SerializeField] protected bool canInteractMultipleTimes = true;

        [Header("Visual Feedback")]
        [SerializeField] protected GameObject highlightEffect;
        [SerializeField] protected bool useOutlineHighlight = false;

        [Header("Audio")]
        [SerializeField] protected AudioClip interactSound;
        [SerializeField] protected AudioSource audioSource;

        protected bool _hasBeenInteracted = false;
        protected bool _isEnabled = true;

        public int InteractionPriority => priority;

        protected virtual void Awake()
        {
            if (interactionPoint == null)
                interactionPoint = transform;

            if (audioSource == null)
                audioSource = GetComponent<AudioSource>();
        }

        public virtual string GetInteractionPrompt()
        {
            return interactionPrompt;
        }

        public virtual bool CanInteract()
        {
            if (!_isEnabled) return false;
            if (!canInteractMultipleTimes && _hasBeenInteracted) return false;
            return true;
        }

        public virtual void Interact(GameObject interactor)
        {
            _hasBeenInteracted = true;

            // Play sound
            if (interactSound != null && audioSource != null)
            {
                audioSource.PlayOneShot(interactSound);
            }

            // Execute specific interaction logic
            OnInteract(interactor);
        }

        public virtual Transform GetInteractionPoint()
        {
            return interactionPoint;
        }

        /// <summary>
        /// Override this to implement specific interaction behavior.
        /// </summary>
        protected abstract void OnInteract(GameObject interactor);

        /// <summary>
        /// Enable or disable this interactable at runtime.
        /// </summary>
        public virtual void SetEnabled(bool enabled)
        {
            _isEnabled = enabled;
        }

        /// <summary>
        /// Reset the interacted state (useful for resetting puzzles, etc.)
        /// </summary>
        public virtual void ResetInteraction()
        {
            _hasBeenInteracted = false;
        }

        /// <summary>
        /// Show visual highlight when player is in range.
        /// </summary>
        public virtual void ShowHighlight()
        {
            if (highlightEffect != null)
                highlightEffect.SetActive(true);
        }

        /// <summary>
        /// Hide visual highlight when player leaves range.
        /// </summary>
        public virtual void HideHighlight()
        {
            if (highlightEffect != null)
                highlightEffect.SetActive(false);
        }
    }
}
