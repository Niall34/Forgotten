using UnityEngine;

// marks a sensor in the level and holds the two spots the monster/player can spawn at near it
// also draws gizmos in editor so its easy to see where everything is without hitting play
public sealed class SensorSpawnPoint : MonoBehaviour
{
    [Header("Sensor")]
    [Range(1, 4)]
    public int sensorNumber = 1;

    [Header("Spawn Points")]
    public Transform spawnPointA;
    public Transform spawnPointB;

#if UNITY_EDITOR
    private void OnDrawGizmos() // draws the sensor + its two spawn points as spheres with labels, editor only
    {
        // the sensor itself, little blue ball
        Gizmos.color = new Color(0.1f, 0.7f, 1f, 0.9f);
        Gizmos.DrawWireSphere(transform.position, 0.5f);

        UnityEditor.Handles.color = Gizmos.color;
        UnityEditor.Handles.Label(transform.position + Vector3.up * 0.8f, "Sensor " + sensorNumber);

        // red = spawn A, orange = spawn B, just so they're easy to tell apart at a glance
        DrawSpawnGizmo(spawnPointA, new Color(0.85f, 0.15f, 0.12f, 0.9f), "Spawn A");
        DrawSpawnGizmo(spawnPointB, new Color(0.95f, 0.55f, 0.1f, 0.9f), "Spawn B");
    }

    private void DrawSpawnGizmo(Transform point, Color color, string label) // shared helper so A and B don't repeat the same gizmo code twice
    {
        if (point == null)
        {
            return;
        }

        Gizmos.color = color;
        Gizmos.DrawWireSphere(point.position + Vector3.up * 1.1f, 0.55f);
        Gizmos.DrawLine(point.position, point.position + Vector3.up * 2.2f);

        UnityEditor.Handles.color = color;
        UnityEditor.Handles.Label(point.position + Vector3.up * 2.4f, "Sensor " + sensorNumber + " " + label);
    }
#endif

    // picks one of the two spawn points at random, falls back to whichever one exists if only one is set
    public Transform GetRandomSpawnPoint()
    {
        if (spawnPointA == null && spawnPointB == null)
        {
            return null;
        }

        if (spawnPointA == null)
        {
            return spawnPointB;
        }

        if (spawnPointB == null)
        {
            return spawnPointA;
        }

        bool pickA = Random.value < 0.5f;
        return pickA ? spawnPointA : spawnPointB;
    }
}