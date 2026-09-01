using System;
using KAM.SaveSystem;
using UnityEngine;

namespace KAM.Interaction
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SaveableId))]
    public class PersistentShortcut : BaseInteractable, ISaveable
    {
        [Header("Shortcut State")]
        [SerializeField] private bool startsOpen;
        [SerializeField] private string openPrompt = "Open Shortcut";
        [SerializeField] private string openedPrompt = "Shortcut Open";

        [Header("Open Behaviour")]
        [SerializeField] private Animator animator;
        [SerializeField] private string openTrigger = "Open";
        [SerializeField] private GameObject[] objectsToDisableWhenOpen;
        [SerializeField] private Collider[] collidersToDisableWhenOpen;

        private bool _isOpen;

        public string SaveableType => "PersistentShortcut";
        public bool IsOpen => _isOpen;

        protected override void Awake()
        {
            base.Awake();
            canInteractMultipleTimes = false;
            SetOpenState(startsOpen, playAnimation: false);
        }

        public override string GetInteractionPrompt()
        {
            return _isOpen ? openedPrompt : openPrompt;
        }

        public override bool CanInteract()
        {
            return base.CanInteract() && !_isOpen;
        }

        protected override void OnInteract(GameObject interactor)
        {
            SetOpenState(true, playAnimation: true);
        }

        public string CaptureStateJson()
        {
            return JsonUtility.ToJson(new PersistentShortcutSaveState
            {
                isOpen = _isOpen
            });
        }

        public void RestoreStateJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return;

            PersistentShortcutSaveState state = JsonUtility.FromJson<PersistentShortcutSaveState>(json);
            SetOpenState(state.isOpen, playAnimation: false);
        }

        private void SetOpenState(bool isOpen, bool playAnimation)
        {
            _isOpen = isOpen;
            _hasBeenInteracted = isOpen;

            if (isOpen && playAnimation && animator != null && !string.IsNullOrWhiteSpace(openTrigger))
                animator.SetTrigger(openTrigger);

            foreach (GameObject target in objectsToDisableWhenOpen)
            {
                if (target != null)
                    target.SetActive(!isOpen);
            }

            foreach (Collider target in collidersToDisableWhenOpen)
            {
                if (target != null)
                    target.enabled = !isOpen;
            }
        }
    }

    [Serializable]
    public class PersistentShortcutSaveState
    {
        public bool isOpen;
    }
}
