using System.Collections.Generic;
using UnityEngine;
using KAM.Combat.TurnSystem;
using KAM.Inventory;

namespace KAM.Core.Combat
{
    public enum EnemySkillProfile
    {
        Auto,
        Generic,
        Aabasi,
        Azar,
        Bazar,
        Kazar,
        Erbuke,
        Esbuke,
        AlKarisi,
        JalamuzKempir,
        Cak,
        BoyuBiKaris
    }

    public struct EnemySkillExecution
    {
        public bool UsedSkill;
        public string SkillName;
        public string Message;
        public List<CombatUnit> PopupTargets;
        public List<DamageResult> DamageResults;

        public static EnemySkillExecution None => new EnemySkillExecution
        {
            UsedSkill = false,
            SkillName = string.Empty,
            Message = string.Empty,
            PopupTargets = new List<CombatUnit>(),
            DamageResults = new List<DamageResult>()
        };
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(EnemyCombatUnit))]
    public class EnemySkillController : MonoBehaviour
    {
        [Header("Skill Profile")]
        [SerializeField] private EnemySkillProfile profile = EnemySkillProfile.Auto;
        [SerializeField, Range(0f, 1f)] private float skillUseChance = 0.7f;
        [SerializeField] private bool enableSkills = true;

        [Header("Debug")]
        [SerializeField] private bool debugLog = true;

        private EnemyCombatUnit _enemy;
        private int _primaryCooldown;
        private int _secondaryCooldown;
        private int _specialCooldown;
        private bool _azarCharging;
        private bool _alKarisiEnraged;

        public EnemySkillProfile Profile => ResolveProfile();

        private void Awake()
        {
            _enemy = GetComponent<EnemyCombatUnit>();
        }

        public bool TryExecuteTurn(CombatUnit player, out EnemySkillExecution execution)
        {
            execution = EnemySkillExecution.None;

            if (!enableSkills || _enemy == null || player == null || !player.IsAlive)
                return false;

            EnemySkillProfile resolved = ResolveProfile();
            if (resolved == EnemySkillProfile.Generic)
                return false;

            if (_azarCharging && resolved == EnemySkillProfile.Azar)
            {
                execution = ExecuteAzabStrike(player);
                return true;
            }

            if (Random.value > skillUseChance && !ShouldForceSkill(resolved))
                return false;

            bool used = resolved switch
            {
                EnemySkillProfile.Aabasi => TryAabasi(player, out execution),
                EnemySkillProfile.Azar => TryAzar(player, out execution),
                EnemySkillProfile.Bazar => TryBazar(player, out execution),
                EnemySkillProfile.Kazar => TryKazar(player, out execution),
                EnemySkillProfile.Erbuke => TryErbuke(player, out execution),
                EnemySkillProfile.Esbuke => TryErbuke(player, out execution),
                EnemySkillProfile.AlKarisi => TryAlKarisi(player, out execution),
                EnemySkillProfile.JalamuzKempir => TryJalamuz(player, out execution),
                EnemySkillProfile.Cak => TryCak(player, out execution),
                EnemySkillProfile.BoyuBiKaris => TryBoyu(player, out execution),
                _ => false
            };

            if (used && debugLog)
            {
                Debug.Log($"[EnemySkillController] {_enemy.UnitName} used {execution.SkillName}");
            }

            return used;
        }

        public void NotifyActionFinished()
        {
            _primaryCooldown = Mathf.Max(0, _primaryCooldown - 1);
            _secondaryCooldown = Mathf.Max(0, _secondaryCooldown - 1);
            _specialCooldown = Mathf.Max(0, _specialCooldown - 1);
        }

        public EnemyIntent PreviewIntent()
        {
            EnemySkillProfile resolved = ResolveProfile();

            if (_azarCharging && resolved == EnemySkillProfile.Azar)
                return EnemyIntent.Attack(Mathf.RoundToInt(_enemy.Stats.EffectiveDamage * 2.4f));

            if (!enableSkills || resolved == EnemySkillProfile.Generic)
                return EnemyIntent.Attack(_enemy.Stats.EffectiveDamage);

            return resolved switch
            {
                EnemySkillProfile.Aabasi => CreateIntent(EnemyIntentType.Debuff, "Decay / Evasion"),
                EnemySkillProfile.Azar => CreateIntent(EnemyIntentType.Special, "Azab / Guard"),
                EnemySkillProfile.Bazar => CreateIntent(EnemyIntentType.Heal, "Heal / Confuse"),
                EnemySkillProfile.Kazar => CreateIntent(EnemyIntentType.Debuff, "Poison / Shadow Step"),
                EnemySkillProfile.Erbuke => CreateIntent(EnemyIntentType.Debuff, "Acid / Tail"),
                EnemySkillProfile.Esbuke => CreateIntent(EnemyIntentType.Debuff, "Acid / Tail"),
                EnemySkillProfile.AlKarisi => CreateIntent(EnemyIntentType.Special, "Bleed / Rage"),
                EnemySkillProfile.JalamuzKempir => CreateIntent(EnemyIntentType.Debuff, "Quake"),
                EnemySkillProfile.Cak => CreateIntent(EnemyIntentType.Debuff, "Pack Howl"),
                EnemySkillProfile.BoyuBiKaris => CreateIntent(EnemyIntentType.Debuff, "Spirit Snatch"),
                _ => EnemyIntent.Attack(_enemy.Stats.EffectiveDamage)
            };
        }

        private bool TryAabasi(CombatUnit player, out EnemySkillExecution execution)
        {
            if (_secondaryCooldown <= 0 && !_enemy.HasEffect(StatusEffectCategory.Evasion) && _enemy.Stats.HealthPercentage <= 0.5f)
            {
                _enemy.ApplyEffect(StatusEffect.CreateEvasion(0.45f, 2));
                _secondaryCooldown = 4;
                execution = Create("Evasion", $"{_enemy.UnitName} slips away and becomes harder to hit.");
                return true;
            }

            if (_primaryCooldown <= 0)
            {
                player.ApplyEffect(StatusEffect.CreateDecay(3, 3));
                _primaryCooldown = 3;
                execution = Create("Decay Strike", $"{_enemy.UnitName} marked {player.UnitName} with decay.");
                return true;
            }

            execution = EnemySkillExecution.None;
            return false;
        }

        private bool TryAzar(CombatUnit player, out EnemySkillExecution execution)
        {
            if (_secondaryCooldown <= 0 && _enemy.Stats.HealthPercentage <= 0.6f && !_enemy.HasEffect(StatusEffectCategory.DefenseUp))
            {
                _enemy.ApplyEffect(StatusEffect.CreateDefenseUp(0.5f, 2));
                _secondaryCooldown = 4;
                execution = Create("Obsidian Guard", $"{_enemy.UnitName} raises obsidian guard and halves incoming damage.");
                return true;
            }

            if (_primaryCooldown <= 0)
            {
                _azarCharging = true;
                _primaryCooldown = 4;
                execution = Create("Azab Focus", $"{_enemy.UnitName} gathers pain for a heavy strike next turn.");
                return true;
            }

            execution = EnemySkillExecution.None;
            return false;
        }

        private EnemySkillExecution ExecuteAzabStrike(CombatUnit player)
        {
            _azarCharging = false;
            int damage = Mathf.Max(1, Mathf.RoundToInt(_enemy.Stats.EffectiveDamage * 2.4f));
            DamageResult result = _enemy.DealDamage(player, damage);
            return CreateDamage("Azab Strike", $"{_enemy.UnitName} releases Azab Strike for {result.DamageDealt} damage.", player, result);
        }

        private bool TryBazar(CombatUnit player, out EnemySkillExecution execution)
        {
            CombatUnit ally = FindLowestHealthAlly();
            if (_primaryCooldown <= 0 && ally != null && ally.Stats.HealthPercentage < 0.8f)
            {
                int heal = Mathf.Max(6, Mathf.RoundToInt(_enemy.Stats.EffectiveDamage * 1.2f));
                int healed = ally.Heal(heal);
                _primaryCooldown = 3;
                execution = Create("Brother's Aid", $"{_enemy.UnitName} healed {ally.UnitName} for {healed} HP.");
                return true;
            }

            if (_secondaryCooldown <= 0)
            {
                player.ApplyEffect(StatusEffect.CreateStun(1));
                _secondaryCooldown = 4;
                execution = Create("Confusing Whisper", $"{_enemy.UnitName} confused {player.UnitName}. Next action is skipped.");
                return true;
            }

            execution = EnemySkillExecution.None;
            return false;
        }

        private bool TryKazar(CombatUnit player, out EnemySkillExecution execution)
        {
            if (_secondaryCooldown <= 0 && _enemy.Stats.HealthPercentage <= 0.45f && !_enemy.HasEffect(StatusEffectCategory.Evasion))
            {
                _enemy.ApplyEffect(StatusEffect.CreateEvasion(0.5f, 2));
                _secondaryCooldown = 4;
                execution = Create("Shadow Step", $"{_enemy.UnitName} steps into shadow and becomes harder to hit.");
                return true;
            }

            if (_primaryCooldown <= 0)
            {
                int damage = Mathf.Max(1, Mathf.RoundToInt(_enemy.Stats.EffectiveDamage * 0.65f));
                DamageResult result = _enemy.DealDamage(player, damage);
                if (result.HitSuccessful)
                    player.ApplyEffect(StatusEffect.CreatePoison(5, 3));

                _primaryCooldown = 3;
                execution = CreateDamage("Poison Cut", $"{_enemy.UnitName} poisoned {player.UnitName}.", player, result);
                return true;
            }

            execution = EnemySkillExecution.None;
            return false;
        }

        private bool TryErbuke(CombatUnit player, out EnemySkillExecution execution)
        {
            if (_primaryCooldown <= 0)
            {
                int damage = Mathf.Max(1, Mathf.RoundToInt(_enemy.Stats.EffectiveDamage * 0.8f));
                DamageResult result = _enemy.DealDamage(player, damage);
                if (result.HitSuccessful)
                    player.ApplyEffect(StatusEffect.CreateAcid(4, 2));

                _primaryCooldown = 3;
                execution = CreateDamage("Acid Spit", $"{_enemy.UnitName} spits acid at {player.UnitName}.", player, result);
                return true;
            }

            int tailDamage = Mathf.Max(1, Mathf.RoundToInt(_enemy.Stats.EffectiveDamage * 1.1f));
            DamageResult tailResult = _enemy.DealDamage(player, tailDamage);
            execution = CreateDamage("Tail Strike", $"{_enemy.UnitName} strikes with its tail for {tailResult.DamageDealt} damage.", player, tailResult);
            return true;
        }

        private bool TryAlKarisi(CombatUnit player, out EnemySkillExecution execution)
        {
            if (!_alKarisiEnraged && _enemy.Stats.HealthPercentage <= 0.5f)
            {
                _alKarisiEnraged = true;
                _enemy.ApplyEffect(StatusEffect.CreateSpeedUp(0.5f, 99));
                execution = Create("Blood Scream", $"{_enemy.UnitName} screams and becomes faster.");
                return true;
            }

            if (_primaryCooldown <= 0)
            {
                int damage = Mathf.Max(1, Mathf.RoundToInt(_enemy.Stats.EffectiveDamage * 1.25f));
                DamageResult result = _enemy.DealDamage(player, damage);
                if (result.HitSuccessful)
                    player.ApplyEffect(StatusEffect.CreateBleed(5, 2));

                _primaryCooldown = 3;
                execution = CreateDamage("Bleeding Claws", $"{_enemy.UnitName} tears {player.UnitName} with bleeding claws.", player, result);
                return true;
            }

            execution = EnemySkillExecution.None;
            return false;
        }

        private bool TryJalamuz(CombatUnit player, out EnemySkillExecution execution)
        {
            if (_primaryCooldown <= 0)
            {
                int damage = Mathf.Max(1, Mathf.RoundToInt(_enemy.Stats.EffectiveDamage * 0.75f));
                DamageResult result = _enemy.DealDamage(player, damage);
                if (result.HitSuccessful)
                    player.ApplyEffect(StatusEffect.CreateSpeedDown(0.35f, 2));

                _primaryCooldown = 4;
                execution = CreateDamage("Quake", $"{_enemy.UnitName} shakes the ground and slows {player.UnitName}.", player, result);
                return true;
            }

            execution = EnemySkillExecution.None;
            return false;
        }

        private bool TryCak(CombatUnit player, out EnemySkillExecution execution)
        {
            if (_primaryCooldown <= 0)
            {
                int cakCount = CountAliveProfile(EnemySkillProfile.Cak);
                if (cakCount > 1)
                    player.ApplyEffect(StatusEffect.CreateVulnerable(0.25f, 1));
                else
                    player.ApplyEffect(StatusEffect.CreateWeak(0.25f, 1));

                _primaryCooldown = 3;
                execution = Create("Pack Howl", $"{_enemy.UnitName} howls. {player.UnitName} is pressured by the pack.");
                return true;
            }

            execution = EnemySkillExecution.None;
            return false;
        }

        private bool TryBoyu(CombatUnit player, out EnemySkillExecution execution)
        {
            if (_specialCooldown <= 0 && _enemy.Stats.HealthPercentage <= 0.5f && !_enemy.HasEffect(StatusEffectCategory.Evasion))
            {
                _enemy.ApplyEffect(StatusEffect.CreateSpeedUp(0.35f, 3));
                _enemy.ApplyEffect(StatusEffect.CreateEvasion(0.45f, 3));
                _specialCooldown = 6;
                execution = Create("Beard Trick", $"{_enemy.UnitName} becomes fast and slippery.");
                return true;
            }

            if (_primaryCooldown <= 0)
            {
                int stolen = 0;
                if (InventoryManager.Instance != null && InventoryManager.Instance.SpiritFragments > 0)
                {
                    int amount = Mathf.Clamp(Mathf.CeilToInt(InventoryManager.Instance.SpiritFragments * 0.08f), 1, 12);
                    if (InventoryManager.Instance.SpendSpiritFragments(amount))
                        stolen = amount;
                }

                if (stolen > 0)
                {
                    _primaryCooldown = 4;
                    execution = Create("Spirit Snatch", $"{_enemy.UnitName} stole {stolen} Spirit Fragments.");
                    return true;
                }

                player.ApplyEffect(StatusEffect.CreateWeak(0.2f, 1));
                _primaryCooldown = 3;
                execution = Create("Mocking Hex", $"{_enemy.UnitName} weakens {player.UnitName}.");
                return true;
            }

            execution = EnemySkillExecution.None;
            return false;
        }

        private bool ShouldForceSkill(EnemySkillProfile resolved)
        {
            return (resolved == EnemySkillProfile.AlKarisi && !_alKarisiEnraged && _enemy.Stats.HealthPercentage <= 0.5f) ||
                   (resolved == EnemySkillProfile.Azar && _azarCharging);
        }

        private CombatUnit FindLowestHealthAlly()
        {
            if (TurnManager.Instance == null)
                return null;

            CombatUnit best = null;
            foreach (CombatUnit unit in TurnManager.Instance.GetAliveEnemies())
            {
                if (unit == null || unit == _enemy || !unit.IsAlive)
                    continue;

                if (best == null || unit.Stats.HealthPercentage < best.Stats.HealthPercentage)
                    best = unit;
            }

            return best;
        }

        private int CountAliveProfile(EnemySkillProfile targetProfile)
        {
            if (TurnManager.Instance == null)
                return 0;

            int count = 0;
            foreach (CombatUnit unit in TurnManager.Instance.GetAliveEnemies())
            {
                EnemySkillController controller = unit.GetComponent<EnemySkillController>();
                if (controller != null && controller.Profile == targetProfile)
                    count++;
            }

            return count;
        }

        private EnemySkillProfile ResolveProfile()
        {
            if (profile != EnemySkillProfile.Auto)
                return profile;

            string source = $"{gameObject.name} {_enemy?.UnitName}".ToLowerInvariant();

            if (source.Contains("aabasi") || source.Contains("abasi"))
                return EnemySkillProfile.Aabasi;
            if (source.Contains("azar"))
                return EnemySkillProfile.Azar;
            if (source.Contains("bazar"))
                return EnemySkillProfile.Bazar;
            if (source.Contains("kazar"))
                return EnemySkillProfile.Kazar;
            if (source.Contains("erbuke"))
                return EnemySkillProfile.Erbuke;
            if (source.Contains("esbuke"))
                return EnemySkillProfile.Esbuke;
            if (source.Contains("al karisi") || source.Contains("alkarisi"))
                return EnemySkillProfile.AlKarisi;
            if (source.Contains("jalamuz"))
                return EnemySkillProfile.JalamuzKempir;
            if (source.Contains("cak"))
                return EnemySkillProfile.Cak;
            if (source.Contains("boyu") || source.Contains("sakal"))
                return EnemySkillProfile.BoyuBiKaris;

            return EnemySkillProfile.Generic;
        }

        private static EnemyIntent CreateIntent(EnemyIntentType type, string description)
        {
            return new EnemyIntent
            {
                ActionType = type,
                DamageAmount = 0,
                HitCount = 0,
                Description = description
            };
        }

        private static EnemySkillExecution Create(string skillName, string message)
        {
            return new EnemySkillExecution
            {
                UsedSkill = true,
                SkillName = skillName,
                Message = message,
                PopupTargets = new List<CombatUnit>(),
                DamageResults = new List<DamageResult>()
            };
        }

        private static EnemySkillExecution CreateDamage(string skillName, string message, CombatUnit target, DamageResult result)
        {
            return new EnemySkillExecution
            {
                UsedSkill = true,
                SkillName = skillName,
                Message = message,
                PopupTargets = new List<CombatUnit> { target },
                DamageResults = new List<DamageResult> { result }
            };
        }
    }
}
