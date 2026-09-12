using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UI = ForgottenSettingsUI;

/// <summary>Tabbed lobby settings with live previews and explicit save/discard behavior.</summary>
public sealed class ForgottenSettingsMenu : MonoBehaviour
{
    private const string LobbyMusicResourcePath = "Forgotten/Audio/whispering_shadows";
    private readonly GameObject[] pages = new GameObject[4];
    private readonly Button[] tabs = new Button[4];
    private readonly Button[] qualityButtons = new Button[3];
    private readonly Button[] frameButtons = new Button[2];
    private GameObject mainLobbyPanel;
    private GameObject settingsPanel;
    private GameObject resetConfirmation;
    private ScrollRect scroll;
    private Slider gameVolumeSlider, musicVolumeSlider, fieldOfViewSlider, hudScaleSlider, sensitivitySlider;
    private TextMeshProUGUI gameVolumeValue, musicVolumeValue, fieldOfViewValue, hudScaleValue, sensitivityValue;
    private TextMeshProUGUI stateLabel, chatValue, invertValue, graphicsDescription;
    private Button chatButton, invertButton;
    private RectTransform previewJoystick, previewChat;
    private AudioSource lobbyMusic, testSound;
    private AudioClip testClip;
    private ForgottenSettingsSnapshot draft, saved;
    private bool initialized, updatingControls;

    public void Initialize(GameObject lobbyPanel)
    {
        if (initialized)
            return;
        initialized = true;
        mainLobbyPanel = lobbyPanel;
        CreateSettingsButton(lobbyPanel.transform);
        settingsPanel = CreateSettingsPanel();
        SetControls(saved = ForgottenGameSettings.Load());
        SelectPage(0);
        settingsPanel.SetActive(false);
        InitializeLobbyMusic();
    }

    public void HideImmediately()
    {
        if (settingsPanel == null)
            return;
        if (settingsPanel.activeSelf)
        {
            SetControls(saved = ForgottenGameSettings.Load());
            ApplyDraft();
        }
        settingsPanel.SetActive(false);
    }

    public void StopLobbyMusic()
    {
        if (lobbyMusic != null)
            lobbyMusic.Stop();
    }

    private void Update()
    {
        if (settingsPanel != null && settingsPanel.activeSelf &&
            Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            if (resetConfirmation.activeSelf)
                resetConfirmation.SetActive(false);
            else
                BackWithoutSaving();
        }
    }

    private void OnDestroy()
    {
        if (settingsPanel != null)
            Destroy(settingsPanel);
        if (testClip != null)
            Destroy(testClip);
    }

    private void OnDisable()
    {
        HideImmediately();
    }

    private void OpenSettings()
    {
        SetControls(saved = ForgottenGameSettings.Load());
        ApplyDraft();
        mainLobbyPanel.SetActive(false);
        settingsPanel.SetActive(true);
        resetConfirmation.SetActive(false);
        SelectPage(0);
    }

    private void SaveAndClose()
    {
        draft = ReadControls();
        ForgottenGameSettings.Save(draft);
        saved = draft;
        ApplyDraft();
        CloseToLobby();
    }

    private void BackWithoutSaving()
    {
        SetControls(saved = ForgottenGameSettings.Load());
        ApplyDraft();
        CloseToLobby();
    }

    private void ResetDraft()
    {
        SetControls(ForgottenGameSettings.Defaults);
        ApplyDraft();
        resetConfirmation.SetActive(false);
    }

    private void CloseToLobby()
    {
        resetConfirmation.SetActive(false);
        settingsPanel.SetActive(false);
        mainLobbyPanel.SetActive(true);
    }

    private void SetControls(ForgottenSettingsSnapshot settings)
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

    private ForgottenSettingsSnapshot ReadControls()
    {
        return new ForgottenSettingsSnapshot(
            gameVolumeSlider.value / 100f, musicVolumeSlider.value / 100f,
            draft.GraphicsQuality, fieldOfViewSlider.value, hudScaleSlider.value / 100f,
            draft.GameChatEnabled, sensitivitySlider.value / 100f,
            draft.InvertLook, draft.FrameRate).Sanitized();
    }

    private void HandleControlChanged(float ignored)
    {
        if (updatingControls)
            return;
        draft = ReadControls();
        RefreshLabels();
        ApplyDraft();
    }

