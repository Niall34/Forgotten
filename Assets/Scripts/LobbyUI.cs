using Photon.Pun;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;


// controls the whole 2D menu, entering your name, then hosting/joining/playing solo and
// the ready up screens once you're in a room

public class LobbyUI : MonoBehaviour
{
    private const string NicknameKey = "Nickname";

    [Header("Panels")]
    public GameObject namePromptPanel;
    public GameObject mainLobbyPanel;
    public GameObject joiningLobbyPanel;
    public GameObject joinedLobbyPanel;
    public GameObject hostingLobbyPanel;

    [Header("Staging")]
    public LobbyStage lobbyStage;

    [Header("Intro Panel")]
    public TMP_InputField nameField;
    public Button continueButton;

    [Header("Main Lobby Panel")]
    public Button playButton;
    public Button hostButton;
    public Button joinOpenButton;

    [Header("Joining Lobby Panel")]
    public TMP_InputField joinCodeField;
    public Button joinConfirmButton;
    public Button joinCancelButton;

    [Header("Joined Lobby Panel (guest)")]
    public Button readyButton;
    public TextMeshProUGUI readyButtonLabel;
    public Button guestLeaveButton;

    [Header("Hosting Lobby Panel (host)")]
    public TextMeshProUGUI hostedCodeText;
    public Button startButton;
    public Button hostLeaveButton;

    [Header("Shared")]
    public TextMeshProUGUI statusText;

    private Canvas canvas;
    private NetworkManager net;
    private string storedNickname = "";

    // if the host or join button is tapped before fully connected, remember what to do and carry it out automatically once the connection finishes
    private bool wantsToHostAfterConnecting = false;
    private string codeToJoinAfterConnecting = "";
    private bool wantsToPlaySoloAfterConnecting = false;

    private bool localReady = false;

    // values that are remembered checked against the network manager's current values every
    // frame in "Update()" to detect when something has changed
    private bool wasInLobby = false;
    private bool wasInRoom = false;
    private int lastSeenErrorVersion = 0;
    private int lastSeenPlayerListVersion = -1;
    private bool handledMatchStarting = false;


    private void Awake() // wires up every button, loads the saved name, and shows the right starting panel
    {
        EnsureEventSystem();
        DontDestroyOnLoad(gameObject);

        canvas = GetComponent<Canvas>();
        net = NetworkManager.Bootstrap();

        continueButton.onClick.AddListener(OnNameContinueClicked);
        playButton.onClick.AddListener(OnPlayClicked);
        hostButton.onClick.AddListener(OnHostClicked);
        joinOpenButton.onClick.AddListener(OnJoinOpenClicked);
        joinConfirmButton.onClick.AddListener(OnJoinConfirmClicked);
        joinCancelButton.onClick.AddListener(OnJoinCancelClicked);
        readyButton.onClick.AddListener(OnReadyClicked);
        guestLeaveButton.onClick.AddListener(OnLeaveLobbyClicked);
        startButton.onClick.AddListener(OnStartClicked);
        hostLeaveButton.onClick.AddListener(OnLeaveLobbyClicked);

        joinCodeField.characterLimit = 6;
        joinCodeField.onValueChanged.AddListener(HandleJoinCodeTyped);

        storedNickname = PlayerPrefs.GetString(NicknameKey, "");
        bool alreadyHaveName = storedNickname != "";

        if (alreadyHaveName)
        {
            ShowPanel(mainLobbyPanel);
            if (lobbyStage != null)
            {
                lobbyStage.SpawnPreview(storedNickname);
            }
        }
        else
        {
            ShowPanel(namePromptPanel);
        }
    }

    private void Update() //checks the network manager variables and compares them
    {
        CheckForNewError();
        CheckForJoinedLobby();
        CheckForJoinedOrLeftRoom();
        CheckForPlayerListChange();
        CheckForMatchStarting();
    }

