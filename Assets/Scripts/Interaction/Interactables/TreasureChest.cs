using System;
using System.Collections.Generic;
using KAM.Inventory;
using KAM.SaveSystem;
using UnityEngine;

namespace KAM.Interaction
{
    /// <summary>
    /// Interactable treasure chest that can contain items/rewards.
    /// Supports open/close animations and one-time opening.
    /// </summary>
    [RequireComponent(typeof(SaveableId))]
    public class TreasureChest : BaseInteractable, ISaveable
    {
        [Header("Chest Settings")]
        [SerializeField] private string chestId; // Unique ID for save/load
        [SerializeField] private ChestRarity rarity = ChestRarity.Common;

        [Header("Contents")]
        [SerializeField] private int spiritFragments = 10;
        [SerializeField] private bool randomizeSpiritFragments = true;
        [SerializeField, Min(0)] private int minSpiritFragments = 300;
        [SerializeField, Min(0)] private int maxSpiritFragments = 500;
        [SerializeField, Range(0f, 1f)] private float spiritFragmentDropChance = 1f;
        [SerializeField] private List<ChestItem> items = new List<ChestItem>();

        [Header("Animation")]
        [SerializeField] private Animator animator;
        [SerializeField] private string openAnimationTrigger = "Open";
        [SerializeField] private Transform lidTransform;
        [SerializeField] private float lidOpenAngle = -110f;
        [SerializeField] private float openAnimationDuration = 0.5f;

        [Header("Effects")]
        [SerializeField] private ParticleSystem openParticles;
        [SerializeField] private GameObject glowEffect;
        [SerializeField] private Light chestLight;

        [Header("Visuals by State")]
        [SerializeField] private Material closedMaterial;
        [SerializeField] private Material openedMaterial;

        // Events
        public static event Action<TreasureChest> OnChestOpened;
        public static event Action<TreasureChest, List<ChestItem>, int> OnChestLootReceived;

        private bool _isOpen = false;
        private MeshRenderer _meshRenderer;

        public bool IsOpen => _isOpen;
        public string ChestId => chestId;
        public ChestRarity Rarity => rarity;
        public int SpiritFragments => spiritFragments;
        public List<ChestItem> Items => items;
        public string SaveableType => "TreasureChest";

        protected override void Awake()
        {
            base.Awake();

            _meshRenderer = GetComponent<MeshRenderer>();

            // Generate ID if not set
            if (string.IsNullOrEmpty(chestId))
            {
                chestId = $"chest_{gameObject.name}_{transform.position.GetHashCode()}";
            }

            // Set initial prompt and make it one-time only
            interactionPrompt = "Open";
            canInteractMultipleTimes = false;

            UpdateVisualState();
        }

        public override string GetInteractionPrompt()
        {
            return _isOpen ? "Empty" : "Open Chest";
        }

        public override bool CanInteract()
        {
            return base.CanInteract() && !_isOpen;
        }

        protected override void OnInteract(GameObject interactor)
        {
            if (_isOpen) return;

            Debug.Log($"[TreasureChest] Opening chest: {chestId} (Rarity: {rarity})");

            _isOpen = true;

            // Play animation
            if (animator != null)
            {
                animator.SetTrigger(openAnimationTrigger);
            }
            else if (lidTransform != null)
            {
                // Simple rotation animation
                StartCoroutine(AnimateLidOpen());
            }

            // Play effects
            if (openParticles != null)
            {
                openParticles.Play();
            }

            // Disable glow
            if (glowEffect != null)
            {
                glowEffect.SetActive(false);
            }

            // Fire events
            int awardedSpiritFragments = RollSpiritFragments();
            AwardSpiritFragments(awardedSpiritFragments);

            OnChestOpened?.Invoke(this);
            OnChestLootReceived?.Invoke(this, items, awardedSpiritFragments);

            // Log loot for now (UI will handle display)
            Debug.Log($"[TreasureChest] Received {awardedSpiritFragments} Spirit Fragments");
            foreach (var item in items)
            {
                Debug.Log($"[TreasureChest] Received: {item.itemName} x{item.quantity}");
            }

            UpdateVisualState();
        }

