// Assets/FrontierComms_FreeSample/Editor/FrontierCommsFreeSampleMaterialAssigner.cs
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace FrontierCommsEditor
{
    /// <summary>
    /// Safe-only auto material assigner for the Distress Beacon Free Sample.
    /// Discovers the package root from THIS script, then assigns materials from:
    /// {PackageRoot}/Materials/{Built-In|URP|HDRP} to prefabs in {PackageRoot}/Prefabs.
    /// Safe mode: never overwrites materials that live outside {PackageRoot}/Materials.
    /// </summary>
    public class FrontierCommsFreeSampleMaterialAssigner : ScriptableObject
    {
        public struct AssignReport
        {
            public int Replaced;
            public int PrefabCount;
            public int SkippedExternal;
            public int Missing;
            public string PipelineLabel;
            public string PackageRoot;
            public string MatsDir;
        }

        [MenuItem("Tools/Frontier Comms Free Sample/Assign Materials")]
        public static void AssignMaterialsMenu()
        {
            var rep = AssignMaterialsForActivePipeline();
            EditorUtility.DisplayDialog(
                "Frontier Comms — Assign Materials",
                $"Pipeline: {rep.PipelineLabel}\n" +
                $"Package root: {rep.PackageRoot}\nMats dir: {rep.MatsDir}\n" +
                $"Prefabs scanned: {rep.PrefabCount}\n" +
                $"Replaced: {rep.Replaced}\nSkipped external: {rep.SkippedExternal}\nMissing matches: {rep.Missing}",
                "OK");
            Debug.Log($"[Frontier Comms] Assigned {rep.Replaced} mat slots; skipped {rep.SkippedExternal} external; missing {rep.Missing}; prefabs {rep.PrefabCount}.");
        }

        /// <summary>Run safe assignment (used by menu + Welcome Window).</summary>
        public static AssignReport AssignMaterialsForActivePipeline()
        {
            // 1) Find the package root robustly (walk up until we find Prefabs/ + Materials/)
            var scriptPath = GetThisScriptPath();
            var startDir = Path.GetDirectoryName(scriptPath).Replace("\\", "/");
            var packageRoot = FindPackageRoot(startDir);

            // 2) Resolve Materials/... for active pipeline (no direct URP/HDRP type references)
            var materialsRoot = FindSubfolderIgnoreCase(packageRoot, "Materials");
            GetPipelineFolder(out var pipelineLabel, out var pipelineSubfolder);
            var matsDir = FindFirstExisting(materialsRoot, new[] { pipelineSubfolder });
            var prefabsDir = FindSubfolderIgnoreCase(packageRoot, "Prefabs");
            var ourMatsRoot = materialsRoot;

            // Early exits with report if structure missing
            if (string.IsNullOrEmpty(packageRoot) || string.IsNullOrEmpty(ourMatsRoot) || string.IsNullOrEmpty(prefabsDir))
            {
                Debug.LogWarning($"[Frontier Comms] Expected folders not found. Root: {packageRoot} | Materials: {ourMatsRoot} | Prefabs: {prefabsDir}");
                return new AssignReport { PipelineLabel = pipelineLabel, PackageRoot = packageRoot, MatsDir = matsDir };
            }
            if (string.IsNullOrEmpty(matsDir))
            {
                Debug.LogWarning($"[Frontier Comms] No pipeline materials folder found under {ourMatsRoot} for pipeline '{pipelineLabel}'.");
                return new AssignReport { PipelineLabel = pipelineLabel, PackageRoot = packageRoot, MatsDir = matsDir };
            }

            // 3) Load materials (lookup by exact + base name)
            var matDict = LoadMaterialsByName(matsDir);

            // 4) Assign across our prefabs only (safe: skip slots using external materials)
            var prefabGUIDs = AssetDatabase.FindAssets("t:Prefab", new[] { prefabsDir });
            int replaced = 0, prefabCount = 0, skippedExternal = 0, missing = 0;
            var changedPaths = new List<string>();

            foreach (var guid in prefabGUIDs)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (!go) continue;
                prefabCount++;

                var renderers = go.GetComponentsInChildren<Renderer>(true);
                bool dirty = false;

                foreach (var r in renderers)
                {
                    var mats = r.sharedMaterials;
                    bool changedAny = false;

                    for (int i = 0; i < mats.Length; i++)
                    {
                        var current = mats[i];
                        if (!current) continue;

                        // SAFE: skip if user already swapped in a material outside our package
                        var curPath = AssetDatabase.GetAssetPath(current)?.Replace("\\", "/");
                        if (!string.IsNullOrEmpty(curPath) && !curPath.StartsWith(ourMatsRoot))
                        {
                            skippedExternal++;
                            continue;
                        }

                        var baseName = ToBaseName(current.name);
                        var desired = pipelineLabel switch
                        {
                            "URP" => baseName + "_URP",
                            "HDRP" => baseName + "_HDRP",
                            _ => baseName // Built-in
                        };

                        Material newMat = null;
                        if (!matDict.TryGetValue(desired, out newMat))
                            matDict.TryGetValue(baseName, out newMat);

                        if (newMat && newMat != current)
                        {
                            mats[i] = newMat;
                            replaced++;
                            changedAny = true;
                        }
                        else if (!newMat)
                        {
                            missing++;
                        }
                    }

                    if (changedAny)
                    {
                        Undo.RecordObject(r, "Assign Frontier Comms Materials");
                        r.sharedMaterials = mats;
                        dirty = true;
                    }
                }

                if (dirty)
                {
                    EditorUtility.SetDirty(go);
                    changedPaths.Add(path);
                }
            }

            // Only write our changed prefabs (avoid saving unrelated assets)
            if (changedPaths.Count > 0)
                AssetDatabase.ForceReserializeAssets(changedPaths);

            AssetDatabase.Refresh();

            return new AssignReport
            {
                Replaced = replaced,
                PrefabCount = prefabCount,
                SkippedExternal = skippedExternal,
                Missing = missing,
                PipelineLabel = pipelineLabel,
                PackageRoot = packageRoot,
                MatsDir = matsDir
            };
        }

        // -------- Pipeline detection with no URP/HDRP type refs --------
        private static void GetPipelineFolder(out string pipelineLabel, out string pipelineSubfolder)
        {
            var srp = GraphicsSettings.currentRenderPipeline;
            if (srp == null)
            {
                pipelineLabel = "Built-in";
                pipelineSubfolder = "Built-In"; // match your folder name
                return;
            }

            var full = srp.GetType().FullName ?? srp.GetType().Name;
            var low = full.ToLowerInvariant();

            if (low.Contains("universal"))
            {
                pipelineLabel = "URP";
                pipelineSubfolder = "URP";
            }
            else if (low.Contains("highdefinition") || low.Contains(".hd") || low.Contains("hdrp"))
            {
                pipelineLabel = "HDRP";
                pipelineSubfolder = "HDRP";
            }
            else
            {
                // Unknown SRP: default to URP folder as a sensible fallback
                pipelineLabel = full;
                pipelineSubfolder = "URP";
            }
        }

        // ----------------- Helpers -----------------
        private static string GetThisScriptPath()
        {
            var typeName = nameof(FrontierCommsFreeSampleMaterialAssigner);
            var guids = AssetDatabase.FindAssets($"{typeName} t:MonoScript");
            foreach (var g in guids)
            {
                var p = AssetDatabase.GUIDToAssetPath(g);
                if (p.EndsWith(".cs")) return p;
            }
            var any = AssetDatabase.FindAssets("t:MonoScript", new[] { "Assets" }).FirstOrDefault();
            return AssetDatabase.GUIDToAssetPath(any);
        }

        private static string FindPackageRoot(string startDir)
        {
            // Walk up max ~8 levels; look for a folder containing Materials/ and Prefabs/
            var dir = startDir;
            for (int i = 0; i < 8 && !string.IsNullOrEmpty(dir); i++)
            {
                if (HasOurStructure(dir)) return dir;
                var parent = Path.GetDirectoryName(dir);
                if (string.IsNullOrEmpty(parent)) break;
                dir = parent.Replace("\\", "/");
            }

            // Fallback: search under Assets for any folder that contains both
            var candidates = new List<string>();
            foreach (var guid in AssetDatabase.FindAssets("", new[] { "Assets" }))
            {
                var p = AssetDatabase.GUIDToAssetPath(guid);
                if (AssetDatabase.IsValidFolder(p) && HasOurStructure(p))
                    candidates.Add(p);
            }

            // Prefer a folder named FrontierComms_FreeSample if present
            var preferred = candidates.FirstOrDefault(p => p.EndsWith("/FrontierComms_FreeSample"));
            if (!string.IsNullOrEmpty(preferred)) return preferred;

            return candidates.FirstOrDefault() ?? "Assets";
        }

        private static bool HasOurStructure(string root)
        {
            return AssetDatabase.IsValidFolder(root + "/Materials") &&
                   AssetDatabase.IsValidFolder(root + "/Prefabs");
        }

        private static string FindSubfolderIgnoreCase(string parent, string name)
        {
            if (string.IsNullOrEmpty(parent)) return null;
            var exact = parent + "/" + name;
            if (AssetDatabase.IsValidFolder(exact)) return exact;

            foreach (var sub in AssetDatabase.GetSubFolders(parent))
            {
                var leaf = sub.Substring(parent.Length + 1);
                if (leaf.Equals(name, System.StringComparison.OrdinalIgnoreCase))
                    return sub;
            }
            return null;
        }

        private static string FindFirstExisting(string parent, IEnumerable<string> names)
        {
            if (string.IsNullOrEmpty(parent)) return null;
            foreach (var n in names)
            {
                var f = FindSubfolderIgnoreCase(parent, n);
                if (!string.IsNullOrEmpty(f)) return f;
            }
            return null;
        }

        private static Dictionary<string, Material> LoadMaterialsByName(string folderPath)
        {
            var dict = new Dictionary<string, Material>();
            if (!AssetDatabase.IsValidFolder(folderPath))
            {
                Debug.LogWarning($"[Frontier Comms] Materials folder not found: {folderPath}");
                return dict;
            }

            foreach (var guid in AssetDatabase.FindAssets("t:Material", new[] { folderPath }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (!mat) continue;

                var exact = mat.name;
                var baseName = ToBaseName(exact);

                if (!dict.ContainsKey(exact)) dict.Add(exact, mat);
                if (!dict.ContainsKey(baseName)) dict.Add(baseName, mat);
            }
            return dict;
        }

        private static string ToBaseName(string name)
        {
            if (string.IsNullOrEmpty(name)) return name;
            var n = name.Replace(" (Instance)", "");
            if (n.EndsWith("_URP")) n = n[..^4];
            if (n.EndsWith("_HDRP")) n = n[..^5];
            if (n.EndsWith("_BI")) n = n[..^3]; // optional convention
            return n;
        }
    }
}
