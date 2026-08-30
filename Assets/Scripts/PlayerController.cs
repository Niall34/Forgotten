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
    public Vector3 cameraOffset = new Vector3(0f, 1.6f, 0f);
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
        playerCamera = cameraObject.AddComponent<Camera>();
        cameraTransform = cameraObject.transform;
        UpdateCameraPosition();
    }

    private void UpdateCameraPosition() // places the camera behind/above the player, looking at them
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
