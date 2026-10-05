using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// handles movement and camera look using a touch joystick and drag-to-look

[RequireComponent(typeof(PhotonView))]
[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviourPun
{
    [Header("Movement")]
    public float moveSpeed = 4.5f;
    public float turnSpeed = 140f;
    public float gravity = -9.81f;

    [Header("Camera")]
<<<<<<< Updated upstream
    public Vector3 cameraOffset = new Vector3(0f, 1.6f, 0f);
=======
    public Vector3 cameraOffset = new Vector3(0f, 1.75f, 0.09f);
    public float standingForwardOffset = 20f; // pushes camera forward out of the hood/mask mesh
    public float crouchForwardOffset = 0.3f; // bigger than standing since the head tucks differently while crouched
>>>>>>> Stashed changes
    public float cameraPitchMin = -80f;
    public float cameraPitchMax = 80f;
    public float lookSensitivity = 0.15f;

    // every spawned player adds itself here, so things that needs to find every player currently visible (like a minimap), gets it here
    private static List<PlayerController> allPlayers = new List<PlayerController>();
    public static List<PlayerController> All
    {
        get { return allPlayers; }
    }

    private CharacterController controller;
    private Camera playerCamera;
    private Transform cameraTransform;
    private float cameraPitch = 0f;
    private float verticalVelocity = 0f;

    private TouchJoystick moveJoystick;
    private TouchLookSurface lookSurface;

    private void Awake() // grabs the CharacterController component off this same object
    {
        controller = GetComponent<CharacterController>();
<<<<<<< Updated upstream
=======
        playerInventory = GetComponent<PlayerInventory>();

        // grabbed here instead of SetupTouchControls so every client's copy of this player has it, not just the local owner's (otherwise the SetFlashlightState RPC has nothing to call on remote clients)
        flashlight = GetComponentInChildren<PlayerFlashLight>();

        // starting guess for remote copies, gets overwritten the moment the first network packet comes in
        networkPosition = transform.position;
        networkRotation = transform.rotation;

        currentCameraHeight = cameraOffset.y;
        currentForwardOffset = standingForwardOffset;

        // built here so every client's copy of this player has them, that's how teammates are heard from where they actually are
        stepSource = MakeSoundSource("Step Sound", stepSoundRange, false, null);
        damageSource = MakeSoundSource("Damage Sound", damageSoundRange, false, null);
        flashlightSource = MakeSoundSource("Flashlight Sound", flashlightSoundRange, false, null);
        crouchBreathingSource = MakeSoundSource("Crouch Breathing Sound", breathingSoundRange, true, crouchBreathingClip);
        WarnAboutMissingSounds();
>>>>>>> Stashed changes
    }

    private void OnEnable() // adds this player to the shared All list
    {
        allPlayers.Add(this);
    }

    private void OnDisable() // removes this player from the shared All list
    {
        allPlayers.Remove(this);
    }

    private void Start() // this is setting your player with a camera and touch controls with a else/if statement 
    {
        if (photonView.IsMine)
        {
            SetupLocalCamera();
            SetupTouchControls();
        }
        else
        {
            controller.enabled = false;
        }
    }

    private void Update() // only the owning player reads input, every frame
    {
        if (photonView.IsMine == false)
        {
            return;
        }

        HandleLook();
        HandleMove();
    }

    private void LateUpdate() // moves the camera after this frame's movement/look is done
    {
        if (photonView.IsMine && playerCamera != null)
        {
            UpdateCameraPosition();
            EnsureAudioListener();
        }
    }

<<<<<<< Updated upstream
    private void HandleLook() // reads the drag surface and turns the player + tilts the camera
=======
    private static bool warnedAboutMissingSounds = false;

    private void WarnAboutMissingSounds() // lists any sound slots that were left empty in one console warning, so a silent game is easy to track down
    {
        if (warnedAboutMissingSounds)
        {
            return;
        }

        string missing = "";
        if (stepClips == null || stepClips.Length == 0) missing += "Step Clips, ";
        if (flashlightOnClip == null) missing += "Flashlight On Clip, ";
        if (flashlightOffClip == null) missing += "Flashlight Off Clip, ";
        if (damageClip == null) missing += "Damage Clip, ";
        if (crouchBreathingClip == null) missing += "Crouch Breathing Clip, ";
        if (buttonClickClip == null) missing += "Button Click Clip, ";

        if (missing != "")
        {
            warnedAboutMissingSounds = true;
            Debug.LogWarning("Player sounds missing: " + missing + "they need assigning on the Player prefab in Resources, not a copy in the scene", this);
        }
    }

    private AudioSource MakeSoundSource(string sourceName, float maxDistance, bool loop, AudioClip clip) // a 3D source parked at this player's head, volume drops evenly until it's silent at maxDistance
    {
        GameObject sourceObject = new GameObject(sourceName);
        sourceObject.transform.SetParent(transform, false);
        sourceObject.transform.localPosition = Vector3.up * cameraOffset.y;

        AudioSource source = sourceObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 1f;
        source.rolloffMode = AudioRolloffMode.Linear;
        source.minDistance = 1f;
        source.maxDistance = maxDistance;
        source.dopplerLevel = 0f;
        source.loop = loop;
        source.clip = clip;
        source.volume = loop ? 0f : 1f; // loops start silent and fade in when they're needed
        return source;
    }

    private void PlayClip(AudioSource source, AudioClip clip, float volume, float pitchMultiplier = 1f) // one shot with a tiny random pitch change so repeats don't sound identical
    {
        if (source == null || clip == null)
        {
            return;
        }

        source.pitch = Random.Range(0.94f, 1.06f) * pitchMultiplier;
        source.PlayOneShot(clip, volume);
    }

    private void PlayButtonClick()
    {
        PlayClip(uiSource, buttonClickClip, 1f);
    }

    private void UpdatePlayerSounds() // reads values that are already synced to every client (moving, sprinting, crouching) so it works the same for your player and everyone else's
    {
        bool isDead = health != null && health.IsDead;

        // the same check the animator uses to move into the Sprint tier, the speed param jumps to 1 for sprinting while walking is capped at 0.85
        bool sprintAnimationPlaying = animatorSpeedParam > 0.9f;

        // crouch walking makes no sound, same as it makes no noise for the monster
        bool stepping = IsMoving && isCrouching == false && isDead == false;
        if (stepping)
        {
            stepTimer -= Time.deltaTime;
            if (stepTimer <= 0f)
            {
                stepTimer = sprintAnimationPlaying ? sprintStepInterval : stepInterval;
                if (stepClips != null && stepClips.Length > 0)
                {
                    PlayClip(stepSource, stepClips[Random.Range(0, stepClips.Length)], stepVolume, sprintAnimationPlaying ? sprintPitchMultiplier : 1f);
                }
            }
        }
        else
        {
            stepTimer = 0f; // so the first step lands right as they start moving
        }

        bool crouchedAndStill = isCrouching && IsMoving == false;
        FadeLoop(crouchBreathingSource, crouchBreathingVolume, crouchedAndStill && isDead == false);
    }

    private void FadeLoop(AudioSource source, float volume, bool shouldPlay) // fades a looping source in or out so it doesn't pop, and only keeps it running while it's audible
    {
        if (source == null || source.clip == null)
        {
            return;
        }

        source.volume = Mathf.MoveTowards(source.volume, shouldPlay ? volume : 0f, 3f * Time.deltaTime);

        if (source.volume > 0f && source.isPlaying == false)
        {
            source.Play();
        }
        else if (source.volume <= 0f && source.isPlaying)
        {
            source.Stop();
        }
    }

    private void ApplyTorchAim() // points the actual Torch Light wherever the camera's aiming, the visible torch model still sticks to the head bone so only the light moves
    {
        if (torchLightAim == null)
        {
            return;
        }

        // same pivot UpdateCameraPosition uses, fixed point straight off the body
        Vector3 lightPosition = transform.position + Vector3.up * torchHeight;
        Quaternion lightRotation = Quaternion.Euler(cameraPitch, transform.eulerAngles.y, 0f);

        torchLightAim.position = lightPosition;
        torchLightAim.rotation = lightRotation;
    }

    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info) // syncs cameraPitch and noise level to other clients
    {
        if (stream.IsWriting)
        {

            stream.SendNext(transform.position);
            stream.SendNext(transform.rotation);
            stream.SendNext(cameraPitch);
            stream.SendNext(movementNoiseLevel);
            stream.SendNext(animatorSpeedParam);
            stream.SendNext(isSprinting);
        }
        else
        {
            // don't touch transform.position/rotation directly here, Update() lerps toward these every frame instead
            networkPosition = (Vector3)stream.ReceiveNext();
            networkRotation = (Quaternion)stream.ReceiveNext();

            cameraPitch = (float)stream.ReceiveNext();
            movementNoiseLevel = (float)stream.ReceiveNext();
            animatorSpeedParam = (float)stream.ReceiveNext();
            isSprinting = (bool)stream.ReceiveNext();

            // remote copies of this player skip UpdateAnimator entirely (Update() bails out early up top for them), so this is the only place the Animator's Speed ever gets set
            if (animator != null)
            {
                animator.SetFloat("Speed", animatorSpeedParam);
            }
        }
    }

    private void HandleLook() // reads touch and mouse look input, turns the player and tilts the camera
>>>>>>> Stashed changes
    {
        Vector2 lookDelta = Vector2.zero;
        if (lookSurface != null)
        {
            lookDelta = lookSurface.ConsumeLookDelta();
        }

        float yawAmount = lookDelta.x * lookSensitivity * turnSpeed * Time.deltaTime * 0.3f;
        transform.Rotate(Vector3.up, yawAmount, Space.World);

        cameraPitch = cameraPitch - (lookDelta.y * lookSensitivity);
        cameraPitch = Mathf.Clamp(cameraPitch, cameraPitchMin, cameraPitchMax);
    }

    private void HandleMove() // reads the joystick and moves the CharacterController, plus gravity
    {
        Vector2 stickInput = Vector2.zero;
        if (moveJoystick != null)
        {
            stickInput = moveJoystick.Value;
        }

        Vector3 moveDirection = new Vector3(stickInput.x, 0f, stickInput.y);
        moveDirection = Vector3.ClampMagnitude(moveDirection, 1f);
        Vector3 worldMove = transform.TransformDirection(moveDirection) * moveSpeed;

        if (controller.isGrounded)
        {
            verticalVelocity = -0.5f; // small downward push keeps isGrounded accurate on slopes
        }
        else
        {
            verticalVelocity = verticalVelocity + (gravity * Time.deltaTime);
        }

        worldMove.y = verticalVelocity;
        controller.Move(worldMove * Time.deltaTime);
    }

    private void SetupLocalCamera() // creates this player's own camera
    {
        GameObject cameraObject = new GameObject("Player Camera");
        cameraObject.AddComponent<AudioListener>(); // nothing in the game is audible without one, and this camera is the only place one lives
        playerCamera = cameraObject.AddComponent<Camera>();
        cameraTransform = cameraObject.transform;
        UpdateCameraPosition();
    }

<<<<<<< Updated upstream
    private void UpdateCameraPosition() // places the camera behind/above the player, looking at them
=======
    private bool warnedAboutLostListener = false;

    private void EnsureAudioListener() // puts the listener straight back if something has removed it, otherwise the whole game goes silent
    {
        if (playerCamera.GetComponent<AudioListener>() != null)
        {
            return;
        }

        playerCamera.gameObject.AddComponent<AudioListener>();

        if (warnedAboutLostListener == false)
        {
            warnedAboutLostListener = true;
            Debug.LogWarning("The Player Camera's AudioListener had been removed, something else in the project is deleting it", this);
        }
    }

    private void UpdateCameraPosition() // positions the camera at head height
>>>>>>> Stashed changes
    {
        cameraTransform.position = transform.position + Vector3.up * cameraOffset.y;

        cameraTransform.rotation = Quaternion.Euler(
            cameraPitch,
            transform.eulerAngles.y,
            0f
        );
    }

    private void SetupTouchControls() // builds the on-screen joystick and look surface
    {
        GameObject canvasObject = new GameObject("Touch Controls Canvas");
        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280f, 720f);
        scaler.matchWidthOrHeight = 0.5f;

        canvasObject.AddComponent<GraphicRaycaster>();
        EnsureEventSystem();

        RectTransform canvasRoot = canvasObject.GetComponent<RectTransform>();

        // The look surface is added first (an earlier sibling draws underneath a later
        // one), so the joystick's own corner can sit on top and steal touches over itself.
        lookSurface = TouchLookSurface.Create(canvasRoot);
        moveJoystick = TouchJoystick.Create(canvasRoot, new Vector2(0f, 0f), new Vector2(0.32f, 0.42f));
    }

    private void EnsureEventSystem() // makes sure exactly one EventSystem exists in the scene
    {
        EventSystem existing = FindAnyObjectByType<EventSystem>();
        if (existing != null)
        {
            return;
        }

        GameObject eventSystemObject = new GameObject("EventSystem");
        eventSystemObject.AddComponent<EventSystem>();
        eventSystemObject.AddComponent<StandaloneInputModule>();
        DontDestroyOnLoad(eventSystemObject);
    }
}
