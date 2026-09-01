using KAM.Core.Combat;
using KAM.UI.Feedback;
using UnityEngine;

namespace KAM.Core
{
    /// <summary>
    /// Kills the player when they fall into a trigger volume below the map.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class PlayerFallKillZone : MonoBehaviour
    {
        [SerializeField] private bool triggerDeathUIIfNoCombatUnit = true;
        [SerializeField] private bool debugLog;

        private bool _hasTriggered;

        private void Reset()
        {
            Collider zoneCollider = GetComponent<Collider>();
            zoneCollider.isTrigger = true;
        }

        private void OnValidate()
        {
            Collider zoneCollider = GetComponent<Collider>();
            if (zoneCollider != null)
                zoneCollider.isTrigger = true;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (_hasTriggered || other == null)
                return;

            PlayerCombatUnit player = ResolvePlayer(other);
            if (player == null)
                return;

            _hasTriggered = true;

            if (debugLog)
                Debug.Log($"[PlayerFallKillZone] {player.name} entered fall kill zone.");

            if (player.Stats != null && player.Stats.IsAlive)
            {
                player.Stats.TakeRawDamage(player.Stats.CurrentHealth);
                return;
            }

            if (triggerDeathUIIfNoCombatUnit)
                DeathUI.ShowRespawnOrFallback();
        }

        private static PlayerCombatUnit ResolvePlayer(Collider other)
        {
            if (!other.CompareTag("Player") &&
                other.GetComponentInParent<PlayerCombatUnit>() == null &&
                other.GetComponentInChildren<PlayerCombatUnit>() == null)
            {
                return null;
            }

            PlayerCombatUnit player = other.GetComponent<PlayerCombatUnit>();
            if (player != null)
                return player;

            player = other.GetComponentInParent<PlayerCombatUnit>();
            if (player != null)
                return player;

            return other.GetComponentInChildren<PlayerCombatUnit>();
        }
    }
}
