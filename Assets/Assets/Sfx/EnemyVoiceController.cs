using UnityEngine;
using System.Collections;

[RequireComponent(typeof(AudioSource))]
public class EnemyVoiceController : MonoBehaviour
{
    [Header("Audio Source")]
    private AudioSource audioSource;

    [Header("Player Distance")]
    [Tooltip("Player transform. If empty, the script finds the object tagged Player.")]
    public Transform player;
    public float minDistance = 3f;
    public float maxDistance = 15f;

    [Header("Idle Sounds")]
    public AudioClip[] idleSounds;
    [Tooltip("Delay between idle sounds when the player is very close.")]
    public float closestInterval = 0.5f;
    [Tooltip("Delay between idle sounds near the outer audible range.")]
    public float furthestInterval = 3f;

    [Header("Combat Sounds")]
    public AudioClip[] combatSounds;

    [Header("Volume")]
    [Range(0f, 1f)] public float volume = 0.8f;

    private bool isInCombat = false;
    private Coroutine idleRoutine;
    private EnemyStateMachine stateMachine;
    private BattleManager battleManager;

    private AudioClip lastPlayedIdle;
    private AudioClip lastPlayedCombat;

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        ConfigureAudioSource();
        stateMachine = GetComponent<EnemyStateMachine>();
    }

    private void OnValidate()
    {
        if (minDistance < 0.1f)
        {
            minDistance = 0.1f;
        }

        if (maxDistance < minDistance)
        {
            maxDistance = minDistance;
        }

        audioSource = GetComponent<AudioSource>();
        ConfigureAudioSource();
    }

    private void OnEnable()
    {
        if (stateMachine == null)
        {
            stateMachine = GetComponent<EnemyStateMachine>();
        }

        if (stateMachine != null)
        {
            stateMachine.OnStateChanged += HandleEnemyStateChanged;
        }
    }

    private void OnDisable()
    {
        if (stateMachine != null)
        {
            stateMachine.OnStateChanged -= HandleEnemyStateChanged;
        }

        if (idleRoutine != null)
        {
            StopCoroutine(idleRoutine);
            idleRoutine = null;
        }

        if (audioSource != null)
        {
            audioSource.Stop();
        }
    }

    private void Start()
    {
        battleManager = BattleManager.Instance;
        FindPlayerIfNeeded();

        if (ShouldSilenceIdleVoice())
        {
            EnterCombat();
        }
        else
        {
            StartIdleSounds();
        }
    }

    private void Update()
    {
        if (ShouldSilenceIdleVoice())
        {
            EnterCombat();
        }
        else if (isInCombat)
        {
            ExitCombat();
        }
    }

    private void StartIdleSounds()
    {
        if (ShouldSilenceIdleVoice())
        {
            EnterCombat();
            return;
        }

        if (idleRoutine != null)
        {
            StopCoroutine(idleRoutine);
        }

        isInCombat = false;
        idleRoutine = StartCoroutine(IdleSoundRoutine());
    }

    private IEnumerator IdleSoundRoutine()
    {
        while (!isInCombat)
        {
            FindPlayerIfNeeded();

            if (player == null)
            {
                yield return new WaitForSeconds(1f);
                continue;
            }

            float distance = Vector3.Distance(transform.position, player.position);
            if (distance > maxDistance)
            {
                yield return new WaitForSeconds(0.5f);
                continue;
            }

            float t = Mathf.InverseLerp(minDistance, maxDistance, distance);
            float waitTime = Mathf.Lerp(closestInterval, furthestInterval, t);
            waitTime += Random.Range(-0.3f, 0.3f);
            if (waitTime < 0.2f)
            {
                waitTime = 0.2f;
            }

            yield return new WaitForSeconds(waitTime);

            if (ShouldSilenceIdleVoice())
            {
                EnterCombat();
                yield break;
            }

            if (!isInCombat && idleSounds != null && idleSounds.Length > 0 && player != null)
            {
                distance = Vector3.Distance(transform.position, player.position);
                if (distance > maxDistance)
                {
                    continue;
                }

                AudioClip clipToPlay = GetRandomClip(idleSounds, lastPlayedIdle);
                if (clipToPlay == null)
                {
                    continue;
                }

                lastPlayedIdle = clipToPlay;
                audioSource.PlayOneShot(clipToPlay, volume);
            }
        }
    }

    private void HandleEnemyStateChanged(EnemyStateMachine.EnemyState previousState, EnemyStateMachine.EnemyState newState)
    {
        if (newState == EnemyStateMachine.EnemyState.Combat)
        {
            EnterCombat();
        }
        else if (previousState == EnemyStateMachine.EnemyState.Combat)
        {
            ExitCombat();
        }
    }

    public void EnterCombat()
    {
        if (isInCombat && idleRoutine == null)
        {
            return;
        }

        isInCombat = true;
        if (idleRoutine != null)
        {
            StopCoroutine(idleRoutine);
            idleRoutine = null;
        }

        if (audioSource != null)
        {
            audioSource.Stop();
        }
    }

    public void ExitCombat()
    {
        if (ShouldSilenceIdleVoice())
        {
            EnterCombat();
        }
        else
        {
            StartIdleSounds();
        }
    }

    public void PlayCombatSound()
    {
        PlayAttackVoice();
    }

    public void PlayAttackVoice()
    {
        if (!isInCombat || combatSounds == null || combatSounds.Length == 0 || audioSource == null)
        {
            return;
        }

        AudioClip clipToPlay = GetRandomClip(combatSounds, lastPlayedCombat);
        if (clipToPlay == null)
        {
            return;
        }

        lastPlayedCombat = clipToPlay;
        audioSource.PlayOneShot(clipToPlay, volume);
    }

    private void FindPlayerIfNeeded()
    {
        if (player != null)
        {
            return;
        }

        GameObject pObj = GameObject.FindGameObjectWithTag("Player");
        if (pObj != null)
        {
            player = pObj.transform;
        }
    }

    private void ConfigureAudioSource()
    {
        if (audioSource == null)
        {
            return;
        }

        audioSource.playOnAwake = false;
        audioSource.loop = false;
        audioSource.spatialBlend = 1f;
        audioSource.rolloffMode = AudioRolloffMode.Logarithmic;
        audioSource.minDistance = minDistance;
        audioSource.maxDistance = maxDistance;
        audioSource.dopplerLevel = 0f;
        audioSource.Stop();
    }

    private bool ShouldSilenceIdleVoice()
    {
        if (stateMachine != null && stateMachine.CurrentState == EnemyStateMachine.EnemyState.Combat)
        {
            return true;
        }

        battleManager ??= BattleManager.Instance;
        return battleManager != null && battleManager.InCombat;
    }

    private AudioClip GetRandomClip(AudioClip[] clips, AudioClip lastPlayed)
    {
        if (clips == null || clips.Length == 0)
        {
            return null;
        }

        if (clips.Length <= 1)
        {
            return clips[0];
        }

        AudioClip newClip = clips[Random.Range(0, clips.Length)];
        int guard = 0;
        while (newClip == lastPlayed && guard < 10)
        {
            newClip = clips[Random.Range(0, clips.Length)];
            guard++;
        }

        return newClip;
    }
}
