using UnityEngine;

namespace KAM.Interaction
{
    /// <summary>
    /// Interface for all objects that can be interacted with in the world.
    /// Implement this on any object that should respond to player interaction.
    /// </summary>
    public interface IInteractable
    {
        /// <summary>
        /// Gets the prompt text to display (e.g., "Open Chest", "Read Lore", "Talk")
        /// </summary>
        string GetInteractionPrompt();

        /// <summary>
        /// Check if this object can currently be interacted with.
        /// Use for conditional interactions (e.g., chest already opened, requires key)
        /// </summary>
        bool CanInteract();

        /// <summary>
        /// Called when the player interacts with this object.
        /// </summary>
        /// <param name="interactor">The GameObject initiating the interaction (usually player)</param>
        void Interact(GameObject interactor);

        /// <summary>
        /// Gets the transform position where the interaction UI should appear.
        /// Usually returns the object's transform or a child point.
        /// </summary>
        Transform GetInteractionPoint();

        /// <summary>
        /// Priority for when multiple interactables are in range.
        /// Higher priority = selected first. Default should be 0.
        /// </summary>
        int InteractionPriority => 0;
    }
}
