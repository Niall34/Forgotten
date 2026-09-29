using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UI = ForgottenSettingsUI;

// Local chat HUD. Text is literal, bounded, and scoped to the current room.
public sealed class RoomChatUI : MonoBehaviour
{
    private static RoomChatUI active;
    public static bool CapturesInput => active != null && active.composer != null && active.composer.activeSelf;
    private RoomChatSession session;
    private RectTransform root, safe, dialog, compact;
    private GameObject composer;
    private TextMeshProUGUI preview, history, status, count, roomLabel;
    private TMP_InputField input;
    private Button send, hudButton, fallbackButton;
    private ScrollRect scroll;
    private float previewUntil;
    private int observedCount;
    private Canvas canvas;

    public void Initialize(RoomChatSession chat)
    {
        if (session != null) return;
        session = chat;
        active = this;
        Build();
        session.Changed += Refresh;
        ForgottenGameSettings.Saved += OnSettingsSaved;
        OnSettingsSaved(ForgottenGameSettings.Load());
    }

    private void OnDestroy()
    {
        if (hudButton != null) hudButton.onClick.RemoveListener(Open);
        if (session != null) session.Changed -= Refresh;
        ForgottenGameSettings.Saved -= OnSettingsSaved;
        if (active == this) active = null;
        if (root != null) Destroy(root.gameObject);
    }
    private void OnDisable() => Close();

    private void Update()
    {
        if (session == null || !session.ChatEnabled) return;
        fallbackButton.gameObject.SetActive(hudButton == null || !hudButton.gameObject.activeInHierarchy);
        if (composer.activeSelf)
        {
            if (Input.GetKeyDown(KeyCode.Escape)) Close();
            FitComposerAboveKeyboard();
        }
        else if (Input.GetKeyDown(KeyCode.T)) Open();
        preview.gameObject.SetActive(!composer.activeSelf && Time.unscaledTime < previewUntil);
    }

    private void OnSettingsSaved(ForgottenSettingsSnapshot settings)
    {
        compact.localScale = Vector3.one * settings.HudScale;
        Refresh();
    }

    public void BindHudButton(Button button)
    {
        if (hudButton != null) hudButton.onClick.RemoveListener(Open);
        hudButton = button;
        if (hudButton != null) hudButton.onClick.AddListener(Open);
        Refresh();
    }

    private void Refresh()
    {
        if (root == null) return;
        if (hudButton != null) hudButton.gameObject.SetActive(session.ChatEnabled);
        if (!session.ChatEnabled) Close();
        compact.gameObject.SetActive(session.ChatEnabled && !composer.activeSelf);
        var full = new StringBuilder();
        var recent = new StringBuilder();
        for (int i = 0; i < session.Messages.Count; i++)
        {
            full.AppendLine(session.Messages[i]);
            if (i >= session.Messages.Count - 3) recent.AppendLine(session.Messages[i]);
        }
        history.text = full.Length == 0 ? "No messages yet. Say something to your team." : full.ToString();
        preview.text = recent.ToString();
        if (session.Messages.Count != observedCount || session.Messages.Count == RoomChatSession.HistoryLimit)
            previewUntil = Time.unscaledTime + 12f;
        observedCount = session.Messages.Count;
        send.interactable = session.ChatEnabled && session.IsInRoom;
        input.interactable = send.interactable;
        roomLabel.text = session.IsInRoom ? "TEAM CHAT  /  THIS ROOM ONLY" : "TEAM CHAT  /  OFFLINE";
        status.text = !session.IsInRoom ? "Disconnected. Messages cannot be sent."
            : Photon.Pun.PhotonNetwork.CurrentRoom.PlayerCount < 2 ? "You're the only player in this room."
            : "Messages are shared with teammates in this room.";
        if (!session.IsInRoom) input.DeactivateInputField();
        if (composer.activeSelf)
        {
            Canvas.ForceUpdateCanvases();
            scroll.verticalNormalizedPosition = 0f;
        }
    }

    public void Open()
    {
        if (session == null || !session.ChatEnabled) return;
        composer.SetActive(true);
        FitComposerAboveKeyboard();
        Refresh();
        if (input.interactable) input.ActivateInputField();
    }

    public void Close()
    {
        if (composer == null) return;
        input.DeactivateInputField();
        input.SetTextWithoutNotify("");
        count.text = "0 / 120";
        composer.SetActive(false);
        compact.gameObject.SetActive(session != null && session.ChatEnabled);
    }

    private void Send()
    {
        if (!session.TrySend(input.text, out string feedback)) { status.text = feedback; return; }
        input.SetTextWithoutNotify("");
        count.text = "0 / 120";
        status.text = "Message sent to your room.";
        input.ActivateInputField();
    }

    private void FitComposerAboveKeyboard()
    {
        float keyboardHeight = 0;
        if (TouchScreenKeyboard.visible)
        {
            float pixels = TouchScreenKeyboard.area.height;
            if (pixels <= 0 && Application.isMobilePlatform) pixels = Screen.height * 0.48f;
            keyboardHeight = Mathf.Max(0, pixels - Screen.safeArea.yMin) / Mathf.Max(0.01f, canvas.scaleFactor);
        }
        float available = Mathf.Max(190, safe.rect.height - keyboardHeight - 24);
        dialog.sizeDelta = new Vector2(Mathf.Min(680, safe.rect.width - 32), Mathf.Min(430, available));
        dialog.anchoredPosition = new Vector2(0, keyboardHeight * 0.5f);
    }