    private void CheckForNewError() // shows the latest error message from the network manager, if it's new
    {
        if (net.ErrorVersion != lastSeenErrorVersion)
        {
            lastSeenErrorVersion = net.ErrorVersion;
            wantsToHostAfterConnecting = false;
            codeToJoinAfterConnecting = "";
            wantsToPlaySoloAfterConnecting = false;
            SetStatus(net.ErrorMessage);
        }
    }

    private void CheckForJoinedLobby() // detects finishing the connection, then runs any pending host/join/play
    {
        bool isInLobbyNow = net.InLobby;
        if (isInLobbyNow && wasInLobby == false)
        {
            SetStatus("");

            // carry out whatever action was waiting on the connection to finish
            if (wantsToHostAfterConnecting)
            {
                wantsToHostAfterConnecting = false;
                DoHost();
            }
            else if (codeToJoinAfterConnecting != "")
            {
                string code = codeToJoinAfterConnecting;
                codeToJoinAfterConnecting = "";
                SetStatus("Joining...");
                net.JoinRoomByCode(code);
            }
            else if (wantsToPlaySoloAfterConnecting)
            {
                wantsToPlaySoloAfterConnecting = false;
                DoPlaySolo();
            }
        }
        wasInLobby = isInLobbyNow;
    }

    private void CheckForJoinedOrLeftRoom() // detects entering or leaving a room and reacts to either
    {
        bool isInRoomNow = net.InRoom;

        if (isInRoomNow && wasInRoom == false)
        {
            HandleJustJoinedRoom();
        }
        else if (isInRoomNow == false && wasInRoom)
        {
            HandleJustLeftRoom();
        }

        wasInRoom = isInRoomNow;
    }

    private void HandleJustJoinedRoom()
    {
        // solo games skip the lobby screens entirely and go straight to gameplay
        if (net.IsSolo)
        {
            canvas.enabled = false;
            return;
        }

        SetStatus("");
        localReady = false;
        canvas.enabled = true;

        if (net.IsMasterClient)
        {
            ShowPanel(hostingLobbyPanel);
        }
        else
        {
            ShowPanel(joinedLobbyPanel);
        }

        if (hostedCodeText != null)
        {
            hostedCodeText.text = net.RoomCode;
        }

        RefreshReadyLabel();
    }

    private void HandleJustLeftRoom() // goes back to the code-entry panel after leaving a room
    {
        SetStatus("");
        canvas.enabled = true;
        ShowPanel(joiningLobbyPanel);
    }

    private void CheckForPlayerListChange() // refreshes the ready label whenever the player list changes
    {
        if (net.PlayerListVersion != lastSeenPlayerListVersion)
        {
            lastSeenPlayerListVersion = net.PlayerListVersion;
            RefreshReadyLabel();
        }
    }

    private void RefreshReadyLabel() // updates the ready button's text to match the current ready state
    {
        if (net.InRoom == false || net.IsSolo)
        {
            return;
        }

        if (net.IsMasterClient == false)
        {
            localReady = net.IsPlayerReady(PhotonNetwork.LocalPlayer);
            if (localReady)
            {
                readyButtonLabel.text = "UNREADY";
            }
            else
            {
                readyButtonLabel.text = "READY UP";
            }
        }
    }

    private void CheckForMatchStarting() // hides the whole menu once the match starts
    {
        if (net.MatchStarting && handledMatchStarting == false)
        {
            handledMatchStarting = true;
            SetStatus("Starting...");
            canvas.enabled = false;
        }
    }

    private void HandleJoinCodeTyped(string typedValue) // forces the join-code field to stay uppercase as you type
    {
        string upperCaseValue = typedValue.ToUpper();
        if (upperCaseValue != typedValue)
        {
            joinCodeField.text = upperCaseValue;
        }
    }

    // button onclick scripts

    private void OnNameContinueClicked() // saves the typed name and moves to Main Lobby
    {
        string typedName = nameField.text.Trim();
        if (typedName == "")
        {
            SetStatus("Enter a name first");
            return;
        }

        storedNickname = typedName;
        PlayerPrefs.SetString(NicknameKey, typedName);
        PlayerPrefs.Save();
        SetStatus("");
        ShowPanel(mainLobbyPanel);

        if (lobbyStage != null)
        {
            lobbyStage.SpawnPreview(storedNickname);
        }
    }

