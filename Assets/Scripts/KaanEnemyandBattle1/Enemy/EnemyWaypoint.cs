using UnityEngine;


public class EnemyWaypoint : MonoBehaviour
{
    [Header("Waypoint Settings")]
    [Tooltip("How long the enemy should wait at this waypoint. 0 = use default from EnemyRoaming.")]
    [SerializeField] private float waitTime = 0f;
    
    [Tooltip("Optional: Make enemy look in this direction when reaching waypoint.")]
    [SerializeField] private bool overrideLookDirection = false;
    [SerializeField] private Vector3 lookDirection = Vector3.forward;
    
    [Header("Visuals")]
    [SerializeField] private Color gizmoColor = Color.cyan;
    [SerializeField] private float gizmoSize = 0.5f;
    
    public float WaitTime => waitTime;
    public bool HasLookDirection => overrideLookDirection;
    public Vector3 LookDirection => transform.TransformDirection(lookDirection.normalized);
    
 
    public Vector3 Position => transform.position;
    
    private void OnDrawGizmos()
    {
        Gizmos.color = gizmoColor;
        Gizmos.DrawWireSphere(transform.position, gizmoSize);
        
        Gizmos.DrawCube(transform.position, Vector3.one * (gizmoSize * 0.3f));
        
        if (overrideLookDirection)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawRay(transform.position, LookDirection * 2f);
        }
    }
    
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(gizmoColor.r, gizmoColor.g, gizmoColor.b, 0.3f);
        Gizmos.DrawSphere(transform.position, gizmoSize * 1.5f);
        
        Gizmos.color = Color.red;
        Gizmos.DrawRay(transform.position, transform.right * gizmoSize * 2f);
        Gizmos.color = Color.green;
        Gizmos.DrawRay(transform.position, transform.up * gizmoSize * 2f);
        Gizmos.color = Color.blue;
        Gizmos.DrawRay(transform.position, transform.forward * gizmoSize * 2f);
    }
}

