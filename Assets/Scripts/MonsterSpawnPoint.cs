using UnityEngine;

// random spawn point for the monster so the monster spawns/respawns unpredicably, also have a backup spot just in case
public sealed class ForgottenMonsterSpawnPoint : MonoBehaviour
{
#if UNITY_EDITOR
    // draws a marker in the scene view so its easy to see and place
    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(0.85f, 0.15f, 0.12f, 0.9f);
        Gizmos.DrawWireSphere(transform.position + Vector3.up * 1.1f, 0.55f);
        Gizmos.DrawLine(transform.position, transform.position + Vector3.up * 2.2f);
        UnityEditor.Handles.color = Gizmos.color;
        UnityEditor.Handles.Label(transform.position + Vector3.up * 2.4f, "Monster Spawn");
    }
#endif
}
