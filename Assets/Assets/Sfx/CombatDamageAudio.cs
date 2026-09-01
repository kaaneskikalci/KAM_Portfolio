using KAM.Core.Combat;
using UnityEngine;

[DisallowMultipleComponent]
public class CombatDamageAudio : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private CombatUnit combatUnit;
    [SerializeField] private AudioSource damageAudioSource;

    [Header("Damage Sounds")]
    [SerializeField] private AudioClip[] damageClips;
    [SerializeField] private AudioClip[] criticalDamageClips;
    [SerializeField] private bool useCriticalClipsWhenAvailable = true;
    [SerializeField] private bool playOnDeathHit = false;

    [Header("Playback")]
    [Range(0f, 1f)]
    [SerializeField] private float volume = 0.85f;
    [SerializeField] private float minSecondsBetweenSounds = 0.08f;
    [SerializeField] private Vector2 randomPitchRange = new Vector2(0.96f, 1.04f);
    [SerializeField] private bool onlyWhenInCombat = true;

    [Header("Auto Audio Source")]
    [SerializeField] private bool createAudioSourceIfMissing = true;
    [SerializeField] private bool configureCreatedAudioSource = true;
    [Range(0f, 1f)]
    [SerializeField] private float createdSourceSpatialBlend = 1f;
    [SerializeField] private float createdSourceMinDistance = 2f;
    [SerializeField] private float createdSourceMaxDistance = 16f;

    private float _lastPlayedTime = -999f;
    private AudioClip _lastClip;
    private CombatUnit _subscribedUnit;

    private void Awake()
    {
        ResolveReferences();
    }

    private void Reset()
    {
        ResolveReferences();
    }

    private void OnValidate()
    {
        if (randomPitchRange.x <= 0f)
            randomPitchRange.x = 0.01f;

        if (randomPitchRange.y < randomPitchRange.x)
            randomPitchRange.y = randomPitchRange.x;

        if (minSecondsBetweenSounds < 0f)
            minSecondsBetweenSounds = 0f;

        if (createdSourceMinDistance < 0.01f)
            createdSourceMinDistance = 0.01f;

        if (createdSourceMaxDistance < createdSourceMinDistance)
            createdSourceMaxDistance = createdSourceMinDistance;
    }

    private void OnEnable()
    {
        Subscribe();
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    private void OnDestroy()
    {
        Unsubscribe();
    }

    [ContextMenu("Auto Setup References")]
    private void AutoSetupReferences()
    {
        ResolveReferences();
    }

    private void ResolveReferences()
    {
        if (combatUnit == null)
        {
            combatUnit = GetComponent<CombatUnit>();
            if (combatUnit == null)
                combatUnit = GetComponentInParent<CombatUnit>();
            if (combatUnit == null)
                combatUnit = GetComponentInChildren<CombatUnit>(true);
        }

        if (damageAudioSource == null)
            damageAudioSource = GetComponent<AudioSource>();

        if (damageAudioSource == null && createAudioSourceIfMissing && Application.isPlaying)
        {
            damageAudioSource = gameObject.AddComponent<AudioSource>();
            if (configureCreatedAudioSource)
                ConfigureCreatedAudioSource();
        }
    }

    private void ConfigureCreatedAudioSource()
    {
        if (damageAudioSource == null)
            return;

        damageAudioSource.playOnAwake = false;
        damageAudioSource.loop = false;
        damageAudioSource.spatialBlend = createdSourceSpatialBlend;
        damageAudioSource.rolloffMode = AudioRolloffMode.Logarithmic;
        damageAudioSource.minDistance = createdSourceMinDistance;
        damageAudioSource.maxDistance = createdSourceMaxDistance;
        damageAudioSource.dopplerLevel = 0f;
    }

    private void Subscribe()
    {
        ResolveReferences();

        if (combatUnit == null || _subscribedUnit == combatUnit)
            return;

        Unsubscribe();
        _subscribedUnit = combatUnit;
        _subscribedUnit.OnDamageTaken += HandleDamageTaken;
    }

    private void Unsubscribe()
    {
        if (_subscribedUnit == null)
            return;

        _subscribedUnit.OnDamageTaken -= HandleDamageTaken;
        _subscribedUnit = null;
    }

    private void HandleDamageTaken(CombatUnit unit, DamageResult result)
    {
        if (!enabled || damageAudioSource == null)
            return;

        if (!result.HitSuccessful || result.DamageDealt <= 0)
            return;

        if (result.KilledTarget && !playOnDeathHit)
            return;

        if (onlyWhenInCombat && unit != null && !unit.IsInCombat)
            return;

        if (Time.unscaledTime - _lastPlayedTime < minSecondsBetweenSounds)
            return;

        AudioClip clip = PickClip(result);
        if (clip == null)
            return;

        float previousPitch = damageAudioSource.pitch;
        damageAudioSource.pitch = Random.Range(randomPitchRange.x, randomPitchRange.y);
        damageAudioSource.PlayOneShot(clip, volume);
        damageAudioSource.pitch = previousPitch;

        _lastPlayedTime = Time.unscaledTime;
        _lastClip = clip;
    }

    private AudioClip PickClip(DamageResult result)
    {
        AudioClip[] pool = useCriticalClipsWhenAvailable &&
            result.DamageInfo.IsCritical &&
            criticalDamageClips != null &&
            criticalDamageClips.Length > 0
                ? criticalDamageClips
                : damageClips;

        if (pool == null || pool.Length == 0)
            return null;

        if (pool.Length == 1)
            return pool[0];

        AudioClip selected = pool[Random.Range(0, pool.Length)];
        int guard = 0;
        while (selected == _lastClip && guard < 8)
        {
            selected = pool[Random.Range(0, pool.Length)];
            guard++;
        }

        return selected;
    }
}
