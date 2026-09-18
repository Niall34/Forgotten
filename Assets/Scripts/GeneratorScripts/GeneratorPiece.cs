using Photon.Pun;
using UnityEngine;
using UnityEngine.UIElements;
using TMPro;
using UnityEngine.SceneManagement;

// generator piece that players can pick up and carry
[RequireComponent(typeof(PhotonView))]
public class GeneratorPiece : MonoBehaviourPun
{
    [Header("Pickup")]
    public float pickupDistance = 2f; // proximity player needs to be to see prompt

    private SphereCollider pickupTrigger;
    private bool isPickedUp = false;
    private PlayerInventory carriedByPlayer;

    private void Start()
    {
        // Create trigger sphere for detection

        pickupTrigger = gameObject.AddComponent<SphereCollider>();
        pickupTrigger.radius = pickupDistance;
        pickupTrigger.isTrigger = true;

        // Keep the piece visible and disable physics if has rigidbody
        Rigidbody rb = GetComponent<Rigidbody>();

        if (rb != null)
        {
            rb.isKinematic = true;
        }
    }

    public void PickUp(PlayerInventory player)
    {
        if (isPickedUp)
        {
            return; // already picked up 
        }

        // INFORM ALL PLAYERS THIS PIECE WAS PICKED UP
        photonView.RPC(nameof(RPC_PickedUp), RpcTarget.All, player.photonView.ViewID);
    }

    [PunRPC]
    private void RPC_PickedUp(int playerViewID)
    {
        isPickedUp = true;

        //Hide the piece in the world 
        GetComponent<Renderer>().enabled = false;
        GetComponent<Collider>().enabled = false;
    }

    public void PlacedOnGenerator()
    {
        //Tell everyone this piece has been placed on the generator
        photonView.RPC(nameof(RPC_Destroyed), RpcTarget.All);
    }

    [PunRPC]
    private void RPC_Destroyed()
    {
        Destroy(gameObject);
    }
    
    public bool IsPickedUp()
    {
        return isPickedUp;
    }
}
