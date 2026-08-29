using UnityEngine;
using UnityEngine.Rendering;
using System.Collections.Generic;

namespace FrontierComms
{
    [CreateAssetMenu(fileName = "FrontierComms_FullMaterialMap", menuName = "FrontierComms/Full Material Map")]
    public class FullMaterialMap : ScriptableObject
    {
        public List<MaterialSlotMapping> mappings = new();

#if UNITY_EDITOR
        public MaterialSlotMapping FindMatch(string assetPath, string rendererPath, int materialIndex)
        {
            string guid = UnityEditor.AssetDatabase.AssetPathToGUID(assetPath);
            foreach (var m in mappings)
            {
                if (m.assetGUID == guid && m.rendererPath == rendererPath && m.materialIndex == materialIndex)
                    return m;
            }
            return null;
        }
#endif
    }

    [System.Serializable]
    public class MaterialSlotMapping
    {
        public string assetGUID;
        public string assetName;
        public string rendererPath;
        public int materialIndex;

        public Material builtInMat;
        public Material urpMat;
        public Material hdrpMat;

        public Material GetMaterialForCurrentPipeline()
        {
            var pipeline = GraphicsSettings.currentRenderPipeline?.GetType().ToString();
            if (pipeline == null) return builtInMat;
            if (pipeline.Contains("Universal")) return urpMat;
            if (pipeline.Contains("HDRenderPipeline")) return hdrpMat;
            return builtInMat;
        }
    }
}
