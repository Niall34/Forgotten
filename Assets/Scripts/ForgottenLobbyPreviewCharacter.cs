using UnityEngine;
using UnityEngine.UI;

// a local-only character model shown on the Main Lobby screen before hosting or joining,
// photon rooms and players don't exist yet at this point so this can't be a real ForgottenLobbyCharacter 
// forgottenlobbystage creates exactly one of these the moment a name is confirmed and destroys it the instant a real room is joined.
public class ForgottenLobbyPreviewCharacter : MonoBehaviour
{
    public float tagHeight = 2.1f;

    private Text nameTag;

    // sets the name tag above the characters head
    public void SetDisplayName(string displayName)
    {
        if (nameTag == null)
        {
            BuildNameTag();
        }
        nameTag.text = displayName;
    }

    private void BuildNameTag() // creates the floating name text
    {
        GameObject tagCanvasObject = new GameObject("Name Tag");
        tagCanvasObject.transform.SetParent(transform, false);
        tagCanvasObject.transform.localPosition = Vector3.up * tagHeight;

        Canvas canvas = tagCanvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;

        RectTransform tagRoot = tagCanvasObject.GetComponent<RectTransform>();
        tagRoot.sizeDelta = new Vector2(2.2f, 0.5f);
        tagRoot.localScale = new Vector3(0.015f, 0.015f, 0.015f);

        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        GameObject textObject = new GameObject("Tag Line");
        textObject.transform.SetParent(tagRoot, false);

        RectTransform textRect = textObject.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        nameTag = textObject.AddComponent<Text>();
        nameTag.font = font;
        nameTag.fontSize = 42;
        nameTag.color = Color.white;
        nameTag.alignment = TextAnchor.MiddleCenter;
        nameTag.raycastTarget = false;
    }
}
