using Photon.Pun;
using UnityEngine;
using UnityEngine.UI;

// this is for the lobby characters when playing with other people, one has a floating name and ready-state label
// which update themselves automatically by reading straight from photon
[RequireComponent(typeof(PhotonView))]
public class ForgottenLobbyCharacter : MonoBehaviourPun
{
    [Header("Name Tag")]
    public float tagHeight = 2.1f;
    public Color readyColor = new Color(0.35f, 0.9f, 0.4f);
    public Color notReadyColor = new Color(0.85f, 0.85f, 0.85f);

    private ForgottenNetworkManager net;
    private Text readyGlyph;
    private RectTransform tagRoot;

    private void Awake() // grabs the network manager
    {
        net = ForgottenNetworkManager.Bootstrap();
    }

    private void Start() // builds the floating name tag
    {
        BuildNameTag();
    }

    private void Update() // keeps the ready glyph up to date every frame
    {
        RefreshReadyGlyph();
    }

    private void BuildNameTag()  // creates the name + ready-state text above the character's head
    {
        GameObject tagCanvasObject = new GameObject("Name Tag");
        tagCanvasObject.transform.SetParent(transform, false);
        tagCanvasObject.transform.localPosition = Vector3.up * tagHeight;

        Canvas canvas = tagCanvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;

        tagRoot = tagCanvasObject.GetComponent<RectTransform>();
        tagRoot.sizeDelta = new Vector2(2.2f, 0.7f);
        tagRoot.localScale = new Vector3(0.015f, 0.015f, 0.015f);

        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        string playerName = "Player";
        if (photonView.Owner != null)
        {
            playerName = photonView.Owner.NickName;
        }
        CreateTagLine(tagRoot, font, playerName, 42, Color.white, new Vector2(0f, 0.35f), new Vector2(1f, 1f));

        readyGlyph = CreateTagLine(tagRoot, font, "...", 30, notReadyColor, new Vector2(0f, 0f), new Vector2(1f, 0.4f));
    }

    private Text CreateTagLine(Transform parent, Font font, string startingText, int fontSize, Color color, Vector2 anchorMin, Vector2 anchorMax) // builds one line of text within the name tag
    {
        GameObject lineObject = new GameObject("Tag Line");
        lineObject.transform.SetParent(parent, false);

        RectTransform lineRect = lineObject.AddComponent<RectTransform>();
        lineRect.anchorMin = anchorMin;
        lineRect.anchorMax = anchorMax;
        lineRect.offsetMin = Vector2.zero;
        lineRect.offsetMax = Vector2.zero;

        Text text = lineObject.AddComponent<Text>();
        text.font = font;
        text.text = startingText;
        text.fontSize = fontSize;
        text.color = color;
        text.alignment = TextAnchor.MiddleCenter;
        text.raycastTarget = false;
        return text;
    }

    private void RefreshReadyGlyph()  // updates the ready glyph's text and color
    {
        if (photonView.Owner == null || net == null)
        {
            return;
        }

        bool isReady = net.IsPlayerReady(photonView.Owner);
        if (isReady)
        {
            readyGlyph.text = "READY";
            readyGlyph.color = readyColor;
        }
        else
        {
            readyGlyph.text = "...";
            readyGlyph.color = notReadyColor;
        }
    }
}
