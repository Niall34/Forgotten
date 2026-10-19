using System.Collections.Generic;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using UnityEngine.UI;

// shows a win or lose screen on top of the game once everyone has died or escaped, then sends the players back to the lobby to play again
public class GameEndScreen : MonoBehaviour, IOnEventCallback
{
    private enum Phase { Playing, Waiting, Ending }

    private const byte ReadyToReturnEvent = 72; // a guest tells the host they are ready to go back, 71 is already the chat

    [Header("Screens")]
    public GameObject winPanel; // full screen panel for players who escaped, starts off
    public GameObject losePanel; // full screen panel for players who did not make it, starts off
    public Button[] exitButtons; // an exit button on each panel, only shows once the match is over, goes back to your lobby or to the main menu when solo

    [Header("Timing")]
    public float returnDelay = 6f; // seconds the screen stays up once every player is finished, the exit button skips the wait
    public float guestFailsafe = 15f; // guests wait this long after the delay for the host to pull them back, then leave on their own

    private Phase phase = Phase.Playing;
    private float checkTimer;
    private float endTimer;
    private float failsafeTimer;
    private bool handedOver;
    private bool exitPressed;
    private bool skipWait;
    private readonly HashSet<int> readyGuests = new HashSet<int>(); // actor numbers of guests who pressed exit, only the host keeps track

    private void OnEnable() // starts listening for guests who press exit
    {
        PhotonNetwork.AddCallbackTarget(this);
    }

    private void OnDisable() // stops listening
    {
        PhotonNetwork.RemoveCallbackTarget(this);
    }

    private void Start() // skips other players' copies of this canvas, hides both screens and hooks up the exit buttons
    {
        // if this lives inside a player prefab only the owner's copy should run, ownership is only known by start
        PhotonView ownerView = GetComponentInParent<PhotonView>();
        if (ownerView != null && !ownerView.IsMine)
        {
            HidePanels();
            enabled = false;
            return;
        }

        foreach (Button exitButton in exitButtons)
        {
            if (exitButton != null) exitButton.onClick.AddListener(OnExitPressed);
        }

        HidePanels();
        SetExitButtons(false);
    }

    public void OnEvent(EventData data) // the host hears here when a guest is ready to go back to the lobby
    {
        if (data.Code != ReadyToReturnEvent) return;
        if (!PhotonNetwork.IsMasterClient) return;

        readyGuests.Add(data.Sender);
    }

    private void Update() // watches for your own escape, then for the whole match to be over
    {
        MinimapTarget me = MinimapTarget.Local;
        if (me == null) return;

        // you got out before everyone else, so you get the win screen straight away and wait for the rest
        if (phase == Phase.Playing && me.HasEscaped)
        {
            phase = Phase.Waiting;
            FreezeMovement(me);
            ShowPanel(true);
        }

        if (phase != Phase.Ending)
        {
            checkTimer -= Time.unscaledDeltaTime;
            if (checkTimer <= 0f)
            {
                checkTimer = 0.25f; // no need to count the players every frame
                if (EveryoneFinished()) BeginEnding(me);
            }
        }

        if (phase == Phase.Ending) TickEnding();
    }

    private bool EveryoneFinished() // true when every player in the room has either escaped or died
    {
        if (!PhotonNetwork.InRoom) return false;

        int expected = PhotonNetwork.CurrentRoom.PlayerCount;
        if (MinimapTarget.All.Count < expected) return false; // somebody's character has not spawned yet

        foreach (MinimapTarget player in MinimapTarget.All)
        {
            if (player == null) continue;
            if (player.IsAlive && !player.HasEscaped) return false;
        }
        return true;
    }

    private void BeginEnding(MinimapTarget me) // picks the right screen for you, starts the wait and lets you press exit
    {
        phase = Phase.Ending;
        endTimer = returnDelay;
        failsafeTimer = guestFailsafe;

        bool won = me.HasEscaped;
        if (won) FreezeMovement(me);
        ShowPanel(won);
        SetExitButtons(true);
    }

    private void TickEnding() // waits out the delay, then the host (or you when solo) sends everyone back
    {
        endTimer -= Time.unscaledDeltaTime;
        if (handedOver) return;

        NetworkManager manager = NetworkManager.Instance;
        if (manager == null) return;

        // for the host this returns the whole room to the lobby, solo players land on the main menu
        if (PhotonNetwork.IsMasterClient)
        {
            bool everyoneReady = skipWait || AllGuestsReady();
            if (endTimer > 0f && !everyoneReady) return;

            handedOver = true;
            manager.QuitToLobby();
            return;
        }

        // guests wait for the host to move the room, and only leave on their own if that never happens
        if (endTimer > 0f) return;

        failsafeTimer -= Time.unscaledDeltaTime;
        if (failsafeTimer <= 0f)
        {
            handedOver = true;
            manager.QuitToLobby();
        }
    }

    private bool AllGuestsReady() // true when there are guests and every one of them has pressed exit
    {
        Player[] others = PhotonNetwork.PlayerListOthers;
        if (others.Length == 0) return false;

        foreach (Player other in others)
        {
            if (!readyGuests.Contains(other.ActorNumber)) return false;
        }
        return true;
    }

    private void OnExitPressed() // the host and solo players go back at once, a guest tells the host they are ready
    {
        if (phase != Phase.Ending || exitPressed) return;

        exitPressed = true;
        SetExitButtons(false);

        if (PhotonNetwork.IsMasterClient)
        {
            skipWait = true; // the next tick moves the room, or takes a solo player to the main menu
            return;
        }

        RaiseEventOptions options = new RaiseEventOptions { Receivers = ReceiverGroup.MasterClient };
        PhotonNetwork.RaiseEvent(ReadyToReturnEvent, true, options, SendOptions.SendReliable);
    }

    private void FreezeMovement(MinimapTarget me) // stops an escaped player walking around behind the win screen
    {
        PlayerController controller = me.GetComponent<PlayerController>();
        if (controller != null) controller.enabled = false;
    }

    private void ShowPanel(bool win) // switches between the two screens
    {
        if (winPanel != null) winPanel.SetActive(win);
        if (losePanel != null) losePanel.SetActive(!win);
    }

    private void SetExitButtons(bool visible) // the buttons only show once everyone is finished, before that nobody can be moved
    {
        foreach (Button exitButton in exitButtons)
        {
            if (exitButton != null) exitButton.gameObject.SetActive(visible);
        }
    }

    private void HidePanels()
    {
        if (winPanel != null) winPanel.SetActive(false);
        if (losePanel != null) losePanel.SetActive(false);
    }
}