using UnityEngine;
// similar to the player spawner but just for the lobby
public sealed class LobbySpawnPoint : MonoBehaviour
{
#if UNITY_EDITOR
    // draws a marker and arrow in the scene view so its easy to see and aim
    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(0.95f, 0.78f, 0.25f, 0.9f);
        Gizmos.DrawWireSphere(transform.position + Vector3.up * 0.9f, 0.4f);
        Gizmos.DrawLine(transform.position + Vector3.up * 0.9f, transform.position + Vector3.up * 0.9f + transform.forward * 1.2f);
        UnityEditor.Handles.color = Gizmos.color;
        UnityEditor.Handles.Label(transform.position + Vector3.up * 1.6f, "Lobby Spawn");
    }
#endif
}
