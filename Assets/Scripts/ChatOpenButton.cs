using Photon.Pun;
using UnityEngine;
using UnityEngine.UI;

// put this on your real chat button, it links the button to the room chat at runtime
[RequireComponent(typeof(Button))]
public sealed class ChatOpenButton : MonoBehaviour
{
    private Button button;
    private RoomChatUI chat;

    private void Awake() => button = GetComponent<Button>();

    private void Update()
    {
        // the chat is created at runtime so keep looking until it exists
        if (chat != null) return;
        // other players' copies of this canvas must not take over the chat button
        PhotonView view = GetComponentInParent<PhotonView>();
        if (view != null && !view.IsMine) return;
        chat = FindAnyObjectByType<RoomChatUI>();
        if (chat != null) chat.BindHudButton(button);
    }

    private void OnDestroy()
    {
        if (chat != null) chat.BindHudButton(null);
    }
}