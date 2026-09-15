using UnityEngine;
using Photon.Pun;

// controls the lobby scene - shows a local-only preview character before joining a room
// and spawns the real networked character once the player is in a room
public class LobbyStage : MonoBehaviour
{
    public string characterPrefabName = "LobbyCharacter";

    public GameObject previewCharacterPrefab;

    private NetworkManager net;
    private bool hasSpawnedReal = false;
    private GameObject previewInstance;
    private bool wasInRoom = false;
    private string myDisplayName = ""; // saved from SpawnPreview, used later by SpawnLocalCharacter

    private void Start() // grabs the network manager
    {
        net = NetworkManager.Bootstrap();
    }

    private void Update() // checks if we just joined a room, and swaps the preview for the real character if so
    {
        bool isInRoomNow = net.InRoom;
        if (isInRoomNow && wasInRoom == false)
        {
            HandleJustJoinedRoom();
        }
        wasInRoom = isInRoomNow;
    }

    // spawns the local-only preview character shown before any room exists
    public void SpawnPreview(string displayName)
    {
        myDisplayName = displayName; // saved for SpawnLocalCharacter to use later

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

    // real networked character, spawned only after actually joining a room

    private void HandleJustJoinedRoom() // spawns the real character the first time we join a room
    {
        // solo sessions skip this and go straight to gameplay

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
        // actor numbers start at 1, so this gives each player a different spawn point
        // and keeps them in the same seat if they rejoin - needs enough spawn points placed or this will throw
        LobbySpawnPoint[] spawnPoints = FindObjectsOfType<LobbySpawnPoint>();
        int mySeat = PhotonNetwork.LocalPlayer.ActorNumber - 1;
        Transform chosenPoint = spawnPoints[mySeat].transform;

        // sends the name as instantiation data so every client can read it right away - LobbyPreviewCharacter picks it up in its own Awake()
        object[] instantiationData = new object[] { myDisplayName };
        PhotonNetwork.Instantiate(characterPrefabName, chosenPoint.position, chosenPoint.rotation, 0, instantiationData);
    }
}
