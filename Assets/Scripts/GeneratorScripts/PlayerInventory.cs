using Photon.Pun;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;

// Debug version - handles touch button interactions for picking up and installing pieces
[RequireComponent(typeof(PhotonView))]
public class PlayerInventory : MonoBehaviourPun
{
    [Header("Inventory")]
    public float speedPenalty = 0.5f; // multiply move speed by this when holding piece (0.5 = half speed)
 
    [Header("Installation")]
    public GeneratorAssembly generator;
    public float installHoldDuration = 10f;
 
    private GeneratorPiece heldPiece;
    private PlayerController playerController;
    private float installHoldTimer = 0f;
    private bool isInstallingPiece = false;
    private UIPrompt uiPrompt;

    private Button pickupButton;
    private Button installButton;
    private Text installProgressText;
 
    private void Start()
    {
        playerController = GetComponent<PlayerController>();
        uiPrompt = GetComponent<UIPrompt>();
 
        if (uiPrompt == null)
        {
            uiPrompt = gameObject.AddComponent<UIPrompt>();
        }
    }

    private void Update()
    {
        if (photonView.IsMine == false)
        {
            return;
        }
 
        CheckForNearbyPieces();
        HandleGeneratorInteraction();
        UpdateMovementSpeed();
    }
 
    private void CheckForNearbyPieces()
{
    GeneratorPiece[] allPieces = FindObjectsOfType<GeneratorPiece>();

    GeneratorPiece closestPiece = null;
    float closestDistance = 3f;

    foreach (GeneratorPiece piece in allPieces)
    {
        if (piece.IsPickedUp())
        {
            continue;
        }

        float distance = Vector3.Distance(transform.position, piece.transform.position);
        if (distance < closestDistance)
        {
            closestPiece = piece;
            closestDistance = distance;
        }
    }

    if (closestPiece != null && heldPiece == null)
    {
        uiPrompt.ShowPickupPrompt(closestPiece, this);
    }
    else if (heldPiece != null && generator != null)
    {
        
        if (generator.CanInstallPiece(this))
        {
            uiPrompt.ShowInstallPrompt(this);
        }
        else
        {
            uiPrompt.HideInstallPrompt();
        }
    }
    else
    {
        uiPrompt.HidePickupPrompt();
        uiPrompt.HideInstallPrompt();
    }
}
    private void HandleGeneratorInteraction()
    {
        if (heldPiece == null || generator == null)
        {
            installHoldTimer = 0f;
            isInstallingPiece = false;
            return;
        }
 
        if (!generator.CanInstallPiece(this))
        {
            installHoldTimer = 0f;
            isInstallingPiece = false;
            return;
        }
 
        // This is now controlled by the Install button
        // The button callback will handle incrementing the timer
    }
 
    // Called by the Install button when held down
    public void OnInstallButtonDown()
    {
        if (heldPiece == null || generator == null || !generator.CanInstallPiece(this))
        {
            return;
        }
 
        if (!isInstallingPiece)
        {
            isInstallingPiece = true;
            installHoldTimer = 0f;
        }
    }
 
    // Called every frame while install button is held (by UIPrompt)
    public void UpdateInstallProgress()
    {
        if (!isInstallingPiece || heldPiece == null)
        {
            return;
        }
 
        installHoldTimer += Time.deltaTime;
 
        // Update UI with progress
        float progress = installHoldTimer / installHoldDuration;
        uiPrompt.UpdateInstallProgress(progress);
 
        if (installHoldTimer >= installHoldDuration)
        {
            // Piece installed!
            generator.InstallPiece(heldPiece);
            heldPiece = null;
            installHoldTimer = 0f;
            isInstallingPiece = false;
            uiPrompt.ShowMessage("Piece Installed!", UIPrompt.PromptType.Success);
        }
    }
 
    // Called when Install button is released
    public void OnInstallButtonUp()
    {
        if (isInstallingPiece)
        {
            isInstallingPiece = false;
            installHoldTimer = 0f;
            uiPrompt.UpdateInstallProgress(0f);
        }
    }
 
    public void PickUpPiece(GeneratorPiece piece)
    {
        heldPiece = piece;
        piece.PickUp(this);
        uiPrompt.ShowMessage("Piece Picked Up!", UIPrompt.PromptType.Success);
        uiPrompt.HidePickupPrompt();
    }
 
    private void UpdateMovementSpeed()
    {
        if (playerController == null)
        {
            return;
        }
 
        if (heldPiece != null)
        {
            // Apply speed penalty when holding a piece
            playerController.moveSpeed = 4.5f * speedPenalty;
            playerController.sprintSpeed = 7.5f * speedPenalty;
        }
        else
        {
            // Reset to normal speed
            playerController.moveSpeed = 4.5f;
            playerController.sprintSpeed = 7.5f;
        }
    }
 
    public GeneratorPiece GetHeldPiece()
    {
        return heldPiece;
    }
 
    public bool IsInstallingPiece()
    {
        return isInstallingPiece;
    }
}
 