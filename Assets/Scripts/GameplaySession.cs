using Photon.Pun;
using UnityEngine;

// the starting point for the real gameplay scene, spawns the local player's character
// at a random point set by forgottenplayerspawnpoint

public class GameplaySession : MonoBehaviour
{
    public string playerPrefabName = "Player/Player";

    private void Start()
    {
        SpawnLocalPlayer();
    }

    private void SpawnLocalPlayer()
    {
        Vector3 spawnPosition = Vector3.zero; // FIX: this was missing, spawnPosition was never declared before
        Quaternion spawnRotation = Quaternion.identity;

        PlayerSpawnPoint[] spawnPoints = FindObjectsOfType<PlayerSpawnPoint>();
        if (spawnPoints.Length > 0) // FIX: added this check back, without it spawnPoints[randomIndex] crashes on an empty scene
        {
            int randomIndex = Random.Range(0, spawnPoints.Length);
            Transform chosenPoint = spawnPoints[randomIndex].transform;
            spawnPosition = chosenPoint.position;
            spawnRotation = chosenPoint.rotation;
        }

        PhotonNetwork.Instantiate(playerPrefabName, spawnPosition, spawnRotation);
    }
}
