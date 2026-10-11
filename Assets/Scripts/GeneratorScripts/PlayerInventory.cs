using System;
using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;
using UnityEngine.EventSystems;
using TMPro;
using Forgotten.Player;

// Item holders are replicated by the master; the bag is a view of that shared state.
[RequireComponent(typeof(PhotonView))]
public class PlayerInventory : MonoBehaviourPun
{
    public const int BackpackCapacity = 8;
    public const int QuickSlotCount = 4;
    public float speedPenalty = 0.5f;
    public GeneratorAssembly generator;
    public float installHoldDuration = 10f;

    private readonly List<GeneratorPiece> items = new List<GeneratorPiece>();
    private PlayerController playerController;
    private PlayerHealthStateMachine health;
    private float baseMoveSpeed, baseSprintSpeed, nextScan, installHoldTimer;
    private int selectedIndex = -1, lastProgressFrame = -1;
    private bool installing, deathReleased;
    private CursorLockMode previousCursorLock;
    private bool previousCursorVisible;
    private int authorityInstallPiece, authorityInstallGenerator;
    private double authorityInstallStart;
    private GeneratorPiece installingPiece;

    public IReadOnlyList<GeneratorPiece> Items => items;
    public int Capacity => BackpackCapacity;
    public int UsedCapacity { get; private set; }
    public int SelectedIndex => selectedIndex;
    public GeneratorPiece SelectedItem => selectedIndex >= 0 && selectedIndex < items.Count ? items[selectedIndex] : null;
    public GeneratorPiece NearbyItem { get; private set; }
    public bool IsBackpackOpen { get; private set; }
    public float InstallProgress => Mathf.Clamp01(installHoldTimer / Mathf.Max(0.05f, installHoldDuration));
    public bool CanInteract => photonView.IsMine && CanAct && Time.timeScale > 0 && !RoomChatUI.CapturesInput;
    public bool CanInstall => CanInteract && !IsBackpackOpen && generator != null && generator.CanInstallPiece(this);
    private bool CanAct => isActiveAndEnabled && (health == null || !health.IsDead) && (playerController == null || !playerController.HasEscaped);
    public event Action Changed;
    public event Action<string> Message;

