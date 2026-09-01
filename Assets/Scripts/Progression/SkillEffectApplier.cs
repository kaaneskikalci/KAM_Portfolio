using UnityEngine;
using UnityEngine.SceneManagement;
using KAM.Inventory;

namespace KAM.Progression
{
    /// <summary>
    /// Bridges unlocked skills to game systems. Reads PlayerProgression state
    /// and applies effects to combat, exploration, and passive systems.
    /// Singleton, DontDestroyOnLoad.
    /// </summary>
    public class SkillEffectApplier : MonoBehaviour
    {
        private static SkillEffectApplier _instance;
        public static SkillEffectApplier Instance => _instance;

        public const string RunePowerupId = "rune_powerup";
        public const string UmayFavoriteId = "umay_favorite";
        public const string AreaDamageId = "area_damage";
        public const string DefenseRuneId = "defense_rune";
        public const string ErlikCloakId = "erlik_cloak";
        public const string EyeNatureId = "eye_nature";
        public const string KeyRuneId = "key_rune";
        public const string EchoAncestorsId = "echo_ancestors";
        public const string BelowHalfHpId = "below_half_hp";
        public const string KamsQuestId = "kams_quest";

        [Header("Debug")]
        [SerializeField] private bool showDebugInfo = true;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);

            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                SceneManager.sceneLoaded -= HandleSceneLoaded;
                _instance = null;
            }
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            // Apply passive effects when entering any scene
            ApplyAllPassiveEffects();
        }

        #region Apply Effects

        /// <summary>
        /// Apply all unlocked passive effects to the current scene.
        /// Called on scene load and when skills are unlocked.
        /// </summary>
        public void ApplyAllPassiveEffects()
        {
            if (PlayerProgression.Instance == null) return;

            // Skill 5: Reduce enemy vision range
            ApplyEnemyVisionReduction();

            // Skill 6: Collectibles glow while roaming
            ApplyCollectibleGlow();

            if (showDebugInfo)
            {
                Debug.Log("[SkillEffectApplier] Applied all passive effects.");
            }
        }

        /// <summary>
        /// Apply effects for a specific newly-unlocked skill.
        /// </summary>
        public void ApplySkill(SkillNode node)
        {
            if (node == null) return;

            switch (node.EffectType)
            {
                case SkillEffectType.ReduceEnemyVision:
                    ApplyEnemyVisionReduction();
                    break;
                case SkillEffectType.CollectibleGlow:
                    ApplyCollectibleGlow();
                    break;
                // Other skills are queried at runtime by combat systems
            }
        }

        #endregion

        #region Skill Queries (called by combat/exploration systems)

        public bool IsSkillUnlocked(string skillId)
        {
            return GetSkill(skillId) is SkillNode skill &&
                   PlayerProgression.Instance != null &&
                   PlayerProgression.Instance.IsSkillUnlocked(skill);
        }

        public SkillEffectType GetActiveEffect(string skillId)
        {
            var skill = GetSkill(skillId);
            if (skill == null || PlayerProgression.Instance == null)
                return SkillEffectType.None;

            return PlayerProgression.Instance.GetActiveEffectForSkill(skill);
        }

        /// <summary>
        /// Get the timed hit multiplier bonus from Skill 1 (Rune Power-up).
        /// Returns 0 if not unlocked, 0.3 if unlocked (added to base 1.3x).
        /// </summary>
        public float GetTimedHitMultiplierBonus()
        {
            var skill = GetUnlockedSkill(RunePowerupId);
            return skill != null ? skill.EffectValue : 0f;
        }

        public float GetTimedHitBlindChance()
        {
            return GetActiveBranchValue(RunePowerupId, SkillEffectType.TimedHitBlinding);
        }

        public float GetTimedHitLifeStealPercent()
        {
            return GetActiveBranchValue(RunePowerupId, SkillEffectType.TimedHitLifeSteal);
        }

        public float GetDoubleBeamChance()
        {
            var skill = GetUnlockedSkill(UmayFavoriteId);
            return skill != null ? skill.EffectValue : 0f;
        }

        public float GetDoubleBeamDamagePercent()
        {
            var skill = GetUnlockedSkill(UmayFavoriteId);
            return skill != null ? skill.SecondaryValue : 0f;
        }

        public bool CanChooseDoubleBeamTarget()
        {
            return GetActiveEffect(UmayFavoriteId) == SkillEffectType.DoubleBeamChooseTarget;
        }

        public float GetDoubleBeamSlowPercent()
        {
            return GetActiveBranchValue(UmayFavoriteId, SkillEffectType.DoubleBeamVineSlow);
        }

        public bool HasDefenseRune()
        {
            return IsSkillUnlocked(DefenseRuneId);
        }

        public float GetDefenseRuneReduction()
        {
            var skill = GetUnlockedSkill(DefenseRuneId);
            return skill != null ? skill.EffectValue : 0f;
        }

        public float GetDefenseReflectionPercent()
        {
            return GetActiveBranchValue(DefenseRuneId, SkillEffectType.DefenseReflection);
        }

        public float GetDefensePerfectReduction()
        {
            return GetActiveBranchValue(DefenseRuneId, SkillEffectType.DefensePerfectTiming);
        }

        /// <summary>
        /// Get the low HP damage bonus from Skill 9 (Below 50% HP).
        /// Returns 0 if not unlocked, 0.15 if unlocked.
        /// </summary>
        public float GetLowHPDamageBonus()
        {
            var skill = GetUnlockedSkill(BelowHalfHpId);
            return skill != null ? skill.EffectValue : 0f;
        }

        /// <summary>
        /// Get the enemy vision range multiplier from Skill 5 (Erlik Kaan's Cloak).
        /// Returns 1.0 if not unlocked, 0.85 if unlocked.
        /// </summary>
        public float GetEnemyVisionMultiplier()
        {
            var skill = GetUnlockedSkill(ErlikCloakId);
            return skill != null ? 1f - skill.EffectValue : 1f;
        }

        /// <summary>
        /// Check if the Key Rune ability is unlocked (Skill 7).
        /// </summary>
        public bool HasKeyRuneAbility()
        {
            return IsSkillUnlocked(KeyRuneId);
        }

        public int GetKeyRuneLockBreakTurns()
        {
            var skill = GetUnlockedSkill(KeyRuneId);
            return skill != null ? Mathf.Max(1, Mathf.RoundToInt(skill.SecondaryValue)) : 0;
        }

        /// <summary>
        /// Check if collectibles should glow (Skill 6: Eye for Nature).
        /// </summary>
        public bool ShouldCollectiblesGlow()
        {
            return IsSkillUnlocked(EyeNatureId);
        }

        #endregion

        #region Effect Implementations

        /// <summary>
        /// Apply vision reduction to all EnemyVision components in the scene.
        /// </summary>
        private void ApplyEnemyVisionReduction()
        {
            float multiplier = GetEnemyVisionMultiplier();
            if (Mathf.Approximately(multiplier, 1f)) return;

            var enemyVisions = FindObjectsByType<EnemyVision>(FindObjectsSortMode.None);
            foreach (var vision in enemyVisions)
            {
                vision.SetVisionRangeMultiplier(multiplier);
            }

            if (showDebugInfo && enemyVisions.Length > 0)
            {
                Debug.Log($"[SkillEffectApplier] Applied vision reduction ({multiplier:F2}x) to {enemyVisions.Length} enemies.");
            }
        }

        private void ApplyCollectibleGlow()
        {
            bool enabled = ShouldCollectiblesGlow();
            var pickups = FindObjectsByType<ItemPickup>(FindObjectsSortMode.None);
            foreach (var pickup in pickups)
            {
                pickup.RefreshSkillGlow(enabled);
            }

            if (showDebugInfo && enabled && pickups.Length > 0)
            {
                Debug.Log($"[SkillEffectApplier] Applied collectible glow to {pickups.Length} pickups.");
            }
        }

        private SkillNode GetSkill(string skillId)
        {
            if (PlayerProgression.Instance == null || PlayerProgression.Instance.SkillTreeData == null)
                return null;

            return PlayerProgression.Instance.SkillTreeData.GetSkillById(skillId);
        }

        private SkillNode GetUnlockedSkill(string skillId)
        {
            var skill = GetSkill(skillId);
            if (skill == null || PlayerProgression.Instance == null)
                return null;

            return PlayerProgression.Instance.IsSkillUnlocked(skill) ? skill : null;
        }

        private float GetActiveBranchValue(string skillId, SkillEffectType expectedEffect)
        {
            var skill = GetUnlockedSkill(skillId);
            if (skill == null || !skill.HasBranches || PlayerProgression.Instance == null)
                return 0f;

            int branch = PlayerProgression.Instance.GetChosenBranch(skill.SkillId);
            if (branch == 0 && skill.BranchA.effectType == expectedEffect)
                return skill.BranchA.effectValue;

            if (branch == 1 && skill.BranchB.effectType == expectedEffect)
                return skill.BranchB.effectValue;

            return 0f;
        }

        #endregion
    }
}
