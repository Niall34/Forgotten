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
    public float sprintSpeed = 7.5f; // used instead of moveSpeed while the joystick is touching the sprint icon
    public float turnSpeed = 140f;
    public float gravity = -9.81f;

    [Header("Crouch")]
    public float crouchSpeed = 2f; // no sprinting while crouched, this is the only speed used
    public float standingHeight = 2f;
    public float crouchingHeight = 1.2f;
    public float crouchCameraHeight = 1.0f; // where the camera sits (matches cameraOffset.y logic) while crouched
    public float cameraCrouchLerpSpeed = 8f; // higher = snappier transition, lower = smoother/slower

    [Header("Camera")]
    public Vector3 cameraOffset = new Vector3(0f, 1.6f, 0f);
    public float standingForwardOffset = 0.15f; // pushes the camera forward out of the hood/mask mesh while standing
    public float crouchForwardOffset = 0.3f; // needs to be bigger than standing since the head tucks differently while crouched
    public float cameraPitchMin = -80f;
    public float cameraPitchMax = 80f;
    public float lookSensitivity = 0.15f;

    [Header("Touch Controls")]
    public GameObject touchControlsCanvasPrefab; // a hand-built Canvas with a TouchJoystick and a TouchLookSurface on it somewhere inside

    [Header("Animation")]
    [SerializeField] private Animator animator; // dragged the Animator in manually cuz GetComponentInChildren was grabbing the wrong one

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
    private bool isCrouching = false;
    private float currentCameraHeight; // lerps toward standing/crouching height each frame instead of snapping
    private float currentForwardOffset; // lerps toward standing/crouching forward offset the same way

    private TouchJoystick moveJoystick;
    private TouchLookSurface lookSurface;
    private PlayerFlashLight flashlight; // found once in SetupTouchControls, reused by ToggleFlashlight later
    private bool isFlashlightOn = false;

    private void Awake() // grabs the CharacterController off this object
    {
        controller = GetComponent<CharacterController>();
        currentCameraHeight = cameraOffset.y;
        currentForwardOffset = standingForwardOffset;
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

        // grab the horizontal move direction from the joystick, then let gravity add the vertical part, then move the controller ONCE with both combined so we don't get double-move jitter
        Vector3 horizontalMove = HandleMove();
        Vector3 gravityMove = ApplyGravity();
        controller.Move((horizontalMove + gravityMove) * Time.deltaTime);

        UpdateAnimator();
    }

    private void UpdateAnimator() // feeds a normalized 0-1 Speed into the Animator, capped so a max-stretch walk can't accidentally cross into the Sprint tier
    {
        if (animator == null)
        {
            return;
        }

        float stickMagnitude = moveJoystick != null ? moveJoystick.Value.magnitude : 0f;
        bool sprinting = isCrouching == false && moveJoystick != null && moveJoystick.IsSprinting;

        // 0.85 cap keeps full-stretch walking below the 0.9 Sprint threshold, sprinting jumps straight to 1
        float speedParam = sprinting ? 1f : Mathf.Min(stickMagnitude, 0.85f);
        animator.SetFloat("Speed", speedParam);
    }

    private Vector3 ApplyGravity() // sets gravity basically so the character is grounded and animations/spawning runs smoothly, returns the vertical part only
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

    private void LateUpdate() // moves the camera after this frame's movement/look is done
    {
        if (photonView.IsMine && playerCamera != null)
        {
            UpdateCameraPosition();
        }
    }

    private void HandleLook() // reads the drag surface and turns the player + tilts the camera
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

    public void ToggleCrouch() 
    {
        if (photonView.IsMine == false)
        {
            return;
        }

        photonView.RPC(nameof(SetCrouchState), RpcTarget.All, !isCrouching);
    }

    [PunRPC]
    private void SetCrouchState(bool crouching) // runs on every client so everyone sees the crouch pose and the smaller collider
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
    private void SetFlashlightState(bool isOn) // runs on every client, this lives on the same object as the PhotonView so Photon can actually find it
    {
        isFlashlightOn = isOn;

        if (flashlight != null)
        {
            flashlight.SetLightOn(isFlashlightOn);
        }
    }

    private Vector3 HandleMove() // reads the joystick, returns just the horizontal movement (no gravity in here anymore)
    {
        Vector2 stickInput = Vector2.zero;
        bool sprinting = false;
        if (moveJoystick != null)
        {
            stickInput = moveJoystick.Value;
            sprinting = moveJoystick.IsSprinting;
        }

        Vector3 moveDirection = new Vector3(stickInput.x, 0f, stickInput.y);
        moveDirection = Vector3.ClampMagnitude(moveDirection, 1f);

        float currentSpeed;
        if (isCrouching)
        {
            currentSpeed = crouchSpeed; // crouched always overrides sprint, doesn't matter if the icon is being touched
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
        playerCamera.nearClipPlane = 0.05f; // default 0.3 clips hand-held stuff like the torch since it sits close to the face
        cameraTransform = cameraObject.transform;
        UpdateCameraPosition();
    }

    private void UpdateCameraPosition() // places the camera inside the player's head, nudged forward so the camera dosent show the gas mask/hood when looking around
    {
        // smoothly blend both the height AND the forward push toward standing or crouching, rather than snapping instantly
        float targetHeight = isCrouching ? crouchCameraHeight : cameraOffset.y;
        float targetForwardOffset = isCrouching ? crouchForwardOffset : standingForwardOffset;

        currentCameraHeight = Mathf.Lerp(currentCameraHeight, targetHeight, Time.deltaTime * cameraCrouchLerpSpeed);
        currentForwardOffset = Mathf.Lerp(currentForwardOffset, targetForwardOffset, Time.deltaTime * cameraCrouchLerpSpeed);

        Vector3 headPosition = transform.position + Vector3.up * currentCameraHeight;
        Vector3 forwardNudge = transform.forward * currentForwardOffset;
        cameraTransform.position = headPosition + forwardNudge;

        cameraTransform.rotation = Quaternion.Euler(
            cameraPitch,
            transform.eulerAngles.y,
            0f
        );
    }

    private void SetupTouchControls() // spawns the hand-built touch controls canvas and grabs its joystick/look surface/buttons
    {
        EnsureEventSystem();

        GameObject canvasInstance = Instantiate(touchControlsCanvasPrefab);
        moveJoystick = canvasInstance.GetComponentInChildren<TouchJoystick>();
        lookSurface = canvasInstance.GetComponentInChildren<TouchLookSurface>();

        WireUpButton(canvasInstance, "CrouchButton", ToggleCrouch);

        // flashlight lives on the child torch object, but the actual RPC has to live here on PlayerController
        // since Photon RPCs can't target components sitting on child objects
        flashlight = GetComponentInChildren<PlayerFlashLight>();
        WireUpButton(canvasInstance, "FlashlightButton", ToggleFlashlight);
    }

    private void WireUpButton(GameObject canvasInstance, string childName, UnityEngine.Events.UnityAction onClickAction)
    {
        // find the button by name ANYWHERE under the canvas, no matter how deeply nested,
        // since it can't be wired up in the Inspector ahead of time (the player/canvas don't exist yet at design time)
        Transform found = FindDeepChild(canvasInstance.transform, childName);
        if (found == null)
        {
            Debug.Log("Couldn't find a button named " + childName + " under the touch controls canvas");
            return;
        }

        Button button = found.GetComponent<Button>();
        if (button != null)
        {
            button.onClick.AddListener(onClickAction);
        }
    }

    private Transform FindDeepChild(Transform parent, string name) // searches every descendant, not just direct children
    {
        foreach (Transform child in parent)
        {
            if (child.name == name)
            {
                return child;
            }

            Transform foundInGrandchildren = FindDeepChild(child, name);
            if (foundInGrandchildren != null)
            {
                return foundInGrandchildren;
            }
        }

        return null;
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