    private void OnPlayClicked() // connects (if needed) then starts a solo game
    {
        if (net.IsConnected)
        {
            DoPlaySolo();
        }
        else
        {
            wantsToPlaySoloAfterConnecting = true;
            SetStatus("Connecting...");
            net.Connect(storedNickname);
        }
    }

    private void DoPlaySolo() // actually kicks off the solo room, once we know we're connected
    {
        SetStatus("Starting...");
        net.PlaySolo();
    }

    private void OnHostClicked() // connects (if needed) then hosts a room
    {
        if (net.IsConnected)
        {
            DoHost();
        }
        else
        {
            wantsToHostAfterConnecting = true;
            SetStatus("Connecting...");
            net.Connect(storedNickname);
        }
    }

    private void DoHost() // creates the room and shows the code
    {
        string code = net.HostRoom();
        if (hostedCodeText != null)
        {
            hostedCodeText.text = code;
        }
        SetStatus("Creating room...");
    }

    private void OnJoinOpenClicked() // opens the join-code entry panel
    {
        joinCodeField.text = "";
        SetStatus("");
        ShowPanel(joiningLobbyPanel);
    }

    private void OnJoinConfirmClicked() // connects (if needed) then joins the typed room code
    {
        string typedCode = joinCodeField.text.Trim();
        if (typedCode == "")
        {
            SetStatus("Enter a code first");
            return;
        }

        if (net.IsConnected)
        {
            SetStatus("Joining...");
            net.JoinRoomByCode(typedCode);
        }
        else
        {
            codeToJoinAfterConnecting = typedCode;
            SetStatus("Connecting...");
            net.Connect(storedNickname);
        }
    }

    private void OnJoinCancelClicked() // cancels joining and goes back to Main Lobby
    {
        SetStatus("");
        ShowPanel(mainLobbyPanel);
    }

    private void OnReadyClicked() // toggles the local player's ready state
    {
        localReady = !localReady;
        net.SetLocalPlayerReady(localReady);

        if (localReady)
        {
            readyButtonLabel.text = "UNREADY";
        }
        else
        {
            readyButtonLabel.text = "READY UP";
        }
    }

    private void OnStartClicked() // host-only, force-starts the match
    {
        SetStatus("Starting...");
        net.ForceStartGame();
    }

    private void OnLeaveLobbyClicked()
    {
        // same button handler for both the guest and host "join another lobby" buttons
        SetStatus("Leaving...");
        net.LeaveRoom();
    }

    private void ShowPanel(GameObject panelToShow) // activates one panel and hides the rest
    {
        namePromptPanel.SetActive(panelToShow == namePromptPanel);
        mainLobbyPanel.SetActive(panelToShow == mainLobbyPanel);
        joiningLobbyPanel.SetActive(panelToShow == joiningLobbyPanel);
        joinedLobbyPanel.SetActive(panelToShow == joinedLobbyPanel);
        hostingLobbyPanel.SetActive(panelToShow == hostingLobbyPanel);
    }

    private void SetStatus(string message) // sets the shared status text
    {
        if (statusText != null)
        {
            statusText.text = message;
        }
    }

    private void EnsureEventSystem() // makes sure exactly one EventSystem exists in the scene
    {
        EventSystem existing = FindAnyObjectByType<EventSystem>();
        if (existing != null)
        {
            return;
        }

        GameObject eventSystemObject = new GameObject("EventSystem");
        eventSystemObject.AddComponent<EventSystem>();
        eventSystemObject.AddComponent<StandaloneInputModule>();
        DontDestroyOnLoad(eventSystemObject);
    }

#if UNITY_EDITOR
    // Editor-only: clears the saved name every time Play Mode is stopped, so the
    // first-launch screen is easy to re-test. This whole method is removed automatically
    // from real builds, so it can never affect an actual player.
    private void OnApplicationQuit()
    {
        PlayerPrefs.DeleteKey(NicknameKey);
    }
#endif
}
