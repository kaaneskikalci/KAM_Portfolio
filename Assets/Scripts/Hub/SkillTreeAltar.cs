using UnityEngine;
using KAM.Interaction;

namespace KAM.Hub
{
    /// <summary>
    /// Hub interactable that opens the Skill Tree UI when the player presses E.
    /// Placed in the Oba scene near the village center.
    /// </summary>
    public class SkillTreeAltar : BaseInteractable
    {
        protected override void Awake()
        {
            base.Awake();
            interactionPrompt = "Open Skill Tree";
            canInteractMultipleTimes = true;
            priority = 10;
        }

        public override string GetInteractionPrompt()
        {
            return "Open Skill Tree";
        }

        protected override void OnInteract(GameObject interactor)
        {
            // Find and open skill tree UI
            var skillTreeUI = FindFirstObjectByType<KAM.UI.SkillTree.SkillTreeUI>();
            if (skillTreeUI != null)
            {
                skillTreeUI.Toggle();
            }
            else
            {
                Debug.LogWarning("SkillTreeAltar: No SkillTreeUI found in scene!");
            }
        }
    }
}
