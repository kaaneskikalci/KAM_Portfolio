using System;
using UnityEngine;

namespace KAM.Inventory
{
    /// <summary>
    /// Component that drops loot from a LootTable.
    /// Attach to enemies, chests, or destructible objects.
    /// </summary>
    public class LootDropper : MonoBehaviour
    {
        [Header("Loot Table")]
        [SerializeField] private LootTable lootTable;

        [Header("Drop Settings")]
        [Tooltip("Where to spawn loot (if null, uses this transform)")]
        [SerializeField] private Transform dropPoint;

        [Tooltip("Random spread radius for dropped items")]
        [SerializeField] private float dropRadius = 1f;

        [Tooltip("Upward force applied to drops")]
        [SerializeField] private float dropForce = 3f;

        [Tooltip("Should items spawn or go directly to inventory?")]
        [SerializeField] private bool spawnPickups = true;

        [Tooltip("Auto-drop on Start (for testing)")]
        [SerializeField] private bool dropOnStart = false;

        [Header("Events")]
        [Tooltip("Drop loot when this object is destroyed?")]
        [SerializeField] private bool dropOnDestroy = false;

        // Events
        public static event Action<LootDropper, LootResult> OnLootDropped;

        private bool _hasDropped = false;

        private void Start()
        {
            if (dropPoint == null)
                dropPoint = transform;

            if (dropOnStart)
            {
                DropLoot();
            }
        }

        private void OnDestroy()
        {
            if (dropOnDestroy && !_hasDropped)
            {
                DropLoot();
            }
        }

        /// <summary>
        /// Drop loot from the loot table.
        /// </summary>
        public void DropLoot()
        {
            if (lootTable == null)
            {
                Debug.LogWarning($"[LootDropper] No loot table assigned on {gameObject.name}");
                return;
            }

            if (_hasDropped)
            {
                Debug.Log($"[LootDropper] {gameObject.name} has already dropped loot");
                return;
            }

            _hasDropped = true;

            LootResult result = lootTable.RollLoot();

            if (!result.HasLoot)
            {
                Debug.Log($"[LootDropper] {gameObject.name} rolled no loot");
                return;
            }

            Debug.Log($"[LootDropper] {gameObject.name} dropping loot:\n{result}");

            if (spawnPickups)
            {
                SpawnPickups(result);
            }
            else
            {
                AddToInventory(result);
            }

            OnLootDropped?.Invoke(this, result);
        }

        /// <summary>
        /// Drop specific items (not from loot table).
        /// </summary>
        public void DropItem(ItemData item, int quantity = 1)
        {
            if (item == null || quantity <= 0) return;

            if (spawnPickups)
            {
                Vector3 pos = GetRandomDropPosition();
                ItemPickup pickup = ItemPickup.SpawnPickup(item, pos, quantity);
                ApplyDropForce(pickup.gameObject);
            }
            else
            {
                var inventory = InventoryManager.Instance;
                if (inventory != null)
                {
                    inventory.AddItem(item, quantity);
                }
            }
        }

        private void SpawnPickups(LootResult result)
        {
            // Spawn spirit fragments as a single pickup
            if (result.spiritFragments > 0)
            {
                SpawnSpiritFragments(result.spiritFragments);
            }

            // Spawn item pickups
            foreach (var kvp in result.items)
            {
                ItemData item = kvp.Key;
                int quantity = kvp.Value;

                // Split into multiple pickups if quantity is high
                int maxPerPickup = item.MaxStackSize;
                while (quantity > 0)
                {
                    int dropQty = Mathf.Min(quantity, maxPerPickup);
                    Vector3 pos = GetRandomDropPosition();

                    ItemPickup pickup = ItemPickup.SpawnPickup(item, pos, dropQty);
                    ApplyDropForce(pickup.gameObject);

                    quantity -= dropQty;
                }
            }
        }

        private void SpawnSpiritFragments(int amount)
        {
            // For spirit fragments, we add directly to inventory
            // since they're a currency, not a regular item
            var inventory = InventoryManager.Instance;
            if (inventory != null)
            {
                inventory.AddSpiritFragments(amount);
            }

            // Could also spawn a visual effect here
            Debug.Log($"[LootDropper] Gained {amount} Spirit Fragments!");
        }

        private void AddToInventory(LootResult result)
        {
            var inventory = InventoryManager.Instance;
            if (inventory == null)
            {
                Debug.LogError("[LootDropper] InventoryManager not found!");
                return;
            }

            // Add spirit fragments
            if (result.spiritFragments > 0)
            {
                inventory.AddSpiritFragments(result.spiritFragments);
            }

            // Add items
            foreach (var kvp in result.items)
            {
                inventory.AddItem(kvp.Key, kvp.Value);
            }
        }

        private Vector3 GetRandomDropPosition()
        {
            if (dropRadius <= 0)
                return dropPoint.position;

            Vector2 randomCircle = UnityEngine.Random.insideUnitCircle * dropRadius;
            return dropPoint.position + new Vector3(randomCircle.x, 0.5f, randomCircle.y);
        }

        private void ApplyDropForce(GameObject dropObject)
        {
            if (dropForce <= 0) return;

            Rigidbody rb = dropObject.GetComponent<Rigidbody>();
            if (rb == null)
            {
                rb = dropObject.AddComponent<Rigidbody>();
                rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            }

            // Random upward and outward force
            Vector3 randomDir = new Vector3(
                UnityEngine.Random.Range(-1f, 1f),
                1f,
                UnityEngine.Random.Range(-1f, 1f)
            ).normalized;

            rb.AddForce(randomDir * dropForce, ForceMode.Impulse);

            // Add some spin
            rb.AddTorque(UnityEngine.Random.insideUnitSphere * 5f, ForceMode.Impulse);

            // Remove rigidbody after landing
            Destroy(rb, 2f);
        }

        /// <summary>
        /// Reset so this dropper can drop again.
        /// </summary>
        public void ResetDropper()
        {
            _hasDropped = false;
        }

        /// <summary>
        /// Set the loot table at runtime.
        /// </summary>
        public void SetLootTable(LootTable table)
        {
            lootTable = table;
            _hasDropped = false;
        }

        private void OnDrawGizmosSelected()
        {
            Transform point = dropPoint != null ? dropPoint : transform;

            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(point.position, dropRadius);

            // Draw drop arc
            Gizmos.color = Color.green;
            Gizmos.DrawLine(point.position, point.position + Vector3.up * dropForce * 0.5f);
        }
    }
}
