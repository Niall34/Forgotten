using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// a fixed-position joystick for touch movement, sits wherever you place it in the Canvas
// read Value every frame for -1..1 movement, and read IsSprinting to know if the handle
// is currently actually sitting on top of the sprint icon (not just stretched to max)
public class TouchJoystick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    private const float Radius = 80f; // how far the handle can travel from the center of the background ring

    [SerializeField] private RectTransform background; // the ring image, drag it in via the Inspector, doesn't move
    [SerializeField] private RectTransform handle; // the knob image, this is the one that slides around
    [SerializeField] private RectTransform sprintIcon; // the little running icon near the edge of the ring, drag it in via the Inspector

    public Vector2 Value { get; private set; } // current joystick input, -1..1 on each axis
    public bool IsSprinting { get; private set; } // only true while the handle is actually overlapping the sprint icon

    public void OnPointerDown(PointerEventData eventData) // touch starts: just runs the same logic as a drag straight away
    {
        OnDrag(eventData);
    }

    public void OnDrag(PointerEventData eventData) // finger is moving: measures against the background ring, which never moves, so no feedback drift
    {
        Vector2 localPoint;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(background, eventData.position, eventData.pressEventCamera, out localPoint);

        Vector2 clampedOffset = Vector2.ClampMagnitude(localPoint, Radius);
        handle.anchoredPosition = clampedOffset;
        Value = clampedOffset / Radius;

        UpdateSprintState(eventData);
    }

    private void UpdateSprintState(PointerEventData eventData) // checks if the handle's actual screen position is overlapping the sprint icon, not just "stretched far enough"
    {
        if (sprintIcon == null)
        {
            IsSprinting = false;
            return;
        }

        Vector2 handleScreenPoint = RectTransformUtility.WorldToScreenPoint(eventData.pressEventCamera, handle.position);
        IsSprinting = RectTransformUtility.RectangleContainsScreenPoint(sprintIcon, handleScreenPoint, eventData.pressEventCamera);
    }

    public void OnPointerUp(PointerEventData eventData) // touch ends: snaps the handle back to center and clears sprint
    {
        handle.anchoredPosition = Vector2.zero;
        Value = Vector2.zero;
        IsSprinting = false;
    }
}
