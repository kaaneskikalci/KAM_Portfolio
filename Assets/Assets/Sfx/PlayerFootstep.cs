using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class PlayerFootstep : MonoBehaviour
{
    [Header("Footstep Audio")]
    public AudioSource footstepSource;
    public AudioClip footstepClip;

    [Header("Volume")]
    [Range(0f, 1f)]
    public float footstepVolume = 0.1f;

    private PlayerMovement _playerMovement;

    private void Awake()
    {
        _playerMovement = GetComponentInParent<PlayerMovement>();
        EnsureFootstepSource();
    }

    private void Start()
    {
        if (_playerMovement == null)
        {
            _playerMovement = GetComponentInParent<PlayerMovement>();
        }

        EnsureFootstepSource();
    }

    private void OnDisable()
    {
        if (footstepSource != null)
        {
            footstepSource.Stop();
        }
    }

    private void Update()
    {
        if (!EnsureFootstepSource() || footstepClip == null)
        {
            return;
        }

        float moveX = Input.GetAxisRaw("Horizontal");
        float moveY = Input.GetAxisRaw("Vertical");
        bool isMoving = moveX != 0 || moveY != 0;
        bool isCrouching = _playerMovement != null && _playerMovement.IsCrouching;

        if (isCrouching)
        {
            if (footstepSource.isPlaying)
            {
                footstepSource.Stop();
            }

            return;
        }

        if (isMoving)
        {
            if (!footstepSource.isPlaying)
            {
                footstepSource.volume = footstepVolume;
                footstepSource.Play();
            }

            return;
        }

        if (footstepSource.isPlaying)
        {
            footstepSource.Stop();
        }
    }

    private bool EnsureFootstepSource()
    {
        if (footstepSource == null)
        {
            footstepSource = GetComponent<AudioSource>();
        }

        if (footstepSource == null)
        {
            footstepSource = gameObject.AddComponent<AudioSource>();
        }

        if (footstepSource == null)
        {
            return false;
        }

        footstepSource.playOnAwake = false;
        footstepSource.clip = footstepClip;
        footstepSource.loop = true;
        footstepSource.volume = footstepVolume;
        return true;
    }
}
