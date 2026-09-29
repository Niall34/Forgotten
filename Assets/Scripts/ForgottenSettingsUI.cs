using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Shared layout and styling for the settings screen.</summary>
internal static class ForgottenSettingsUI
{
    internal static readonly Color Background = new Color32(22, 18, 17, 244);
    internal static readonly Color Surface = new Color32(35, 29, 27, 240);
    internal static readonly Color Control = new Color32(61, 53, 50, 255);
    internal static Color Accent = new Color(0.9019608f, 0.4428978f, 0.4428978f, 1f);
    internal static readonly Color TextColor = new Color32(239, 230, 218, 255);
    internal static readonly Color Muted = new Color32(185, 173, 164, 255);
    internal static readonly Color OnAccent = new Color32(28, 15, 14, 255);
    internal static TMP_FontAsset Font;

    internal static void MatchLobby(Transform lobby)
    {
        foreach (var text in lobby.root.GetComponentsInChildren<TextMeshProUGUI>(true))
        {
            if (text.text.Trim().ToUpperInvariant() != "FORGOTTEN") continue;
            Accent = text.color;
            Font = text.font;
            break;
        }
    }

    internal static void SetSelected(Button button, bool selected)
    {
        button.image.color = selected ? Accent : Control;
        button.GetComponentInChildren<TextMeshProUGUI>().color = selected ? OnAccent : TextColor;
    }

    internal static RectTransform Rect(string name, Transform parent)
    {
        RectTransform rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        return rect;
    }

    internal static void Anchors(RectTransform rect, Vector2 min, Vector2 max)
    {
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }

    internal static void Stretch(RectTransform rect) => Anchors(rect, Vector2.zero, Vector2.one);

    internal static Image Paint(RectTransform rect, Color color)
    {
        Image image = rect.gameObject.AddComponent<Image>();
        image.color = color;
        return image;
    }

    internal static void Layout(GameObject go, float height, float width = -1f, float flexibleWidth = 0f)
    {
        LayoutElement element = go.GetComponent<LayoutElement>() ?? go.AddComponent<LayoutElement>();
        element.minHeight = element.preferredHeight = height;
        element.preferredWidth = width;
        element.flexibleWidth = flexibleWidth;
    }

    internal static VerticalLayoutGroup Vertical(RectTransform root, int padding = 0, float spacing = 10f)
    {
        VerticalLayoutGroup layout = root.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(padding, padding, padding, padding);
        layout.spacing = spacing;
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        return layout;
    }

    internal static HorizontalLayoutGroup Horizontal(RectTransform root, float spacing = 12f)
    {
        HorizontalLayoutGroup layout = root.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = spacing;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = layout.childForceExpandHeight = false;
        return layout;
    }

    internal static TextMeshProUGUI Text(Transform parent, string name, string value, float size = 20f, bool muted = false)
    {
        TextMeshProUGUI text = Rect(name, parent).gameObject.AddComponent<TextMeshProUGUI>();
        text.text = value;
        if (Font != null) text.font = Font;
        text.fontSize = size;
        text.color = muted ? Muted : TextColor;
        text.alignment = TextAlignmentOptions.MidlineLeft;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.raycastTarget = false;
        text.richText = false;
        return text;
    }

    internal static Button Button(Transform parent, string name, string label, bool accent = false)
    {
        RectTransform rect = Rect(name, parent);
        Image image = Paint(rect, accent ? Accent : Control);
        Button button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        ColorBlock colors = button.colors;
        colors.highlightedColor = new Color(1.18f, 1.18f, 1.18f);
        colors.pressedColor = new Color(0.7f, 0.7f, 0.7f);
        colors.fadeDuration = 0.08f;
        button.colors = colors;
        TextMeshProUGUI text = Text(rect, "Label", label, 18f);
        text.alignment = TextAlignmentOptions.Center;
        Stretch(text.rectTransform);
        text.rectTransform.offsetMin = new Vector2(6f, 3f);
        text.rectTransform.offsetMax = new Vector2(-6f, -3f);
        text.color = accent ? OnAccent : TextColor;
        return button;
    }

    internal static Slider Slider(Transform parent, float minimum, float maximum)
    {
        RectTransform root = Rect("Slider", parent);
        Paint(root, Color.clear); // Full-height touch target around the thin track.
        RectTransform track = Rect("Track", root);
        Anchors(track, new Vector2(0f, 0.43f), new Vector2(1f, 0.57f));
        track.offsetMin = new Vector2(12f, 0f);
        track.offsetMax = new Vector2(-12f, 0f);
        Paint(track, Control).raycastTarget = false;

        RectTransform fillArea = Rect("Fill Area", track);
        Stretch(fillArea);
        RectTransform fill = Rect("Fill", fillArea);
        Stretch(fill);
        Paint(fill, Accent).raycastTarget = false;

        RectTransform handleArea = Rect("Handle Area", root);
        Anchors(handleArea, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f));
        handleArea.offsetMin = new Vector2(12f, -14f);
        handleArea.offsetMax = new Vector2(-12f, 14f);
        RectTransform handle = Rect("Handle", handleArea);
        handle.anchorMin = handle.anchorMax = new Vector2(0f, 0.5f);
        handle.sizeDelta = new Vector2(20f, 0f);
        Image handleImage = Paint(handle, TextColor);
        Slider slider = root.gameObject.AddComponent<Slider>();
        slider.fillRect = fill;
        slider.handleRect = handle;
        slider.targetGraphic = handleImage;
        slider.minValue = minimum;
        slider.maxValue = maximum;
        return slider;
    }
}
