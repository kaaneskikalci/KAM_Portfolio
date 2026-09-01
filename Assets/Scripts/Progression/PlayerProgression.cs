using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;
using KAM.Inventory;

namespace KAM.Progression
{
    /// <summary>
    /// Persistent runtime state for the player's skill tree progression.
    /// Tracks which skills are unlocked and which branch variant is chosen.
    /// Singleton, DontDestroyOnLoad.
    /// </summary>
    public class PlayerProgression : MonoBehaviour
    {
        private static PlayerProgression _instance;
        public static PlayerProgression Instance => _instance;

        [Header("Skill Tree")]
        [SerializeField] private SkillTreeData skillTreeData;

        [Header("Debug")]
        [SerializeField] private bool showDebugInfo = true;

        // Runtime state
        private HashSet<string> _unlockedSkillIds = new HashSet<string>();
        private Dictionary<string, int> _branchChoices = new Dictionary<string, int>(); // skillId -> 0 (A) or 1 (B)

        // Events
        public event Action<SkillNode> OnSkillUnlocked;
        public event Action<SkillNode, int> OnBranchChosen; // node, branchIndex (0=A, 1=B)

        // Properties
        public SkillTreeData SkillTreeData => skillTreeData;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }
        }

        #region Query Methods

        /// <summary>
        /// Check if a skill is unlocked by ID.
        /// </summary>
        public bool IsSkillUnlocked(string skillId)
        {
            return _unlockedSkillIds.Contains(skillId);
        }

        /// <summary>
        /// Check if a skill is unlocked by node reference.
        /// </summary>
        public bool IsSkillUnlocked(SkillNode node)
        {
            return node != null && _unlockedSkillIds.Contains(node.SkillId);
        }

        /// <summary>
        /// Check if a skill can be unlocked (prerequisites met, can afford).
        /// </summary>
        public bool CanUnlockSkill(SkillNode node)
        {
            if (node == null) return false;
            if (IsSkillUnlocked(node)) return false;

            // Check prerequisites
            foreach (var prereq in node.Prerequisites)
            {
                if (prereq != null && !IsSkillUnlocked(prereq))
                {
                    return false;
                }
            }

            // Check cost
            if (InventoryManager.Instance == null) return false;
            return InventoryManager.Instance.CanAfford(node.SpiritFragmentCost);
        }

        /// <summary>
        /// Check if all prerequisites are met (regardless of cost).
        /// </summary>
        public bool ArePrerequisitesMet(SkillNode node)
        {
            if (node == null) return false;

            foreach (var prereq in node.Prerequisites)
            {
                if (prereq != null && !IsSkillUnlocked(prereq))
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// Check if a branch has been chosen for a skill.
        /// </summary>
        public bool HasChosenBranch(string skillId)
        {
            return _branchChoices.ContainsKey(skillId);
        }

        /// <summary>
        /// Get the chosen branch index. Returns -1 if no branch chosen.
        /// </summary>
        public int GetChosenBranch(string skillId)
        {
            return _branchChoices.TryGetValue(skillId, out int choice) ? choice : -1;
        }

        /// <summary>
        /// Get the active effect type for a skill, considering branch selection.
        /// </summary>
        public SkillEffectType GetActiveEffectForSkill(SkillNode node)
        {
            if (node == null || !IsSkillUnlocked(node)) return SkillEffectType.None;

            if (node.HasBranches && HasChosenBranch(node.SkillId))
            {
                int branch = GetChosenBranch(node.SkillId);
                return branch == 0 ? node.BranchA.effectType : node.BranchB.effectType;
            }

            return node.EffectType;
        }

        /// <summary>
        /// Get all unlocked skills.
        /// </summary>
        public List<SkillNode> GetUnlockedSkills()
        {
            if (skillTreeData == null) return new List<SkillNode>();

            return skillTreeData.AllSkills
                .Where(s => s != null && _unlockedSkillIds.Contains(s.SkillId))
                .ToList();
        }

        /// <summary>
        /// Get the total number of unlocked skills.
        /// </summary>
        public int UnlockedSkillCount => _unlockedSkillIds.Count;

        #endregion

        #region Actions

        /// <summary>
        /// Try to unlock a skill. Spends spirit fragments via InventoryManager.
        /// Returns true if successful.
        /// </summary>
        public bool TryUnlockSkill(SkillNode node)
        {
            if (!CanUnlockSkill(node))
            {
                if (showDebugInfo)
                {
                    Debug.LogWarning($"[PlayerProgression] Cannot unlock {node?.SkillName}: " +
                        $"already unlocked={IsSkillUnlocked(node)}, " +
                        $"prereqs met={ArePrerequisitesMet(node)}, " +
                        $"can afford={InventoryManager.Instance?.CanAfford(node?.SpiritFragmentCost ?? 0)}");
                }
                return false;
            }

            // Spend fragments
            if (!InventoryManager.Instance.SpendSpiritFragments(node.SpiritFragmentCost))
            {
                return false;
            }

            _unlockedSkillIds.Add(node.SkillId);
            OnSkillUnlocked?.Invoke(node);

            // Apply skill effects immediately
            if (SkillEffectApplier.Instance != null)
            {
                SkillEffectApplier.Instance.ApplySkill(node);
            }

            if (showDebugInfo)
            {
                Debug.Log($"[PlayerProgression] Unlocked skill: {node.SkillName} (cost: {node.SpiritFragmentCost})");
            }

            return true;
        }

        /// <summary>
        /// Choose a branch for a branching skill. 0 = Branch A, 1 = Branch B.
        /// Skill must be unlocked and have branches.
        /// </summary>
        public bool TryChooseBranch(SkillNode node, int branchIndex)
        {
            if (node == null) return false;
            if (!IsSkillUnlocked(node)) return false;
            if (!node.HasBranches) return false;
            if (branchIndex < 0 || branchIndex > 1) return false;

            _branchChoices[node.SkillId] = branchIndex;
            OnBranchChosen?.Invoke(node, branchIndex);

            // Re-apply skill effects with new branch
            if (SkillEffectApplier.Instance != null)
            {
                SkillEffectApplier.Instance.ApplySkill(node);
            }

            if (showDebugInfo)
            {
                string branchName = branchIndex == 0 ? node.BranchA.branchName : node.BranchB.branchName;
                Debug.Log($"[PlayerProgression] Chose branch '{branchName}' for {node.SkillName}");
            }

            return true;
        }

        #endregion

        #region Serialization (for future Save/Load)

        [System.Serializable]
        public struct ProgressionSaveData
        {
            public string[] unlockedSkillIds;
            public string[] branchSkillIds;
            public int[] branchChoices;
        }

        public ProgressionSaveData GetSaveData()
        {
            var data = new ProgressionSaveData
            {
                unlockedSkillIds = _unlockedSkillIds.ToArray(),
                branchSkillIds = _branchChoices.Keys.ToArray(),
                branchChoices = _branchChoices.Values.ToArray()
            };
            return data;
        }

        public void LoadSaveData(ProgressionSaveData data)
        {
            _unlockedSkillIds.Clear();
            _branchChoices.Clear();

            if (data.unlockedSkillIds != null)
            {
                foreach (var id in data.unlockedSkillIds)
                {
                    _unlockedSkillIds.Add(id);
                }
            }

            if (data.branchSkillIds != null && data.branchChoices != null)
            {
                for (int i = 0; i < data.branchSkillIds.Length && i < data.branchChoices.Length; i++)
                {
                    _branchChoices[data.branchSkillIds[i]] = data.branchChoices[i];
                }
            }

            // Apply all effects after loading
            if (SkillEffectApplier.Instance != null)
            {
                SkillEffectApplier.Instance.ApplyAllPassiveEffects();
            }

            if (showDebugInfo)
            {
                Debug.Log($"[PlayerProgression] Loaded save data: {_unlockedSkillIds.Count} skills unlocked");
            }
        }

        #endregion
    }
}
