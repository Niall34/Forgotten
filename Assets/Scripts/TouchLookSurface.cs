using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// a full-screen area that tracks finger drags for camera look on touch
public class TouchLookSurface : MonoBehaviour, IDragHandler
{
    private Vector2 accumulatedDelta = Vector2.zero;

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
