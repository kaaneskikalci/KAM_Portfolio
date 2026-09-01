using System;
using System.Collections.Generic;
using UnityEngine;

namespace KAM.Inventory
{
    /// <summary>
    /// Defines a table of possible loot drops with weights and quantities.
    /// Attach to enemies, chests, or any source of random items.
    /// </summary>
    [CreateAssetMenu(fileName = "New Loot Table", menuName = "KAM/Inventory/Loot Table")]
    public class LootTable : ScriptableObject
    {
        [Header("Loot Entries")]
        [SerializeField] private List<LootEntry> entries = new List<LootEntry>();

        [Header("Spirit Fragments")]
        [SerializeField] private int minSpiritFragments = 0;
        [SerializeField] private int maxSpiritFragments = 10;
        [SerializeField, Range(0, 100)] private float spiritFragmentChance = 75f;

        [Header("Roll Settings")]
        [Tooltip("How many items to roll from this table")]
        [SerializeField] private int rollCount = 1;

        [Tooltip("Can the same item drop multiple times in one roll?")]
        [SerializeField] private bool allowDuplicates = true;

        [Header("Guaranteed Drops")]
        [SerializeField] private List<GuaranteedDrop> guaranteedDrops = new List<GuaranteedDrop>();

        /// <summary>
        /// Roll loot from this table.
        /// </summary>
        public LootResult RollLoot()
        {
            LootResult result = new LootResult();

            // Add guaranteed drops
            foreach (var guaranteed in guaranteedDrops)
            {
                if (guaranteed.item != null)
                {
                    int qty = UnityEngine.Random.Range(guaranteed.minQuantity, guaranteed.maxQuantity + 1);
                    result.AddItem(guaranteed.item, qty);
                }
            }

            // Roll spirit fragments
            if (spiritFragmentChance > 0 && UnityEngine.Random.Range(0f, 100f) <= spiritFragmentChance)
            {
                result.spiritFragments = UnityEngine.Random.Range(minSpiritFragments, maxSpiritFragments + 1);
            }

            // Roll random items
            if (entries.Count > 0)
            {
                float totalWeight = 0f;
                foreach (var entry in entries)
                {
                    totalWeight += entry.weight;
                }

                List<LootEntry> usedEntries = new List<LootEntry>();

                for (int i = 0; i < rollCount; i++)
                {
                    LootEntry selected = RollEntry(totalWeight, allowDuplicates ? null : usedEntries);

                    if (selected != null && selected.item != null)
                    {
                        int qty = UnityEngine.Random.Range(selected.minQuantity, selected.maxQuantity + 1);
                        result.AddItem(selected.item, qty);

                        if (!allowDuplicates)
                        {
                            usedEntries.Add(selected);
                            totalWeight -= selected.weight;
                        }
                    }
                }
            }

            return result;
        }

        private LootEntry RollEntry(float totalWeight, List<LootEntry> excludeList)
        {
            if (totalWeight <= 0) return null;

            float roll = UnityEngine.Random.Range(0f, totalWeight);
            float cumulative = 0f;

            foreach (var entry in entries)
            {
                if (excludeList != null && excludeList.Contains(entry))
                    continue;

                cumulative += entry.weight;

                if (roll <= cumulative)
                {
                    // Check drop chance
                    if (UnityEngine.Random.Range(0f, 100f) <= entry.dropChance)
                    {
                        return entry;
                    }
                    else
                    {
                        return null; // Failed drop chance
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Preview what items could drop (for debugging/UI).
        /// </summary>
        public List<ItemData> GetPossibleItems()
        {
            List<ItemData> items = new List<ItemData>();

            foreach (var entry in entries)
            {
                if (entry.item != null && !items.Contains(entry.item))
                {
                    items.Add(entry.item);
                }
            }

            foreach (var guaranteed in guaranteedDrops)
            {
                if (guaranteed.item != null && !items.Contains(guaranteed.item))
                {
                    items.Add(guaranteed.item);
                }
            }

            return items;
        }
    }

    /// <summary>
    /// A single entry in a loot table.
    /// </summary>
    [Serializable]
    public class LootEntry
    {
        public ItemData item;

        [Tooltip("Weight for random selection (higher = more likely)")]
        [Range(0.1f, 100f)]
        public float weight = 10f;

        [Tooltip("Chance to actually drop if selected (0-100%)")]
        [Range(0, 100)]
        public float dropChance = 100f;

        [Tooltip("Minimum quantity to drop")]
        [Min(1)]
        public int minQuantity = 1;

        [Tooltip("Maximum quantity to drop")]
        [Min(1)]
        public int maxQuantity = 1;
    }

    /// <summary>
    /// An item that always drops.
    /// </summary>
    [Serializable]
    public class GuaranteedDrop
    {
        public ItemData item;
        public int minQuantity = 1;
        public int maxQuantity = 1;
    }

    /// <summary>
    /// Result of rolling a loot table.
    /// </summary>
    public class LootResult
    {
        public Dictionary<ItemData, int> items = new Dictionary<ItemData, int>();
        public int spiritFragments = 0;

        public void AddItem(ItemData item, int quantity)
        {
            if (item == null || quantity <= 0) return;

            if (items.ContainsKey(item))
            {
                items[item] += quantity;
            }
            else
            {
                items[item] = quantity;
            }
        }

        public bool HasLoot => items.Count > 0 || spiritFragments > 0;

        public override string ToString()
        {
            string result = "";

            if (spiritFragments > 0)
            {
                result += $"Spirit Fragments: {spiritFragments}\n";
            }

            foreach (var kvp in items)
            {
                result += $"{kvp.Key.ItemName} x{kvp.Value}\n";
            }

            return result.TrimEnd('\n');
        }
    }
}
