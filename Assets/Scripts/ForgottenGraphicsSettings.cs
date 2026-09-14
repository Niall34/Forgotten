using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>Applies mobile presets to a runtime copy, keeping the project's renderer assets intact.</summary>
public static class ForgottenGraphicsSettings
{
    private static RenderPipelineAsset originalPipeline;
    private static UniversalRenderPipelineAsset runtimePipeline;
    private static int originalVSync;
    private static int originalFrameRate;
    private static bool initialized;
    private static int appliedQuality = -1;

    public static void Apply(int quality, int frameRate)
    {
        if (!Application.isPlaying)
            return;
#if UNITY_EDITOR
        if (!UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode)
            return;
#endif

        if (!initialized)
        {
            initialized = true;
            originalPipeline = QualitySettings.renderPipeline;
            originalVSync = QualitySettings.vSyncCount;
            originalFrameRate = Application.targetFrameRate;
            UniversalRenderPipelineAsset source = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (source != null)
            {
                runtimePipeline = Object.Instantiate(source);
                runtimePipeline.name = "Runtime Graphics Settings";
                runtimePipeline.hideFlags = HideFlags.DontSave;
                QualitySettings.renderPipeline = runtimePipeline;
            }
            Application.quitting += Restore;
        }

        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = frameRate <= 30 ? 30 : 60;
        quality = Mathf.Clamp(quality, 0, 2);
        if (runtimePipeline == null || appliedQuality == quality)
            return;

        appliedQuality = quality;
        runtimePipeline.renderScale = quality == 0 ? 0.65f : quality == 1 ? 0.85f : 1f;
        runtimePipeline.shadowDistance = quality == 0 ? 20f : quality == 1 ? 35f : 50f;
        runtimePipeline.msaaSampleCount = quality == 0 ? 1 : quality == 1 ? 2 : 4;
    }

    public static void Restore()
    {
        if (!initialized)
            return;
        QualitySettings.renderPipeline = originalPipeline;
        QualitySettings.vSyncCount = originalVSync;
        Application.targetFrameRate = originalFrameRate;
        if (runtimePipeline != null)
            Object.DestroyImmediate(runtimePipeline);
        runtimePipeline = null;
        initialized = false;
        appliedQuality = -1;
        Application.quitting -= Restore;
    }

#if UNITY_EDITOR
    [UnityEditor.InitializeOnLoadMethod]
    private static void RegisterEditorCleanup()
    {
        UnityEditor.EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        UnityEditor.EditorApplication.playModeStateChanged += OnPlayModeChanged;
        UnityEditor.AssemblyReloadEvents.beforeAssemblyReload -= Restore;
        UnityEditor.AssemblyReloadEvents.beforeAssemblyReload += Restore;
    }

    private static void OnPlayModeChanged(UnityEditor.PlayModeStateChange state)
    {
        if (state == UnityEditor.PlayModeStateChange.ExitingPlayMode)
            Restore();
    }
#endif
}
