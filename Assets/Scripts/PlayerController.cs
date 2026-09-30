using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Forgotten.Player;

// handles movement and camera look using a touch joystick and drag-to-look

[RequireComponent(typeof(PhotonView))]
[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviourPun, IPunObservable
{
    [Header("Movement")]
    public float moveSpeed = 4.5f;
    public float sprintSpeed = 7.5f; // used instead of moveSpeed while sprint is held/touched
    public float turnSpeed = 140f;
    public float gravity = -9.81f;

    [Header("Crouch")]
    public float crouchSpeed = 2f; // no sprinting while crouched
    public float standingHeight = 2f;
    public float crouchingHeight = 1.2f;
    public float crouchCameraHeight = 1.0f; // camera height while crouched
    public float cameraCrouchLerpSpeed = 8f; // higher = snappier crouch transition

    [Header("Camera")]
    public Vector3 cameraOffset = new Vector3(0f, 1.6f, 0f);
    public float standingForwardOffset = 0.15f; // pushes camera forward out of the hood/mask mesh
    public float crouchForwardOffset = 0.3f; // bigger than standing since the head tucks differently while crouched
    public float cameraPitchMin = -80f;
    public float cameraPitchMax = 80f;
    public float lookSensitivity = 0.15f;
    public float mouseLookMultiplier = 6f; // scales mouse deltas up to match touch deltas before lookSensitivity applies

    [Header("Touch Controls")]
    public GameObject touchControlsCanvasPrefab; // canvas with a TouchJoystick and TouchLookSurface somewhere inside

    [Header("Animation")]
    [SerializeField] private Animator animator;

    [Header("Torch")]
    [SerializeField] private Transform torchLightAim; // the actual torch light object, not the visible model, the model stays parented to the head bone instead to ride along with animations
    public float torchHeight = 1.7f; // roughly head height, sets where the light itself sits

    [SerializeField] private PlayerHealthStateMachine health;

    // every spawned player adds itself here, so anything needing every visible player (like a minimap) can find them
    private static List<PlayerController> allPlayers = new List<PlayerController>();
    public static List<PlayerController> All
    {
        get { return allPlayers; }
    }

    private CharacterController controller;
    private PlayerInventory playerInventory; // only used by the I/P test shortcuts below
    private Camera playerCamera;
    private Transform cameraTransform;
    private float cameraPitch = 0f;
    private float verticalVelocity = 0f;
    private bool isCrouching = false;
    private float currentCameraHeight; // lerps toward standing/crouching height each frame
    private float currentForwardOffset; // lerps toward standing/crouching forward offset

    private TouchJoystick moveJoystick;
    private TouchLookSurface lookSurface;
    private PlayerFlashLight flashlight; // grabbed in Awake now, reused by ToggleFlashlight
    private bool isFlashlightOn = false;

    [Header("Remote Smoothing")]
    public float remoteLerpSpeed = 12f; // how fast a non-owned copy slides toward the real position (higher snaps quicker, lower is smoother but laggier)
    private Vector3 networkPosition; // latest position received for the player if you don't own it
    private Quaternion networkRotation; // same idea but for facing direction
    public bool IsFlashlightOn // lets the monster check if this player's light is on
    {
        get { return isFlashlightOn; }
    }

    private float animatorSpeedParam = 0f; // the value UpdateAnimator works out on the owner so remote clients never run their own Update() loop for this player, so this needs to travel over OnPhotonSerializeView like cameraPitch does

    private float movementNoiseLevel = 0f; // computed on the owner each frame, synced to others via OnPhotonSerializeView
    public float MovementNoiseLevel // 0 = silent, 1 = loud, monster uses this to gauge how easy this player is to find
    {
        get { return movementNoiseLevel; }
    }

    private bool isSprinting = false; // computed on the owner alongside movementNoiseLevel, synced the same way (can't just check MovementNoiseLevel==1 for this, since a diagonal keyboard press can also hit 1 without sprinting)
    public bool IsSprinting
    {
        get { return isSprinting; }
    }

    public bool IsCrouching // already kept in sync everywhere via the SetCrouchState RPC, just exposing it here
    {
        get { return isCrouching; }
    }

    public bool IsMoving // true the instant there's any real movement input at all, crouched or not unlike MovementNoiseLevel this doesn't collapse to 0 just because you're crouching, only actual stillness does that
    {
        get { return animatorSpeedParam > 0.05f; }
    }

    public float EyeHeight // roughly where this player's head is right now, for the monster's line of sight checks, reuses the same numbers the camera itself already sits at, so crouching lowers the profile
    {
        get { return isCrouching ? crouchCameraHeight : cameraOffset.y; }
    }

    public bool HasEscaped { get; private set; } = false; // set once by WinTrigger when this player wins

    public void MarkEscaped() // flips HasEscaped, WinTrigger already handles the RPC to every client
    {
        HasEscaped = true;
    }

    private void Awake() // sets up references and starting camera values
    {
        controller = GetComponent<CharacterController>();
        playerInventory = GetComponent<PlayerInventory>();

        // grabbed here instead of SetupTouchControls so every client's copy of this player has it, not just the local owner's (otherwise the SetFlashlightState RPC has nothing to call on remote clients)
        flashlight = GetComponentInChildren<PlayerFlashLight>();

        // starting guess for remote copies, gets overwritten the moment the first network packet comes in
        networkPosition = transform.position;
        networkRotation = transform.rotation;

        currentCameraHeight = cameraOffset.y;
        currentForwardOffset = standingForwardOffset;
    }

    private void OnEnable() // adds this player to the shared all list
    {
        allPlayers.Add(this);
    }

    private void OnDisable() // removes this player from the shared all list
    {
        allPlayers.Remove(this);
    }

    private void Start() // sets up camera and touch controls if this is your player, otherwise disables movement
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
            // not our player, the CharacterController is disabled on this copy (see Start()), so just slide the raw transform toward the last position/rotation we heard about over the network
            transform.position = Vector3.Lerp(transform.position, networkPosition, Time.deltaTime * remoteLerpSpeed);
            transform.rotation = Quaternion.Lerp(transform.rotation, networkRotation, Time.deltaTime * remoteLerpSpeed);
            return;
        }

        HandleLook();

        // combine horizontal move + gravity into one Move call so theres no double move jitter
        Vector3 horizontalMove = HandleMove();
        Vector3 gravityMove = ApplyGravity();
        controller.Move((horizontalMove + gravityMove) * Time.deltaTime);

        UpdateMovementNoiseLevel();
        UpdateAnimator();

        // keyboard shortcuts for testing in the Editor, touch buttons call the same methods on mobile
        if (Input.GetKeyDown(KeyCode.F))
        {
            ToggleFlashlight();
        }

        if (Input.GetKeyDown(KeyCode.C))
        {
            ToggleCrouch();
        }

        // P/I are PC stand-ins for the pickup/install UI buttons, just call PlayerInventory's own methods
        if (Input.GetKeyDown(KeyCode.P))
        {
            TryPickUpNearestPiece();
        }

        if (Input.GetKeyDown(KeyCode.I))
        {
            playerInventory?.OnInstallButtonDown();
        }

        if (Input.GetKey(KeyCode.I))
        {
            playerInventory?.UpdateInstallProgress();
        }

        if (Input.GetKeyUp(KeyCode.I))
        {
            playerInventory?.OnInstallButtonUp();
        }
    }

    private void TryPickUpNearestPiece() // finds and picks up the closest piece, mirrors PlayerInventory's own search
    {
        if (playerInventory == null || playerInventory.GetHeldPiece() != null)
        {
            return;
        }

        GeneratorPiece[] allPieces = FindObjectsByType<GeneratorPiece>();
        GeneratorPiece closestPiece = null;
        float closestDistance = 3f; // matches PlayerInventory's own pickup range

        foreach (GeneratorPiece piece in allPieces)
        {
            if (piece.IsPickedUp())
            {
                continue;
            }

            float distance = Vector3.Distance(transform.position, piece.transform.position);
            if (distance < closestDistance)
            {
                closestPiece = piece;
                closestDistance = distance;
            }
        }

        if (closestPiece != null)
        {
            playerInventory.PickUpPiece(closestPiece);
        }
    }

    private void UpdateMovementNoiseLevel() // works out how loud the player is right now
    {
        if (isCrouching)
        {
            movementNoiseLevel = 0f; // crouching is always silent
            isSprinting = false; // can't sprint while crouched anyway but just a safety net
            return;
        }

        isSprinting = IsSprintHeld();

        // clamped to 0-1, GetCombinedMoveInput() can return a bit over 1 on a diagonal keyboard press and this value's meant to stay a clean 0-1
        movementNoiseLevel = isSprinting ? 1f : Mathf.Clamp01(GetCombinedMoveInput().magnitude);
    }

    private Vector2 GetCombinedMoveInput() // keyboard input if pressed, otherwise falls back to the touch joystick
    {
        Vector2 keyboardInput = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
        if (keyboardInput.sqrMagnitude > 0.01f)
        {
            return keyboardInput;
        }

        return moveJoystick != null ? moveJoystick.Value : Vector2.zero;
    }

    private bool IsSprintHeld() // Left Shift or the touch joystick's sprint icon
    {
        if (Input.GetKey(KeyCode.LeftShift))
        {
            return true;
        }

        return moveJoystick != null && moveJoystick.IsSprinting;
    }

    private void UpdateAnimator() // feeds movement speed into the Animator, capped so walking can't cross into the Sprint tier
    {
        if (animator == null)
        {
            return;
        }

        float stickMagnitude = GetCombinedMoveInput().magnitude;
        bool sprinting = isCrouching == false && IsSprintHeld();

        // 0.85 cap keeps full walking below the 0.9 Sprint threshold, sprinting jumps straight to 1
        animatorSpeedParam = sprinting ? 1f : Mathf.Min(stickMagnitude, 0.85f);
        animator.SetFloat("Speed", animatorSpeedParam);
    }

    private Vector3 ApplyGravity() // applies gravity so the character stays grounded, returns the vertical movement only
    {
        if (controller.isGrounded)
        {
            verticalVelocity = -0.5f; // small downward push keeps isGrounded accurate on slopes
        }
        else
        {
            verticalVelocity += gravity * Time.deltaTime;
        }

        return Vector3.up * verticalVelocity;
    }

    private void LateUpdate() // moves the camera and aims the torch light after movement/look, runs for every client
    {
        if (photonView.IsMine && playerCamera != null)
        {
            UpdateCameraPosition();
        }

        ApplyTorchAim(); // points the actual light at wherever the camera's aiming, same pivot idea as the camera itself
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
    {
        Vector2 lookDelta = Vector2.zero;
        if (lookSurface != null)
        {
            lookDelta = lookSurface.ConsumeLookDelta();
        }

        // scales mouse deltas up to match touch deltas so both work together
        Vector2 mouseDelta = new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y"));
        lookDelta += mouseDelta * mouseLookMultiplier;

        float yawAmount = lookDelta.x * lookSensitivity * turnSpeed * Time.deltaTime * 0.3f;
        transform.Rotate(Vector3.up, yawAmount, Space.World);

        cameraPitch = cameraPitch - (lookDelta.y * lookSensitivity);
        cameraPitch = Mathf.Clamp(cameraPitch, cameraPitchMin, cameraPitchMax);
    }

    public void ToggleCrouch()
    {
        if (photonView.IsMine == false)
        {
            return;
        }

        photonView.RPC(nameof(SetCrouchState), RpcTarget.All, !isCrouching);
    }

    [PunRPC]
    private void SetCrouchState(bool crouching) // runs on every client, updates crouch pose and collider size
    {
        isCrouching = crouching;

        if (animator != null)
        {
            animator.SetBool("IsCrouching", isCrouching);
        }

        float targetHeight = isCrouching ? crouchingHeight : standingHeight;
        controller.height = targetHeight;
        controller.center = new Vector3(controller.center.x, targetHeight * 0.5f, controller.center.z);
    }

    public void ToggleFlashlight()
    {
        if (photonView.IsMine == false)
        {
            return;
        }

        photonView.RPC(nameof(SetFlashlightState), RpcTarget.All, !isFlashlightOn);
    }

    [PunRPC]
    private void SetFlashlightState(bool isOn) // runs on every client, turns the flashlight on/off
    {
        isFlashlightOn = isOn;

        if (flashlight != null)
        {
            flashlight.SetLightOn(isFlashlightOn);
        }
    }

    [PunRPC]
    private void TakeDamage(int amount)
    {
        if (health == null || health.IsDead) return;
        health.CurrentHealth -= amount;
    }

    private Vector3 HandleMove() // reads WASD or the joystick
    {
        Vector2 stickInput = GetCombinedMoveInput();
        bool sprinting = IsSprintHeld();

        Vector3 moveDirection = new Vector3(stickInput.x, 0f, stickInput.y);
        moveDirection = Vector3.ClampMagnitude(moveDirection, 1f);

        float currentSpeed;
        if (isCrouching)
        {
            currentSpeed = crouchSpeed; // crouched always overrides sprint
        }
        else
        {
            currentSpeed = sprinting ? sprintSpeed : moveSpeed;
        }

        Vector3 worldMove = transform.TransformDirection(moveDirection) * currentSpeed;

        return worldMove;
    }

    private void SetupLocalCamera() // creates this player's own camera
    {
        GameObject cameraObject = new GameObject("Player Camera");
        playerCamera = cameraObject.AddComponent<Camera>();
        playerCamera.nearClipPlane = 0.05f;
        cameraTransform = cameraObject.transform;
        UpdateCameraPosition();
    }

    private void UpdateCameraPosition() // positions the camera at head height
    {
        // blends height and forward push toward standing or crouching, instead of snapping instantly
        float targetHeight = isCrouching ? crouchCameraHeight : cameraOffset.y;
        float targetForwardOffset = isCrouching ? crouchForwardOffset : standingForwardOffset;

        currentCameraHeight = Mathf.Lerp(currentCameraHeight, targetHeight, Time.deltaTime * cameraCrouchLerpSpeed);
        currentForwardOffset = Mathf.Lerp(currentForwardOffset, targetForwardOffset, Time.deltaTime * cameraCrouchLerpSpeed);

        Vector3 headPosition = transform.position + Vector3.up * currentCameraHeight;

        // rotates the forward offset with the camera so it orbits headPosition properly instead of clipping at steep angles
        Quaternion lookRotation = Quaternion.Euler(cameraPitch, transform.eulerAngles.y, 0f);
        Vector3 forwardNudge = lookRotation * Vector3.forward * currentForwardOffset;

        cameraTransform.position = headPosition + forwardNudge;
        cameraTransform.rotation = lookRotation;
    }

    private void SetupTouchControls() // spawns the touch controls canvas and grabs its joystick/look surface/buttons
    {
        EnsureEventSystem();

        GameObject canvasInstance = Instantiate(touchControlsCanvasPrefab);
        moveJoystick = canvasInstance.GetComponentInChildren<TouchJoystick>();
        lookSurface = canvasInstance.GetComponentInChildren<TouchLookSurface>();

        WireUpButton(canvasInstance, "CrouchButton", ToggleCrouch);

        // flashlight reference is already grabbed in Awake() for every client, the RPC that toggles it still has to live here on PlayerController though, since Photon RPCs can't target child components directly
        WireUpButton(canvasInstance, "FlashlightButton", ToggleFlashlight);
    }

    // finds a button by name and wires it to a method in code, since the canvas is only created at runtime
    private void WireUpButton(GameObject canvasInstance, string childName, UnityEngine.Events.UnityAction onClickAction)
    {
        // searches anywhere under the canvas, no matter how deeply nested
        Transform found = FindDeepChild(canvasInstance.transform, childName);
        if (found == null)
        {
            // only prints if the name doesn't match anything, check the button's actual name in the Hierarchy
            Debug.Log("Couldn't find a button named " + childName + " under the touch controls canvas");
            return;
        }

        // needs an actual Button component to have onClick, comes back null if childName pointed at the wrong object
        Button button = found.GetComponent<Button>();
        if (button != null)
        {
            button.onClick.AddListener(onClickAction);
        }
    }

    // searches every child and grandchild, since Transform.Find only checks direct children one level down
    private Transform FindDeepChild(Transform parent, string name) // searches every descendant, not just direct children
    {
        foreach (Transform child in parent) // walks through parent's direct children first
        {
            if (child.name == name)
            {
                return child; // found it at this level, stop here
            }

            // not this one, check if it has a matching child further down
            Transform foundInGrandchildren = FindDeepChild(child, name);
            if (foundInGrandchildren != null)
            {
                return foundInGrandchildren;
            }
        }

        return null; // nothing named childName anywhere in this branch
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