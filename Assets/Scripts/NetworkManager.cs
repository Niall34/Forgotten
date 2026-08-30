using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

// handles alP photon PUN2 connection, room, and ready-up logic
// other scripts like forgottenlobbyUI just check this scripts variables in
// their own Update() method, and react when something changes
// like when InRoom flips from false to true, that means the player just joined a room
public class NetworkManager : MonoBehaviourPunCallbacks
{
    // a room code is made from these characters 0, 1, O and I left out since they're easy to mix up
    private const string CodeCharacters = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
    private const int CodeLength = 6;
    private const int MaxHostRetries = 3;
    private const byte DefaultMaxPlayers = 4;

    private const string ReadyPropertyKey = "ready";
    private const string StartedPropertyKey = "started";

    [Header("Scenes")]
    public string lobbySceneName = "Forgotten_Menu";

    public string gameplaySceneName = "Forgotten_Map";

    [Header("Versioning")]
    public string gameVersion = "0.1";

    // other scripts read these values

    public static NetworkManager Instance;

    public bool IsConnected { get { return PhotonNetwork.IsConnectedAndReady; } }
    public bool InLobby { get { return PhotonNetwork.InLobby; } }
    public bool InRoom { get { return PhotonNetwork.InRoom; } }
    public bool IsMasterClient { get { return PhotonNetwork.IsMasterClient; } }
    public string LobbySceneName { get { return lobbySceneName; } }
    public string GameplaySceneName { get { return gameplaySceneName; } }

    // the current room's join code which is empty when not hosting/joined
    public string RoomCode = "";

    // turns true the moment the match is actually starting
    public bool MatchStarting = false;

    // whenever something goes wrong the message goes here, and errorversion increases by 1, other scripts remember the last
    // errorversion and show a new message whenever the number goes up
    public string ErrorMessage = "";
    public int ErrorVersion = 0;

    // increases by 1 every time a player joins, leaves, or changes their ready status
    // other scripts remember the last number and refresh their player list display when this number increases
    public int PlayerListVersion = 0;

    // true when the current room is a 1-player solo game, so the UI knows to skip the lobby screens
    public bool IsSolo = false;

    private bool goStraightToGameplay = false;   // true only for solo play
    private byte lastRequestedMaxPlayers = DefaultMaxPlayers;
    private int hostRetryCount = 0;
    private bool isConnecting = false; //stops double clicks to prevent race conditions while loading

    // any script that needs the network manager call this
    public static NetworkManager Bootstrap()
    {
        if (Instance != null)
        {
            return Instance;
        }

        GameObject managerObject = new GameObject("NetworkManager");
        NetworkManager manager = managerObject.AddComponent<NetworkManager>();
        DontDestroyOnLoad(managerObject);
        return manager;
    }

