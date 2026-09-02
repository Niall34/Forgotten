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

    [Header("Camera")]
    public Vector3 cameraOffset = new Vector3(0f, 1.6f, 0f);
    public float cameraPitchMin = -80f;
    public float cameraPitchMax = 80f;
    public float lookSensitivity = 0.15f;

    [Header("Touch Controls")]
    public GameObject touchControlsCanvasPrefab; // a hand-built Canvas with a TouchJoystick and a TouchLookSurface on it somewhere inside

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

        // grab the horizontal move direction from the joystick, then let gravity add the vertical part,
        // then move the controller ONCE with both combined so we don't get double-move jitter
        Vector3 horizontalMove = HandleMove();
        Vector3 gravityMove = ApplyGravity();
        controller.Move((horizontalMove + gravityMove) * Time.deltaTime);
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

        float currentSpeed = sprinting ? sprintSpeed : moveSpeed;
        Vector3 worldMove = transform.TransformDirection(moveDirection) * currentSpeed;

        return worldMove;
    }

    private void SetupLocalCamera() // creates this player's own camera
    {
        GameObject cameraObject = new GameObject("Player Camera");
        playerCamera = cameraObject.AddComponent<Camera>();
        cameraTransform = cameraObject.transform;
        UpdateCameraPosition();
    }

    private void UpdateCameraPosition() // places the camera inside the player's head, nudged slightly forward so the camera dosent show the gas mask when looking around
    {
        Vector3 headPosition = transform.position + Vector3.up * cameraOffset.y;
        Vector3 forwardNudge = transform.forward * 0.05f;
        cameraTransform.position = headPosition + forwardNudge;

        cameraTransform.rotation = Quaternion.Euler(
            cameraPitch,
            transform.eulerAngles.y,
            0f
        );
    }

    private void SetupTouchControls() // spawns the hand-built touch controls canvas and grabs its joystick/look surface
    {
        EnsureEventSystem();

        GameObject canvasInstance = Instantiate(touchControlsCanvasPrefab);
        moveJoystick = canvasInstance.GetComponentInChildren<TouchJoystick>();
        lookSurface = canvasInstance.GetComponentInChildren<TouchLookSurface>();
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
