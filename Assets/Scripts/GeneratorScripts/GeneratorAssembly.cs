using Photon.Pun;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;

// Main generator hub which receives pieces and tracks the progress of how many pieces it has received.
[RequireComponent(typeof(PhotonView))]
public class GeneratorAssembly : MonoBehaviourPun, IPunObservable
{
    [Header("Assembly")]
    public int totalPiecesNeeded = 5;
    public float installHoldDuration = 10f; // player must hold button for 10 seconds for piece to be added
    public float interactionDistance = 2f;

    [Header("Visual Feedback")]
    public Transform motorShaft; 
    public float maxMotorSpinSpeed = 360f;
    public AudioClip mechanicalSoundEffect;
    public float soundVolume = 0.7f;

    [Header("Door")]
    public GameObject doorToOpen;
    public WinTrigger winTrigger;

    private int piecesInstalled = 0;
    private float currentMotorSpeed = 0f;
    private AudioSource audioSource;

    private void Start()
    {
        // AUDIO SET UP 
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }
    }

    private void Update()
    {
        //Spin the motor based on assembly progress 
        if (motorShaft != null)
        {
            currentMotorSpeed = Mathf.Lerp(currentMotorSpeed, (piecesInstalled / (float)totalPiecesNeeded) * maxMotorSpinSpeed, Time.deltaTime * 2f);
            motorShaft.Rotate(Vector3.forward, currentMotorSpeed * Time.deltaTime);
        }
        
    }

    public bool CanInstallPiece(PlayerInventory player)
    {
        // Check if player is close enough and holding a piece

        if (player == null || player.GetHeldPiece() == null)
        {
            return false;
        }

        float distanceToPlayer = Vector3.Distance(transform.position, player.transform.position);
        return distanceToPlayer <= interactionDistance;
    }

    public void InstallPiece(GeneratorPiece piece)
    {
        if (piece == null)
        {
            return;
        }

        // tell everyone a piece was added to the generator
        photonView.RPC(nameof(RPC_PieceInstalled), RpcTarget.All);
        piece.PlacedOnGenerator();
    }

    [PunRPC]
    private void RPC_PieceInstalled()
    {
        piecesInstalled++;

        // Play sound effect 
        if (audioSource != null && mechanicalSoundEffect != null)
        {
            audioSource.PlayOneShot(mechanicalSoundEffect, soundVolume);
        }

        Debug.Log($"Piece installed! Progress: {piecesInstalled}/{totalPiecesNeeded}");

        // check if all pieces are installed 
        if (piecesInstalled >= totalPiecesNeeded)
        {
            OpenDoor();
        }
    }

    private void OpenDoor()
    {
        if (doorToOpen != null)
        {
            doorToOpen.SetActive(false);
        }

        // win trigger enable
        if (winTrigger != null)
        {
            winTrigger.Enable();
        }

        Debug.Log("Generator completed! Door opened!");
    }

    public int GetPiecesInstalled()
    {
        return piecesInstalled;
    }

    public int GetTotalPiecesNeeded()
    {
        return totalPiecesNeeded;
    }
    
    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        // Sync the number of pieces installed across all clients 
        if (stream.IsWriting)
        {
            stream.SendNext(piecesInstalled);
        } else {
            piecesInstalled = (int)stream.ReceiveNext();
        }
    }
}
