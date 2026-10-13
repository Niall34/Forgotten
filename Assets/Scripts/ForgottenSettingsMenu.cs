using TMPro;
using UnityEngine;
using UnityEngine.UI;

// pairs a settings button with the panel that should come back when settings closes
[System.Serializable]
public class SettingsOpener
{
    public Button button;
    public GameObject returnPanel;
}

/// <summary>Tabbed lobby settings with live previews and explicit save/discard behavior.</summary>
public sealed class ForgottenSettingsMenu : MonoBehaviour
{
    [Header("Panels")]
    public GameObject settingsPanel; // the whole settings screen, starts hidden at runtime
    public SettingsOpener[] openers; // one entry per settings button, with the panel to come back to

    [Header("Tabs")]
    public Button[] tabs; // audio, display, controls, chat in that order
    public GameObject[] pages; // same order as the tabs

    [Header("Audio Page")]
    public Slider gameVolumeSlider;
    public Slider musicVolumeSlider;
    public Button testSoundButton;

    [Header("Display Page")]
    public Button[] qualityButtons; // low, medium, high
    public Button[] frameButtons; // 20 fps, 40 fps then 60 fps
    public Slider fieldOfViewSlider;

    [Header("Controls Page")]
    public Slider hudScaleSlider;
    public TextMeshProUGUI hudScaleValue; // optional
    public Slider sensitivitySlider;
    public TextMeshProUGUI sensitivityValue; // optional
    public Button invertOnButton;
    public Button invertOffButton;
    public RectTransform previewJoystick; // the fake joystick in the size preview
    public RectTransform previewChat; // the fake chat button in the size preview

    [Header("Chat Page")]
    public Button chatOnButton;
    public Button chatOffButton;

    [Header("Footer")]
    public Button backButton;
    public Button saveButton;

    [Header("Colors")]
    public Color accentColor = new Color(0.9019608f, 0.4428978f, 0.4428978f, 1f);
    public Color controlColor = new Color32(61, 53, 50, 255);
    public Color textColor = new Color32(239, 230, 218, 255);
    public Color mutedColor = new Color32(185, 173, 164, 255);
    public Color onAccentColor = new Color32(28, 15, 14, 255);

    private GameObject returnPanel;
    private int lastClosedFrame = -1;
    private AudioSource lobbyMusic, testSound;
    private AudioClip testClip;
    private ForgottenSettingsSnapshot draft;
    private bool initialized, updatingControls;

    public bool IsOpen => settingsPanel != null && settingsPanel.activeSelf; // true while the settings screen is showing
    public bool JustClosed => lastClosedFrame == Time.frameCount; // true if it closed this frame, lets other escape handlers back off

    private void Awake() // sets itself up so any scene can use it without a parent script calling initialize
    {
        Initialize();
    }

    public void Initialize() // wires every button and slider, loads the saved settings and hides the panel
    {
        if (initialized)
            return;
        initialized = true;

        // slider ranges live here so the inspector can't drift out of sync with the settings class
        ConfigureSlider(gameVolumeSlider, 0f, 100f);
        ConfigureSlider(musicVolumeSlider, 0f, 100f);
        ConfigureSlider(fieldOfViewSlider, ForgottenGameSettings.MinimumFieldOfView, ForgottenGameSettings.MaximumFieldOfView);
        ConfigureSlider(hudScaleSlider, ForgottenGameSettings.MinimumHudScale * 100f, ForgottenGameSettings.MaximumHudScale * 100f);
        ConfigureSlider(sensitivitySlider, ForgottenGameSettings.MinimumLookSensitivity * 100f, ForgottenGameSettings.MaximumLookSensitivity * 100f);

        for (int i = 0; i < tabs.Length; i++)
        {
            int index = i;
            tabs[i].onClick.AddListener(() => SelectPage(index));
        }

        for (int i = 0; i < qualityButtons.Length; i++)
        {
            int quality = i;
            qualityButtons[i].onClick.AddListener(() => ChangeChoice(quality: quality));
        }

        for (int i = 0; i < frameButtons.Length && i < ForgottenGameSettings.FrameRates.Length; i++)
        {
            int rate = ForgottenGameSettings.FrameRates[i];
            frameButtons[i].onClick.AddListener(() => ChangeChoice(frameRate: rate));
        }

        invertOnButton.onClick.AddListener(() => ChangeChoice(invert: true));
        invertOffButton.onClick.AddListener(() => ChangeChoice(invert: false));
        chatOnButton.onClick.AddListener(() => ChangeChoice(chat: true));
        chatOffButton.onClick.AddListener(() => ChangeChoice(chat: false));
        testSoundButton.onClick.AddListener(PlayTestSound);
        foreach (SettingsOpener opener in openers)
        {
            if (opener.button == null) continue;
            GameObject target = opener.returnPanel;
            opener.button.onClick.AddListener(() => OpenSettings(target));
        }
        backButton.onClick.AddListener(BackWithoutSaving);
        saveButton.onClick.AddListener(SaveAndClose);

        SetControls(ForgottenGameSettings.Load());
        SelectPage(0);
        settingsPanel.SetActive(false);
    }

