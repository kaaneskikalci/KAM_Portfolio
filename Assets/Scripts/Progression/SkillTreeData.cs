using UnityEngine;
using System.Collections.Generic;

namespace KAM.Progression
{
    /// <summary>
    /// Container ScriptableObject holding references to all skill nodes.
    /// Defines the full tree layout. Single asset that the UI reads.
    /// </summary>
    [CreateAssetMenu(fileName = "SkillTreeLayout", menuName = "KAM/Progression/Skill Tree Data")]
    public class SkillTreeData : ScriptableObject
    {
        [SerializeField] private string treeName = "Kam's Skill Tree";
        [SerializeField] private SkillNode[] allSkills;

        public string TreeName => treeName;
        public IReadOnlyList<SkillNode> AllSkills => allSkills;

        /// <summary>
        /// Look up a skill by its unique ID.
        /// </summary>
        public SkillNode GetSkillById(string id)
        {
            if (allSkills == null) return null;

            foreach (var skill in allSkills)
            {
                if (skill != null && skill.SkillId == id)
                {
                    return skill;
                }
            }
            return null;
        }

        /// <summary>
        /// Get all skills at a specific tier.
        /// </summary>
        public List<SkillNode> GetSkillsByTier(int tier)
        {
            var result = new List<SkillNode>();
            if (allSkills == null) return result;

            foreach (var skill in allSkills)
            {
                if (skill != null && skill.Tier == tier)
                {
                    result.Add(skill);
                }
            }
            return result;
        }

        /// <summary>
        /// Get the maximum tier in the tree.
        /// </summary>
        public int MaxTier
        {
            get
            {
                int max = 0;
                if (allSkills == null) return max;
                foreach (var skill in allSkills)
                {
                    if (skill != null && skill.Tier > max)
                        max = skill.Tier;
                }
                return max;
            }
        }
    }
}