        private int RollSpiritFragments()
        {
            if (spiritFragmentDropChance <= 0f || UnityEngine.Random.value > spiritFragmentDropChance)
                return 0;

            if (!randomizeSpiritFragments)
                return Mathf.Max(0, spiritFragments);

            int min = Mathf.Max(0, Mathf.Min(minSpiritFragments, maxSpiritFragments));
            int max = Mathf.Max(min, Mathf.Max(minSpiritFragments, maxSpiritFragments));
            return UnityEngine.Random.Range(min, max + 1);
        }

        private void AwardSpiritFragments(int amount)
        {
            if (amount <= 0)
                return;

            if (InventoryManager.Instance == null)
            {
                Debug.LogWarning($"[TreasureChest] Cannot award {amount} Spirit Fragments because InventoryManager is missing.");
                return;
            }

            InventoryManager.Instance.AddSpiritFragments(amount);
        }

        private System.Collections.IEnumerator AnimateLidOpen()
        {
            if (lidTransform == null) yield break;

            Quaternion startRotation = lidTransform.localRotation;
            Quaternion endRotation = Quaternion.Euler(lidOpenAngle, 0, 0);

            float elapsed = 0f;

            while (elapsed < openAnimationDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / openAnimationDuration;
                // Ease out
                t = 1f - Mathf.Pow(1f - t, 3f);

                lidTransform.localRotation = Quaternion.Lerp(startRotation, endRotation, t);
                yield return null;
            }

            lidTransform.localRotation = endRotation;
        }

        private void UpdateVisualState()
        {
            // Change material
            if (_meshRenderer != null)
            {
                if (_isOpen && openedMaterial != null)
                {
                    _meshRenderer.material = openedMaterial;
                }
                else if (!_isOpen && closedMaterial != null)
                {
                    _meshRenderer.material = closedMaterial;
                }
            }

            // Toggle light
            if (chestLight != null)
            {
                chestLight.enabled = !_isOpen;
            }

            // Toggle glow
            if (glowEffect != null)
            {
                glowEffect.SetActive(!_isOpen);
            }
        }

        /// <summary>
        /// Set the chest state (used when loading save data).
        /// </summary>
        public void SetOpenState(bool isOpen)
        {
            _isOpen = isOpen;
            _hasBeenInteracted = isOpen;

            if (isOpen && lidTransform != null)
            {
                lidTransform.localRotation = Quaternion.Euler(lidOpenAngle, 0, 0);
            }

            UpdateVisualState();
        }

        /// <summary>
        /// Set chest contents at runtime.
        /// </summary>
        public void SetContents(int fragments, List<ChestItem> newItems)
        {
            spiritFragments = fragments;
            items = newItems ?? new List<ChestItem>();
        }

        public string CaptureStateJson()
        {
            return JsonUtility.ToJson(new TreasureChestSaveState
            {
                isOpen = _isOpen
            });
        }

        public void RestoreStateJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return;

            TreasureChestSaveState state = JsonUtility.FromJson<TreasureChestSaveState>(json);
            SetOpenState(state.isOpen);
        }
    }

    [Serializable]
    public class TreasureChestSaveState
    {
        public bool isOpen;
    }

    /// <summary>
    /// Rarity levels for chests (affects loot and visuals).
    /// </summary>
    public enum ChestRarity
    {
        Common,
        Uncommon,
        Rare,
        Epic,
        Legendary
    }

    /// <summary>
    /// Represents an item contained in a chest.
    /// </summary>
    [Serializable]
    public class ChestItem
    {
        public string itemName;
        public string itemId;
        public int quantity = 1;
        public Sprite icon;
        // Add ScriptableObject reference when item system is implemented
        // public ItemData itemData;
    }
}