    private void Awake() // sets Instance and turns on scene sync so every client follows the host's scene loads
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // whenever the host client loads a new scene every other client automatically follows along
        PhotonNetwork.AutomaticallySyncScene = true;
        PhotonNetwork.GameVersion = gameVersion;
    }

    private void ReportError(string message) // saves an error message and bumps ErrorVersion so other scripts notice it
    {
        ErrorMessage = message;
        ErrorVersion = ErrorVersion + 1;
    }

    // connects to photon, picking a random name if none was typed, and joins the lobby
    public void Connect(string nickname)
    {
        Debug.Log("Connect() called"); // debug 
        if (nickname == null || nickname == "")
        {
            PhotonNetwork.NickName = "Player" + Random.Range(1000, 9999); // sets the username to something random if someone didnt input a name
        }
        else
        {
            PhotonNetwork.NickName = nickname;
        }

        if (PhotonNetwork.OfflineMode)
        {
            // leaving a solo session behind before going online
            PhotonNetwork.OfflineMode = false;
        }

        if (PhotonNetwork.InLobby)
        {
            return; // already fully connected and in the lobby, nothing to do
        }

        if (isConnecting)
        {
            return; // a connection attempt is already running, dont start a second one
        }

        if (PhotonNetwork.IsConnected)
        {
            // socket is up but we're not in the lobby yet - only safe to join once we're
            // actually at the master server, not mid-handshake on the name server
            if (PhotonNetwork.NetworkClientState == ClientState.ConnectedToMasterServer)
            {
                PhotonNetwork.JoinLobby();
            }
            return;
        }

        isConnecting = true;
        PhotonNetwork.ConnectUsingSettings();
    }

    public override void OnConnectedToMaster() // photon callback: this is the safe moment to join the lobby
    {
        Debug.Log("OnConnectedToMaster fired");// debug
        isConnecting = false;
        PhotonNetwork.JoinLobby();
    }

    public override void OnDisconnected(DisconnectCause cause) // photon callback: fires on disconnect
    {
        isConnecting = false;
        ReportError("Disconnected: " + cause);
    }

    // starts a single-player game as a real 1-player room (needs a real connection now, no more offline faking)
    // whoever calls this should already be connected first - same expectation as HostRoom/JoinRoomByCode below
    public void PlaySolo()
    {
        IsSolo = true;
        goStraightToGameplay = true;

        string soloRoomName = "Solo" + Random.Range(1000, 9999); // random suffix so two people testing at once dont collide
        CreateRoomWithCode(soloRoomName, 1);
    }

    // makes up a new room code and starts creating a room using it, returns the code right away so the ui can show it immediately before the room finishes creating
    public string HostRoom()
    {
        IsSolo = false;
        goStraightToGameplay = false;
        lastRequestedMaxPlayers = DefaultMaxPlayers;
        hostRetryCount = 0;

        string code = MakeRandomCode();
        CreateRoomWithCode(code, DefaultMaxPlayers);
        return code;
    }

    public void JoinRoomByCode(string code) // cleans up the typed code and asks photon to join that room
    {
        if (code == null || code == "")
        {
            ReportError("Enter a room code");
            return;
        }

        IsSolo = false;
        string cleanCode = code.Trim().ToUpper();
        PhotonNetwork.JoinRoom(cleanCode);
    }

    private void CreateRoomWithCode(string code, byte maxPlayers) // tells photon to actually create the room
    {
        Debug.Log("CreateRoomWithCode called with code " + code); // debug

        RoomOptions options = new RoomOptions();
        options.MaxPlayers = maxPlayers;
        options.IsVisible = false; // makes sure the only way to enter is via code input and not a list
        options.IsOpen = true;

        Hashtable properties = new Hashtable();
        properties[StartedPropertyKey] = false;
        options.CustomRoomProperties = properties;

        PhotonNetwork.CreateRoom(code, options);
    }

    private string MakeRandomCode() // builds a random 6-character room code
    {
        char[] letters = new char[CodeLength];
        for (int i = 0; i < CodeLength; i++)
        {
            int randomIndex = Random.Range(0, CodeCharacters.Length);
            letters[i] = CodeCharacters[randomIndex];
        }
        return new string(letters);
    }

    public override void OnJoinedRoom() // photon callback, fires once joined a room
    {
        SetLocalPlayerReady(false); // everyone always starts a fresh room un-ready
        RoomCode = PhotonNetwork.CurrentRoom.Name;
        PlayerListVersion = PlayerListVersion + 1;
    }

    public override void OnCreatedRoom() // photon callback fires once a room we created is ready, loads the next scene
    {
        // solo sessions go straight to gameplay
        Debug.Log("OnCreatedRoom fired"); // debug

        string sceneToLoad = lobbySceneName;
        if (goStraightToGameplay)
        {
            sceneToLoad = gameplaySceneName;
        }
        goStraightToGameplay = false;

        PhotonNetwork.LoadLevel(sceneToLoad);
    }

    public override void OnJoinRoomFailed(short returnCode, string message) // photon callback: fires if a join-by-code attempt fails
    {
        ReportError("No room found with that code");
    }

    public override void OnCreateRoomFailed(short returnCode, string message)
    {
        // error if cant create a hosted lobby
        Debug.Log("OnCreateRoomFailed fired: " + message); // debug
        if (hostRetryCount < MaxHostRetries)
        {
            hostRetryCount = hostRetryCount + 1;
            string newCode = MakeRandomCode();
            RoomCode = newCode;
            CreateRoomWithCode(newCode, lastRequestedMaxPlayers);
            return;
        }

        ReportError("Couldn't create a room right now - try again");
    }

    public void LeaveRoom() // leaves the current room, if we're in one
    {
        if (PhotonNetwork.InRoom)
        {
            PhotonNetwork.LeaveRoom();
        }
    }

    public override void OnLeftRoom() // photon callback: fires once we've left a room
    {
        RoomCode = "";
    }

    public override void OnPlayerEnteredRoom(Player newPlayer) // photon callback: fires when another player joins
    {
        PlayerListVersion = PlayerListVersion + 1;
    }

    public override void OnPlayerLeftRoom(Player otherPlayer) // photon callback: fires when another player leaves
    {
        PlayerListVersion = PlayerListVersion + 1;
    }

    public override void OnMasterClientSwitched(Player newMasterClient) // photon callback: fires when host status changes hands
    {
        PlayerListVersion = PlayerListVersion + 1;
    }

    public void SetLocalPlayerReady(bool ready) // sets or clears the local player's ready flag
    {
        if (PhotonNetwork.InRoom == false)
        {
            return;
        }

        Hashtable properties = new Hashtable();
        properties[ReadyPropertyKey] = ready;
        PhotonNetwork.LocalPlayer.SetCustomProperties(properties);
    }

    public bool IsPlayerReady(Player player) // checks whether a given player has marked themselves ready
    {
        if (player.CustomProperties == null)
        {
            return false;
        }

        object value;
        bool found = player.CustomProperties.TryGetValue(ReadyPropertyKey, out value);
        if (found == false)
        {
            return false;
        }

        return value is bool && (bool)value;
    }

    public override void OnPlayerPropertiesUpdate(Player targetPlayer, Hashtable changedProps) // photon callback: fires when any player's properties change
    {
        PlayerListVersion = PlayerListVersion + 1;

        bool readyStateChanged = changedProps.ContainsKey(ReadyPropertyKey);
        if (PhotonNetwork.IsMasterClient && readyStateChanged)
        {
            TryAutoStart();
        }
    }

    public bool AreAllPlayersReady() // checks whether every player in the room is ready
    {
        if (PhotonNetwork.InRoom == false)
        {
            return false;
        }

        if (PhotonNetwork.CurrentRoom.PlayerCount < 1)
        {
            return false;
        }

        foreach (Player player in PhotonNetwork.CurrentRoom.Players.Values)
        {
            if (IsPlayerReady(player) == false)
            {
                return false;
            }
        }

        return true;
    }

    private void TryAutoStart()
    {
        if (PhotonNetwork.IsMasterClient == false)
        {
            return;
        }

        // wait until everyone has actually finished loading into the staging scene before allowing an auto-start
        string activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        if (activeScene != lobbySceneName)
        {
            return;
        }

        if (AreAllPlayersReady() == false)
        {
            return;
        }

        BeginMatch();
    }

    // lets the host start the match manually, even if not everyone has readied up yet
    public void ForceStartGame()
    {
        if (PhotonNetwork.IsMasterClient == false || PhotonNetwork.InRoom == false)
        {
            return;
        }

        BeginMatch();
    }

    private void BeginMatch() // closes the room, marks it started, and loads the gameplay scene
    {
        PhotonNetwork.CurrentRoom.IsOpen = false;
        PhotonNetwork.CurrentRoom.IsVisible = false;

        Hashtable properties = new Hashtable();
        properties[StartedPropertyKey] = true;
        PhotonNetwork.CurrentRoom.SetCustomProperties(properties);

        PhotonNetwork.LoadLevel(gameplaySceneName);
    }

    public override void OnRoomPropertiesUpdate(Hashtable propertiesThatChanged)
    {
        // this fires on every client the moment the room has started, and its the one signal the UI can rely on to know if the match has started
        if (propertiesThatChanged.ContainsKey(StartedPropertyKey))
        {
            object value = propertiesThatChanged[StartedPropertyKey];
            if (value is bool && (bool)value)
            {
                MatchStarting = true;
            }
        }
    }
}
