using Photon.Pun;
using UnityEngine;
using UnityEngine.UI;

// fades your dark overlay in and plays a heartbeat that gets louder and faster the closer the monster is
public class MonsterProximityEffect : MonoBehaviour
{
    [Header("Overlay")]
    public Image darkenImage; // full screen image with your sprite on it, set the sprite and color in the editor, only the transparency is changed here

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
    [Range(0f, 1f)] public float maxDarkenAlpha = 0.45f; // how see through the image still is at full strength, 1 means fully solid
    public float fadeInSpeed = 1.5f; // how fast the effect comes on, full strength per second
    public float fadeOutSpeed = 0.7f; // how fast it fades when the monster leaves

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
            SetAlpha(darkenImage, 0f);
            enabled = false;
            return;
        }

        if (darkenImage != null) darkenImage.raycastTarget = false;

        heartbeatSource = gameObject.AddComponent<AudioSource>();
        heartbeatSource.clip = heartbeatClip;
        heartbeatSource.loop = true;
        heartbeatSource.playOnAwake = false;
        heartbeatSource.spatialBlend = 0f; // plain 2d sound, it is inside your own head
        heartbeatSource.volume = 0f;

        SetAlpha(darkenImage, 0f);
    }

    private void Update() // works out how close the monster is, then drives the overlays and the heartbeat
    {
        MinimapTarget me = MinimapTarget.Local;
        // spectators (dead players) and escaped players are never affected, the health script is read directly so there is no delay
        bool affected = me != null && me.IsAlive && !me.IsDeadNow && !me.HasEscaped;

        searchTimer -= Time.unscaledDeltaTime;
        if (searchTimer <= 0f)
        {
            if (me != null) FindNearestMonster(me.transform.position);
            searchTimer = 0.5f; // looking through the scene is not free, so only twice a second
        }

        float target = 0f;
        if (affected && monster != null)
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
        if (!affected) intensity = 0f; // switches off at once instead of fading, so a spectator never sees or hears it

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

    private void ApplyOverlays() // only the transparency changes, the sprite and color are whatever you set in the editor
    {
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
}