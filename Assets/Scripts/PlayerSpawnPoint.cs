using UnityEngine;

// set where the player spawns and it randomizies which one the player spawns on while having a backup point
public sealed class PlayerSpawnPoint : MonoBehaviour
{
#if UNITY_EDITOR
    // draws a marker and arrow in the scene view, editor only, no effect on the actual game
    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(0.2f, 0.85f, 0.95f, 0.85f);
        Gizmos.DrawWireSphere(transform.position + Vector3.up * 0.9f, 0.4f);
        Gizmos.DrawLine(transform.position + Vector3.up * 0.9f, transform.position + Vector3.up * 0.9f + transform.forward * 1.2f);
        UnityEditor.Handles.color = Gizmos.color;
        UnityEditor.Handles.Label(transform.position + Vector3.up * 1.6f, "Player Spawn");
    }
#endif
}