    private void ConfigureSlider(Slider slider, float minimum, float maximum) // sets the slider range and listens for changes
    {
        slider.minValue = minimum;
        slider.maxValue = maximum;
        slider.wholeNumbers = true;
        slider.onValueChanged.AddListener(HandleControlChanged);
    }

    public void HideImmediately() // closes the panel right away and throws away any unsaved changes
    {
        if (!initialized || settingsPanel == null)
            return;
        if (settingsPanel.activeSelf)
        {
            SetControls(ForgottenGameSettings.Load());
            ApplyDraft();
            lastClosedFrame = Time.frameCount;
        }
        settingsPanel.SetActive(false);
    }

    public void SetLobbyMusic(AudioSource source) // LobbyUI owns the lobby music now, it hands the source over so the music slider can still change its volume live
    {
        lobbyMusic = source;
    }

    private void Update() // escape backs out of settings without saving
    {
        if (settingsPanel != null && settingsPanel.activeSelf &&
            Input.GetKeyDown(KeyCode.Escape))
        {
            BackWithoutSaving();
        }
    }

    private void OnDestroy() // cleans up the generated test tone
    {
        if (testClip != null)
            Destroy(testClip);
    }

    private void OnDisable() // makes sure the panel is closed if this gets switched off
    {
        HideImmediately();
    }

    private void OpenSettings(GameObject openedFrom) // loads the saved values, hides the panel it was opened from and shows settings
    {
        SetControls(ForgottenGameSettings.Load());
        ApplyDraft();
        returnPanel = openedFrom;
        if (returnPanel != null) returnPanel.SetActive(false);
        settingsPanel.SetActive(true);
        SelectPage(0);
    }

    private void SaveAndClose() // writes the draft to disk then goes back to the lobby
    {
        draft = ReadControls();
        ForgottenGameSettings.Save(draft);
        ApplyDraft();
        CloseToPreviousPanel();
    }

    private void BackWithoutSaving() // reverts to what was saved then goes back to the lobby
    {
        SetControls(ForgottenGameSettings.Load());
        ApplyDraft();
        CloseToPreviousPanel();
    }

    private void CloseToPreviousPanel() // hides settings and brings back the panel it was opened from
    {
        settingsPanel.SetActive(false);
        lastClosedFrame = Time.frameCount;
        if (returnPanel != null) returnPanel.SetActive(true);
        returnPanel = null;
    }

    private void SetControls(ForgottenSettingsSnapshot settings) // pushes a snapshot into the sliders without firing their events
    {
        updatingControls = true;
        draft = settings.Sanitized();
        gameVolumeSlider.SetValueWithoutNotify(draft.GameVolume * 100f);
        musicVolumeSlider.SetValueWithoutNotify(draft.MusicVolume * 100f);
        fieldOfViewSlider.SetValueWithoutNotify(draft.FieldOfView);
        hudScaleSlider.SetValueWithoutNotify(draft.HudScale * 100f);
        sensitivitySlider.SetValueWithoutNotify(draft.LookSensitivity * 100f);
        updatingControls = false;
        RefreshLabels();
    }

