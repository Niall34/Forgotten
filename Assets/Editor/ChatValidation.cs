using System;
using System.IO;
using System.Linq;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// Opt-in integration checks for the current Editor; never exits the user's Editor.
[InitializeOnLoad]
public static class ChatValidation
{
    private const string ModeKey = "Forgotten.ChatValidation.Mode";
    private static int stage;
    private static double deadline;
    private static string room;
    private static int gameErrors;
    private static string PeerFolder => Path.GetFullPath(Path.Combine(Application.dataPath, "../../work/ChatValidationPeer-2026-09-30"));
    [Serializable] public class PeerConfig { public AppSettings settings; public string room; }
    [Serializable] private class Pref { public string key; public bool had; public int integer; public float number; public string text; public int type; }
    [Serializable] private class Prefs { public Pref[] entries; }

    static ChatValidation()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.EnteredEditMode)
            {
                SessionState.SetString(ModeKey, "");
                EditorApplication.update -= Tick;
                Application.logMessageReceived -= OnLog;
                if (SessionState.GetBool("ChatPrefsBackup", false)) RestorePrefs();
            }
        };
        if (SessionState.GetString(ModeKey, "") != "")
        {
            deadline = EditorApplication.timeSinceStartup + 360;
            EditorApplication.update += Tick;
            Application.logMessageReceived += OnLog;
        }
    }

    [MenuItem("Forgotten/Validate Chat/Offline UI and Settings")]
    public static void Offline() => Begin("offline");
    [MenuItem("Forgotten/Validate Chat/Online Room Delivery")]
    public static void Online() => Begin("online");
    [MenuItem("Forgotten/Validate Chat/Preview Settings")]
    public static void PreviewSettings() => Begin("preview");
    [MenuItem("Forgotten/Validate Chat/Preview In-Game Chat")]
    public static void PreviewGame() => Begin("gamepreview");

    private static void Begin(string mode)
    {
        if (EditorApplication.isPlaying || SceneManager.GetActiveScene().name != "Forgotten_Menu")
        { Debug.LogWarning("Stop Play Mode and open Forgotten_Menu before running chat validation."); return; }
        BackupPrefs();
        if (mode == "online" && File.Exists(Path.Combine(PeerFolder, "result.txt")))
            File.Delete(Path.Combine(PeerFolder, "result.txt"));
        SessionState.SetString(ModeKey, mode);
        EditorApplication.isPlaying = true;
    }

    private static void OnLog(string message, string stack, LogType type)
    {
        if ((type == LogType.Exception || type == LogType.Error || type == LogType.Assert)
            && stack.Contains("Assets/Scripts/")) gameErrors++;
    }

    private static void Tick()
    {
        if (!EditorApplication.isPlaying) return;
        try
        {
            if (stage == 99) return;
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Chat test timed out at stage " + stage);
            bool online = SessionState.GetString(ModeKey, "") == "online";
            if (stage == 0 && Time.timeSinceLevelLoad > 3)
            {
                Check(RoomChatSession.CleanText(" \nhello\t\u202E ", 120) == "hello", "sanitize controls and whitespace");
                Check(RoomChatSession.CleanText(new string('x', 130), 120).Length == 120, "120-character bound");
                var settingsButton = Resources.FindObjectsOfTypeAll<Button>().First(b => b.name == "SettingsButton");
                settingsButton.onClick.Invoke();
                Check(Object.FindObjectsByType<TextMeshProUGUI>(FindObjectsSortMode.None).Any(t => t.text == "SETTINGS"), "settings opens");
                if (SessionState.GetString(ModeKey, "") == "preview") { stage = 99; return; }
                Object.FindObjectsByType<Button>(FindObjectsSortMode.None).First(b => b.name == "TEAM CHAT").onClick.Invoke();
                Check(Object.FindObjectsByType<Button>(FindObjectsSortMode.None).Any(b => b.name == "Game Chat"), "chat settings tab opens");
                Object.FindObjectsByType<Button>(FindObjectsSortMode.None).First(b => b.name == "Back").onClick.Invoke();
                foreach (var lobby in Object.FindObjectsByType<LobbyUI>(FindObjectsSortMode.None)) Object.Destroy(lobby.gameObject);
                foreach (var net in Object.FindObjectsByType<NetworkManager>(FindObjectsSortMode.None)) Object.Destroy(net.gameObject);
                foreach (var staging in Object.FindObjectsByType<LobbyStage>(FindObjectsSortMode.None)) Object.Destroy(staging.gameObject);
                var saved = ForgottenGameSettings.Load();
                ForgottenGameSettings.Save(WithChat(saved, true));
                stage = 1;
            }
            else if (stage == 1)
            {
                PhotonNetwork.AutomaticallySyncScene = false;
                PhotonNetwork.NickName = "ChatHost";
                if (online)
                {
                    var config = PhotonNetwork.PhotonServerSettings.AppSettings.CopyTo(new AppSettings());
                    config.AppVersion = "chat-validation-20260930";
                    Check(PhotonNetwork.ConnectUsingSettings(config), "Photon connection initiated");
                    stage = 2;
                }
                else
                {
                    PhotonNetwork.OfflineMode = true;
                    PhotonNetwork.CreateRoom("ChatOfflineValidation");
                    SceneManager.LoadScene("Forgotten_Map");
                    stage = 4;
                }
            }
            else if (stage == 2 && PhotonNetwork.NetworkClientState == ClientState.ConnectedToMasterServer)
            {
                room = "ChatValidation-" + Guid.NewGuid().ToString("N");
                Check(PhotonNetwork.CreateRoom(room, new RoomOptions { IsVisible = false, MaxPlayers = 2 }), "private test room requested");
                stage = 3;
            }
            else if (stage == 3 && PhotonNetwork.InRoom)
            {
                var config = PhotonNetwork.PhotonServerSettings.AppSettings.CopyTo(new AppSettings());
                config.AppVersion = PhotonNetwork.AppVersion;
                config.FixedRegion = PhotonNetwork.CloudRegion;
                Directory.CreateDirectory(PeerFolder);
                File.WriteAllText(Path.Combine(PeerFolder, "peer.json"), JsonUtility.ToJson(new PeerConfig { settings = config, room = room }));
                Debug.Log("CHAT VALIDATION: private room ready for separate peer process");
                SceneManager.LoadScene("Forgotten_Map");
                stage = 4;
            }
            else if (stage == 4 && SceneManager.GetActiveScene().name == "Forgotten_Map" && Time.timeSinceLevelLoad > 6)
            {
                var session = Object.FindAnyObjectByType<RoomChatSession>();
                var ui = Object.FindAnyObjectByType<RoomChatUI>();
                Check(session != null && ui != null, "chat attaches to real gameplay scene");
                ui.Open();
                Check(RoomChatUI.CapturesInput, "composer captures gameplay input");
                var input = Object.FindObjectsByType<TMP_InputField>(FindObjectsSortMode.None).First(f => f.name == "Message Input");
                Check(!input.textComponent.richText, "message text does not execute formatting");
                var hudChat = Resources.FindObjectsOfTypeAll<Button>().First(b => b.name == "ChatButton" && b.gameObject.scene.IsValid());
                ui.Close();
                hudChat.onClick.Invoke();
                Check(RoomChatUI.CapturesInput, "existing touch chat icon opens composer");
                if (SessionState.GetString(ModeKey, "") == "gamepreview") { stage = 99; return; }
                if (!online)
                {
                    input.text = "Hello team";
                    Object.FindObjectsByType<Button>(FindObjectsSortMode.None).First(b => b.name == "Send Message").onClick.Invoke();
                    Check(session.Messages.Count == 1 && session.Messages[0] == "ChatHost: Hello team", "input and Send display username/message");
                    Check(!session.TrySend("Too fast", out _), "send cooldown enforced");
                    Check(!session.TrySend("   ", out _), "empty messages rejected");
                    var invalid = new EventData { Code = RoomChatSession.MessageEvent };
                    invalid.Parameters[ParameterCode.Data] = "spoof";
                    invalid.Parameters[ParameterCode.ActorNr] = 99999;
                    session.OnEvent(invalid);
                    Check(session.Messages.Count == 1, "unknown sender rejected");
                    ForgottenGameSettings.Save(WithChat(ForgottenGameSettings.Load(), false));
                    Check(!session.ChatEnabled && session.Messages.Count == 0 && !RoomChatUI.CapturesInput,
                        "saved chat off clears messages and closes UI");
                    Check(!session.TrySend("Hidden", out _), "chat off prevents sending");
                    ForgottenGameSettings.Save(WithChat(ForgottenGameSettings.Load(), true));
                    ui.Open(); ui.Close(); ui.Open(); ui.Close();
                    Check(!RoomChatUI.CapturesInput, "repeated close releases input");
                    PhotonNetwork.LeaveRoom();
                    Check(!session.TrySend("No room", out _), "disconnected sending rejected");
                    Finish("PASS: offline chat, UI, settings and lifecycle checks");
                }
                else stage = 5;
            }
            else if (stage == 5)
            {
                var session = Object.FindAnyObjectByType<RoomChatSession>();
                if (!session.Messages.Any(m => m == "ChatPeer: <b>Peer hello</b>")) return;
                Check(PhotonNetwork.CurrentRoom.PlayerCount == 2, "separate Photon peer joined");
                Check(Object.FindObjectsByType<TextMeshProUGUI>(FindObjectsSortMode.None).Any(t => t.name == "Messages" && t.text.Contains("<b>Peer hello</b>")),
                    "remote text appears literally in game chat");
                Check(session.TrySend("Host reply", out _), "host sends reply to peer");
                stage = 6;
            }
            else if (stage == 6 && File.Exists(Path.Combine(PeerFolder, "result.txt")))
            {
                Check(File.ReadAllText(Path.Combine(PeerFolder, "result.txt")).StartsWith("PASS"), "separate peer receives host reply");
                Finish("PASS: bidirectional chat with a separate Photon peer process");
            }
        }
        catch (Exception exception) { Debug.LogException(exception); Finish("FAIL: " + exception.Message); }
    }

    private static ForgottenSettingsSnapshot WithChat(ForgottenSettingsSnapshot s, bool enabled) =>
        new ForgottenSettingsSnapshot(s.GameVolume, s.MusicVolume, s.GraphicsQuality, s.FieldOfView,
            s.HudScale, enabled, s.LookSensitivity, s.InvertLook, s.FrameRate);
    private static void Check(bool value, string label)
    {
        if (!value) throw new Exception(label);
        Debug.Log("CHAT CHECK PASS: " + label);
    }
    private static void Finish(string result)
    {
        if (gameErrors > 0 && result.StartsWith("PASS")) result = "FAIL: runtime errors occurred during validation";
        Debug.Log("CHAT VALIDATION " + result + "; game errors=" + gameErrors);
        SessionState.SetString(ModeKey, "");
        EditorApplication.update -= Tick;
        Application.logMessageReceived -= OnLog;
        PhotonNetwork.Disconnect();
        EditorApplication.isPlaying = false;
    }

    private static void BackupPrefs()
    {
        string[] integers = { "GraphicsQuality", "GameChat", "VoiceChat", "InvertLook", "FrameRate" };
        string[] floats = { "MasterVolume", "MusicVolume", "FieldOfView", "HudScale", "LookSensitivity" };
        var list = integers.Select(k => new Pref { key = "Forgotten.Settings." + k, type = 0 })
            .Concat(floats.Select(k => new Pref { key = "Forgotten.Settings." + k, type = 1 }))
            .Concat(new[] { new Pref { key = "Nickname", type = 2 } }).ToArray();
        foreach (var p in list)
        {
            p.had = PlayerPrefs.HasKey(p.key);
            if (p.type == 0) p.integer = PlayerPrefs.GetInt(p.key);
            else if (p.type == 1) p.number = PlayerPrefs.GetFloat(p.key);
            else p.text = PlayerPrefs.GetString(p.key);
        }
        SessionState.SetString("ChatPrefs", JsonUtility.ToJson(new Prefs { entries = list }));
        SessionState.SetBool("ChatPrefsBackup", true);
    }
    private static void RestorePrefs()
    {
        foreach (var p in JsonUtility.FromJson<Prefs>(SessionState.GetString("ChatPrefs", "")).entries)
        {
            if (!p.had) PlayerPrefs.DeleteKey(p.key);
            else if (p.type == 0) PlayerPrefs.SetInt(p.key, p.integer);
            else if (p.type == 1) PlayerPrefs.SetFloat(p.key, p.number);
            else PlayerPrefs.SetString(p.key, p.text);
        }
        PlayerPrefs.Save();
        SessionState.SetBool("ChatPrefsBackup", false);
        Debug.Log("CHAT VALIDATION: original preferences restored");
    }
}
