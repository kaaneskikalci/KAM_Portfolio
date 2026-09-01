using UnityEngine;
using KAM.Combat.TurnSystem;

public class CombatSoundManager : MonoBehaviour
{
    [Header("Ses Kaynaðý")]
    public AudioSource combatAudioSource;

    [Header("Genel Savaþ Sesleri")]
    public AudioClip combatStartSound;     // Savaþ baþladýðýnda çalacak ses
    public AudioClip playerTurnStartSound; // Sýra oyuncuya geçtiðinde
    public AudioClip enemyTurnStartSound;  // Sýra düþmana geçtiðinde (tehditkar bir ses olabilir)
    public AudioClip victorySound;         // Savaþý kazandýðýnda
    public AudioClip defeatSound;          // Kaybettiðinde

    [Header("Aksiyon Sesleri")]
    public AudioClip attackSound;          // Saldýrý yapýldýðýnda (Kýlýç savurma vb.)
    public AudioClip hitDamageSound;       // Hasar alýndýðýnda (Et/Zýrh sesi)
    public AudioClip deathSound;           // Karakter öldüðünde
    public AudioClip missSound;            // Iskaladýðýnda veya kilitli hedefe vurduðunda
    public AudioClip healSound;            // Ýyileþme efekti

    [Header("Ses Seviyesi")]
    [Range(0f, 1f)] public float volume = 0.8f;

    public void PlayCombatStart()
    {
        if (combatAudioSource != null && combatStartSound != null)
            combatAudioSource.PlayOneShot(combatStartSound, volume);
    }

    public void PlayTurnStart(bool isPlayerTurn)
    {
        if (combatAudioSource == null) return;

        if (isPlayerTurn && playerTurnStartSound != null)
            combatAudioSource.PlayOneShot(playerTurnStartSound, volume);
        else if (!isPlayerTurn && enemyTurnStartSound != null)
            combatAudioSource.PlayOneShot(enemyTurnStartSound, volume);
    }

    public void PlayCombatEnd(CombatResult result)
    {
        if (combatAudioSource == null) return;

        if (result == CombatResult.Victory && victorySound != null)
            combatAudioSource.PlayOneShot(victorySound, volume);
        else if (result == CombatResult.Defeat && defeatSound != null)
            combatAudioSource.PlayOneShot(defeatSound, volume);
    }

    // YENÝ EKLENEN AKSÝYON SESLERÝ
    public void PlayAttackSound()
    {
        if (combatAudioSource != null && attackSound != null)
            combatAudioSource.PlayOneShot(attackSound, volume);
    }

    public void PlayHitDamageSound()
    {
        if (combatAudioSource != null && hitDamageSound != null)
            combatAudioSource.PlayOneShot(hitDamageSound, volume);
    }

    public void PlayDeathSound()
    {
        if (combatAudioSource != null && deathSound != null)
            combatAudioSource.PlayOneShot(deathSound, volume);
    }

    public void PlayMissSound()
    {
        if (combatAudioSource != null && missSound != null)
            combatAudioSource.PlayOneShot(missSound, volume);
    }

    public void PlayHealSound()
    {
        if (combatAudioSource != null && healSound != null)
            combatAudioSource.PlayOneShot(healSound, volume);
    }
}