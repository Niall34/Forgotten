using Photon.Pun;
using UnityEngine;

// the starting point for the real gameplay scene and spawns the local player's character at a random point set by playerspawnpoint

public class GameplaySession : MonoBehaviour
{
    public string playerPrefabName = "Player/Player";
    public string monsterPrefabName = "Monster/Prefabs/Monster";

    private void Start()
    {
        SpawnLocalPlayer();

        if (PhotonNetwork.IsMasterClient) // set it so the master client spawns the monster so there is only one
        {
            SpawnMonster();
        }
    }

    private void SpawnLocalPlayer()
    {
        Vector3 spawnPosition = Vector3.zero;
        Quaternion spawnRotation = Quaternion.identity;

        PlayerSpawnPoint[] spawnPoints = FindObjectsOfType<PlayerSpawnPoint>();

        if (spawnPoints.Length > 0)
        {
            int randomIndex = Random.Range(0, spawnPoints.Length);
            Transform chosenPoint = spawnPoints[randomIndex].transform;
            spawnPosition = chosenPoint.position;
            spawnRotation = chosenPoint.rotation;
        }

        PhotonNetwork.Instantiate(playerPrefabName, spawnPosition, spawnRotation);
    }

    private void SpawnMonster()
    {
        Vector3 spawnPosition = Vector3.zero;
        Quaternion spawnRotation = Quaternion.identity;

        MonsterSpawnPoint[] spawnPoints = FindObjectsOfType<MonsterSpawnPoint>();

        if (spawnPoints.Length > 0)
        {
            int randomIndex = Random.Range(0, spawnPoints.Length);
            Transform chosenPoint = spawnPoints[randomIndex].transform;
            spawnPosition = chosenPoint.position;
            spawnRotation = chosenPoint.rotation;
        }

        PhotonNetwork.Instantiate(monsterPrefabName, spawnPosition, spawnRotation);
    }
}