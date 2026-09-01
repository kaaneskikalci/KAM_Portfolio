using UnityEngine;
using System.Collections.Generic;

namespace KAM.Progression
{
    /// <summary>
    /// ScriptableObject defining a single skill in the skill tree.
    /// One asset per GDD skill (10 total).
    /// </summary>
    [CreateAssetMenu(fileName = "NewSkill", menuName = "KAM/Progression/Skill Node")]
    public class SkillNode : ScriptableObject
    {
        [Header("Skill Identity")]
        [SerializeField] private string skillId;
        [SerializeField] private string skillName;
        [SerializeField, TextArea(2, 4)] private string description;
        [SerializeField] private Sprite icon;
        [SerializeField] private SkillCategory category;

        [Header("Cost")]
        [SerializeField] private int spiritFragmentCost = 50;

        [Header("Prerequisites")]
        [SerializeField] private SkillNode[] prerequisites;

        [Header("Branching")]
        [SerializeField] private bool hasBranches = false;
        [SerializeField] private SkillBranch branchA;
        [SerializeField] private SkillBranch branchB;

        [Header("Effect")]
        [SerializeField] private SkillEffectType effectType;
        [SerializeField] private float effectValue;
        [SerializeField] private float secondaryValue;

        [Header("Tree Layout")]
        [SerializeField] private int tier;
        [SerializeField] private Vector2 uiPosition;

        // Public accessors
        public string SkillId => skillId;
        public string SkillName => skillName;
        public string Description => description;
        public Sprite Icon => icon;
        public SkillCategory Category => category;
        public int SpiritFragmentCost => spiritFragmentCost;
        public IReadOnlyList<SkillNode> Prerequisites => prerequisites;
        public bool HasBranches => hasBranches;
        public SkillBranch BranchA => branchA;
        public SkillBranch BranchB => branchB;
        public SkillEffectType EffectType => effectType;
        public float EffectValue => effectValue;
        public float SecondaryValue => secondaryValue;
        public int Tier => tier;
        public Vector2 UIPosition => uiPosition;
    }

    /// <summary>
    /// A branch variant for skills that offer two upgrade paths.
    /// Only one branch can be active at a time.
    /// </summary>
    [System.Serializable]
    public class SkillBranch
    {
        public string branchName;
        [TextArea(1, 3)] public string description;
        public Sprite icon;
        public SkillEffectType effectType;
        public float effectValue;
    }

    /// <summary>
    /// Categories for skill tree organization.
    /// </summary>
    public enum SkillCategory
    {
        Combat,
        Exploration,
        Passive,
        Ultimate
    }

    /// <summary>
    /// Effect types mapped to each GDD skill and branch variant.
    /// Used by SkillEffectApplier to route effects to game systems.
    /// </summary>
    public enum SkillEffectType
    {
        None,

        // Skill 1: Rune Power-up
        TimedHitDamageBonus,        // Base: +30% timed hit damage
        TimedHitBlinding,           // Branch A: 50% miss chance for 1 turn
        TimedHitLifeSteal,          // Branch B: 20% heal on timed hit

        // Skill 2: Umay Ana's Favorite
        DoubleBeamChance,           // Base: 25% chance second beam at 30% damage
        DoubleBeamChooseTarget,     // Branch A: choose target for second beam
        DoubleBeamVineSlow,         // Branch B: 30% speed slow for 1 turn

        // Skill 3: Area Damage
        AreaDamage,                 // Staff slam, 50% base to all enemies

        // Skill 4: Defense Rune
        DefenseRune,                // Timed hit during enemy attack, -30% damage
        DefenseReflection,          // Branch A: reflect damage
        DefensePerfectTiming,       // Branch B: 0.25s window for -60% damage

        // Skill 5: Erlik Kaan's Cloak
        ReduceEnemyVision,          // -15% enemy vision range while roaming

        // Skill 6: Eye for Nature
        CollectibleGlow,            // Collectibles glow while roaming

        // Skill 7: Key Rune Power-up
        KeyRuneUnlock,              // Timed hit unlocks boss damage locks

        // Skill 8: Echo of Ancestors
        EchoOfAncestors,            // Skip action, +15% damage echo (bleed) for 3 turns

        // Skill 9: Below 50% HP
        LowHPDamageBoost,           // +15% damage when below 50% HP

        // Skill 10: Kam's Quest (Ultimate)
        UltimateIlgensEye,          // Branch A: all enemies +100% damage
        UltimateErliksEffort        // Branch B: +50% damage, -10% HP/turn for 5 turns
    }
}
