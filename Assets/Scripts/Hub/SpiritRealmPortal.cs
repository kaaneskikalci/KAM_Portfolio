using UnityEngine;
using KAM.Interaction;
using KAM.Core;

namespace KAM.Hub
{
    /// <summary>
    /// Hub interactable that starts a new run into the Spirit Realm.
    /// Calls GameManager.StartRun() on interaction.
    /// </summary>
    public class SpiritRealmPortal : BaseInteractable
    {
        protected override void Awake()
        {
            base.Awake();
            interactionPrompt = "Enter Spirit Realm";
            canInteractMultipleTimes = true;
            priority = 10;
        }

        public override string GetInteractionPrompt()
        {
            return "Enter Spirit Realm";
        }

        protected override void OnInteract(GameObject interactor)
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.StartRun();
            }
            else
            {
                Debug.LogWarning("SpiritRealmPortal: No GameManager found! Cannot start run.");
            }
        }
    }
}
