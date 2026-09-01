using UnityEngine;
using KAM.Core.Combat;

namespace KAM.Hub
{
    /// <summary>
    /// Hub scene controller for the Oba (village).
    /// Handles player positioning, HP reset, and hub initialization.
    /// Placed in the Oba scene (NOT DontDestroyOnLoad).
    /// </summary>
    public class ObaManager : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private PlayerSpawnPoint spawnPoint;

        [Header("Debug")]
        [SerializeField] private bool showDebugInfo = true;

        private void Start()
        {
            // Find spawn point if not assigned
            if (spawnPoint == null)
            {
                spawnPoint = FindFirstObjectByType<PlayerSpawnPoint>();
            }

            SpawnOrFindPlayer();
        }

        /// <summary>
        /// Find the player in the scene and position at spawn point.
        /// </summary>
        private void SpawnOrFindPlayer()
        {
            var player = FindFirstObjectByType<PlayerMovement>();
            if (player == null)
            {
                if (showDebugInfo)
                {
                    Debug.LogWarning("ObaManager: No PlayerMovement found in scene!");
                }
                return;
            }

            // Position player at spawn point
            if (spawnPoint != null)
            {
                player.transform.position = spawnPoint.SpawnPosition;
                player.transform.rotation = spawnPoint.SpawnRotation;
            }

            // Ensure player movement is enabled in hub
            player.SetMovementEnabled(true);

            // Reset player HP to full in hub
            var playerCombat = player.GetComponent<PlayerCombatUnit>();
            if (playerCombat != null && playerCombat.Stats != null)
            {
                playerCombat.Stats.Initialize();
            }

            if (showDebugInfo)
            {
                Debug.Log("ObaManager: Player positioned at hub spawn point.");
            }
        }
    }
}
