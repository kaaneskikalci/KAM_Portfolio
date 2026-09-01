using UnityEngine;
using System.Collections.Generic;
using KAM.Progression;
using KAM.Combat.Actions;

namespace KAM.Combat
{
    /// <summary>
    /// Bridges the skill tree progression system to combat.
    /// Checks which active combat skills are unlocked and provides
    /// runtime CombatAction instances for the action menu.
    /// Singleton, DontDestroyOnLoad.
    /// </summary>
    public class SkillCombatBridge : MonoBehaviour
    {
        private static SkillCombatBridge _instance;
        public static SkillCombatBridge Instance => _instance;

        [Header("Debug")]
        [SerializeField] private bool showDebugInfo = true;

        // Cached runtime action instances (created once, reused)
        private AreaDamageAction _areaDamageAction;
        private EchoOfAncestorsAction _echoAction;

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

        /// <summary>
        /// Get all unlocked active combat skills as CombatAction instances.
        /// Called by CombatUIController when populating the Skills submenu.
        /// </summary>
        public List<CombatAction> GetAvailableCombatSkills()
        {
            var skills = new List<CombatAction>();

            if (PlayerProgression.Instance == null)
            {
                if (showDebugInfo)
                    Debug.Log("[SkillCombatBridge] No PlayerProgression instance");
                return skills;
            }

            var treeData = PlayerProgression.Instance.SkillTreeData;
            if (treeData == null)
            {
                if (showDebugInfo)
                    Debug.Log("[SkillCombatBridge] No SkillTreeData");
                return skills;
            }

            // Skill 3: Area Damage
            if (IsCombatSkillUnlocked(treeData, SkillEffectApplier.AreaDamageId, SkillEffectType.AreaDamage))
            {
                if (_areaDamageAction == null)
                {
                    _areaDamageAction = AreaDamageAction.CreateDefault();
                }
                // Reset cooldown state for fresh combat
                skills.Add(_areaDamageAction);

                if (showDebugInfo)
                    Debug.Log("[SkillCombatBridge] Area Damage skill available");
            }

            // Skill 8: Echo of Ancestors
            if (IsCombatSkillUnlocked(treeData, SkillEffectApplier.EchoAncestorsId, SkillEffectType.EchoOfAncestors))
            {
                if (_echoAction == null)
                {
                    _echoAction = EchoOfAncestorsAction.CreateDefault();
                }
                skills.Add(_echoAction);

                if (showDebugInfo)
                    Debug.Log("[SkillCombatBridge] Echo of Ancestors skill available");
            }

            if (showDebugInfo)
                Debug.Log($"[SkillCombatBridge] {skills.Count} combat skills available");

            return skills;
        }

        private bool IsCombatSkillUnlocked(SkillTreeData treeData, string skillId, SkillEffectType effectType)
        {
            var progression = PlayerProgression.Instance;
            if (progression == null)
                return false;

            var node = treeData.GetSkillById(skillId);
            bool unlocked = node != null
                ? progression.IsSkillUnlocked(node)
                : progression.IsSkillUnlocked(skillId);

            if (!unlocked)
            {
                foreach (var unlockedSkill in progression.GetUnlockedSkills())
                {
                    if (unlockedSkill == null) continue;

                    if (unlockedSkill.SkillId == skillId || unlockedSkill.EffectType == effectType)
                    {
                        unlocked = true;
                        node = unlockedSkill;
                        break;
                    }
                }
            }

            if (showDebugInfo)
            {
                string nodeState = node != null ? node.SkillName : "missing node";
                Debug.Log($"[SkillCombatBridge] Combat skill check: {skillId} ({nodeState}) unlocked={unlocked}");
            }

            return unlocked;
        }

        /// <summary>
        /// Reset all skill cooldowns. Called at combat start.
        /// </summary>
        public void ResetCooldowns()
        {
            if (_areaDamageAction != null) _areaDamageAction.ResetCooldown();
            if (_echoAction != null) _echoAction.ResetCooldown();
        }

        /// <summary>
        /// Tick cooldowns for all skills. Called at the end of each player turn.
        /// </summary>
        public void TickCooldowns()
        {
            if (_areaDamageAction != null) _areaDamageAction.TickCooldown();
            if (_echoAction != null) _echoAction.TickCooldown();
        }
    }
}
