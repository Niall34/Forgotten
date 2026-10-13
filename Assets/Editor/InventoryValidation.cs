using System;
using System.Linq;
using Photon.Pun;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// Run with -batchmode -executeMethod InventoryValidation.Run (without -quit).
// Uses the real map and player prefab. Test changes exist only in Play Mode.
[InitializeOnLoad]
public static class InventoryValidation
{
    private const string RunningKey = "Forgotten.InventoryValidation.Running";
    private const string PreviewKey = "Forgotten.InventoryValidation.Preview";
    private static double deadline;
    private static int stage, checks, errors;
    private static PlayerInventory inventory;
    private static GeneratorPiece[] pieces;
    private static GeneratorAssembly generator;
    private static double stageStart;
    private static int installedBefore;
    private static bool enteredPlayMode;
    static InventoryValidation()
    {
        if (SessionState.GetBool(RunningKey, false)) Attach();
    }
    public static void Run()
    {
        SessionState.SetBool(RunningKey, true);
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        Attach();
        EditorApplication.isPlaying = true;
    }
    [MenuItem("Forgotten/Preview Backpack (Offline Test)")]
    public static void Preview()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        SessionState.SetBool(PreviewKey, true);
        Run();
    }
    private static void Attach()
    {
        deadline = EditorApplication.timeSinceStartup + 180;
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
        Application.logMessageReceived -= Log;
        Application.logMessageReceived += Log;
        EditorApplication.playModeStateChanged -= OnPlayMode;
        EditorApplication.playModeStateChanged += OnPlayMode;
    }
    private static void OnPlayMode(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredPlayMode) return;
        enteredPlayMode = true;
        stage = 0;
        deadline = EditorApplication.timeSinceStartup + 180;
        Debug.Log("INVENTORY TEST: Play Mode ready");
    }
    private static void Log(string text, string stack, LogType type)
    {
        if ((type == LogType.Error || type == LogType.Exception || type == LogType.Assert) &&
            !stack.Contains("UnityEditor.Search.SearchDatabase")) errors++;
    }
    private static void Check(bool ok, string label)
    {
        if (!ok) throw new Exception("INVENTORY FAIL: " + label);
        checks++;
        Debug.Log("INVENTORY PASS: " + label);
    }
    private static void MoveTo(Vector3 position)
    {
        inventory.transform.position = position;
        Physics.SyncTransforms();
    }
    private static void Collect(GeneratorPiece item)
    {
        MoveTo(item.transform.position + Vector3.right * 0.5f);
        inventory.PickUpPiece(item);
    }
    private static void Tick()
    {
        if (!EditorApplication.isPlaying || !enteredPlayMode) return;
        EditorApplication.QueuePlayerLoopUpdate();
        try
        {
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Inventory validation timed out");
            if (stage == 0)
            {
                PhotonNetwork.OfflineMode = true;
                PhotonNetwork.CreateRoom("InventoryValidation");
                SceneManager.LoadScene("Forgotten_Map");
                Debug.Log("INVENTORY TEST: requested map load");
                stage = 1;
            }
            else if (stage == 1 && SceneManager.GetActiveScene().name == "Forgotten_Map" && Time.timeSinceLevelLoad > 2)
            {
                inventory = Object.FindObjectsByType<PlayerInventory>(FindObjectsSortMode.None).FirstOrDefault(x => x.photonView.IsMine);
                Check(inventory != null, "real player prefab has a local inventory");
                Vector3 previewSpawnPosition = inventory.transform.position;
                Quaternion previewSpawnRotation = inventory.transform.rotation;
                foreach (var monster in Object.FindObjectsByType<MonsterAI>(FindObjectsSortMode.None)) monster.enabled = false;
                inventory.GetComponent<PlayerController>().enabled = false;
                inventory.GetComponent<CharacterController>().enabled = false;
                pieces = GeneratorPiece.All.Where(x => !x.IsPickedUp()).OrderBy(x => x.photonView.ViewID).ToArray();
                Check(pieces.Length >= 5, "map contains at least five registered generator parts");
                Check(pieces.All(x => x.photonView.ViewID != 0), "pickup network IDs exist");
                Check(inventory.Capacity == 8 && inventory.UsedCapacity == 0, "backpack starts at 0/8");
                var ui = inventory.GetComponent<PlayerInventoryUI>();
                Check(ui != null, "inventory UI attaches to real player");
                Check(ui.GetComponentsInChildren<Button>(true).Count(x => x.name.StartsWith("Quick Slot ")) == 4, "exactly four quick slots");
                Collect(pieces[0]);
                Check(inventory.UsedCapacity == 3 && pieces[0].HolderViewId == inventory.photonView.ViewID, "pickup uses 3/8");
                inventory.PickUpPiece(pieces[0]);
                Check(inventory.Items.Count == 1 && inventory.UsedCapacity == 3, "duplicate pickup does not duplicate item");
                Check(pieces[0].GetComponentsInChildren<Renderer>().All(x => !x.enabled), "carried item hidden");
                Check(pieces[0].GetComponentsInChildren<Collider>().All(x => !x.enabled), "carried item cannot collide");
                Collect(pieces[1]);
                Check(inventory.UsedCapacity == 6 && inventory.Items.Count == 2, "second generator part uses 6/8");
                if (SessionState.GetBool(PreviewKey, false))
                {
                    SessionState.SetBool(PreviewKey, false);
                    SessionState.SetBool(RunningKey, false);
                    // Collection tests teleport beside props, which may be inside walls
                    // or above the floor. A playable preview must start at the real spawn.
                    MoveTo(previewSpawnPosition);
                    inventory.transform.rotation = previewSpawnRotation;
                    inventory.SetBackpackOpen(true);
                    inventory.GetComponent<CharacterController>().enabled = true;
                    inventory.GetComponent<PlayerController>().enabled = true;
                    EditorApplication.update -= Tick;
                    Application.logMessageReceived -= Log;
                    Debug.Log("INVENTORY PREVIEW READY: real player and map, two parts, backpack open.");
                    return;
                }
                Collect(pieces[2]);
                Check(inventory.UsedCapacity == 6 && !pieces[2].IsPickedUp(), "third 3-unit part refused at capacity");
                inventory.SelectSlot(0);
                inventory.DropSelected();
                Check(inventory.UsedCapacity == 3 && pieces[0].HolderViewId == 0, "drop returns 3 capacity");
                Check(pieces[0].GetComponentsInChildren<Renderer>().Any(x => x.enabled), "dropped mesh visible again");
                Check(pieces[0].GetComponentsInChildren<Collider>().Any(x => x.enabled), "dropped colliders restored");
                Collect(pieces[0]);
                Check(inventory.UsedCapacity == 6, "dropped item can be picked up again");
                pieces[2].capacityCost = 2;
                pieces[2].isGeneratorPart = false;
                pieces[2].displayName = "Tool kit";
                Collect(pieces[2]);
                Check(inventory.UsedCapacity == 8 && inventory.Items.Count == 3, "different item sizes exactly fill 8/8");
                pieces[3].capacityCost = 1;
                Collect(pieces[3]);
                Check(!pieces[3].IsPickedUp() && inventory.UsedCapacity == 8, "one extra unit refused");
                inventory.SelectSlot(2);
                Check(inventory.GetHeldPiece() == null, "non-generator item cannot be installed");
                inventory.SetBackpackOpen(true);
                Check(inventory.IsBackpackOpen && ui.GetComponentsInChildren<Transform>(true).First(x => x.name == "Backpack Panel").gameObject.activeSelf, "backpack panel opens");
                inventory.DropSelected();
                Check(inventory.UsedCapacity == 6, "selected item drops through backpack");
                inventory.SetBackpackOpen(false);
                MoveTo(pieces[3].transform.position + Vector3.right * 20);
                inventory.PickUpPiece(pieces[3]);
                Check(!pieces[3].IsPickedUp(), "distant pickup rejected");
                generator = Object.FindAnyObjectByType<GeneratorAssembly>();
                Check(generator != null, "real generator exists");
                inventory.generator = generator;
                inventory.SelectSlot(0);
                MoveTo(generator.transform.position + Vector3.right * 0.5f);
                inventory.installHoldDuration = 0.5f;
                installedBefore = generator.GetPiecesInstalled();
                inventory.TryInstallPiece(inventory.SelectedItem, generator);
                Check(generator.GetPiecesInstalled() == installedBefore, "install without holding is rejected");
                inventory.OnInstallButtonDown();
                inventory.OnInstallButtonUp();
                Check(!inventory.IsInstallingPiece() && inventory.InstallProgress == 0, "released hold cancels installation");
                inventory.OnInstallButtonDown();
                stageStart = EditorApplication.timeSinceStartup;
                stage = 2;
            }
            else if (stage == 2 && EditorApplication.timeSinceStartup - stageStart > 1.5)
            {
                Check(generator.GetPiecesInstalled() == installedBefore + 1, "held installation adds exactly one part");
                Check(inventory.UsedCapacity == 3 && inventory.Items.Count == 1, "installed part frees capacity");
                GeneratorPiece installed = pieces.First(x => x.IsInstalled);
                Check(installed.HolderViewId == 0 && installed.IsPickedUp(), "installed item unavailable for pickup");
                inventory.TryInstallPiece(installed, generator);
                Check(generator.GetPiecesInstalled() == installedBefore + 1, "duplicate install rejected");
                inventory.SetBackpackOpen(true);
                inventory.SetBackpackOpen(false);
                inventory.SetBackpackOpen(true);
                Check(inventory.IsBackpackOpen, "repeated backpack open and close works");
                inventory.SetBackpackOpen(false);
                // Fill the bag with 1-unit entries to ensure entries beyond the four quick slots remain reachable.
                inventory.SelectSlot(0);
                inventory.DropSelected();
                foreach (var p in pieces.Where(x => !x.IsInstalled))
                {
                    p.capacityCost = 1;
                    Collect(p);
                }
                Check(inventory.UsedCapacity == inventory.Items.Count && inventory.Items.Count >= 4, "small pickups share capacity correctly");
                inventory.SelectSlot(inventory.Items.Count - 1);
                Check(inventory.SelectedItem == inventory.Items[inventory.Items.Count - 1], "backpack entry selection works");
                var health = inventory.GetComponent<Forgotten.Player.PlayerHealthStateMachine>();
                health.SetDead();
                stageStart = EditorApplication.timeSinceStartup;
                stage = 3;
            }
            else if (stage == 3 && EditorApplication.timeSinceStartup - stageStart > 0.4)
            {
                Check(inventory.UsedCapacity == 0, "death releases carried items");
                Check(!inventory.CanInteract && !inventory.IsBackpackOpen, "dead player cannot use inventory");
                Check(errors == 0, "no runtime errors in inventory smoke test");
                Debug.Log("INVENTORY VALIDATION COMPLETE: " + checks + " checks passed.");
                Finish(0);
            }
        }
        catch (Exception e) { Debug.LogException(e); Finish(1); }
    }
    private static void Finish(int code)
    {
        SessionState.SetBool(RunningKey, false);
        EditorApplication.update -= Tick;
        Application.logMessageReceived -= Log;
        EditorApplication.Exit(code);
    }
}
