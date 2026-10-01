using Photon.Pun;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;


// controls the whole UI menus for the lobby screen

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
    public Button quitButton; // drag your existing quit button in here

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

    [Header("Loading Popup")]
    public GameObject loadingPopup; // full screen overlay with a raycast-blocking image behind the text that blocks inputs
    public TextMeshProUGUI loadingPopupText;

    [Header("Game Loading Panel")]
    public GameObject gameLoadingPanel; // shown while the actual gameplay scene loads in

    [Header("Sounds")]
    public AudioClip buttonClickClip; // plays for every button on this canvas
    public AudioClip matchStartClip; // plays for everyone in the lobby when the host starts, and for solo when play is pressed

    private static LobbyUI current; // this object survives scene loads, so coming back to the lobby scene would leave two of them without this

    private Canvas canvas;
    private AudioSource uiSource;
    private NetworkManager net;
    private ForgottenSettingsMenu settingsMenu;
    private string storedNickname = "";

    // if the host or join button is tapped before fully connected, remember what to do and carry it out automatically once the connection finishes - this is a backup/safety net
    private bool wantsToHostAfterConnecting = false;
    private string codeToJoinAfterConnecting = "";
    private bool wantsToPlaySoloAfterConnecting = false;

    private bool localReady = false;

    // values that are remembered checked against the network manager's current values every frame in "Update()" to detect when something has changed
    private bool wasInLobby = false;
    private bool wasInRoom = false;
    private int lastSeenErrorVersion = 0;
    private int lastSeenPlayerListVersion = -1;
    private bool handledMatchStarting = false;
    private bool hasEnteredGameplayScene = false; // flips true the moment we actually land in the gameplay scene - stops
    // CheckForMatchStarting from re-showing the cover if MatchStarting only arrives after we're already there (it's a separate network round-trip from the scene load itself, so it can genuinely show up late)

    private void Awake() // wires up every button, loads the saved name, and shows the right starting panel
    {
        if (current != null && current != this)
        {
            Destroy(current.gameObject); // the old one still points at the previous scene's LobbyStage, the fresh one takes over
        }
        current = this;

        EnsureEventSystem();
        DontDestroyOnLoad(gameObject);

        canvas = GetComponent<Canvas>();
        net = NetworkManager.Bootstrap();
        SceneManager.sceneLoaded += HandleGameplaySceneLoaded; // catches the moment the new scene's actually ready
        settingsMenu = GetComponent<ForgottenSettingsMenu>();
        if (settingsMenu == null) settingsMenu = gameObject.AddComponent<ForgottenSettingsMenu>();
        settingsMenu.Initialize(mainLobbyPanel);

        continueButton.onClick.AddListener(OnNameContinueClicked);
        playButton.onClick.AddListener(OnPlayClicked);
        hostButton.onClick.AddListener(OnHostClicked);
        joinOpenButton.onClick.AddListener(OnJoinOpenClicked);
        if (quitButton != null)
        {
            quitButton.onClick.AddListener(OnQuitClicked);
        }
        joinConfirmButton.onClick.AddListener(OnJoinConfirmClicked);
        joinCancelButton.onClick.AddListener(OnJoinCancelClicked);
        readyButton.onClick.AddListener(OnReadyClicked);
        guestLeaveButton.onClick.AddListener(OnLeaveLobbyClicked);
        startButton.onClick.AddListener(OnStartClicked);
        hostLeaveButton.onClick.AddListener(OnLeaveLobbyClicked);

        uiSource = gameObject.AddComponent<AudioSource>();
        uiSource.playOnAwake = false;
        uiSource.spatialBlend = 0f;
        foreach (Button button in GetComponentsInChildren<Button>(true))
        {
            button.onClick.AddListener(PlayButtonClick);
        }

        joinCodeField.characterLimit = 6;
        joinCodeField.onValueChanged.AddListener(HandleJoinCodeTyped);

        HideLoadingPopup(); // just in case someone left it active in the editor by accident
        HideGameLoadingPanel(); // same deal - starts hidden regardless of how it was left in the editor

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
            HideLoadingPopup(); // whatever we were waiting on just failed, no point leaving it up
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
        HideLoadingPopup(); // whatever got us here (hosting or joining) is done now

        // solo games skip the lobby screens entirely and go straight to gameplay
        if (net.IsSolo)
        {
            settingsMenu.HideImmediately();
            settingsMenu.StopLobbyMusic();
            ShowPanel(null); // hides every lobby panel without touching the canvas itself, so gameLoadingPanel can still show
            ShowGameLoadingPanel();
            return;
        }

        SetStatus("");
        localReady = false;
        canvas.enabled = true;
        handledMatchStarting = false; // reset in case this isn't the first room we've been in this session
        hasEnteredGameplayScene = false;

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
        HideLoadingPopup();
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

    private void CheckForMatchStarting() // hides the lobby panels and covers the screen once the match starts
    {
        if (net.MatchStarting && handledMatchStarting == false)
        {
            handledMatchStarting = true;
            PlayUiSound(matchStartClip); // runs on every client in the lobby when the host starts, so everyone hears it
            if (hostedCodeText != null)
            {
                hostedCodeText.text = ""; // the code shows on the pause menu once the match is going
            }
            SetStatus("Starting...");
            settingsMenu.HideImmediately();
            settingsMenu.StopLobbyMusic();

            // MatchStarting can arrive a moment after the scene itself already finished loading, it's a separate network round-trip, not tied to the local scene swap. if that's already happened, showing the cover now would just leave it stuck on screen forever, since nothing's left to hide it a second time
            if (hasEnteredGameplayScene == false)
            {
                ShowPanel(null);
                ShowGameLoadingPanel();
            }
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

    public void OnPlayClicked() // connects (if needed) then starts a solo game
    {
        wantsToHostAfterConnecting = false;
        codeToJoinAfterConnecting = "";
        PlayUiSound(matchStartClip); // solo never goes through MatchStarting, so this is where its start sound comes from

        // show the cover right away whether we're already connected or still need to connect first, we don't
        // want a gap where nothing's covering the screen while that happens
        settingsMenu.HideImmediately();
        settingsMenu.StopLobbyMusic();
        ShowPanel(null);
        ShowGameLoadingPanel();

        if (net.InLobby)
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

    public void OnHostClicked() // connects then hosts a room
    {
        ShowLoadingPopup(); // pops up right away, whether we're already connected or still need to connect first

        codeToJoinAfterConnecting = "";
        wantsToPlaySoloAfterConnecting = false;

        if (net.InLobby)
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

    private void OnJoinOpenClicked() // opens the join code entry panel
    {
        joinCodeField.text = "";
        SetStatus("");
        ShowPanel(joiningLobbyPanel);
    }

    private void OnJoinConfirmClicked() // connects (if needed) then joins the typed room code
    {
        wantsToHostAfterConnecting = false;
        wantsToPlaySoloAfterConnecting = false;

        string typedCode = joinCodeField.text.Trim();
        if (typedCode == "")
        {
            SetStatus("Enter a code first");
            return;
        }

        ShowLoadingPopup(); // only pops up once we know there's an actual code to try, not on an empty submit

        if (net.InLobby)
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

    private void PlayButtonClick()
    {
        PlayUiSound(buttonClickClip);
    }

    private void PlayUiSound(AudioClip clip)
    {
        if (clip != null)
        {
            uiSource.PlayOneShot(clip);
        }
    }

    private void OnQuitClicked() // closes the game, in the editor it stops play mode instead since Application.Quit() does nothing there
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void OnStartClicked() // host-only, force-starts the match
    {
        SetStatus("Starting...");
        ShowPanel(null); // show the loading cover right away rather than waiting for MatchStarting to flip
        ShowGameLoadingPanel();
        net.ForceStartGame();
    }

    private void OnLeaveLobbyClicked()
    {
        // same button handler for both the guest and host "join another lobby" buttons
        ShowLoadingPopup();
        SetStatus("Leaving...");
        net.LeaveRoom();
    }

    private void ShowPanel(GameObject panelToShow) // activates one panel and hides the rest
    {
        settingsMenu?.HideImmediately();
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

    private void ShowLoadingPopup() // pops up the moment a button's tapped, so it's obvious the input actually registered and stops any further input
    {
        if (loadingPopup != null)
        {
            loadingPopup.SetActive(true);
        }

        if (loadingPopupText != null)
        {
            loadingPopupText.text = "Loading...";
        }
    }

    private void HideLoadingPopup()
    {
        if (loadingPopup != null)
        {
            loadingPopup.SetActive(false);
        }
    }

    private void HandleGameplaySceneLoaded(Scene loadedScene, LoadSceneMode mode) // fires for any scene load
    {
        hasEnteredGameplayScene = true;
        HideGameLoadingPanel();
    }

    private void ShowGameLoadingPanel()
    {
        if (gameLoadingPanel != null)
        {
            gameLoadingPanel.SetActive(true);
        }
    }

    private void HideGameLoadingPanel()
    {
        if (gameLoadingPanel != null)
        {
            gameLoadingPanel.SetActive(false);
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

    private void OnDestroy() // tidy up the subscription from Awake, this object should live for the whole game, but just in case
    {
        SceneManager.sceneLoaded -= HandleGameplaySceneLoaded;
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