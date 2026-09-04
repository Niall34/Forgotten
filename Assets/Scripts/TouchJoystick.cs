using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// a fixed-position joystick for touch movement, sits wherever you place it in the Canvas.
// quickly flicking the handle to max stretch toward the sprint icon TWICE in a row unlocks a bigger
// travel radius and engages sprint - a single touch doesn't trigger it, has to be a deliberate double-flick
// so a casual brush past the icon doesn't accidentally pop you into sprint. sprint stays engaged the whole
// time you're stretched out, and only disengages once you're pulled back within the normal radius
public class TouchJoystick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    private const float Radius = 80f; // how far the handle can travel from the center while locked to normal movement
    public float sprintRadius = 130f; // once unlocked for sprinting, the handle can travel out this much further instead
    public float doubleFlickWindow = 0.4f; // how many seconds apart the two flicks can be and still count as a double-flick

    [SerializeField] private RectTransform background; // the ring image, drag it in via the Inspector, doesn't move
    [SerializeField] private RectTransform handle; // the knob image, this is the one that slides around
    [SerializeField] private RectTransform sprintIcon; // the little running icon near the edge of the ring, drag it in via the Inspector

    public Vector2 Value { get; private set; } // current joystick input, -1..1 on each axis
    public bool IsSprinting { get; private set; } // true once the double-flick lands, until dragged back within the normal Radius

    private float lastFlickTime = -10f; // when the last "reached max on the icon" moment happened
    private bool wasAtMaxOnIconLastFrame = false; // used to only count the MOMENT it reaches max, not every frame it stays there

    public void OnPointerDown(PointerEventData eventData) // touch starts: just runs the same logic as a drag straight away
    {
        OnDrag(eventData);
    }

    public void OnDrag(PointerEventData eventData) // finger is moving: measures against the background ring, which never moves, so no feedback drift
    {
        Vector2 localPoint;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(background, eventData.position, eventData.pressEventCamera, out localPoint);

        // still sprinting from before, check if we've been pulled back into the normal zone - if so, relock right away
        if (IsSprinting && localPoint.magnitude <= Radius)
        {
            IsSprinting = false;
        }

        float clampRadius = IsSprinting ? sprintRadius : Radius;
        Vector2 clampedOffset = Vector2.ClampMagnitude(localPoint, clampRadius);
        handle.anchoredPosition = clampedOffset;

        // Value still normalizes against the normal Radius so movement speed elsewhere doesn't need to change,
        // just clamp it to 1 in case the overstretch pushed it past that
        Value = Vector2.ClampMagnitude(clampedOffset / Radius, 1f);

        if (IsSprinting == false)
        {
            CheckForDoubleFlick(eventData, clampedOffset);
        }
    }

    private void CheckForDoubleFlick(PointerEventData eventData, Vector2 clampedOffset) // needs two quick separate touches at max-toward-the-icon, not just holding there
    {
        if (sprintIcon == null)
        {
            return;
        }

        bool isPeggedAtMax = clampedOffset.magnitude >= Radius * 0.95f; // basically all the way out
        Vector2 handleScreenPoint = RectTransformUtility.WorldToScreenPoint(eventData.pressEventCamera, handle.position);
        bool overlappingIcon = RectTransformUtility.RectangleContainsScreenPoint(sprintIcon, handleScreenPoint, eventData.pressEventCamera);
        bool isAtMaxOnIconNow = isPeggedAtMax && overlappingIcon;

        // only counts the RISING EDGE - the instant it arrives there, not every frame it's held in place
        if (isAtMaxOnIconNow && wasAtMaxOnIconLastFrame == false)
        {
            if (Time.time - lastFlickTime <= doubleFlickWindow)
            {
                IsSprinting = true; // second flick landed within the window - go straight into sprint
            }

            lastFlickTime = Time.time;
        }

        wasAtMaxOnIconLastFrame = isAtMaxOnIconNow;
    }

    public void OnPointerUp(PointerEventData eventData) // touch ends: snaps the handle back to center and clears sprint
    {
        handle.anchoredPosition = Vector2.zero;
        Value = Vector2.zero;
        IsSprinting = false;
        wasAtMaxOnIconLastFrame = false;
    }
}
