using TMPro;
using UnityEngine;
using UnityEngine.UI;

// a simple local pause menu, press Escape  or tap the pause button to open/close it
// this only pauses things on their screen since Time.timeScale is a per-client setting
public class PauseMenuController : MonoBehaviour
{
    [Header("UI")]
    public GameObject pausePanel; // drag in your pause menu panel here, should start INACTIVE in the Inspector
    public TextMeshProUGUI roomCodeText; // optional, a text inside the pause panel, shows the room code while paused (blank in solo)
    public AudioClip buttonClickClip; // plays when any button inside the pause panel is pressed

    private bool isPaused = false;
    private AudioSource uiSource;

    private void Awake() // gives every button inside the pause panel the click sound
    {
        if (pausePanel == null)
        {
            return;
        }

        uiSource = gameObject.AddComponent<AudioSource>();
        uiSource.playOnAwake = false;
        uiSource.spatialBlend = 0f;
        foreach (Button button in pausePanel.GetComponentsInChildren<Button>(true))
        {
            button.onClick.AddListener(PlayButtonClick);
        }
    }

    private void PlayButtonClick()
    {
        if (buttonClickClip != null)
        {
            uiSource.PlayOneShot(buttonClickClip);
        }
    }

    private void Update() // watches for the Escape key every frame, works even while paused since Input isn't affected by timeScale
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            TogglePause();
        }
    }

    public void TogglePause() // hook this up to your pause button's OnClick
    {
        isPaused = !isPaused;
        ApplyPausedState(isPaused);
    }

    public void ResumeButton() // hook this up to a Resume button inside the pause panel, if you have one - only ever resumes, never pauses
    {
        isPaused = false;
        ApplyPausedState(false);
    }

    public void QuitButton() // hook this up to your Quit button
    {
        // inside a room (solo counts) this goes back to the lobby instead of closing the game
        if (NetworkManager.Instance != null && NetworkManager.Instance.InRoom)
        {
            NetworkManager.Instance.QuitToLobby();
            return;
        }

#if UNITY_EDITOR
        // Application.Quit() does nothing while testing in the Editor, this stops Play mode instead
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void ApplyPausedState(bool paused) // does the actual work - shows/hides the panel, freezes/unfreezes the game, shows/hides the cursor
    {
        if (pausePanel != null)
        {
            pausePanel.SetActive(paused);
        }

        if (roomCodeText != null)
        {
            NetworkManager net = NetworkManager.Instance;
            bool showCode = paused && net != null && net.InRoom && net.IsSolo == false;
            roomCodeText.text = showCode ? "Room code: " + net.RoomCode : "";
        }

        Time.timeScale = paused ? 0f : 1f;

        Cursor.lockState = paused ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = paused;
    }
}