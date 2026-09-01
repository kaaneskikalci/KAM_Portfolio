using System;
using UnityEngine;
using KAM.Interaction;
using KAM.Progression;
using KAM.SaveSystem;

namespace KAM.Inventory
{
    /// <summary>
    /// World object that can be picked up to add items to inventory.
    /// Implements IInteractable for integration with the Interaction System.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    [RequireComponent(typeof(SaveableId))]
    public class ItemPickup : MonoBehaviour, IInteractable, ISaveable
    {
        [Header("Item")]
        [SerializeField] private ItemData item;
        [SerializeField] private int quantity = 1;

        [Header("Pickup Behavior")]
        [SerializeField] private bool autoPickup = false;
        [SerializeField] private float autoPickupRadius = 1.5f;
        [SerializeField] private bool destroyOnPickup = true;

        [Header("Visual")]
        [SerializeField] private Transform interactionPoint;
        [SerializeField] private bool bobUpAndDown = true;
        [SerializeField] private float bobSpeed = 2f;
        [SerializeField] private float bobHeight = 0.2f;
        [SerializeField] private bool rotate = true;
        [SerializeField] private float rotateSpeed = 45f;
        [SerializeField] private GameObject glowEffect;
        [SerializeField] private bool useMaterialEmissionFallback = false;
        [SerializeField] private Color fallbackEmissionColor = Color.white;
        [SerializeField, Min(0f)] private float fallbackEmissionIntensity = 0.35f;

        [Header("Pickup Audio")]
        [Tooltip("Optional pickup sound for this world pickup. If empty, the ItemData Pickup Sound is used.")]
        [SerializeField] private AudioClip pickupSoundOverride;
        [SerializeField] private AudioSource audioSource;

        // Events
        public static event Action<ItemPickup> OnItemPickedUp;

        private Vector3 _startPosition;
        private bool _isPickedUp = false;
        private EmissionState[] _originalEmissionStates;

        public ItemData Item => item;
        public int Quantity => quantity;
        public int InteractionPriority => 1; // Higher than default
        public string SaveableType => "ItemPickup";

        private void Start()
        {
            _startPosition = transform.position;
            CacheOriginalEmissionStates();

            if (interactionPoint == null)
                interactionPoint = transform;

            // Ensure collider is trigger for auto-pickup
            var collider = GetComponent<Collider>();
            if (autoPickup && collider != null)
            {
                collider.isTrigger = true;
            }

            RefreshSkillGlow();
        }

        private void Update()
        {
            if (_isPickedUp) return;

            // Bob animation
            if (bobUpAndDown)
            {
                float newY = _startPosition.y + Mathf.Sin(Time.time * bobSpeed) * bobHeight;
                transform.position = new Vector3(transform.position.x, newY, transform.position.z);
            }

            // Rotation
            if (rotate)
            {
                transform.Rotate(Vector3.up, rotateSpeed * Time.deltaTime);
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!autoPickup || _isPickedUp) return;

            if (other.CompareTag("Player"))
            {
                TryPickup(other.gameObject);
            }
        }

        #region IInteractable Implementation

        public string GetInteractionPrompt()
        {
            if (item == null) return "Pick up";

            if (quantity > 1)
                return $"Pick up {item.ItemName} x{quantity}";

            return $"Pick up {item.ItemName}";
        }

        public bool CanInteract()
        {
            return !_isPickedUp && item != null;
        }

        public void Interact(GameObject interactor)
        {
            TryPickup(interactor);
        }

        public Transform GetInteractionPoint()
        {
            return interactionPoint;
        }

        #endregion

        /// <summary>
        /// Attempt to pick up this item.
        /// </summary>
        public bool TryPickup(GameObject picker)
        {
            if (_isPickedUp || item == null) return false;

            // Get inventory manager
            var inventory = InventoryManager.Instance;
            if (inventory == null)
            {
                Debug.LogError("[ItemPickup] InventoryManager not found!");
                return false;
            }

            // Check if it's spirit fragments (special currency)
            if (item.Type == ItemType.KeyItem && item.ItemId.Contains("spirit_fragment"))
            {
                inventory.AddSpiritFragments(quantity);
                CompletePickup();
                return true;
            }

            // Try to add to inventory
            if (inventory.AddItem(item, quantity))
            {
                CompletePickup();
                return true;
            }
            else
            {
                Debug.Log($"[ItemPickup] Could not pick up {item.ItemName} - inventory full?");
                return false;
            }
        }

        private void CompletePickup()
        {
            _isPickedUp = true;
            bool isSaveablePickup = GetComponent<SaveableId>() != null;
            AudioClip pickupSound = GetPickupSound();

            // Play pickup sound
            if (pickupSound != null)
            {
                if (isSaveablePickup)
                {
                    AudioSource.PlayClipAtPoint(pickupSound, transform.position);
                }
                else if (audioSource != null)
                {
                    audioSource.PlayOneShot(pickupSound);
                }
                else
                {
                    AudioSource.PlayClipAtPoint(pickupSound, transform.position);
                }
            }

            Debug.Log($"[ItemPickup] Picked up {quantity}x {item.ItemName}");

            // Fire event
            OnItemPickedUp?.Invoke(this);

            // Destroy or disable
            if (destroyOnPickup)
            {
                if (isSaveablePickup)
                {
                    HidePickupObject();
                    gameObject.SetActive(false);
                    return;
                }

                // Delay destruction slightly so sound can play
                if (pickupSound != null)
                {
                    HidePickupObject();
                    Destroy(gameObject, pickupSound.length);
                }
                else
                {
                    Destroy(gameObject);
                }
            }
            else
            {
                gameObject.SetActive(false);
            }
        }

