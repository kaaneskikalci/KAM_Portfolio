using UnityEngine;
using UnityEngine.SceneManagement; // Sahne kontrolü için gerekli

public class AudioManager : MonoBehaviour
{
    public static AudioManager instance;
    public AudioSource musicSource;

    [Header("Müzik Dosyalarý")]
    public AudioClip menuMusic;
    public AudioClip obaMusic;
    public AudioClip spiritRealmMusic;

    void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
            // Sahne deðiþimini takip eden event'e abone oluyoruz
            SceneManager.sceneLoaded += OnSceneLoaded;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    // Her sahne yüklendiðinde bu fonksiyon çalýþýr
    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == "MainMenu")
        {
            PlayMusic(menuMusic);
        }
        else if (scene.name == "OBA")
        {
            PlayMusic(obaMusic);
        }
        else if (scene.name == "Lvl1")
        {
            PlayMusic(spiritRealmMusic);
        }
    }

    public void PlayMusic(AudioClip clip)
    {
        if (musicSource.clip == clip) return;
        musicSource.clip = clip;
        musicSource.Play();
    }
}