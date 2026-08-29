// Assets/FrontierComms_FreeSample/Editor/FrontierCommsFreeSampleWelcomeWindow.cs
using UnityEditor;
using UnityEngine;
using System.IO;

namespace FrontierCommsEditor
{
    public class FrontierCommsFreeSampleWelcomeWindow : EditorWindow
    {
        private const string HasShownKey = "FrontierComms_FreeSample_WelcomeShown";
        private const string ReadmePath = "Assets/FrontierComms_FreeSample/Documentation/README_FreeSample_DistressBeacon.pdf";
        private const string FullPackURL = "https://assetstore.unity.com/packages/3d/props/electronics/frontier-comms-5-sci-fi-communication-devices-pack-324485";
        private const string CreatorHubURL = "https://martinljungblad.carrd.co";

        private static readonly Vector2 WinSize = new(520, 300);
        private GUIStyle authorStyle;

        [InitializeOnLoadMethod]
        private static void ShowOnLoad()
        {
            if (!EditorPrefs.GetBool(HasShownKey, false) && !EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorApplication.delayCall += () =>
                {
                    try
                    {
                        var window = CreateInstance<FrontierCommsFreeSampleWelcomeWindow>();
                        window.titleContent = new GUIContent("Distress Beacon — Free Sample");
                        window.minSize = WinSize;
                        CenterWindow(window, WinSize);
                        window.ShowUtility(); // safer than ShowPopup
                        EditorPrefs.SetBool(HasShownKey, true);
                    }
                    catch (System.Exception ex)
                    {
                        Debug.LogError($"[Frontier Comms] Failed to show Welcome window: {ex}");
                    }
                };
            }
        }

        // Build styles lazily in OnGUI; EditorStyles can be null during OnEnable after domain reload
        private void EnsureStyles()
        {
            if (authorStyle != null) return;

            GUIStyle baseLabel = null;
            try { baseLabel = EditorStyles.label; } catch { /* not ready yet */ }
            if (baseLabel == null) baseLabel = GUI.skin != null ? GUI.skin.label : new GUIStyle();

            authorStyle = new GUIStyle(baseLabel)
            {
                fontSize = 10,
                fontStyle = FontStyle.Italic,
                alignment = TextAnchor.MiddleRight,
            };
            authorStyle.normal.textColor = Color.gray;
        }

        private void OnGUI()
        {
            try
            {
                EnsureStyles();

                using (new GUILayout.VerticalScope("box"))
                {
                    GUILayout.Space(10);
                    var titleStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 16, alignment = TextAnchor.MiddleCenter };
                    GUILayout.Label("Thanks for trying Distress Beacon", titleStyle);
                    GUILayout.Space(12);

                    var bodyStyle = new GUIStyle(EditorStyles.wordWrappedLabel)
                    {
                        fontSize = 12,
                        alignment = TextAnchor.UpperLeft,
                        wordWrap = true,
                        margin = new RectOffset(10, 10, 0, 0)
                    };
                    GUILayout.Label(
                        "This is the free sample from the Frontier Comms series. " +
                        "If you have questions or ideas, I’d love your feedback.\n\n" +
                        "If you find it useful, a quick review helps a lot—and you can upgrade to the full Frontier Comms pack for matching props.",
                        bodyStyle);
                }

                GUILayout.Space(4);
                GUILayout.Label("// Martin Ljungblad", authorStyle);
                GUILayout.Space(20);

                using (new GUILayout.HorizontalScope())
                {
                    GUILayout.FlexibleSpace();

                    if (GUILayout.Button("Open Readme", GUILayout.Width(120), GUILayout.Height(30)))
                        OpenReadme();

                    GUILayout.Space(12);

                    if (GUILayout.Button("Assign Materials", GUILayout.Width(120), GUILayout.Height(30)))
                        RunAssignerSafe();

                    GUILayout.Space(12);

                    if (GUILayout.Button("Full Pack", GUILayout.Width(120), GUILayout.Height(30)))
                        Application.OpenURL(FullPackURL);

                    GUILayout.Space(12);

                    if (GUILayout.Button("Creator’s Hub", GUILayout.Width(120), GUILayout.Height(30)))
                        Application.OpenURL(CreatorHubURL);

                    GUILayout.Space(12);

                    if (GUILayout.Button("Close", GUILayout.Width(120), GUILayout.Height(30)))
                        Close();

                    GUILayout.FlexibleSpace();
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[Frontier Comms] Welcome window OnGUI error: {ex}");
                GUILayout.Space(10);
                EditorGUILayout.HelpBox("An error occurred while drawing the window. See Console for details.", MessageType.Error);
            }
        }

        private void RunAssignerSafe()
        {
            try
            {
                var rep = FrontierCommsFreeSampleMaterialAssigner.AssignMaterialsForActivePipeline();
                EditorUtility.DisplayDialog(
                    "Frontier Comms — Assign Materials",
                    $"Pipeline: {rep.PipelineLabel}\n" +
                    $"Package root: {rep.PackageRoot}\nMats dir: {rep.MatsDir}\n" +
                    $"Prefabs scanned: {rep.PrefabCount}\n" +
                    $"Replaced: {rep.Replaced}\nSkipped external: {rep.SkippedExternal}\nMissing matches: {rep.Missing}",
                    "OK");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[Frontier Comms] Assign Materials failed: {ex}");
                EditorUtility.DisplayDialog("Frontier Comms — Assign Materials", "An error occurred. See Console for details.", "OK");
            }
        }

        private void OpenReadme()
        {
            if (File.Exists(ReadmePath)) Application.OpenURL(Path.GetFullPath(ReadmePath));
            else Debug.LogWarning("[Frontier Comms] Could not find the README at: " + ReadmePath);
        }

        [MenuItem("Tools/Frontier Comms Free Sample/Show Welcome Message")]
        public static void ShowWelcomeManually()
        {
            var window = CreateInstance<FrontierCommsFreeSampleWelcomeWindow>();
            window.titleContent = new GUIContent("Distress Beacon — Free Sample");
            window.minSize = WinSize;
            CenterWindow(window, WinSize);
            window.ShowUtility();
        }

        [MenuItem("Tools/Frontier Comms Free Sample/Reset Welcome Flag")]
        public static void ResetWelcomeFlag()
        {
            EditorPrefs.DeleteKey(HasShownKey);
            Debug.Log("[Frontier Comms] Welcome flag reset. It will show again next reload.");
        }

        private static void CenterWindow(EditorWindow window, Vector2 size)
        {
            var mw = EditorGUIUtility.GetMainWindowPosition();
            var pos = new Rect(mw.x + (mw.width - size.x) * 0.5f, mw.y + (mw.height - size.y) * 0.5f, size.x, size.y);
            window.position = pos;
        }
    }
}
