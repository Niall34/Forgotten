using System;
using System.Linq;
using System.Reflection;
using Photon.Pun;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// Repeatable integration checks. No scenes or preferences are saved by this tool.
[InitializeOnLoad]
public static class CombinedProjectValidation
{
    private const string Running = "Forgotten.Validation.Running";
    private static double deadline;
    private static int stage;
    private static int errors;
    private static int editorSearchErrors;

    [MenuItem("Forgotten/Open Game Menu")]
    public static void OpenMenu()
    {
        if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            EditorSceneManager.OpenScene("Assets/Scenes/Forgotten_Menu.unity");
    }

    static CombinedProjectValidation()
    {
        if (SessionState.GetBool(Running, false))
        {
            deadline = EditorApplication.timeSinceStartup + 240;
            EditorApplication.update += Tick;
            Application.logMessageReceived += Log;
        }
    }

    public static void Run()
    {
        try
        {
            foreach (var entry in EditorBuildSettings.scenes.Where(s => s.enabled))
            {
                var scene = EditorSceneManager.OpenScene(entry.path);
                foreach (var root in scene.GetRootGameObjects()) CheckTree(root, entry.path);
                Debug.Log("VALIDATION scene references passed: " + entry.path);
            }
            var player = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Player/Player.prefab");
            Check(player != null && player.GetComponent<PlayerController>() != null, "Player prefab");
            Check(player.GetComponent<PlayerController>().touchControlsCanvasPrefab != null, "Touch controls assigned");
            CheckTree(player, "Player prefab");
            CheckTree(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Monster/Prefabs/Monster.prefab"), "Monster prefab");
            var invalid = new ForgottenSettingsSnapshot(float.NaN, 5, 99, 999, 0, true, -1, false, 200).Sanitized();
            Check(invalid.GameVolume == 0.85f && invalid.MusicVolume == 1 && invalid.FieldOfView == 100
                && invalid.HudScale == 0.7f && invalid.FrameRate == 60, "Settings validation bounds");
            EditorSceneManager.OpenScene("Assets/Scenes/Forgotten_Menu.unity");
            SessionState.SetBool(Running, true);
            EditorApplication.isPlaying = true;
        }
        catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
    }

    private static void CheckTree(GameObject root, string path)
    {
        Check(root != null, path);
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
            if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject) != 0)
                throw new Exception(path + " missing script on " + t.name);
    }

    private static void Check(bool value, string label)
    {
        if (!value) throw new Exception("VALIDATION FAILED: " + label);
        Debug.Log("VALIDATION PASS: " + label);
    }

    private static void Log(string message, string stack, LogType type)
    {
        // Preserve the exception in the log and report it separately: this reproduced
        // in Unity's editor search index, not in the game or its dependencies.
        if (type == LogType.Exception && message.StartsWith("ArgumentOutOfRangeException:")
            && stack.Contains("UnityEditor.Search.SearchDatabase") && !stack.Contains("Assets/Scripts"))
        {
            editorSearchErrors++;
            return;
        }
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors++;
    }

    private static void Tick()
    {
        if (!EditorApplication.isPlaying) return;
        try
        {
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Smoke test timeout");
            if (stage == 0 && Time.time > 5)
            {
                var lobby = Object.FindFirstObjectByType<LobbyUI>();
                Check(lobby != null, "Lobby starts");
                var settings = lobby.GetComponent<ForgottenSettingsMenu>();
                Check(settings != null, "Settings attached to lobby");
                Call(settings, "OpenSettings");
                Check(Object.FindObjectsByType<Slider>(FindObjectsSortMode.None).Length >= 2, "Settings controls visible");
                Call(settings, "BackWithoutSaving");
                Check(lobby.mainLobbyPanel.activeSelf, "Discard returns to lobby");
                // Isolate gameplay from cloud callbacks; this is intentionally an offline smoke test.
                Object.Destroy(lobby.gameObject);
                foreach (var manager in Object.FindObjectsByType<NetworkManager>(FindObjectsSortMode.None))
                    Object.Destroy(manager.gameObject);
                stage = 1;
            }
            else if (stage == 1)
            {
                PhotonNetwork.OfflineMode = true;
                PhotonNetwork.CreateRoom("IntegrationSmoke");
                SceneManager.LoadScene("Forgotten_Map");
                stage = 2;
            }
            else if (stage == 2 && SceneManager.GetActiveScene().name == "Forgotten_Map" && Time.timeSinceLevelLoad > 10)
            {
                var player = Object.FindObjectsByType<PlayerController>(FindObjectsSortMode.None).FirstOrDefault(p => p.photonView.IsMine);
                Check(player != null, "Local player spawned");
                Check(Object.FindFirstObjectByType<MonsterAI>() != null, "Monster spawned");
                Check(Object.FindFirstObjectByType<GeneratorAssembly>() != null, "Generator exists");
                Check(Object.FindObjectsByType<GeneratorPiece>(FindObjectsSortMode.None).Length >= 5, "Five generator pieces exist");
                Check(player.GetComponent<PlayerInventory>() != null, "Spawned player has generator inventory");
                Check(Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Count(l => l.enabled) == 1,
                    "Exactly one active audio listener");
                Check(Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).Any(c =>
                    Mathf.Abs(c.fieldOfView - ForgottenGameSettings.FieldOfView) < 0.01f), "Saved FOV applied");
                var health = player.GetComponent<Forgotten.Player.PlayerHealthStateMachine>();
                Check(health != null, "Player branch health is loaded by Unity");
                health.CurrentHealth = 40;
                Check(health.CurrentState == Forgotten.Player.PlayerLifeState.Injured, "Health injured transition");
                Check(errors == 0, "No runtime errors during lobby/map smoke test");
                var generator = Object.FindFirstObjectByType<GeneratorAssembly>();
                foreach (var piece in Object.FindObjectsByType<GeneratorPiece>(FindObjectsSortMode.None))
                {
                    piece.PickUp(player.GetComponent<PlayerInventory>());
                    generator.InstallPiece(piece);
                }
                stage = 3;
            }
            else if (stage == 3)
            {
                var generator = Object.FindFirstObjectByType<GeneratorAssembly>();
                Check(generator.GetPiecesInstalled() >= generator.GetTotalPiecesNeeded(), "Generator installation RPCs");
                Check(!generator.doorToOpen.activeSelf, "Completed generator opens exit door");
                var player = Object.FindObjectsByType<PlayerController>(FindObjectsSortMode.None).First(p => p.photonView.IsMine);
                typeof(WinTrigger).GetMethod("OnTriggerEnter", BindingFlags.NonPublic | BindingFlags.Instance)
                    .Invoke(generator.winTrigger, new object[] { player.GetComponent<CharacterController>() });
                stage = 4;
            }
            else if (stage == 4)
            {
                // Escaping disables movement and removes the player from the active-player registry.
                Check(Object.FindObjectsByType<PlayerController>(FindObjectsSortMode.None).Any(p => p.HasEscaped),
                    "Exit registers player escape");
                Check(errors == 0, "No errors during generator and exit checks");
                Debug.Log("VALIDATION COMPLETE: scene/prefab checks, offline lobby/map, generator and exit smoke passed");
                Debug.Log("VALIDATION EDITOR SEARCH EXCEPTIONS (unresolved): " + editorSearchErrors);
                Finish(0);
            }
        }
        catch (Exception e) { Debug.LogException(e); Finish(1); }
    }

    private static void Call(object target, string method) => target.GetType()
        .GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, null);

    private static void Finish(int code)
    {
        SessionState.SetBool(Running, false);
        EditorApplication.update -= Tick;
        Application.logMessageReceived -= Log;
        EditorApplication.Exit(code);
    }
}
