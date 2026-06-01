using UnityEngine;

public class PauseManager : MonoBehaviour
{
    // Tracks the current pause state
    public static bool isPaused = false;

    // Reference to your Canvas Pause Menu Panel
    [SerializeField] private GameObject pauseMenuUI;

    void Update()
    {
        // Detects when the player presses the Escape key
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (isPaused)
            {
                Resume();
            }
            else
            {
                Pause();
            }
        }
    }

    public void Resume()
    {
        pauseMenuUI.SetActive(false); // Hides the pause menu UI
        Time.timeScale = 1f;          // Restores normal flow of time
        isPaused = false;
    }

    public void Pause()
    {
        pauseMenuUI.SetActive(true);  // Displays the pause menu UI
        Time.timeScale = 0f;          // Freezes all time-based operations
        isPaused = true;
    }
}