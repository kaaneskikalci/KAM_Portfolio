using UnityEngine;

public class SkillTreeSoundManager : MonoBehaviour
{
    public static SkillTreeSoundManager Instance { get; private set; }

    [Header("Ses Kaynaðý")]
    public AudioSource uiAudioSource;

    [Header("Ses Dosyalarý")]
    public AudioClip unlockSound; // Hayat Aðacý Özel Sesi
    public AudioClip errorSound;  // Ýptal / Hata Sesi

    [Header("Ses Seviyesi")]
    [Range(0f, 1f)]
    public float volume = 0.8f;

    private void Awake()
    {
        Instance = this;

        if (uiAudioSource == null)
        {
            uiAudioSource = GetComponent<AudioSource>();
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    public static void PlayUnlock()
    {
        SkillTreeSoundManager manager = GetInstance();
        if (manager != null)
        {
            manager.PlayUnlockSound();
        }
    }

    public static void PlayError()
    {
        SkillTreeSoundManager manager = GetInstance();
        if (manager != null)
        {
            manager.PlayErrorSound();
        }
    }

    private static SkillTreeSoundManager GetInstance()
    {
        if (Instance == null)
        {
            Instance = FindAnyObjectByType<SkillTreeSoundManager>();
        }

        return Instance;
    }

    // Bu fonksiyon yetenek baþarýyla açýldýðýnda çaðrýlacak
    public void PlayUnlockSound()
    {
        if (uiAudioSource != null && unlockSound != null)
        {
            uiAudioSource.PlayOneShot(unlockSound, volume);
        }
    }

    // Bu fonksiyon yetenek açýlamadýðýnda (kaynak yetersiz/kilitli) çaðrýlacak
    public void PlayErrorSound()
    {
        if (uiAudioSource != null && errorSound != null)
        {
            uiAudioSource.PlayOneShot(errorSound, volume);
        }
    }
}