    private void ChangeChoice(int? quality = null, int? frameRate = null, bool? chat = null, bool? invert = null)
    {
        draft = new ForgottenSettingsSnapshot(draft.GameVolume, draft.MusicVolume,
            quality ?? draft.GraphicsQuality, draft.FieldOfView, draft.HudScale,
            chat ?? draft.GameChatEnabled, draft.LookSensitivity,
            invert ?? draft.InvertLook, frameRate ?? draft.FrameRate);
        RefreshLabels();
        ApplyDraft();
    }

    private void ApplyDraft()
    {
        ForgottenGameSettings.Apply(draft);
        if (lobbyMusic != null)
            lobbyMusic.volume = draft.MusicVolume;
    }

    private void RefreshLabels()
    {
        gameVolumeValue.text = gameVolumeSlider.value == 0f ? "MUTED" : gameVolumeSlider.value + "%";
        musicVolumeValue.text = musicVolumeSlider.value == 0f ? "MUTED" : musicVolumeSlider.value + "%";
        fieldOfViewValue.text = fieldOfViewSlider.value + "°";
        hudScaleValue.text = hudScaleSlider.value + "%";
        sensitivityValue.text = (sensitivitySlider.value / 100f).ToString("0.00") + "x";
        chatValue.text = draft.GameChatEnabled ? "ON" : "OFF";
        invertValue.text = draft.InvertLook ? "ON" : "OFF";
        chatButton.image.color = draft.GameChatEnabled ? UI.Accent : UI.Control;
        invertButton.image.color = draft.InvertLook ? UI.Accent : UI.Control;
        for (int i = 0; i < qualityButtons.Length; i++)
            qualityButtons[i].image.color = draft.GraphicsQuality == i ? UI.Accent : UI.Control;
        for (int i = 0; i < frameButtons.Length; i++)
            frameButtons[i].image.color = draft.FrameRate == (i == 0 ? 30 : 60) ? UI.Accent : UI.Control;
        graphicsDescription.text = draft.GraphicsQuality == 0
            ? "LOW  /  Reduced resolution and shadows. Start here on older phones."
            : draft.GraphicsQuality == 1
                ? "MEDIUM  /  Balanced resolution, shadows and edge smoothing."
                : "HIGH  /  Full resolution, longer shadows and smoother edges.";
        bool changed = !draft.Equals(saved);
        stateLabel.text = changed ? "Unsaved changes" : "All changes saved";
        stateLabel.color = changed ? new Color32(231, 168, 108, 255) : UI.Muted;
        previewJoystick.localScale = Vector3.one * (draft.HudScale * 0.43f);
        previewChat.localScale = Vector3.one * (draft.HudScale * 0.7f);
    }

    private void SelectPage(int index)
    {
        scroll.StopMovement();
        for (int i = 0; i < pages.Length; i++)
        {
            pages[i].SetActive(i == index);
            tabs[i].image.color = i == index ? UI.Accent : UI.Control;
        }
        scroll.content = (RectTransform)pages[index].transform;
        Canvas.ForceUpdateCanvases();
        scroll.verticalNormalizedPosition = 1f;
    }

    private void InitializeLobbyMusic()
    {
        AudioClip clip = Resources.Load<AudioClip>(LobbyMusicResourcePath);
        if (clip == null)
        {
            Debug.LogWarning("Lobby music is missing from Resources/Forgotten/Audio/whispering_shadows.", this);
            return;
        }
        lobbyMusic = gameObject.AddComponent<AudioSource>();
        lobbyMusic.clip = clip;
        lobbyMusic.loop = true;
        lobbyMusic.playOnAwake = false;
        lobbyMusic.spatialBlend = 0f;
        lobbyMusic.volume = draft.MusicVolume;
        lobbyMusic.Play();
    }

    private void PlayTestSound()
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

    private void CreateSettingsButton(Transform parent)
    {
        Button button = UI.Button(parent, "SettingsButton", "SETTINGS");
        RectTransform rect = (RectTransform)button.transform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(-536f, -172f);
        rect.sizeDelta = new Vector2(160f, 44f);
        button.onClick.AddListener(OpenSettings);
    }

