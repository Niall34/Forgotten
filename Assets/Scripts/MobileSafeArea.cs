using UnityEngine;

[RequireComponent(typeof(RectTransform))]
public sealed class MobileSafeArea : MonoBehaviour
{
    private RectTransform panel;
    private Rect lastSafeArea;
    private Vector2Int lastScreenSize;

    private void Awake()
    {
        panel = GetComponent<RectTransform>();
        ApplySafeArea();
    }

    private void Update()
    {
        Vector2Int screenSize = new Vector2Int(Screen.width, Screen.height);
        if (Screen.safeArea != lastSafeArea || screenSize != lastScreenSize)
            ApplySafeArea();
    }

    private void ApplySafeArea()
    {
        Rect safeArea = Screen.safeArea;
        float width = Mathf.Max(1f, Screen.width);
        float height = Mathf.Max(1f, Screen.height);

        panel.anchorMin = new Vector2(safeArea.xMin / width, safeArea.yMin / height);
        panel.anchorMax = new Vector2(safeArea.xMax / width, safeArea.yMax / height);
        panel.offsetMin = Vector2.zero;
        panel.offsetMax = Vector2.zero;

        lastSafeArea = safeArea;
        lastScreenSize = new Vector2Int(Screen.width, Screen.height);
    }
}
