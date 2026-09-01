using KAM.Core.Combat;
using KAM.Core.Stats;
using UnityEngine;

namespace KAM.Inventory
{
    public enum PlantConsumableEffect
    {
        HealFlat,
        HealPercent,
        StrengthUp,
        DefenseUp,
        SpeedUp,
        Evasion,
        Regeneration,
        GainActionPoint,
        CleanseDebuffs
    }

    /// <summary>
    /// Consumable plant item used by collectible world plants.
    /// Effects are intentionally small and combat-safe so plants can be used from quick slots.
    /// </summary>
    [CreateAssetMenu(fileName = "New Plant Consumable", menuName = "KAM/Inventory/Plant Consumable")]
    public class PlantConsumableData : ItemData
    {
        [Header("Plant Effect")]
        [SerializeField] private PlantConsumableEffect effect = PlantConsumableEffect.HealFlat;
        [SerializeField] private float effectValue = 20f;
        [SerializeField] private int durationTurns = 0;
        [SerializeField] private bool requiresCombat = false;

        public PlantConsumableEffect Effect => effect;
        public float EffectValue => effectValue;
        public int DurationTurns => durationTurns;
        public bool RequiresCombat => requiresCombat;

        public override bool Use(GameObject user)
        {
            PlayerCombatUnit player = ResolvePlayer(user);
            if (player == null)
            {
                Debug.LogWarning($"[PlantConsumableData] Could not use {ItemName}: PlayerCombatUnit not found.");
                return false;
            }

            if (requiresCombat && !player.IsInCombat)
            {
                Debug.Log($"[PlantConsumableData] {ItemName} can only be used in combat.");
                TryLog($"{ItemName} can only be used in combat.");
                return false;
            }

            bool applied = ApplyEffect(player, out string message);
            if (!applied)
            {
                Debug.Log($"[PlantConsumableData] {ItemName} had no effect.");
                TryLog($"{ItemName} had no effect.");
                return false;
            }

            Debug.Log($"[PlantConsumableData] {message}");
            TryLog(message);
            return true;
        }

        private bool ApplyEffect(PlayerCombatUnit player, out string message)
        {
            message = $"{ItemName} used.";
            int turns = Mathf.Max(1, durationTurns);

            switch (effect)
            {
                case PlantConsumableEffect.HealFlat:
                {
                    int healAmount = Mathf.Max(1, Mathf.RoundToInt(effectValue));
                    int healed = player.Heal(healAmount);
                    message = healed > 0
                        ? $"{ItemName} restored {healed} HP."
                        : $"{ItemName} could not restore HP.";
                    return healed > 0;
                }

                case PlantConsumableEffect.HealPercent:
                {
                    int healAmount = Mathf.Max(1, Mathf.RoundToInt(player.Stats.MaxHealth * Mathf.Clamp01(effectValue)));
                    int healed = player.Heal(healAmount);
                    message = healed > 0
                        ? $"{ItemName} restored {healed} HP."
                        : $"{ItemName} could not restore HP.";
                    return healed > 0;
                }

                case PlantConsumableEffect.StrengthUp:
                    player.ApplyEffect(StatusEffect.CreateStrengthUp(Mathf.Max(0f, effectValue), turns));
                    message = $"{ItemName} increased damage for {turns} turn(s).";
                    return true;

                case PlantConsumableEffect.DefenseUp:
                    player.ApplyEffect(StatusEffect.CreateDefenseUp(Mathf.Max(0f, effectValue), turns));
                    message = $"{ItemName} increased defense for {turns} turn(s).";
                    return true;

                case PlantConsumableEffect.SpeedUp:
                    player.ApplyEffect(StatusEffect.CreateSpeedUp(Mathf.Max(0f, effectValue), turns));
                    message = $"{ItemName} increased speed for {turns} turn(s).";
                    return true;

                case PlantConsumableEffect.Evasion:
                    player.ApplyEffect(StatusEffect.CreateEvasion(Mathf.Clamp01(effectValue), turns));
                    message = $"{ItemName} increased evasion for {turns} turn(s).";
                    return true;

                case PlantConsumableEffect.Regeneration:
                    player.ApplyEffect(StatusEffect.CreateRegeneration(Mathf.Max(1, Mathf.RoundToInt(effectValue)), turns));
                    message = $"{ItemName} grants regeneration for {turns} turn(s).";
                    return true;

                case PlantConsumableEffect.GainActionPoint:
                {
                    int gained = player.PlayerSpecificStats.GainActionPoints(Mathf.Max(1, Mathf.RoundToInt(effectValue)));
                    message = gained > 0
                        ? $"{ItemName} restored {gained} AP."
                        : $"{ItemName} could not restore AP.";
                    return gained > 0;
                }

                case PlantConsumableEffect.CleanseDebuffs:
                {
                    int debuffCount = 0;
                    foreach (StatusEffect activeEffect in player.GetActiveEffects())
                    {
                        if (activeEffect.EffectType == StatusEffectType.Debuff)
                            debuffCount++;
                    }

                    player.RemoveEffectsOfType(StatusEffectType.Debuff);
                    message = debuffCount > 0
                        ? $"{ItemName} cleansed {debuffCount} debuff(s)."
                        : $"{ItemName} found no debuffs to cleanse.";
                    return debuffCount > 0;
                }

                default:
                    return false;
            }
        }

        private static PlayerCombatUnit ResolvePlayer(GameObject user)
        {
            if (user != null)
            {
                PlayerCombatUnit unit = user.GetComponent<PlayerCombatUnit>();
                if (unit != null) return unit;

                unit = user.GetComponentInParent<PlayerCombatUnit>();
                if (unit != null) return unit;

                unit = user.GetComponentInChildren<PlayerCombatUnit>();
                if (unit != null) return unit;
            }

            return FindFirstObjectByType<PlayerCombatUnit>();
        }

        private static void TryLog(string message)
        {
            if (KAM.UI.Combat.CombatLogUI.Instance != null)
                KAM.UI.Combat.CombatLogUI.Instance.Log(message);
        }
    }
}