    private ForgottenSettingsSnapshot ReadControls() // builds a snapshot from the sliders plus the button choices in the draft
    {
        return new ForgottenSettingsSnapshot(
            gameVolumeSlider.value / 100f, musicVolumeSlider.value / 100f,
            draft.GraphicsQuality, fieldOfViewSlider.value, hudScaleSlider.value / 100f,
            draft.GameChatEnabled, sensitivitySlider.value / 100f,
            draft.InvertLook, draft.FrameRate).Sanitized();
    }

    private void HandleControlChanged(float ignored) // runs when any slider moves, updates the draft and previews it live
    {
        if (updatingControls)
            return;
        draft = ReadControls();
        RefreshLabels();
        ApplyDraft();
    }

    private void ChangeChoice(int? quality = null, int? frameRate = null, bool? chat = null, bool? invert = null) // changes one button setting in the draft and leaves the rest alone
    {
        draft = new ForgottenSettingsSnapshot(draft.GameVolume, draft.MusicVolume,
            quality ?? draft.GraphicsQuality, draft.FieldOfView, draft.HudScale,
            chat ?? draft.GameChatEnabled, draft.LookSensitivity,
            invert ?? draft.InvertLook, frameRate ?? draft.FrameRate);
        RefreshLabels();
        ApplyDraft();
    }

    private void ApplyDraft() // previews the draft right now, including the lobby music volume
    {
        ForgottenGameSettings.Apply(draft);
        if (lobbyMusic != null)
            lobbyMusic.volume = draft.MusicVolume;
    }

    private void SetSelected(Button button, bool selected) // colours a button as picked or not picked
    {
        if (button.image != null)
            button.image.color = selected ? accentColor : controlColor;
        TextMeshProUGUI label = button.GetComponentInChildren<TextMeshProUGUI>();
        if (label != null)
            label.color = selected ? onAccentColor : textColor;
    }

    private void RefreshLabels() // updates the button highlights, and the remaining value labels
    {
        if (hudScaleValue != null)
            hudScaleValue.text = hudScaleSlider.value + "%";
        if (sensitivityValue != null)
            sensitivityValue.text = (sensitivitySlider.value / 100f).ToString("0.00") + "x";
        SetSelected(chatOnButton, draft.GameChatEnabled);
        SetSelected(chatOffButton, !draft.GameChatEnabled);
        SetSelected(invertOnButton, draft.InvertLook);
        SetSelected(invertOffButton, !draft.InvertLook);
        for (int i = 0; i < qualityButtons.Length; i++)
            SetSelected(qualityButtons[i], draft.GraphicsQuality == i);
        for (int i = 0; i < frameButtons.Length && i < ForgottenGameSettings.FrameRates.Length; i++)
            SetSelected(frameButtons[i], draft.FrameRate == ForgottenGameSettings.FrameRates[i]);
        if (previewJoystick != null)
            previewJoystick.localScale = Vector3.one * (draft.HudScale * 0.43f);
        if (previewChat != null)
            previewChat.localScale = Vector3.one * (draft.HudScale * 0.7f);
    }

    private void SelectPage(int index) // shows one page and highlights its tab
    {
        for (int i = 0; i < pages.Length; i++)
        {
            pages[i].SetActive(i == index);
            SetSelected(tabs[i], i == index);
        }
    }

    private void PlayTestSound() // builds a short quiet tone the first time then plays it to check volume
    {
        if (testSound == null)
        {
            testSound = gameObject.AddComponent<AudioSource>();
            testSound.playOnAwake = false;
            testSound.spatialBlend = 0f;
            const int sampleRate = 22050;
            float[] samples = new float[sampleRate / 4];
            for (int i = 0; i < samples.Length; i++)
            {
                float t = (float)i / sampleRate;
                float envelope = Mathf.Sin(Mathf.PI * i / (samples.Length - 1));
                samples[i] = Mathf.Sin(2f * Mathf.PI * 440f * t) * envelope * 0.16f;
            }
            testClip = AudioClip.Create("Volume Test", samples.Length, 1, sampleRate, false);
            testClip.SetData(samples, 0);
            testSound.clip = testClip;
        }
        testSound.Stop();
        testSound.Play();
    }
}