    private void Awake()
    {
        playerController = GetComponent<PlayerController>();
        health = GetComponent<PlayerHealthStateMachine>();
    }
    private void OnEnable() => GeneratorPiece.Changed += RefreshItems;
    private void Start()
    {
        RefreshItems();
        if (!photonView.IsMine) return;
        if (playerController != null)
        {
            baseMoveSpeed = playerController.moveSpeed;
            baseSprintSpeed = playerController.sprintSpeed;
        }
        if (generator == null) generator = FindAnyObjectByType<GeneratorAssembly>();
        gameObject.AddComponent<PlayerInventoryUI>().Initialize(this);
        if (GetComponent<UIPrompt>() == null) gameObject.AddComponent<UIPrompt>();
    }
    private void OnDisable()
    {
        GeneratorPiece.Changed -= RefreshItems;
        SetBackpackOpen(false);
        OnInstallButtonUp();
        if (photonView.IsMine && playerController != null && baseMoveSpeed > 0)
        {
            playerController.moveSpeed = baseMoveSpeed;
            playerController.sprintSpeed = baseSprintSpeed;
        }
    }
    private void Update()
    {
        if (PhotonNetwork.IsMasterClient && authorityInstallGenerator != 0)
        {
            var assembly = PhotonView.Find(authorityInstallGenerator)?.GetComponent<GeneratorAssembly>();
            if (!CanAct || assembly == null || Vector3.Distance(transform.position, assembly.transform.position) > assembly.interactionDistance + 0.25f)
                ClearAuthorityInstall();
        }
        if (!photonView.IsMine) return;
        if (!CanAct)
        {
            SetBackpackOpen(false);
            OnInstallButtonUp();
            if (!deathReleased && PhotonNetwork.InRoom)
            {
                deathReleased = true;
                photonView.RPC(nameof(RequestReleaseAll), RpcTarget.MasterClient);
            }
            return;
        }
        if (!CanInteract) { OnInstallButtonUp(); return; }
        bool typing = EventSystem.current != null && EventSystem.current.currentSelectedGameObject != null &&
            EventSystem.current.currentSelectedGameObject.GetComponent<TMP_InputField>() != null;
        if (!typing)
        {
            if (Input.GetKeyDown(KeyCode.B) || Input.GetKeyDown(KeyCode.Tab)) SetBackpackOpen(!IsBackpackOpen);
            for (int i = 0; i < QuickSlotCount; i++)
                if (Input.GetKeyDown((KeyCode)((int)KeyCode.Alpha1 + i))) SelectSlot(i);
            if (Input.GetKeyDown(KeyCode.G)) DropSelected();
            if (!IsBackpackOpen && (Input.GetKeyDown(KeyCode.P) || Input.GetKeyDown(KeyCode.E))) TryPickUpNearestPiece();
            if (Input.GetKeyDown(KeyCode.I)) OnInstallButtonDown();
            if (Input.GetKeyUp(KeyCode.I)) OnInstallButtonUp();
        }
        if (Time.unscaledTime >= nextScan)
        {
            nextScan = Time.unscaledTime + 0.15f;
            ScanNearby();
            Changed?.Invoke();
        }
        if (installing) UpdateInstallProgress();
        if (playerController != null)
        {
            float multiplier = items.Count > 0 ? Mathf.Clamp(speedPenalty, 0.1f, 1f) : 1f;
            playerController.moveSpeed = baseMoveSpeed * multiplier;
            playerController.sprintSpeed = baseSprintSpeed * multiplier;
        }
    }
    public void RefreshItems()
    {
        GeneratorPiece previous = SelectedItem;
        items.RemoveAll(p => p == null || p.IsInstalled || p.HolderViewId != photonView.ViewID);
        if (photonView.ViewID != 0)
            foreach (GeneratorPiece piece in GeneratorPiece.All)
                if (piece != null && !piece.IsInstalled && piece.HolderViewId == photonView.ViewID && !items.Contains(piece)) items.Add(piece);
        UsedCapacity = 0;
        foreach (GeneratorPiece item in items) UsedCapacity += item.CapacityCost;
        selectedIndex = previous != null && items.Contains(previous) ? items.IndexOf(previous) : Mathf.Clamp(selectedIndex, 0, items.Count - 1);
        if (items.Count == 0) selectedIndex = -1;
        Changed?.Invoke();
    }
    private void ScanNearby()
    {
        NearbyItem = null;
        float closest = float.PositiveInfinity;
        foreach (GeneratorPiece piece in GeneratorPiece.All)
        {
            if (piece == null || piece.IsPickedUp()) continue;
            float distance = Vector3.Distance(transform.position, piece.transform.position);
            if (distance <= piece.pickupDistance && distance < closest) { closest = distance; NearbyItem = piece; }
        }
    }
    public void SelectSlot(int index)
    {
        if (!CanInteract || index < 0 || index >= items.Count) return;
        OnInstallButtonUp();
        selectedIndex = index;
        Changed?.Invoke();
    }
    public void SetBackpackOpen(bool open)
    {
        if ((open && !CanInteract) || IsBackpackOpen == open) return;
        OnInstallButtonUp();
        IsBackpackOpen = open;
        if (open)
        {
            previousCursorLock = Cursor.lockState;
            previousCursorVisible = Cursor.visible;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        else { Cursor.lockState = previousCursorLock; Cursor.visible = previousCursorVisible; }
        Changed?.Invoke();
    }
    public void TryPickUpNearestPiece()
    {
        ScanNearby();
        if (NearbyItem != null) PickUpPiece(NearbyItem);
    }
    public void PickUpPiece(GeneratorPiece piece)
    {
        if (!CanInteract || IsBackpackOpen || piece == null || piece.IsPickedUp()) return;
        if (Vector3.Distance(transform.position, piece.transform.position) > piece.pickupDistance) { ShowMessage("Move closer to pick up."); return; }
        if (UsedCapacity + piece.CapacityCost > Capacity) { ShowMessage("Backpack full — drop an item first."); return; }
        if (!PhotonNetwork.InRoom || photonView.ViewID == 0 || piece.photonView.ViewID == 0) { ShowMessage("This item needs a registered Photon View."); return; }
        photonView.RPC(nameof(RequestPickup), RpcTarget.MasterClient, piece.photonView.ViewID);
    }
    private bool Authorized(PhotonMessageInfo info) => PhotonNetwork.IsMasterClient && info.Sender != null && info.Sender == photonView.Owner;
    [PunRPC]
    private void RequestPickup(int pieceId, PhotonMessageInfo info)
    {
        if (!Authorized(info) || !CanAct) return;
        var piece = PhotonView.Find(pieceId)?.GetComponent<GeneratorPiece>();
        RefreshItems();
        if (piece == null || piece.IsPickedUp() || Vector3.Distance(transform.position, piece.transform.position) > piece.pickupDistance + 0.25f) return;
        if (UsedCapacity + piece.CapacityCost > Capacity) { NotifyOwner("Backpack full — drop an item first."); return; }
        if (piece.SetCarriedBy(photonView.ViewID)) NotifyOwner("Picked up " + piece.DisplayName + " (" + piece.CapacityCost + " capacity).");
    }
    public void DropSelected()
    {
        if (!CanInteract || SelectedItem == null || !PhotonNetwork.InRoom) return;
        OnInstallButtonUp();
        photonView.RPC(nameof(RequestDrop), RpcTarget.MasterClient, SelectedItem.photonView.ViewID);
    }
    [PunRPC]
    private void RequestDrop(int pieceId, PhotonMessageInfo info)
    {
        if (!Authorized(info) || !CanAct) return;
        var piece = PhotonView.Find(pieceId)?.GetComponent<GeneratorPiece>();
        if (piece == null || piece.HolderViewId != photonView.ViewID || piece.IsInstalled) return;
        ClearAuthorityInstall();
        if (DropItemInWorld(piece)) NotifyOwner("Dropped " + piece.DisplayName + ".");
    }
    private bool DropItemInWorld(GeneratorPiece piece)
    {
        Vector3 origin = transform.position + Vector3.up;
        Vector3 target = origin + transform.forward * 1.2f;
        foreach (RaycastHit hit in Physics.RaycastAll(origin, transform.forward, 1.2f, ~0, QueryTriggerInteraction.Ignore))
            if (!hit.transform.IsChildOf(transform) && hit.distance < Vector3.Distance(origin, target)) target = origin + transform.forward * Mathf.Max(0, hit.distance - 0.25f);
        float floorDistance = float.PositiveInfinity;
        foreach (RaycastHit hit in Physics.RaycastAll(target, Vector3.down, 4f, ~0, QueryTriggerInteraction.Ignore))
            if (!hit.transform.IsChildOf(transform) && hit.distance < floorDistance) floorDistance = hit.distance;
        if (!float.IsPositiveInfinity(floorDistance)) target += Vector3.down * Mathf.Max(0, floorDistance - 0.15f);
        return piece.DropAt(target, Quaternion.identity);
    }
    [PunRPC]
    private void RequestReleaseAll(PhotonMessageInfo info)
    {
        if (!Authorized(info)) return;
        ClearAuthorityInstall();
        RefreshItems();
        foreach (var piece in items.ToArray()) DropItemInWorld(piece);
    }
    public GeneratorPiece GetHeldPiece() => SelectedItem != null && SelectedItem.IsGeneratorPart ? SelectedItem : null;
    public bool IsInstallingPiece() => installing;
    public void OnInstallButtonDown()
    {
        if (!CanInstall || installing) return;
        installing = true;
        installingPiece = GetHeldPiece();
        installHoldTimer = 0;
        photonView.RPC(nameof(RequestBeginInstall), RpcTarget.MasterClient, installingPiece.photonView.ViewID, generator.photonView.ViewID);
    }
    public void UpdateInstallProgress()
    {
        if (lastProgressFrame == Time.frameCount) return;
        lastProgressFrame = Time.frameCount;
        if (!installing) return;
        if (!CanInstall || GetHeldPiece() != installingPiece) { OnInstallButtonUp(); return; }
        installHoldTimer += Time.deltaTime;
        Changed?.Invoke();
        if (installHoldTimer >= Mathf.Max(0.05f, installHoldDuration))
        {
            installing = false;
            TryInstallPiece(installingPiece, generator);
            installHoldTimer = 0;
            installingPiece = null;
        }
    }
    public void OnInstallButtonUp()
    {
        if (!installing && installHoldTimer == 0) return;
        installing = false;
        installHoldTimer = 0;
        installingPiece = null;
        if (photonView.IsMine && PhotonNetwork.InRoom) photonView.RPC(nameof(RequestCancelInstall), RpcTarget.MasterClient);
        Changed?.Invoke();
    }
    public bool TryInstallPiece(GeneratorPiece piece, GeneratorAssembly assembly)
    {
        if (!CanInteract || IsBackpackOpen || piece == null || assembly == null || piece.HolderViewId != photonView.ViewID || !PhotonNetwork.InRoom) return false;
        photonView.RPC(nameof(RequestFinishInstall), RpcTarget.MasterClient, piece.photonView.ViewID, assembly.photonView.ViewID);
        return true;
    }
    [PunRPC]
    private void RequestBeginInstall(int pieceId, int generatorId, PhotonMessageInfo info)
    {
        if (!Authorized(info) || !CanAct) return;
        var piece = PhotonView.Find(pieceId)?.GetComponent<GeneratorPiece>();
        var assembly = PhotonView.Find(generatorId)?.GetComponent<GeneratorAssembly>();
        if (piece == null || !piece.IsGeneratorPart || piece.HolderViewId != photonView.ViewID || assembly == null ||
            Vector3.Distance(transform.position, assembly.transform.position) > assembly.interactionDistance + 0.25f) return;
        authorityInstallPiece = pieceId;
        authorityInstallGenerator = generatorId;
        authorityInstallStart = PhotonNetwork.Time;
    }
    [PunRPC]
    private void RequestCancelInstall(PhotonMessageInfo info) { if (Authorized(info)) ClearAuthorityInstall(); }
    [PunRPC]
    private void RequestFinishInstall(int pieceId, int generatorId, PhotonMessageInfo info)
    {
        if (!Authorized(info) || !CanAct || authorityInstallPiece != pieceId || authorityInstallGenerator != generatorId ||
            PhotonNetwork.Time - authorityInstallStart < Mathf.Max(0.05f, installHoldDuration) - 0.04f) return;
        var piece = PhotonView.Find(pieceId)?.GetComponent<GeneratorPiece>();
        var assembly = PhotonView.Find(generatorId)?.GetComponent<GeneratorAssembly>();
        ClearAuthorityInstall();
        if (assembly != null && assembly.TryInstallAuthoritative(piece, this)) NotifyOwner("Generator part installed.");
    }
    private void ClearAuthorityInstall() { authorityInstallPiece = 0; authorityInstallGenerator = 0; authorityInstallStart = 0; }
    private void NotifyOwner(string text) => photonView.RPC(nameof(ReceiveMessage), photonView.Owner, text);
    [PunRPC]
    private void ReceiveMessage(string text, PhotonMessageInfo info)
    {
        if (info.Sender != null && info.Sender.IsMasterClient && photonView.IsMine) ShowMessage(text);
    }
    public void ShowMessage(string text) => Message?.Invoke(text);
}