    private GameObject CreateSettingsPanel()
    {
        RectTransform overlay = UI.Rect("Settings Menu", null);
        Canvas canvas = overlay.gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        CanvasScaler scaler = overlay.gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280f, 720f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        overlay.gameObject.AddComponent<GraphicRaycaster>();
        if (Application.isPlaying)
            DontDestroyOnLoad(overlay.gameObject);
        UI.Paint(overlay, new Color(0.005f, 0.008f, 0.012f, 0.96f));
        RectTransform safe = UI.Rect("Safe Area", overlay);
        UI.Stretch(safe);
        safe.gameObject.AddComponent<MobileSafeArea>();
        RectTransform card = UI.Rect("Settings Card", safe);
        UI.Anchors(card, new Vector2(0.05f, 0.04f), new Vector2(0.95f, 0.96f));
        UI.Paint(card, UI.Background);
        UI.Vertical(card, 24, 12f);

        RectTransform header = UI.Rect("Header", card);
        UI.Layout(header.gameObject, 64f);
        TextMeshProUGUI eyebrow = UI.Text(header, "Eyebrow", "FORGOTTEN  /  PREFERENCES", 13f, true);
        UI.Anchors(eyebrow.rectTransform, new Vector2(0f, 0.65f), Vector2.one);
        TextMeshProUGUI title = UI.Text(header, "Title", "Make it your game.", 30f);
        UI.Anchors(title.rectTransform, Vector2.zero, new Vector2(0.75f, 0.65f));
        TextMeshProUGUI hint = UI.Text(header, "Hint", "SAVED ON THIS DEVICE", 12f, true);
        hint.alignment = TextAlignmentOptions.Right;
        UI.Anchors(hint.rectTransform, new Vector2(0.72f, 0f), new Vector2(1f, 0.65f));

        RectTransform tabBar = UI.Rect("Tabs", card);
        UI.Layout(tabBar.gameObject, 48f);
        UI.Horizontal(tabBar, 8f);
        string[] labels = { "01   AUDIO", "02   DISPLAY", "03   CONTROLS", "04   CHAT" };
        for (int i = 0; i < tabs.Length; i++)
        {
            int index = i;
            tabs[i] = UI.Button(tabBar, labels[i], labels[i]);
            UI.Layout(tabs[i].gameObject, 48f, 0f, 1f);
            tabs[i].onClick.AddListener(() => SelectPage(index));
        }

        RectTransform body = UI.Rect("Page View", card);
        UI.Layout(body.gameObject, 60f);
        body.GetComponent<LayoutElement>().flexibleHeight = 1f;
        UI.Paint(body, UI.Surface);
        scroll = body.gameObject.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 24f;
        RectTransform viewport = UI.Rect("Viewport", body);
        UI.Stretch(viewport);
        viewport.gameObject.AddComponent<RectMask2D>();
        scroll.viewport = viewport;
        string[] pageNames = { "Audio", "Display", "Controls", "Chat" };
        for (int i = 0; i < pages.Length; i++)
        {
            RectTransform page = UI.Rect(pageNames[i] + " Page", viewport);
            page.anchorMin = new Vector2(0f, 1f);
            page.anchorMax = Vector2.one;
            page.pivot = new Vector2(0.5f, 1f);
            page.sizeDelta = Vector2.zero;
            UI.Vertical(page, 16, 8f);
            page.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            pages[i] = page.gameObject;
        }
        BuildAudio(pages[0].transform);
        BuildDisplay(pages[1].transform);
        BuildControls(pages[2].transform);
        BuildChat(pages[3].transform);
        CreateFooter(card);
        CreateResetConfirmation(overlay);
        return overlay.gameObject;
    }

    private void BuildAudio(Transform parent)
    {
        Description(parent, "Keep the atmosphere. Hear your team.", 24f);
        Description(parent, "Adjust the lobby music live. Game volume controls all sound, including music.", 16f, true);
        gameVolumeSlider = SliderRow(parent, "Game volume", "Overall listening level", 0f, 100f, out gameVolumeValue);
        musicVolumeSlider = SliderRow(parent, "Music volume", "Lobby soundtrack", 0f, 100f, out musicVolumeValue);
        RectTransform row = Row(parent, "Audio test", "A short, gentle tone to check game volume.", 60f);
        Button test = UI.Button(row, "Test Sound", "TEST SOUND");
        UI.Layout(test.gameObject, 48f, 170f);
        test.onClick.AddListener(PlayTestSound);
    }

