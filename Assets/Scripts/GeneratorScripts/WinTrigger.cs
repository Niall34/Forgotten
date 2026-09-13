using Photon.Pun;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(SphereCollider))]
public class WinTrigger : MonoBehaviourPun
{
    [Header("Win")]
    public string winSceneName = "WinScreen";
    public Vector3 teleportPosition = Vector3.zero;
    
    private bool isEnabled = false;

    private void Start()
    {
        SphereCollider trigger = GetComponent<SphereCollider>();
        trigger.isTrigger = true;
        trigger.radius = 2f;

        gameObject.layer = LayerMask.NameToLayer("Default");
    }

    public void Enable()
    {
        isEnabled = true;
        GetComponent<SphereCollider>().enabled = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!isEnabled)
        {
            return;
        }

        PlayerController player = other.GetComponent<PlayerController>();
        if (player == null)
        {
            return;
        }

        // Only trigger once per player
        if (player.photonView.IsMine)
        {
            // notify all players, player has reached win trigger
            photonView.RPC(nameof(RPC_PlayerWon), RpcTarget.All, player.photonView.ViewID);
        }
    }

    [PunRPC]
    private void RPC_PlayerWon(int playerViewID)
    {
        PhotonView pv = PhotonView.Find(playerViewID);
        if (pv == null)
        {
            return;
        }

        PlayerController player = pv.GetComponent<PlayerController>();
        if (player == null)
        {
            return;
        }

        // Check if this is the local player
        if (pv.IsMine)
        {
            // load win scene
            if (!string.IsNullOrEmpty(winSceneName))
            {
                SceneManager.LoadScene(winSceneName);
            }
            else
            {
                player.transform.position = teleportPosition;

            }
        }
    }
}