        private AudioClip GetPickupSound()
        {
            if (pickupSoundOverride != null)
                return pickupSoundOverride;

            return item != null ? item.PickupSound : null;
        }

        public string CaptureStateJson()
        {
            return JsonUtility.ToJson(new ItemPickupSaveState
            {
                isPickedUp = _isPickedUp
            });
        }

        public void RestoreStateJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return;

            ItemPickupSaveState state = JsonUtility.FromJson<ItemPickupSaveState>(json);
            SetPickedUpState(state.isPickedUp);
        }

        private void SetPickedUpState(bool isPickedUp)
        {
            _isPickedUp = isPickedUp;

            if (isPickedUp)
            {
                HidePickupObject();
                gameObject.SetActive(false);
            }
            else
            {
                gameObject.SetActive(true);
                ShowPickupObject();
                RefreshSkillGlow();
            }
        }

        private void HidePickupObject()
        {
            Collider pickupCollider = GetComponent<Collider>();
            if (pickupCollider != null)
            {
                pickupCollider.enabled = false;
            }

            foreach (var renderer in GetComponentsInChildren<Renderer>())
            {
                renderer.enabled = false;
            }

            if (glowEffect != null)
            {
                glowEffect.SetActive(false);
            }
        }

        private void ShowPickupObject()
        {
            Collider pickupCollider = GetComponent<Collider>();
            if (pickupCollider != null)
            {
                pickupCollider.enabled = true;
            }

            foreach (var renderer in GetComponentsInChildren<Renderer>())
            {
                renderer.enabled = true;
            }
        }

        /// <summary>
        /// Set the item data at runtime (for spawned pickups).
        /// </summary>
        public void SetItem(ItemData newItem, int newQuantity = 1)
        {
            item = newItem;
            quantity = Mathf.Max(1, newQuantity);
            _isPickedUp = false;
            RefreshSkillGlow();
        }

        public void RefreshSkillGlow()
        {
            bool enabled = SkillEffectApplier.Instance != null &&
                SkillEffectApplier.Instance.ShouldCollectiblesGlow();
            RefreshSkillGlow(enabled);
        }

        public void RefreshSkillGlow(bool enabled)
        {
            if (glowEffect != null)
            {
                glowEffect.SetActive(enabled);
                return;
            }

            if (_originalEmissionStates == null || _originalEmissionStates.Length == 0)
                CacheOriginalEmissionStates();

            if (!useMaterialEmissionFallback)
            {
                RestoreOriginalEmissionStates();
                return;
            }

            foreach (var state in _originalEmissionStates)
            {
                if (state.Material == null || !state.HasEmissionColor)
                    continue;

                if (enabled)
                {
                    state.Material.EnableKeyword("_EMISSION");
                    state.Material.SetColor("_EmissionColor", fallbackEmissionColor * fallbackEmissionIntensity);
                }
                else
                {
                    RestoreOriginalEmissionState(state);
                }
            }
        }

        private void RestoreOriginalEmissionStates()
        {
            if (_originalEmissionStates == null)
                return;

            foreach (var state in _originalEmissionStates)
            {
                RestoreOriginalEmissionState(state);
            }
        }

        private static void RestoreOriginalEmissionState(EmissionState state)
        {
            if (state.Material == null || !state.HasEmissionColor)
                return;

            state.Material.SetColor("_EmissionColor", state.OriginalEmissionColor);

            if (state.EmissionKeywordEnabled)
                state.Material.EnableKeyword("_EMISSION");
            else
                state.Material.DisableKeyword("_EMISSION");
        }

        private void CacheOriginalEmissionStates()
        {
            var renderers = GetComponentsInChildren<Renderer>(true);
            var states = new System.Collections.Generic.List<EmissionState>();

            foreach (var renderer in renderers)
            {
                if (renderer == null)
                    continue;

                Material[] materials = renderer.materials;
                foreach (var material in materials)
                {
                    if (material == null)
                        continue;

                    bool hasEmissionColor = material.HasProperty("_EmissionColor");
                    states.Add(new EmissionState
                    {
                        Material = material,
                        HasEmissionColor = hasEmissionColor,
                        OriginalEmissionColor = hasEmissionColor ? material.GetColor("_EmissionColor") : Color.black,
                        EmissionKeywordEnabled = material.IsKeywordEnabled("_EMISSION")
                    });
                }
            }

            _originalEmissionStates = states.ToArray();
        }

        private struct EmissionState
        {
            public Material Material;
            public bool HasEmissionColor;
            public Color OriginalEmissionColor;
            public bool EmissionKeywordEnabled;
        }

        /// <summary>
        /// Spawn an item pickup at a position.
        /// </summary>
        public static ItemPickup SpawnPickup(ItemData item, Vector3 position, int quantity = 1)
        {
            // Create a basic pickup object
            GameObject pickupObj = new GameObject($"Pickup_{item.ItemName}");
            pickupObj.transform.position = position;

            // Add collider
            SphereCollider collider = pickupObj.AddComponent<SphereCollider>();
            collider.isTrigger = true;
            collider.radius = 0.5f;

            // Add pickup component
            ItemPickup pickup = pickupObj.AddComponent<ItemPickup>();
            pickup.SetItem(item, quantity);

            // Create visual (simple cube for now)
            GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            visual.transform.SetParent(pickupObj.transform);
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localScale = Vector3.one * 0.3f;

            // Set color based on rarity
            var renderer = visual.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material.color = item.GetRarityColor();
            }

            // Remove the cube's collider
            Destroy(visual.GetComponent<Collider>());

            return pickup;
        }

        private void OnDrawGizmosSelected()
        {
            if (autoPickup)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(transform.position, autoPickupRadius);
            }
        }
    }

    [Serializable]
    public class ItemPickupSaveState
    {
        public bool isPickedUp;
    }
}
