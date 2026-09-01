using System;
using UnityEngine;

namespace KAM.Interaction
{
    /// <summary>
    /// Interactable object that displays lore/story text when activated.
    /// Can be attached to tablets, scrolls, books, wall carvings, etc.
    /// </summary>
    public class LoreObject : BaseInteractable
    {
        [Header("Lore Content")]
        [SerializeField] private string loreTitle = "Ancient Text";
        [SerializeField, TextArea(3, 10)] private string[] lorePages;
        [SerializeField] private string loreId; // Unique ID for save/load

        [Header("Visual Indicators")]
        [SerializeField] private GameObject unreadIndicator; // Glow when not yet read
        [SerializeField] private Material readMaterial;      // Optional: change material after reading
        [SerializeField] private Material unreadMaterial;

        [Header("Settings")]
        [SerializeField] private bool markAsReadOnInteract = true;

        // Events
        public static event Action<LoreObject> OnLoreOpened;
        public static event Action<LoreObject> OnLoreClosed;
        public static event Action<string> OnLoreRead; // Fires with loreId when first read

        private bool _hasBeenRead = false;
        private MeshRenderer _meshRenderer;

        public string LoreTitle => loreTitle;
        public string[] LorePages => lorePages;
        public string LoreId => loreId;
        public bool HasBeenRead => _hasBeenRead;

        protected override void Awake()
        {
            base.Awake();

            _meshRenderer = GetComponent<MeshRenderer>();

            // Generate ID if not set
            if (string.IsNullOrEmpty(loreId))
            {
                loreId = $"lore_{gameObject.name}_{transform.position.GetHashCode()}";
            }

            // Set initial prompt
            interactionPrompt = "Read";
            canInteractMultipleTimes = true; // Can re-read lore

            UpdateVisualState();
        }

        public override string GetInteractionPrompt()
        {
            return _hasBeenRead ? "Read Again" : "Read";
        }

        protected override void OnInteract(GameObject interactor)
        {
            Debug.Log($"[LoreObject] Opening lore: {loreTitle}");

            // Mark as read
            if (markAsReadOnInteract && !_hasBeenRead)
            {
                MarkAsRead();
            }

            // Fire event for UI to handle
            OnLoreOpened?.Invoke(this);

            // If no UI is listening, just log the content
            if (OnLoreOpened == null)
            {
                Debug.Log($"=== {loreTitle} ===");
                for (int i = 0; i < lorePages.Length; i++)
                {
                    Debug.Log($"Page {i + 1}: {lorePages[i]}");
                }
            }
        }

        /// <summary>
        /// Called when the lore panel is closed.
        /// </summary>
        public void CloseLore()
        {
            OnLoreClosed?.Invoke(this);
        }

        /// <summary>
        /// Mark this lore as read (updates visuals and fires event).
        /// </summary>
        public void MarkAsRead()
        {
            if (_hasBeenRead) return;

            _hasBeenRead = true;
            UpdateVisualState();

            OnLoreRead?.Invoke(loreId);
            Debug.Log($"[LoreObject] Lore read for first time: {loreTitle} (ID: {loreId})");
        }

        /// <summary>
        /// Set read state (used when loading save data).
        /// </summary>
        public void SetReadState(bool hasBeenRead)
        {
            _hasBeenRead = hasBeenRead;
            UpdateVisualState();
        }

        private void UpdateVisualState()
        {
            // Toggle unread indicator
            if (unreadIndicator != null)
            {
                unreadIndicator.SetActive(!_hasBeenRead);
            }

            // Change material if configured
            if (_meshRenderer != null)
            {
                if (_hasBeenRead && readMaterial != null)
                {
                    _meshRenderer.material = readMaterial;
                }
                else if (!_hasBeenRead && unreadMaterial != null)
                {
                    _meshRenderer.material = unreadMaterial;
                }
            }
        }

        /// <summary>
        /// Get all lore content as a single string (for simple display).
        /// </summary>
        public string GetFullLoreText()
        {
            return string.Join("\n\n", lorePages);
        }

        /// <summary>
        /// Get a specific page of lore (0-indexed).
        /// </summary>
        public string GetPage(int index)
        {
            if (index < 0 || index >= lorePages.Length)
                return "";
            return lorePages[index];
        }

        public int PageCount => lorePages?.Length ?? 0;
    }
}
