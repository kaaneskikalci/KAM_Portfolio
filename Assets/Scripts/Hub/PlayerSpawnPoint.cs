using UnityEngine;

namespace KAM.Hub
{
    /// <summary>
    /// Marker component for player spawn location in a scene.
    /// Place on an empty GameObject to define where the player appears.
    /// </summary>
    public class PlayerSpawnPoint : MonoBehaviour
    {
        [SerializeField] private bool isHubSpawn = true;

        public Vector3 SpawnPosition => transform.position;
        public Quaternion SpawnRotation => transform.rotation;
        public bool IsHubSpawn => isHubSpawn;

        private void OnDrawGizmos()
        {
            Gizmos.color = isHubSpawn ? Color.green : Color.cyan;
            Gizmos.DrawWireSphere(transform.position, 0.5f);
            Gizmos.DrawRay(transform.position, transform.forward * 2f);
        }
    }
}
