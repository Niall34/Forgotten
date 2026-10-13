using Photon.Pun;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

// flat radar style minimap, every icon sits at the rim of the circle and points toward what it stands for
public class Minimap : MonoBehaviour
{
    public static Minimap Instance { get; private set; }
    public static bool GeneratorComplete { get; private set; } // true once every piece is installed

    [Header("UI")]
    public GameObject visualRoot; // the whole minimap picture, hidden until your player spawns, do not put this script on it
    [FormerlySerializedAs("playerIcons")] public Image[] teammateArrows = new Image[4]; // blue, green, yellow, orange in that order
    [FormerlySerializedAs("objectiveIcon")] public Image guideIcon; // sits at the rim toward the generator
    public Image exitArrow; // shows up once the generator is finished

    [Header("Targets")]
    [FormerlySerializedAs("objectiveTarget")] public Transform guideTarget; // what the guide icon points at, filled in automatically while auto guide is on
    public Transform exitTarget; // falls back on the wintrigger in the scene if left empty
    public GeneratorAssembly generator; // optional, found on its own, the minimap reads its progress to know when it is finished
    [FormerlySerializedAs("autoObjective")] public bool autoGuide = true; // guide icon points at the generator by itself, turn off to set the target yourself

    [Header("Radar")]
    public bool rotateWithPlayer = true; // up on the circle is the way you are facing, off means up is always the same world direction
    [FormerlySerializedAs("arrowDistance")] public float rimDistance = 50f; // how far from the center every icon sits, match it to the radius of your circle
    public float arrowRotationOffset = 0f; // 0 if your arrow sprites point right, -90 if they point up

    private float guideTimer;

    private void Start() // skips other players' copies of this canvas, then finds the generator and the exit
    {
        // if this lives inside a player prefab only the owner's copy should run, ownership is only known by start
        PhotonView ownerView = GetComponentInParent<PhotonView>();
        if (ownerView != null && !ownerView.IsMine)
        {
            if (visualRoot != null) visualRoot.SetActive(false);
            enabled = false;
            return;
        }

        Instance = this;
        GeneratorComplete = false;

        if (visualRoot == gameObject)
        {
            Debug.LogWarning("Minimap script is on its own visual root, it would hide itself, put it on a parent instead", this);
            visualRoot = null;
        }

        if (generator == null) generator = FindAnyObjectByType<GeneratorAssembly>();

        if (exitTarget == null)
        {
            // the generator already knows which win trigger it opens, otherwise take any in the scene
            WinTrigger win = generator != null ? generator.winTrigger : null;
            if (win == null) win = FindAnyObjectByType<WinTrigger>();
            if (win != null) exitTarget = win.transform;
        }
    }

    private void OnDestroy() // lets go of the shared instance
    {
        if (Instance == this) Instance = null;
    }

    public void SetGuideTarget(Transform newTarget) // lets the game point the guide icon at something else
    {
        guideTarget = newTarget;
    }

    private void LateUpdate() // shows the minimap once your player exists, then places every icon
    {
        MinimapTarget me = MinimapTarget.Local;
        bool haveMe = me != null;

        if (visualRoot != null && visualRoot.activeSelf != haveMe)
        {
            visualRoot.SetActive(haveMe);
        }

        if (!haveMe) return;

        guideTimer -= Time.unscaledDeltaTime;
        if (guideTimer <= 0f)
        {
            RefreshGuide(me);
            guideTimer = 0.4f; // the scene searches in there are cheap but not free, so not every frame
        }

        float yaw = GetFacingYaw(me);
        UpdateTeammateArrows(me, yaw);
        UpdateGuideIcon(me, yaw);
        UpdateExitArrow(me, yaw);
    }

    private float GetFacingYaw(MinimapTarget me) // the way you are looking, read off the camera when there is one
    {
        if (!rotateWithPlayer) return 0f;

        Camera cam = Camera.main;
        Transform facing = cam != null ? cam.transform : me.transform;
        return facing.eulerAngles.y;
    }

    private Vector2 ToRadar(Vector3 from, Vector3 to, float yaw) // flat offset from you to a target, turned so up is where you face
    {
        Vector3 diff = to - from;
        Vector3 flat = new Vector3(diff.x, diff.z, 0f);

        // facing yaw degrees from north means turning the offset counter clockwise by yaw puts that way on top
        Vector3 turned = Quaternion.Euler(0f, 0f, yaw) * flat;
        return new Vector2(turned.x, turned.y);
    }

    private void PointArrow(Image arrow, Vector2 offset) // puts an icon out at the rim and turns it toward the target
    {
        Vector2 dir = offset;
        if (dir.sqrMagnitude < 0.0001f) dir = Vector2.up;
        dir.Normalize();

        arrow.rectTransform.anchoredPosition = dir * rimDistance;

        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        arrow.rectTransform.localRotation = Quaternion.Euler(0f, 0f, angle + arrowRotationOffset);
    }

    private void RefreshGuide(MinimapTarget me) // works out if the generator is finished and what the guide icon should point at
    {
        if (generator == null) generator = FindAnyObjectByType<GeneratorAssembly>();
        if (generator == null) return;

        int installed = generator.GetPiecesInstalled();
        int needed = generator.GetTotalPiecesNeeded();
        bool allDone = installed >= needed;
        GeneratorComplete = allDone; // every client counts the same installs through the generator's own rpc, so no extra syncing

        if (allDone || !autoGuide) return;

        guideTarget = generator.transform; // points to generator 
    }

    private void UpdateTeammateArrows(MinimapTarget me, float yaw) // one arrow per living teammate, colour comes from the seat number
    {
        for (int i = 0; i < teammateArrows.Length; i++)
        {
            if (teammateArrows[i] != null) teammateArrows[i].gameObject.SetActive(false);
        }

        if (teammateArrows.Length == 0) return;

        foreach (MinimapTarget target in MinimapTarget.All)
        {
            if (target == null || target.IsMine) continue;
            if (!target.IsAlive || target.HasEscaped) continue;

            int slot = (target.ActorNumber - 1) % teammateArrows.Length;
            if (slot < 0) slot = 0;

            Image arrow = teammateArrows[slot];
            if (arrow == null) continue;

            Vector2 offset = ToRadar(me.transform.position, target.transform.position, yaw);
            arrow.gameObject.SetActive(true);
            PointArrow(arrow, offset);
        }
    }

    private void UpdateGuideIcon(MinimapTarget me, float yaw) // shows which way to go next until the generator is finished
    {
        if (guideIcon == null) return;

        bool showIt = !GeneratorComplete && guideTarget != null;
        guideIcon.gameObject.SetActive(showIt);
        if (!showIt) return;

        Vector2 offset = ToRadar(me.transform.position, guideTarget.position, yaw);
        PointArrow(guideIcon, offset);
    }

    private void UpdateExitArrow(MinimapTarget me, float yaw) // appears once the generator is finished and points toward the exit
    {
        if (exitArrow == null) return;

        bool showIt = GeneratorComplete && exitTarget != null;
        exitArrow.gameObject.SetActive(showIt);
        if (!showIt) return;

        Vector2 offset = ToRadar(me.transform.position, exitTarget.position, yaw);
        PointArrow(exitArrow, offset);
    }
}