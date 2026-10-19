using System.Collections.Generic;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using PlayerHealth = Forgotten.Player.PlayerHealthStateMachine;

// goes on the player prefab, lets the minimap find every player and know who is still alive
[RequireComponent(typeof(PhotonView))]
public class MinimapTarget : MonoBehaviour
{
    public const string AliveKey = "mmAlive";

    public static readonly List<MinimapTarget> All = new List<MinimapTarget>(); // every player currently in the scene
    public static MinimapTarget Local; // the copy that belongs to this client

    private PhotonView view;
    private PlayerController controller;
    private PlayerHealth health;
    private bool lastPublishedAlive = true;
    private bool hiddenAfterEscape;

    public int ActorNumber
    {
        get
        {
            Player owner = view.Owner;
            return owner != null ? owner.ActorNumber : 0;
        }
    }

    public bool IsMine => view.IsMine;
    public bool HasEscaped => controller != null && controller.HasEscaped; // players who reached the exit leave the map
    public bool IsDeadNow => health != null && health.IsDead; // reads the health script directly, only trustworthy for your own copy of the player

    public bool IsAlive // everyone counts as alive until the owner says otherwise
    {
        get
        {
            Player owner = view.Owner;
            if (owner == null) return true;

            object stored;
            bool found = owner.CustomProperties.TryGetValue(AliveKey, out stored);
            if (found && stored is bool)
            {
                return (bool)stored;
            }
            return true;
        }
    }

    private void Awake() // grabs the photon view, the player controller and the health script once so the getters stay cheap
    {
        view = GetComponent<PhotonView>();
        controller = GetComponent<PlayerController>();
        health = GetComponentInChildren<PlayerHealth>();
    }

    private void OnEnable() // registers this player in the shared list
    {
        All.Add(this);
    }

    private void Start() // ownership is only set once photon finished instantiating, so the local check waits until here
    {
        if (view.IsMine)
        {
            Local = this;
            SetLocalAlive(true); // fresh match, we start out alive
        }
    }

    private void Update() // hides anyone who escaped, and the owner shares dead or alive with everyone
    {
        if (HasEscaped && !hiddenAfterEscape) HideAfterEscape();

        if (!view.IsMine || health == null) return;

        bool aliveNow = !health.IsDead;
        if (aliveNow != lastPublishedAlive)
        {
            lastPublishedAlive = aliveNow;
            SetLocalAlive(aliveNow);
        }
    }

    private void HideAfterEscape() // an escaped player is gone from the game for everyone, so the model, lights and colliders switch off
    {
        hiddenAfterEscape = true;

        foreach (Renderer part in GetComponentsInChildren<Renderer>(true)) part.enabled = false;
        foreach (Light lamp in GetComponentsInChildren<Light>(true)) lamp.enabled = false;
        foreach (Collider body in GetComponentsInChildren<Collider>(true)) body.enabled = false;
    }

    private void OnDisable() // takes this player back out of the list
    {
        All.Remove(this);
        if (Local == this) Local = null;
    }

    // the update above calls this by itself from the health script, you can also call it by hand, it syncs through photon player properties
    public static void SetLocalAlive(bool alive)
    {
        if (!PhotonNetwork.InRoom) return;

        Hashtable props = new Hashtable();
        props[AliveKey] = alive;
        PhotonNetwork.LocalPlayer.SetCustomProperties(props);
    }
}