    private void BuildDisplay(Transform parent)
    {
        Description(parent, "Find your balance.", 24f);
        RectTransform qualityRow = Row(parent, "Graphics", "Detail and image clarity", 66f);
        string[] names = { "LOW", "MEDIUM", "HIGH" };
        for (int i = 0; i < names.Length; i++)
        {
            int quality = i;
            qualityButtons[i] = UI.Button(qualityRow, names[i], names[i]);
            UI.Layout(qualityButtons[i].gameObject, 48f, 96f);
            qualityButtons[i].onClick.AddListener(() => ChangeChoice(quality: quality));
        }
        graphicsDescription = Description(parent, "", 15f, true);
        RectTransform fpsRow = Row(parent, "Frame rate", "30 uses less power; 60 feels smoother.", 66f);
        for (int i = 0; i < 2; i++)
        {
            int rate = i == 0 ? 30 : 60;
            frameButtons[i] = UI.Button(fpsRow, rate + " FPS", rate + " FPS");
            UI.Layout(frameButtons[i].gameObject, 48f, 150f);
            frameButtons[i].onClick.AddListener(() => ChangeChoice(frameRate: rate));
        }
        fieldOfViewSlider = SliderRow(parent, "Field of view", "A wider view shows more of the room.", 60f, 100f, out fieldOfViewValue);
        Description(parent, "Camera changes apply in your next match. Actual FPS depends on your device.", 15f, true);
    }

    private void BuildControls(Transform parent)
    {
        Description(parent, "A comfortable fit for your thumbs.", 24f);
        hudScaleSlider = SliderRow(parent, "HUD & controls", "Resize the movement and chat controls.", 70f, 110f, out hudScaleValue);
        sensitivitySlider = SliderRow(parent, "Look sensitivity", "How quickly the camera follows your swipe.", 50f, 200f, out sensitivityValue);
        RectTransform invertRow = Row(parent, "Invert vertical look", "Reverse the camera's up and down movement.", 60f);
        invertButton = UI.Button(invertRow, "Invert Look", "OFF");
        UI.Layout(invertButton.gameObject, 48f, 150f);
        invertValue = invertButton.GetComponentInChildren<TextMeshProUGUI>();
        invertButton.onClick.AddListener(() => ChangeChoice(invert: !draft.InvertLook));
        CreateHudPreview(parent);
        Description(parent, "Preview shows control size. Saved changes apply when your next match starts.", 15f, true);
    }

    private void BuildChat(Transform parent)
    {
        Description(parent, "Stay in touch. Stay together.", 24f);
        RectTransform row = Row(parent, "Team text chat", "Enable messages in online matches.", 70f);
        chatButton = UI.Button(row, "Game Chat", "ON");
        UI.Layout(chatButton.gameObject, 48f, 150f);
        chatValue = chatButton.GetComponentInChildren<TextMeshProUGUI>();
        chatButton.onClick.AddListener(() => ChangeChoice(chat: !draft.GameChatEnabled));
        Description(parent, "IN YOUR MATCH", 14f, true);
        Description(parent, "Open CHAT to message everyone in your room. Your lobby username appears beside your messages.", 18f);
        Description(parent, "Up to 120 characters per message. The latest six messages stay visible.", 16f, true);
        Description(parent, "Available in multiplayer only. This setting takes effect in your next match.", 16f, true);
    }

    private TextMeshProUGUI Description(Transform parent, string value, float size, bool muted = false)
    {
        TextMeshProUGUI text = UI.Text(parent, value.Length > 0 ? value : "Description", value, size, muted);
        UI.Layout(text.gameObject, size >= 24f ? 32f : 30f);
        return text;
    }

    private RectTransform Row(Transform parent, string label, string description, float height)
    {
        RectTransform row = UI.Rect(label + " Row", parent);
        UI.Layout(row.gameObject, height);
        UI.Horizontal(row, 12f);
        RectTransform labels = UI.Rect("Labels", row);
        UI.Layout(labels.gameObject, height, 250f, 1f);
        TextMeshProUGUI title = UI.Text(labels, "Label", label, 20f);
        UI.Anchors(title.rectTransform, new Vector2(0f, 0.46f), Vector2.one);
        TextMeshProUGUI note = UI.Text(labels, "Description", description, 14f, true);
        UI.Anchors(note.rectTransform, Vector2.zero, new Vector2(1f, 0.46f));
        return row;
    }

