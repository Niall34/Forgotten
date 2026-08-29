using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// a full-screen area that tracks finger drags for camera look on touch
// NOTE TO SELF: add it BEFORE the movement joystick*******
public class TouchLookSurface : MonoBehaviour, IDragHandler
{
    private Vector2 accumulatedDelta = Vector2.zero;

    public static TouchLookSurface Create(Transform canvasParent) // builds a full-screen invisible drag surface on the given Canvas
    {
        GameObject rootObject = new GameObject("Touch Look Surface");
        rootObject.transform.SetParent(canvasParent, false);

        RectTransform rootRect = rootObject.AddComponent<RectTransform>();
        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.one;
        rootRect.offsetMin = Vector2.zero;
        rootRect.offsetMax = Vector2.zero;

        // a image is needed so this whole area can be touched and drags dont pass straight through
        Image image = rootObject.AddComponent<Image>();
        image.color = new Color(0f, 0f, 0f, 0.01f);

        return rootObject.AddComponent<TouchLookSurface>();
    }

    public void OnDrag(PointerEventData eventData) // adds up how far the finger has dragged
    {
        accumulatedDelta = accumulatedDelta + eventData.delta;
    }

    // returns however much dragging has happened since the last time this was called then resets back to zero
    public Vector2 ConsumeLookDelta()
    {
        Vector2 result = accumulatedDelta;
        accumulatedDelta = Vector2.zero;
        return result;
    }
}
