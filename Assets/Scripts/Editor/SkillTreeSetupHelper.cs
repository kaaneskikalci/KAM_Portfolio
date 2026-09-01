#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using KAM.Progression;

namespace KAM.Editor
{
    /// <summary>
    /// Editor helper to auto-create all 10 GDD skill node assets
    /// and the SkillTreeData layout asset.
    /// </summary>
    public static class SkillTreeSetupHelper
    {
        private const string SkillAssetPath = "Assets/ScriptableObjects/Skills";

        [MenuItem("KAM/Progression/Create All Skill Node Assets")]
        public static void CreateAllSkillNodes()
        {
            // Ensure directory exists
            if (!AssetDatabase.IsValidFolder("Assets/ScriptableObjects"))
                AssetDatabase.CreateFolder("Assets", "ScriptableObjects");
            if (!AssetDatabase.IsValidFolder(SkillAssetPath))
                AssetDatabase.CreateFolder("Assets/ScriptableObjects", "Skills");

            // Create all 10 skill nodes (prerequisites linked after creation)
            var skill1 = CreateSkillNode("rune_powerup", "Rune Power-up",
                "Enhances timed hit damage by 30%. Successful timed hits deal significantly more damage.",
                SkillCategory.Combat, 30, SkillEffectType.TimedHitDamageBonus, 0.3f, 0, 0,
                true, "Blinding", "50% miss chance for 1 turn on timed hit",
                SkillEffectType.TimedHitBlinding, 0.5f,
                "Life Steal", "Heal 20% of timed hit damage",
                SkillEffectType.TimedHitLifeSteal, 0.2f);

            var skill2 = CreateSkillNode("umay_favorite", "Umay Ana's Favorite",
                "25% chance to fire a second beam at 30% damage after basic attack.",
                SkillCategory.Combat, 50, SkillEffectType.DoubleBeamChance, 0.25f, 0.3f, 1,
                true, "Choose Target", "Choose the target for the second beam",
                SkillEffectType.DoubleBeamChooseTarget, 1f,
                "Vine Slow", "Second beam applies 30% speed slow for 1 turn",
                SkillEffectType.DoubleBeamVineSlow, 0.3f);

            var skill3 = CreateSkillNode("area_damage", "Area Damage",
                "Staff slam dealing 50% base damage to all enemies. 1 turn cooldown.",
                SkillCategory.Combat, 40, SkillEffectType.AreaDamage, 0.5f, 0, 1,
                false, null, null, SkillEffectType.None, 0,
                null, null, SkillEffectType.None, 0);

            var skill4 = CreateSkillNode("defense_rune", "Defense Rune",
                "Timed hit during enemy attacks reduces incoming damage by 30%.",
                SkillCategory.Combat, 40, SkillEffectType.DefenseRune, 0.3f, 0, 0,
                true, "Reflection", "Reflect a portion of blocked damage back to attacker",
                SkillEffectType.DefenseReflection, 1f,
                "Perfect Timing", "Tighter 0.25s window for 60% damage reduction",
                SkillEffectType.DefensePerfectTiming, 0.6f);

            var skill5 = CreateSkillNode("erlik_cloak", "Erlik Kaan's Cloak",
                "Reduces enemy vision range by 15% while roaming.",
                SkillCategory.Exploration, 25, SkillEffectType.ReduceEnemyVision, 0.15f, 0, 0,
                false, null, null, SkillEffectType.None, 0,
                null, null, SkillEffectType.None, 0);

            var skill6 = CreateSkillNode("eye_nature", "Eye for Nature",
                "Collectible items and resources glow while roaming.",
                SkillCategory.Exploration, 25, SkillEffectType.CollectibleGlow, 1f, 0, 1,
                false, null, null, SkillEffectType.None, 0,
                null, null, SkillEffectType.None, 0);

            var skill7 = CreateSkillNode("key_rune", "Key Rune Power-up",
                "Timed hit can unlock boss damage locks. Lock breaks for 3 turns.",
                SkillCategory.Combat, 60, SkillEffectType.KeyRuneUnlock, 1f, 3f, 2,
                false, null, null, SkillEffectType.None, 0,
                null, null, SkillEffectType.None, 0);

            var skill8 = CreateSkillNode("echo_ancestors", "Echo of Ancestors",
                "Skip action to apply +15% damage echo (bleed) on target for 3 turns.",
                SkillCategory.Combat, 50, SkillEffectType.EchoOfAncestors, 0.15f, 3f, 2,
                false, null, null, SkillEffectType.None, 0,
                null, null, SkillEffectType.None, 0);

            var skill9 = CreateSkillNode("below_half_hp", "Below 50% HP",
                "+15% echo damage for 2 turns when below 50% HP.",
                SkillCategory.Passive, 35, SkillEffectType.LowHPDamageBoost, 0.15f, 2f, 3,
                false, null, null, SkillEffectType.None, 0,
                null, null, SkillEffectType.None, 0);

            var skill10 = CreateSkillNode("kams_quest", "Kam's Quest (Ultimate)",
                "50 successful hits fills ultimate bar. Choose between two devastating ultimates.",
                SkillCategory.Ultimate, 100, SkillEffectType.None, 0, 0, 4,
                true, "Ilgen's Eye", "All enemies take +100% damage",
                SkillEffectType.UltimateIlgensEye, 1f,
                "Erlik's Last Effort", "+50% damage but -10% HP per turn for 5 turns",
                SkillEffectType.UltimateErliksEffort, 0.5f);

            // Set prerequisites using SerializedObject
            SetPrerequisites(skill2, new[] { skill1 });  // Umay Ana requires Rune Power-up
            SetPrerequisites(skill3, new[] { skill1 });  // Area Damage requires Rune Power-up
            SetPrerequisites(skill6, new[] { skill5 });  // Eye for Nature requires Erlik's Cloak
            SetPrerequisites(skill7, new[] { skill1 });  // Key Rune requires Rune Power-up
            SetPrerequisites(skill8, new[] { skill3 });  // Echo requires Area Damage
            SetPrerequisites(skill9, new[] { skill8 });  // Below 50% requires Echo
            SetPrerequisites(skill10, new[] { skill7, skill9 }); // Ultimate requires Key Rune + Below 50%

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"SkillTreeSetupHelper: Created 10 skill node assets in {SkillAssetPath}/");
        }

