using UnityEngine;
using UnityEngine.UI;
using Photon.Pun;

// local-only character shown in the lobby before joining a room
// LobbyStage makes one when a name is confirmed, and destroys it once a real room is joined
// this same prefab is also reused as the real networked character once you're in a room -
// in that case it reads its name from Photon's instantiation data automatically
public class LobbyPreviewCharacter : MonoBehaviour
{
    [Header("Gravity")]
    public float gravity = -9.81f;

    public float tagHeight = 2.1f;

    private Text nameTag;

    private CharacterController controller;
    private float verticalVelocity;
    private PhotonView photonView; // only exists on the real networked spawn, not the local-only preview

    private void Awake() // grabs the name from Photon if this is a real networked spawn, so the tag shows up automatically
    {
        controller = GetComponent<CharacterController>();

        // runs on every client's copy of this object, picks up the name if one was passed in on spawn -
        // saves needing a separate RPC just to send the name
        photonView = GetComponent<PhotonView>();
        if (photonView != null && photonView.InstantiationData != null && photonView.InstantiationData.Length > 0)
        {
            string myName = (string)photonView.InstantiationData[0];
            SetDisplayName(myName);
        }
    }

    private void Update() // applies gravity so the character stays grounded
    {
        if (controller.isGrounded)
        {

            verticalVelocity = -0.5f;
        }
        else
        {
            verticalVelocity += gravity * Time.deltaTime;
        }

        Vector3 gravityMovement = Vector3.up * verticalVelocity;

        controller.Move(gravityMovement * Time.deltaTime);

        Transform tag = nameTag.transform.parent;
        tag.forward = Camera.main.transform.forward;
    }

    // sets the name tag above the character's head, building it first if it doesn't exist yet
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
        tagRoot.sizeDelta = new Vector2(150f, 50f); // old size was way too small, was basically invisible
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
        nameTag.fontSize = 15;
        nameTag.color = Color.white;
        nameTag.alignment = TextAnchor.MiddleCenter;
        nameTag.raycastTarget = false;
    }
}
