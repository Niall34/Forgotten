using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;
using UnityEngine.UI;

public class PlayerDownedState : MonoBehaviourPun
{
    public int maxHearts = 3;
    public int hitsPerHeart = 2;
    public float reviveRange = 2.5f;

    public bool IsDowned { get; private set; }
    public int HitsTaken { get; private set; }
    private static List<PlayerDownedState> allPlayers = new List<PlayerDownedState>();

    private Forgotten.Player.PlayerHealthStateMachine health;
    private PlayerDownedState reviveTarget;
    private GameObject canvasObject;
    private GameObject reviveButtonObject;
    private Text statusText;

    private void OnEnable()
    {
        allPlayers.Add(this);
    }

    private void OnDisable()
    {
        allPlayers.Remove(this);
    }
    private void Start()
    {
        health = GetComponent<Forgotten.Player.PlayerHealthStateMachine>();

        // only your own player gets the revive button and status text
        if (photonView.IsMine)
        {
            BuildUI();
        }
    }

    private void OnDestroy()
    {
        if (canvasObject != null)
        {
            Destroy(canvasObject);
        }
    }
    private void Update()
    {
        if (photonView.IsMine == false || reviveButtonObject == null)
        {
            return;
        }

#if UNITY_EDITOR
        if (Input.GetKeyDown(KeyCode.T)) RegisterHit();
        if (Input.GetKeyDown(KeyCode.R)) Revive();
#endif

        bool amDead = health != null && health.IsDead;

        // look for a downed teammate close enough to revive
        reviveTarget = null;
        if (IsDowned == false && amDead == false)
        {
            foreach (PlayerDownedState other in allPlayers)
            {
                if (other == this || other.IsDowned == false)
                {
                    continue;
                }

                float distance = Vector3.Distance(transform.position, other.transform.position);
                if (distance <= reviveRange)
                {
                    reviveTarget = other;
                    break;
                }
            }
        }

        reviveButtonObject.SetActive(reviveTarget != null);
        statusText.text = IsDowned ? "KNOCKED DOWN : Calling for back up" : "";
    }
    // PlayerController will call this on every client each time the monster hits this player
    public void RegisterHit()
    {
        if (IsDowned)
        {
            return;
        }

        HitsTaken++;
        Debug.Log("Hit " + HitsTaken + " of " + (maxHearts * hitsPerHeart));
      // only when a whole heart has just emptied
        if (HitsTaken % hitsPerHeart != 0)
        {
            return;
        }

        if (HitsTaken >= maxHearts * hitsPerHeart)
        {
            // last heart gone, the team's script handles spectator mode
            if (health != null)
            {
                health.SetDead();
            }
            Debug.Log("Dead");
        }
        else
        {
            IsDowned = true;
            Debug.Log("Knocked down");
        }
    }

    private void OnRevivePressed()
    {
        if (reviveTarget != null)
        {
            // runs Revive on the downed player's copy on every client
            reviveTarget.photonView.RPC(nameof(Revive), RpcTarget.All);
        }
    }

    [PunRPC]
    private void Revive()
    {
        if (IsDowned == false)
        {
            return;
        }

        IsDowned = false;
        Debug.Log("Revived");
    }

    private void BuildUI()
    {
        canvasObject = new GameObject("Revive Canvas");
        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280f, 720f);

        canvasObject.AddComponent<GraphicRaycaster>();

        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        // status text, top middle under the hearts
        GameObject statusObject = new GameObject("Status Text");
        statusObject.transform.SetParent(canvasObject.transform, false);
        RectTransform statusRect = statusObject.AddComponent<RectTransform>();
        statusRect.anchorMin = new Vector2(0.5f, 1f);
        statusRect.anchorMax = new Vector2(0.5f, 1f);
        statusRect.sizeDelta = new Vector2(900f, 60f);
        statusRect.anchoredPosition = new Vector2(0f, -150f);
        statusText = statusObject.AddComponent<Text>();
        statusText.font = font;
        statusText.fontSize = 34;
        statusText.alignment = TextAnchor.MiddleCenter;
        statusText.color = Color.white;
        statusText.text = "";

        // revive button, middle of the screen, hidden until a downed teammate is close
        reviveButtonObject = new GameObject("Revive Button");
        reviveButtonObject.transform.SetParent(canvasObject.transform, false);
        RectTransform buttonRect = reviveButtonObject.AddComponent<RectTransform>();
        buttonRect.anchorMin = new Vector2(0.5f, 0.5f);
        buttonRect.anchorMax = new Vector2(0.5f, 0.5f);
        buttonRect.sizeDelta = new Vector2(260f, 80f);
        buttonRect.anchoredPosition = new Vector2(0f, -120f);
        Image buttonImage = reviveButtonObject.AddComponent<Image>();
        buttonImage.color = new Color(0.78f, 0.62f, 0.34f, 0.9f);
        Button button = reviveButtonObject.AddComponent<Button>();
        button.onClick.AddListener(OnRevivePressed);

        GameObject labelObject = new GameObject("Label");
        labelObject.transform.SetParent(reviveButtonObject.transform, false);
        RectTransform labelRect = labelObject.AddComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;
        Text label = labelObject.AddComponent<Text>();
        label.font = font;
        label.fontSize = 32;
        label.alignment = TextAnchor.MiddleCenter;
        label.color = Color.white;
        label.text = "Revive";

        reviveButtonObject.SetActive(false);
    }
}
        