    private void Build()
    {
        root = UI.Rect("Team Chat HUD", transform);
        canvas = root.gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 220;
        var scaler = root.gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280, 720);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        root.gameObject.AddComponent<GraphicRaycaster>();
        safe = UI.Rect("Safe Area", root);
        UI.Stretch(safe);
        safe.gameObject.AddComponent<MobileSafeArea>();
        compact = UI.Rect("Chat HUD", safe);
        compact.anchorMin = compact.anchorMax = compact.pivot = new Vector2(0, 1);
        compact.anchoredPosition = new Vector2(20, -18);
        compact.sizeDelta = new Vector2(440, 190);
        var open = UI.Button(compact, "Open Chat", Application.isMobilePlatform ? "CHAT" : "CHAT  [T]");
        fallbackButton = open;
        var openRect = (RectTransform)open.transform;
        openRect.anchorMin = openRect.anchorMax = openRect.pivot = new Vector2(0, 1);
        openRect.sizeDelta = new Vector2(132, 44);
        open.onClick.AddListener(Open);
        preview = UI.Text(compact, "Recent Messages", "", 17);
        UI.Stretch(preview.rectTransform);
        preview.rectTransform.offsetMin = new Vector2(4, 0);
        preview.rectTransform.offsetMax = new Vector2(-4, -54);
        preview.alignment = TextAlignmentOptions.TopLeft;
        preview.overflowMode = TextOverflowModes.Ellipsis;
        var shadow = preview.gameObject.AddComponent<Shadow>();
        shadow.effectColor = Color.black;
        shadow.effectDistance = new Vector2(1, -1);

        var shade = UI.Rect("Chat Composer", safe);
        UI.Stretch(shade);
        UI.Paint(shade, new Color(0, 0, 0, 0.62f));
        composer = shade.gameObject;
        dialog = UI.Rect("Chat Panel", shade);
        dialog.anchorMin = dialog.anchorMax = dialog.pivot = new Vector2(0.5f, 0.5f);
        UI.Paint(dialog, UI.Background);
        UI.Vertical(dialog, 16, 8);
        var heading = UI.Rect("Heading", dialog);
        UI.Layout(heading.gameObject, 34);
        UI.Horizontal(heading);
        roomLabel = UI.Text(heading, "Room", "TEAM CHAT", 18);
        roomLabel.color = UI.Accent;
        roomLabel.fontStyle = FontStyles.Bold | FontStyles.Italic;
        UI.Layout(roomLabel.gameObject, 34, 0, 1);
        var close = UI.Button(heading, "Close Chat", "BACK");
        UI.Layout(close.gameObject, 34, 84);
        close.onClick.AddListener(Close);
        var viewport = UI.Rect("Message View", dialog);
        UI.Layout(viewport.gameObject, 28);
        viewport.GetComponent<LayoutElement>().flexibleHeight = 1;
        UI.Paint(viewport, UI.Surface);
        viewport.gameObject.AddComponent<RectMask2D>();
        scroll = viewport.gameObject.AddComponent<ScrollRect>();
        scroll.viewport = viewport;
        scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 25;
        history = UI.Text(viewport, "Messages", "", 18);
        history.rectTransform.anchorMin = new Vector2(0, 1);
        history.rectTransform.anchorMax = Vector2.one;
        history.rectTransform.pivot = new Vector2(0.5f, 1);
        history.rectTransform.sizeDelta = new Vector2(-16, 0);
        history.alignment = TextAlignmentOptions.TopLeft;
        history.margin = new Vector4(4, 6, 4, 6);
        history.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        scroll.content = history.rectTransform;
        var info = UI.Rect("Status Row", dialog);
        UI.Layout(info.gameObject, 22);
        UI.Horizontal(info);
        status = UI.Text(info, "Status", "", 13, true);
        UI.Layout(status.gameObject, 22, 0, 1);
        count = UI.Text(info, "Character Count", "0 / 120", 13, true);
        UI.Layout(count.gameObject, 22, 76);
        count.alignment = TextAlignmentOptions.Right;
        var entry = UI.Rect("Message Entry", dialog);
        UI.Layout(entry.gameObject, 48);
        UI.Horizontal(entry, 10);
        var field = UI.Rect("Message Input", entry);
        UI.Layout(field.gameObject, 48, 0, 1);
        var background = UI.Paint(field, UI.Control);
        input = field.gameObject.AddComponent<TMP_InputField>();
        input.targetGraphic = background;
        var textArea = UI.Rect("Text Area", field);
        UI.Stretch(textArea);
        textArea.offsetMin = new Vector2(12, 4);
        textArea.offsetMax = new Vector2(-12, -4);
        textArea.gameObject.AddComponent<RectMask2D>();
        var text = UI.Text(textArea, "Input Text", "", 18);
        UI.Stretch(text.rectTransform);
        text.textWrappingMode = TextWrappingModes.NoWrap;
        var placeholder = UI.Text(textArea, "Placeholder", "Message your team...", 18, true);
        UI.Stretch(placeholder.rectTransform);
        input.textViewport = textArea;
        input.textComponent = text;
        input.placeholder = placeholder;
        input.characterLimit = RoomChatSession.MaximumCharacters;
        input.lineType = TMP_InputField.LineType.SingleLine;
        input.contentType = TMP_InputField.ContentType.Standard;
        input.richText = false;
        input.customCaretColor = true;
        input.caretColor = UI.Accent;
        input.onValueChanged.AddListener(value => count.text = value.Length + " / 120");
        input.onSubmit.AddListener(_ => Send());
        send = UI.Button(entry, "Send Message", "SEND", true);
        UI.Layout(send.gameObject, 48, 90);
        send.onClick.AddListener(Send);
        composer.SetActive(false);
    }
}
