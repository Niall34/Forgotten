using UnityEngine;
using Photon.Pun;

// this controls the lobby scene which controls and shows a local-only preview character before anyone has hosted or joined
// and spawns the real networked character once the player is in a room
public class LobbyStage : MonoBehaviour
{
    public string characterPrefabName = "LobbyCharacter";

    public GameObject previewCharacterPrefab;

    private NetworkManager net;
    private bool hasSpawnedReal = false;
    private GameObject previewInstance;
    private bool wasInRoom = false;

    private void Start() // grabs the network manager
    {
        net = NetworkManager.Bootstrap();
    }

    private void Update() // watches for joining a room, to swap the preview for the real character
    {
        bool isInRoomNow = net.InRoom;
        if (isInRoomNow && wasInRoom == false)
        {
            HandleJustJoinedRoom();
        }
        wasInRoom = isInRoomNow;
    }

    // local only preview character which is shown before any room exists
    public void SpawnPreview(string displayName)
    {
        if (previewInstance != null)
        {
            // just update its label instead of making a duplicate
            LobbyPreviewCharacter existing = previewInstance.GetComponent<LobbyPreviewCharacter>();
            if (existing != null)
            {
                existing.SetDisplayName(displayName);
            }
            return;
        }

        Vector3 spawnPosition = Vector3.zero;
        Quaternion spawnRotation = Quaternion.identity;

        LobbySpawnPoint[] spawnPoints = FindObjectsOfType<LobbySpawnPoint>();
        if (spawnPoints.Length > 0)
        {
            Transform firstPoint = spawnPoints[0].transform;
            spawnPosition = firstPoint.position;
            spawnRotation = firstPoint.rotation;
        }

        previewInstance = Instantiate(previewCharacterPrefab, spawnPosition, spawnRotation);

        LobbyPreviewCharacter previewScript = previewInstance.GetComponent<LobbyPreviewCharacter>();
        if (previewScript != null)
        {
            previewScript.SetDisplayName(displayName);
        }
    }

    public void ClearPreview() // removes the preview character, if one exists
    {
        if (previewInstance != null)
        {
            Destroy(previewInstance);
            previewInstance = null;
        }
    }

    // real networked character which is spawned only after actually joining a room

    private void HandleJustJoinedRoom()
    {
        // solo sessions skip this scene's staging entirely and go straight to gameplay

        if (hasSpawnedReal || net.IsSolo)
        {
            return;
        }

        hasSpawnedReal = true;
        ClearPreview();
        SpawnLocalCharacter();
    }

    private void SpawnLocalCharacter() // spawns the real character at a seat based on the player's actor number
    {
        // player numbers start at 1, so this gives every player a different spawn point and
        // keeps the same player in the same seat if they rejoin - if you've got more players
        // than spawn points placed this will throw, so make sure you've placed enough
        LobbySpawnPoint[] spawnPoints = FindObjectsOfType<LobbySpawnPoint>();
        int mySeat = PhotonNetwork.LocalPlayer.ActorNumber - 1;
        Transform chosenPoint = spawnPoints[mySeat].transform;

        PhotonNetwork.Instantiate(characterPrefabName, chosenPoint.position, chosenPoint.rotation);
    }
}
