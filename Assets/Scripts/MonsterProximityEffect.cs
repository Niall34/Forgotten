using Photon.Pun;
using UnityEngine;
using UnityEngine.UI;

// darkens the screen edges, dims the whole screen and plays a heartbeat that gets louder and faster the closer the monster is
public class MonsterProximityEffect : MonoBehaviour
{
    [Header("Overlays")]
    public Image vignetteImage; // full screen image for the dark border, set its color in the editor, only the transparency is changed here
    public Image darkenImage; // full screen image that dims everything, set its color in the editor too

    [Header("Heartbeat")]
    public AudioClip heartbeatClip; // a loop of your heartbeat sound
    [Range(0f, 1f)] public float maxVolume = 0.8f; // volume when the monster is right on top of you
    public float quietPitch = 0.9f; // pitch right as the effect starts, lower is a slower beat
    public float closePitch = 1.3f; // pitch when the monster is right on top of you

    [Header("Distance")]
    public float startDistance = 20f; // in world units, the effect begins when the monster gets this close
    public float fullDistance = 4f; // in world units, the effect is at full strength at this distance or closer
    public float maxHeightDifference = 3.5f; // a monster further than this above or below you on another floor does not count

    [Header("Strength")]
    [Range(0f, 1f)] public float maxVignetteAlpha = 0.9f;
    [Range(0f, 1f)] public float maxDarkenAlpha = 0.45f;
    [Range(0f, 1f)] public float vignetteInnerRadius = 0.45f; // how far out from the center the dark border starts, only used when the vignette has no sprite of its own
    public float fadeInSpeed = 1.5f; // how fast the effect comes on, full strength per second
    public float fadeOutSpeed = 0.7f; // how fast it fades when the monster leaves
    public float pulseAmount = 0.15f; // how much the border throbs, 0 for none
    public float pulseSpeed = 8f;

    private Transform monster;
    private AudioSource heartbeatSource;
    private float intensity;
    private float searchTimer;

    // pure maths so it can be tested, 0 means nothing and 1 means full effect
    public static float ProximityIntensity(float distance, float startDistance, float fullDistance)
    {
        if (startDistance <= fullDistance) return distance <= fullDistance ? 1f : 0f; // guards against the two being set the wrong way round

        float t = Mathf.InverseLerp(startDistance, fullDistance, distance); // 0 at the start distance, 1 at the full distance
        return Mathf.SmoothStep(0f, 1f, t);
    }

    private void Start() // skips other players' copies of this canvas, then sets up the overlays and the heartbeat
    {
        // if this lives inside a player prefab only the owner's copy should run, ownership is only known by start
        PhotonView ownerView = GetComponentInParent<PhotonView>();
        if (ownerView != null && !ownerView.IsMine)
        {
            SetAlpha(vignetteImage, 0f);
            SetAlpha(darkenImage, 0f);
            enabled = false;
            return;
        }

        if (vignetteImage != null)
        {
            vignetteImage.raycastTarget = false;
            if (vignetteImage.sprite == null) vignetteImage.sprite = MakeVignetteSprite();
        }
        if (darkenImage != null) darkenImage.raycastTarget = false;

        heartbeatSource = gameObject.AddComponent<AudioSource>();
        heartbeatSource.clip = heartbeatClip;
        heartbeatSource.loop = true;
        heartbeatSource.playOnAwake = false;
        heartbeatSource.spatialBlend = 0f; // plain 2d sound, it is inside your own head
        heartbeatSource.volume = 0f;

        SetAlpha(vignetteImage, 0f);
        SetAlpha(darkenImage, 0f);
    }

    private void Update() // works out how close the monster is, then drives the overlays and the heartbeat
    {
        MinimapTarget me = MinimapTarget.Local;
        bool alive = me != null && me.IsAlive;

        searchTimer -= Time.unscaledDeltaTime;
        if (searchTimer <= 0f)
        {
            if (me != null) FindNearestMonster(me.transform.position);
            searchTimer = 0.5f; // looking through the scene is not free, so only twice a second
        }

        float target = 0f;
        if (alive && monster != null)
        {
            Vector3 diff = monster.position - me.transform.position;
            float height = Mathf.Abs(diff.y);
            float flatDistance = new Vector2(diff.x, diff.z).magnitude;

            if (height <= maxHeightDifference)
            {
                target = ProximityIntensity(flatDistance, startDistance, fullDistance);
            }
        }

        float speed = target > intensity ? fadeInSpeed : fadeOutSpeed;
        intensity = Mathf.MoveTowards(intensity, target, speed * Time.deltaTime);

        ApplyOverlays();
        ApplyHeartbeat();
    }

    private void FindNearestMonster(Vector3 from) // remembers the closest monster in the scene, the monster is spawned at runtime so it has to be searched for
    {
        MonsterAI[] monsters = FindObjectsByType<MonsterAI>();

        Transform closest = null;
        float closestDistance = float.MaxValue;
        foreach (MonsterAI candidate in monsters)
        {
            if (candidate == null) continue;

            float dist = Vector3.Distance(from, candidate.transform.position);
            if (dist < closestDistance)
            {
                closestDistance = dist;
                closest = candidate.transform;
            }
        }

        monster = closest;
    }

    private void ApplyOverlays() // only the transparency changes, the color is whatever you set in the editor
    {
        float pulse = 1f + Mathf.Sin(Time.time * pulseSpeed) * pulseAmount * intensity;

        SetAlpha(vignetteImage, intensity * maxVignetteAlpha * pulse);
        SetAlpha(darkenImage, intensity * maxDarkenAlpha);
    }

    private void ApplyHeartbeat() // starts and stops the loop, and sets how loud and how fast it is
    {
        if (heartbeatSource == null || heartbeatClip == null) return;

        // the loop is paused with the game so it does not restart when you resume
        if (Time.timeScale <= 0f)
        {
            heartbeatSource.Pause();
            return;
        }
        heartbeatSource.UnPause();

        bool wanted = intensity > 0.02f;
        if (wanted && !heartbeatSource.isPlaying) heartbeatSource.Play();
        if (!wanted && heartbeatSource.isPlaying) heartbeatSource.Stop();

        heartbeatSource.volume = intensity * maxVolume;
        heartbeatSource.pitch = Mathf.Lerp(quietPitch, closePitch, intensity);
    }

    private void SetAlpha(Image image, float alpha) // changes only the transparency of an image and switches it off when it cannot be seen
    {
        if (image == null) return;

        float clamped = Mathf.Clamp01(alpha);
        Color tint = image.color;
        tint.a = clamped;
        image.color = tint;
        image.enabled = clamped > 0.001f; // a full screen image that draws nothing still costs phones fill rate
    }

    private Sprite MakeVignetteSprite() // builds a soft dark edge sprite so no art asset is needed, the color comes from the image
    {
        int size = 128;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.wrapMode = TextureWrapMode.Clamp;

        float half = size * 0.5f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f - half) / half;
                float dy = (y + 0.5f - half) / half;
                float fromCenter = Mathf.Sqrt(dx * dx + dy * dy) / 1.4142f; // 0 in the middle and 1 in the corners

                float alpha = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(vignetteInnerRadius, 1f, fromCenter));
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }
        texture.Apply();

        return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f));
    }
}