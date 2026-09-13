using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Reusable virtual joystick built entirely at runtime - no prefab needed. Call
/// ForgottenTouchJoystick.Create(canvasTransform, anchorMin, anchorMax, controlScale) to spawn one
/// anywhere (bottom-left zone for movement, typically). Read .Value each frame for a
/// -1..1 normalized input vector - same shape as the old Input.GetAxis("Horizontal"/"Vertical")
/// pair it replaces.
/// </summary>
public sealed class ForgottenTouchJoystick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    private const float Radius = 80f;

    private RectTransform background;
    private RectTransform handle;
    private Vector2 dragOrigin;
    private float radius;

    public Vector2 Value { get; private set; }

    public static ForgottenTouchJoystick Create(
        Transform canvasParent,
        Vector2 anchorMin,
        Vector2 anchorMax,
        float controlScale = 1f)
    {
        float safeScale = Mathf.Clamp(
            controlScale,
            ForgottenGameSettings.MinimumHudScale,
            ForgottenGameSettings.MaximumHudScale);
        float scaledRadius = Radius * safeScale;
        RectTransform root = new GameObject("Touch Joystick", typeof(RectTransform)).GetComponent<RectTransform>();
        root.SetParent(canvasParent, false);
        root.anchorMin = anchorMin;
        root.anchorMax = anchorMax;
        root.offsetMin = Vector2.zero;
        root.offsetMax = Vector2.zero;

        // Covers the whole corner zone (not just the visible circle) so a thumb can touch
        // down anywhere in that area and the joystick recentres under it.
        Image zoneImage = root.gameObject.AddComponent<Image>();
        zoneImage.color = new Color(0f, 0f, 0f, 0.01f);
        ForgottenTouchJoystick joystick = root.gameObject.AddComponent<ForgottenTouchJoystick>();

        RectTransform bg = new GameObject("Background", typeof(RectTransform)).GetComponent<RectTransform>();
        bg.SetParent(root, false);
        bg.sizeDelta = new Vector2(scaledRadius * 2f, scaledRadius * 2f);
        bg.anchorMin = bg.anchorMax = new Vector2(0.5f, 0.5f);
        bg.anchoredPosition = Vector2.zero;
        Image bgImage = bg.gameObject.AddComponent<Image>();
        bgImage.color = new Color(0.9f, 0.9f, 0.9f, 0.15f);

        RectTransform handleRect = new GameObject("Handle", typeof(RectTransform)).GetComponent<RectTransform>();
        handleRect.SetParent(bg, false);
        handleRect.sizeDelta = new Vector2(scaledRadius, scaledRadius);
        handleRect.anchorMin = handleRect.anchorMax = new Vector2(0.5f, 0.5f);
        handleRect.anchoredPosition = Vector2.zero;
        Image handleImage = handleRect.gameObject.AddComponent<Image>();
        handleImage.color = new Color(0.78f, 0.62f, 0.34f, 0.85f);

        joystick.background = bg;
        joystick.handle = handleRect;
        joystick.radius = scaledRadius;
        return joystick;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)transform, eventData.position, eventData.pressEventCamera, out Vector2 local);
        background.anchoredPosition = local;
        dragOrigin = local;
        OnDrag(eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)transform, eventData.position, eventData.pressEventCamera, out Vector2 local);
        Vector2 delta = Vector2.ClampMagnitude(local - dragOrigin, radius);
        handle.anchoredPosition = delta;
        Value = delta / Mathf.Max(radius, 1f);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        handle.anchoredPosition = Vector2.zero;
        Value = Vector2.zero;
    }
}
