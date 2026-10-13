using System;
using System.Collections.Generic;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

// Existing scene and prefab references stay valid. Untick Is Generator Part for other pickups.
[RequireComponent(typeof(PhotonView))]
public class GeneratorPiece : MonoBehaviourPunCallbacks
{
    public float pickupDistance = 2f;
    [Header("Backpack")]
    public string displayName = "";
    [Min(1)] public int capacityCost = 3;
    public bool isGeneratorPart = true;
    public Sprite icon;

    private static readonly HashSet<GeneratorPiece> pieces = new HashSet<GeneratorPiece>();
    public static IReadOnlyCollection<GeneratorPiece> All => pieces;
    public static event Action Changed;
    public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? (isGeneratorPart ? "Generator part" : gameObject.name) : displayName;
    public int CapacityCost => Mathf.Max(1, capacityCost);
    public bool IsGeneratorPart => isGeneratorPart;
    public Sprite Icon => icon;
    public int HolderViewId { get; private set; }
    public bool IsInstalled { get; private set; }
    private int holderActor;
    private Renderer[] renderers;
    private Collider[] colliders;
    private bool[] rendererEnabled, colliderEnabled;
    private Vector3 pickupPosition;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { pieces.Clear(); Changed = null; }

    private void Awake()
    {
        renderers = GetComponentsInChildren<Renderer>(true);
        colliders = GetComponentsInChildren<Collider>(true);
        rendererEnabled = new bool[renderers.Length];
        colliderEnabled = new bool[colliders.Length];
        for (int i = 0; i < renderers.Length; i++) rendererEnabled[i] = renderers[i].enabled;
        for (int i = 0; i < colliders.Length; i++) colliderEnabled[i] = colliders[i].enabled;
        foreach (var body in GetComponentsInChildren<Rigidbody>(true)) body.isKinematic = true;
        pickupPosition = transform.position;
    }
    public override void OnEnable() { base.OnEnable(); pieces.Add(this); Changed?.Invoke(); }
    public override void OnDisable() { pieces.Remove(this); Changed?.Invoke(); base.OnDisable(); }
    public bool IsPickedUp() => HolderViewId != 0 || IsInstalled;
    public void PickUp(PlayerInventory player) { if (player != null) player.PickUpPiece(this); }
    private bool HasAuthority => !PhotonNetwork.InRoom || PhotonNetwork.IsMasterClient;

    public bool SetCarriedBy(int holderViewId)
    {
        if (!HasAuthority || IsPickedUp() || holderViewId == 0) return false;
        var holder = PhotonView.Find(holderViewId);
        if (holder == null || holder.GetComponent<PlayerInventory>() == null) return false;
        pickupPosition = transform.position;
        return Broadcast(holderViewId, false, transform.position, transform.rotation, holder.OwnerActorNr);
    }
    public bool DropAt(Vector3 position, Quaternion rotation)
    {
        if (!HasAuthority || IsInstalled || HolderViewId == 0) return false;
        return Broadcast(0, false, position, rotation, 0);
    }
    public bool Install()
    {
        if (!HasAuthority || IsInstalled || HolderViewId == 0 || !IsGeneratorPart) return false;
        return Broadcast(0, true, transform.position, transform.rotation, 0);
    }
    public void PlacedOnGenerator()
    {
        var inventory = PhotonView.Find(HolderViewId)?.GetComponent<PlayerInventory>();
        if (inventory != null && inventory.generator != null) inventory.TryInstallPiece(this, inventory.generator);
    }
    private bool Broadcast(int holder, bool installed, Vector3 position, Quaternion rotation, int actor)
    {
        if (PhotonNetwork.InRoom && photonView.ViewID != 0)
            photonView.RPC(nameof(ApplyInventoryState), RpcTarget.AllBuffered, holder, installed, position, rotation, actor);
        else ApplyState(holder, installed, position, rotation, actor);
        return true;
    }
    [PunRPC]
    private void ApplyInventoryState(int holder, bool installed, Vector3 position, Quaternion rotation, int actor, PhotonMessageInfo info)
    {
        // Buffered state may have been written by a previous master before a host migration.
        if (info.Sender == null || (!info.Sender.IsMasterClient && PhotonNetwork.CurrentRoom.GetPlayer(info.Sender.ActorNumber) != null)) return;
        ApplyState(holder, installed, position, rotation, actor);
    }
    private void ApplyState(int holder, bool installed, Vector3 position, Quaternion rotation, int actor)
    {
        if (holder != 0 && HolderViewId == 0) pickupPosition = position;
        HolderViewId = holder;
        IsInstalled = installed;
        holderActor = actor;
        transform.SetPositionAndRotation(position, rotation);
        bool visible = !IsPickedUp();
        for (int i = 0; i < renderers.Length; i++) if (renderers[i] != null) renderers[i].enabled = visible && rendererEnabled[i];
        for (int i = 0; i < colliders.Length; i++) if (colliders[i] != null) colliders[i].enabled = visible && colliderEnabled[i];
        Changed?.Invoke();
    }
    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        if (PhotonNetwork.IsMasterClient && holderActor == otherPlayer.ActorNumber) DropAt(pickupPosition, transform.rotation);
    }
    public override void OnMasterClientSwitched(Player newMasterClient)
    {
        if (PhotonNetwork.IsMasterClient && HolderViewId != 0 && PhotonNetwork.CurrentRoom.GetPlayer(holderActor) == null)
            DropAt(pickupPosition, transform.rotation);
    }
}
