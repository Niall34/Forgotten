using UnityEngine;

// a simple local pause menu, press Escape  or tap the pause button to open/close it
// this only pauses things on their screen since Time.timeScale is a per-client setting
public class PauseMenuController : MonoBehaviour
{
    [Header("UI")]
    public GameObject pausePanel; // drag in your pause menu panel here, should start INACTIVE in the Inspector

    private bool isPaused = false;

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

        Time.timeScale = paused ? 0f : 1f;

        Cursor.lockState = paused ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = paused;
    }
}