        [MenuItem("KAM/Progression/Create Skill Tree Layout Asset")]
        public static void CreateSkillTreeLayout()
        {
            if (!AssetDatabase.IsValidFolder(SkillAssetPath))
            {
                Debug.LogError("SkillTreeSetupHelper: Skill assets not found. Create skill nodes first.");
                return;
            }

            var layout = ScriptableObject.CreateInstance<SkillTreeData>();

            // Load all skill node assets
            string[] guids = AssetDatabase.FindAssets("t:SkillNode", new[] { SkillAssetPath });
            var skills = new SkillNode[guids.Length];
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                skills[i] = AssetDatabase.LoadAssetAtPath<SkillNode>(path);
            }

            // Set allSkills via SerializedObject
            string layoutPath = $"{SkillAssetPath}/SkillTreeLayout.asset";
            AssetDatabase.CreateAsset(layout, layoutPath);

            var so = new SerializedObject(layout);
            var allSkillsProp = so.FindProperty("allSkills");
            allSkillsProp.arraySize = skills.Length;
            for (int i = 0; i < skills.Length; i++)
            {
                allSkillsProp.GetArrayElementAtIndex(i).objectReferenceValue = skills[i];
            }
            so.ApplyModifiedProperties();

            AssetDatabase.SaveAssets();
            Debug.Log($"SkillTreeSetupHelper: Created SkillTreeLayout at {layoutPath} with {skills.Length} skills.");
        }

        private static SkillNode CreateSkillNode(
            string skillId, string skillName, string description,
            SkillCategory category, int cost,
            SkillEffectType effectType, float effectValue, float secondaryValue, int tier,
            bool hasBranches,
            string branchAName, string branchADesc, SkillEffectType branchAEffect, float branchAValue,
            string branchBName, string branchBDesc, SkillEffectType branchBEffect, float branchBValue)
        {
            var node = ScriptableObject.CreateInstance<SkillNode>();
            string assetPath = $"{SkillAssetPath}/{skillId}.asset";

            AssetDatabase.CreateAsset(node, assetPath);

            // Use SerializedObject to set private serialized fields
            var so = new SerializedObject(node);
            so.FindProperty("skillId").stringValue = skillId;
            so.FindProperty("skillName").stringValue = skillName;
            so.FindProperty("description").stringValue = description;
            so.FindProperty("category").enumValueIndex = (int)category;
            so.FindProperty("spiritFragmentCost").intValue = cost;
            so.FindProperty("effectType").enumValueIndex = (int)effectType;
            so.FindProperty("effectValue").floatValue = effectValue;
            so.FindProperty("secondaryValue").floatValue = secondaryValue;
            so.FindProperty("tier").intValue = tier;
            so.FindProperty("hasBranches").boolValue = hasBranches;

            // UI position will be calculated at runtime by SkillTreeUI.PopulateTree()
            so.FindProperty("uiPosition").vector2Value = Vector2.zero;

            if (hasBranches)
            {
                var branchAProp = so.FindProperty("branchA");
                branchAProp.FindPropertyRelative("branchName").stringValue = branchAName ?? "";
                branchAProp.FindPropertyRelative("description").stringValue = branchADesc ?? "";
                branchAProp.FindPropertyRelative("effectType").enumValueIndex = (int)branchAEffect;
                branchAProp.FindPropertyRelative("effectValue").floatValue = branchAValue;

                var branchBProp = so.FindProperty("branchB");
                branchBProp.FindPropertyRelative("branchName").stringValue = branchBName ?? "";
                branchBProp.FindPropertyRelative("description").stringValue = branchBDesc ?? "";
                branchBProp.FindPropertyRelative("effectType").enumValueIndex = (int)branchBEffect;
                branchBProp.FindPropertyRelative("effectValue").floatValue = branchBValue;
            }

            so.ApplyModifiedProperties();
            return node;
        }

        private static void SetPrerequisites(SkillNode node, SkillNode[] prerequisites)
        {
            var so = new SerializedObject(node);
            var prereqProp = so.FindProperty("prerequisites");
            prereqProp.arraySize = prerequisites.Length;
            for (int i = 0; i < prerequisites.Length; i++)
            {
                prereqProp.GetArrayElementAtIndex(i).objectReferenceValue = prerequisites[i];
            }
            so.ApplyModifiedProperties();
        }
    }
}
#endif
