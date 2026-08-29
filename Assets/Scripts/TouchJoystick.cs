using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// a joystick for touch movement built at runtime, 
// read value every frame to get a -1..1 movement direction
public class TouchJoystick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    private const float Radius = 80f; // how far the handle can travel

    private RectTransform background;
    private RectTransform handle;
    private Vector2 dragStartPosition;

    public Vector2 Value { get; private set; } // current joystick input, -1..1 on each axis

    // builds a joystick as a child of the given Canvas, filling the rectangle described by anchorMin/anchorMax
    public static TouchJoystick Create(Transform canvasParent, Vector2 anchorMin, Vector2 anchorMax)
    {
        GameObject rootObject = new GameObject("Touch Joystick");
        rootObject.transform.SetParent(canvasParent, false);

        RectTransform rootRect = rootObject.AddComponent<RectTransform>();
        rootRect.anchorMin = anchorMin;
        rootRect.anchorMax = anchorMax;
        rootRect.offsetMin = Vector2.zero;
        rootRect.offsetMax = Vector2.zero;

        // a invisible image is needed so the whole zone can be touched
        Image zoneImage = rootObject.AddComponent<Image>();
        zoneImage.color = new Color(0f, 0f, 0f, 0.01f);

        TouchJoystick joystick = rootObject.AddComponent<TouchJoystick>();

        GameObject backgroundObject = new GameObject("Background"); // makes the background
        backgroundObject.transform.SetParent(rootObject.transform, false);
        RectTransform backgroundRect = backgroundObject.AddComponent<RectTransform>();
        backgroundRect.sizeDelta = new Vector2(Radius * 2f, Radius * 2f);
        backgroundRect.anchorMin = new Vector2(0.5f, 0.5f);
        backgroundRect.anchorMax = new Vector2(0.5f, 0.5f);
        backgroundRect.anchoredPosition = Vector2.zero;
        Image backgroundImage = backgroundObject.AddComponent<Image>();
        backgroundImage.color = new Color(0.9f, 0.9f, 0.9f, 0.15f);

        GameObject handleObject = new GameObject("Handle"); // makes the handle
        handleObject.transform.SetParent(backgroundObject.transform, false);
        RectTransform handleRect = handleObject.AddComponent<RectTransform>();
        handleRect.sizeDelta = new Vector2(Radius, Radius);
        handleRect.anchorMin = new Vector2(0.5f, 0.5f);
        handleRect.anchorMax = new Vector2(0.5f, 0.5f);
        handleRect.anchoredPosition = Vector2.zero;
        Image handleImage = handleObject.AddComponent<Image>();
        handleImage.color = new Color(0.78f, 0.62f, 0.34f, 0.85f);

        joystick.background = backgroundRect;
        joystick.handle = handleRect;
        return joystick;
    }

    public void OnPointerDown(PointerEventData eventData) // touch starts: recentres the ring under the finger
    {
        // recentre the visible ring under wherever the finger first touched
        Vector2 localPoint;
        RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)transform, eventData.position, eventData.pressEventCamera, out localPoint);
        background.anchoredPosition = localPoint;
        dragStartPosition = localPoint;

        OnDrag(eventData);
    }

    public void OnDrag(PointerEventData eventData) // finger is moving: updates the handle position and Value
    {
        Vector2 localPoint;
        RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)transform, eventData.position, eventData.pressEventCamera, out localPoint);

        Vector2 dragAmount = localPoint - dragStartPosition;
        dragAmount = Vector2.ClampMagnitude(dragAmount, Radius);

        handle.anchoredPosition = dragAmount;
        Value = dragAmount / Radius;
    }

    public void OnPointerUp(PointerEventData eventData) // touch ends: snaps the handle back to center
    {
        handle.anchoredPosition = Vector2.zero;
        Value = Vector2.zero;
    }
}
