using UnityEngine;

/// <summary>
/// Persistent, platform-safe settings shared by the lobby and gameplay scenes.
/// UI code edits a snapshot first, then saves it only when the player confirms.
/// </summary>
public static class ForgottenGameSettings
{
    private const string Prefix = "Forgotten.Settings.";
    private const string GameVolumeKey = Prefix + "MasterVolume";
    private const string MusicVolumeKey = Prefix + "MusicVolume";
    private const string GraphicsQualityKey = Prefix + "GraphicsQuality";
    private const string FieldOfViewKey = Prefix + "FieldOfView";
    private const string HudScaleKey = Prefix + "HudScale";
    private const string GameChatKey = Prefix + "GameChat";
    private const string LegacyGameChatKey = Prefix + "VoiceChat";
    private const string LookSensitivityKey = Prefix + "LookSensitivity";
    private const string InvertLookKey = Prefix + "InvertLook";
    private const string FrameRateKey = Prefix + "FrameRate";

    public const float MinimumFieldOfView = 60f;
    public const float MaximumFieldOfView = 100f;
    public const float MinimumHudScale = 0.7f;
    public const float MaximumHudScale = 1.1f;
    public const float MinimumLookSensitivity = 0.5f;
    public const float MaximumLookSensitivity = 2f;

    public static ForgottenSettingsSnapshot Defaults => new ForgottenSettingsSnapshot(
        gameVolume: 0.85f,
        musicVolume: 0.75f,
        graphicsQuality: 1,
        fieldOfView: 72f,
        hudScale: 0.82f,
        gameChatEnabled: true);

    public static float FieldOfView => Load().FieldOfView;
    public static float HudScale => Load().HudScale;
    public static bool IsGameChatEnabled => Load().GameChatEnabled;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void ApplyAtStartup()
    {
        Apply(Load());
    }

    public static ForgottenSettingsSnapshot Load()
    {
        ForgottenSettingsSnapshot defaults = Defaults;
        bool chatEnabled = PlayerPrefs.HasKey(GameChatKey)
            ? PlayerPrefs.GetInt(GameChatKey) == 1
            : PlayerPrefs.GetInt(LegacyGameChatKey, defaults.GameChatEnabled ? 1 : 0) == 1;

        return new ForgottenSettingsSnapshot(
            PlayerPrefs.GetFloat(GameVolumeKey, defaults.GameVolume),
            PlayerPrefs.GetFloat(MusicVolumeKey, defaults.MusicVolume),
            PlayerPrefs.GetInt(GraphicsQualityKey, defaults.GraphicsQuality),
            PlayerPrefs.GetFloat(FieldOfViewKey, defaults.FieldOfView),
            PlayerPrefs.GetFloat(HudScaleKey, defaults.HudScale),
            chatEnabled,
            PlayerPrefs.GetFloat(LookSensitivityKey, 1f),
            PlayerPrefs.GetInt(InvertLookKey, 0) == 1,
            PlayerPrefs.GetInt(FrameRateKey, 60)).Sanitized();
    }

    public static void Save(ForgottenSettingsSnapshot settings)
    {
        ForgottenSettingsSnapshot sanitized = settings.Sanitized();
        PlayerPrefs.SetFloat(GameVolumeKey, sanitized.GameVolume);
        PlayerPrefs.SetFloat(MusicVolumeKey, sanitized.MusicVolume);
        PlayerPrefs.SetInt(GraphicsQualityKey, sanitized.GraphicsQuality);
        PlayerPrefs.SetFloat(FieldOfViewKey, sanitized.FieldOfView);
        PlayerPrefs.SetFloat(HudScaleKey, sanitized.HudScale);
        PlayerPrefs.SetInt(GameChatKey, sanitized.GameChatEnabled ? 1 : 0);
        PlayerPrefs.SetFloat(LookSensitivityKey, sanitized.LookSensitivity);
        PlayerPrefs.SetInt(InvertLookKey, sanitized.InvertLook ? 1 : 0);
        PlayerPrefs.SetInt(FrameRateKey, sanitized.FrameRate);
        PlayerPrefs.DeleteKey(LegacyGameChatKey);
        PlayerPrefs.Save();
        Apply(sanitized);
    }

    public static void Apply(ForgottenSettingsSnapshot settings)
    {
        ForgottenSettingsSnapshot sanitized = settings.Sanitized();
        AudioListener.volume = sanitized.GameVolume;

        ForgottenGraphicsSettings.Apply(sanitized.GraphicsQuality, sanitized.FrameRate);
    }
}

public readonly struct ForgottenSettingsSnapshot
{
    public float GameVolume { get; }
    public float MusicVolume { get; }
    public int GraphicsQuality { get; }
    public float FieldOfView { get; }
    public float HudScale { get; }
    public bool GameChatEnabled { get; }
    public float LookSensitivity { get; }
    public bool InvertLook { get; }
    public int FrameRate { get; }

    public ForgottenSettingsSnapshot(
        float gameVolume,
        float musicVolume,
        int graphicsQuality,
        float fieldOfView,
        float hudScale,
        bool gameChatEnabled,
        float lookSensitivity = 1f,
        bool invertLook = false,
        int frameRate = 60)
    {
        GameVolume = gameVolume;
        MusicVolume = musicVolume;
        GraphicsQuality = graphicsQuality;
        FieldOfView = fieldOfView;
        HudScale = hudScale;
        GameChatEnabled = gameChatEnabled;
        LookSensitivity = lookSensitivity;
        InvertLook = invertLook;
        FrameRate = frameRate;
    }

    public ForgottenSettingsSnapshot Sanitized()
    {
        return new ForgottenSettingsSnapshot(
            ClampFinite(GameVolume, 0f, 1f, 0.85f),
            ClampFinite(MusicVolume, 0f, 1f, 0.75f),
            Mathf.Clamp(GraphicsQuality, 0, 2),
            ClampFinite(FieldOfView, ForgottenGameSettings.MinimumFieldOfView, ForgottenGameSettings.MaximumFieldOfView, 72f),
            ClampFinite(HudScale, ForgottenGameSettings.MinimumHudScale, ForgottenGameSettings.MaximumHudScale, 0.82f),
            GameChatEnabled,
            ClampFinite(LookSensitivity, ForgottenGameSettings.MinimumLookSensitivity, ForgottenGameSettings.MaximumLookSensitivity, 1f),
            InvertLook,
            FrameRate <= 30 ? 30 : 60);
    }

    private static float ClampFinite(float value, float minimum, float maximum, float fallback)
    {
        return float.IsNaN(value) || float.IsInfinity(value) ? fallback : Mathf.Clamp(value, minimum, maximum);
    }
}
