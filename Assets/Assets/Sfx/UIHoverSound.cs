using UnityEngine;
using UnityEngine.EventSystems; // Fare etkileþimlerini algýlamak için gerekli kütüphane

// IPointerEnterHandler arayüzü, farenin objenin üzerine geldiði aný yakalar
public class UIHoverSound : MonoBehaviour, IPointerEnterHandler
{
    [Header("Ses Ayarlarý")]
    public AudioSource uiAudioSource;
    public AudioClip hoverClip;

    [Header("Ses Seviyesi")]
    [Range(0f, 1f)]
    public float hoverVolume = 0.5f; // Hover sesleri genelde biraz kýsýk olmalýdýr

    // Fare butonun üzerine girdiði an bu fonksiyon otomatik çalýþýr
    public void OnPointerEnter(PointerEventData eventData)
    {
        if (uiAudioSource != null && hoverClip != null)
        {
            // PlayOneShot, sesi kesmeden üst üste çalabilmesini saðlar
            uiAudioSource.PlayOneShot(hoverClip, hoverVolume);
        }
    }
}