    private Slider SliderRow(Transform parent, string label, string description, float min, float max, out TextMeshProUGUI value)
    {
        RectTransform row = Row(parent, label, description, 62f);
        Slider slider = UI.Slider(row, min, max);
        slider.wholeNumbers = true;
        UI.Layout(slider.gameObject, 48f, 250f);
        value = UI.Text(row, "Value", "", 19f);
        value.alignment = TextAlignmentOptions.Right;
        UI.Layout(value.gameObject, 48f, 72f);
        slider.onValueChanged.AddListener(HandleControlChanged);
        return slider;
    }

    private void CreateHudPreview(Transform parent)
    {
        RectTransform preview = UI.Rect("HUD Preview", parent);
        UI.Layout(preview.gameObject, 82f);
        UI.Paint(preview, UI.Background);
        TextMeshProUGUI label = UI.Text(preview, "Preview Label", "CONTROL SIZE PREVIEW", 12f, true);
        UI.Anchors(label.rectTransform, new Vector2(0.3f, 0.5f), new Vector2(0.8f, 1f));
        TextMeshProUGUI instruction = UI.Text(preview, "Preview Hint", "Movement and chat", 15f);
        UI.Anchors(instruction.rectTransform, new Vector2(0.3f, 0f), new Vector2(0.8f, 0.5f));
        ForgottenTouchJoystick joystick = ForgottenTouchJoystick.Create(preview, Vector2.zero, new Vector2(0.28f, 1f));
        foreach (Graphic graphic in joystick.GetComponentsInChildren<Graphic>())
            graphic.raycastTarget = false;
        previewJoystick = (RectTransform)joystick.transform.Find("Background");
        Button chat = UI.Button(preview, "Chat Preview", "CHAT");
        previewChat = (RectTransform)chat.transform;
        previewChat.anchorMin = previewChat.anchorMax = new Vector2(0.88f, 0.5f);
        previewChat.sizeDelta = new Vector2(116f, 44f);
        chat.interactable = false;
    }

    private void CreateFooter(Transform parent)
    {
        RectTransform footer = UI.Rect("Footer", parent);
        UI.Layout(footer.gameObject, 76f);
        UI.Horizontal(footer, 12f);
        stateLabel = UI.Text(footer, "Save Status", "All changes saved", 16f, true);
        UI.Layout(stateLabel.gameObject, 48f, 100f, 1f);
        Button reset = UI.Button(footer, "Reset", "RESET");
        Button back = UI.Button(footer, "Back", "BACK");
        Button save = UI.Button(footer, "Save and Back", "SAVE & BACK", true);
        UI.Layout(reset.gameObject, 48f, 110f);
        UI.Layout(back.gameObject, 48f, 110f);
        UI.Layout(save.gameObject, 48f, 164f);
        reset.onClick.AddListener(() => resetConfirmation.SetActive(true));
        back.onClick.AddListener(BackWithoutSaving);
        save.onClick.AddListener(SaveAndClose);
    }

    private void CreateResetConfirmation(Transform parent)
    {
        RectTransform shade = UI.Rect("Reset Confirmation", parent);
        UI.Stretch(shade);
        UI.Paint(shade, new Color(0f, 0f, 0f, 0.85f));
        resetConfirmation = shade.gameObject;
        RectTransform dialog = UI.Rect("Dialog", shade);
        dialog.sizeDelta = new Vector2(510f, 240f);
        UI.Paint(dialog, UI.Surface);
        UI.Vertical(dialog, 24, 12f);
        Description(dialog, "Reset all settings?", 24f);
        Description(dialog, "You can review the defaults before saving. Back will keep your saved settings.", 17f, true);
        RectTransform buttons = UI.Rect("Actions", dialog);
        UI.Layout(buttons.gameObject, 48f);
        UI.Horizontal(buttons);
        Button cancel = UI.Button(buttons, "Cancel Reset", "KEEP EDITING");
        Button confirm = UI.Button(buttons, "Confirm Reset", "RESET", true);
        UI.Layout(cancel.gameObject, 48f, 0f, 1f);
        UI.Layout(confirm.gameObject, 48f, 0f, 1f);
        cancel.onClick.AddListener(() => resetConfirmation.SetActive(false));
        confirm.onClick.AddListener(ResetDraft);
        resetConfirmation.SetActive(false);
    }
}
