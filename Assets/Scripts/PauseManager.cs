using UnityEngine;
using UnityEngine.UI;

public class PauseManager : MonoBehaviour
{
    [Header("UI Sub-Elements")]
    [Tooltip("The panel containing the Play, Restart, and Exit buttons.")]
    [SerializeField] private GameObject pauseMenuPanel;

    [Header("Dependencies")]
    [Tooltip("Reference to your SumoGameManager to access active game setups.")]
    [SerializeField] private SumoGameManager gameManager;

    private Button selfButtonComponent;
    private Image selfImageComponent;

    private bool isPaused = false;
    private bool isMatchActive = false; // Tracks if gameplay is actively running

    private void Awake()
    {
        selfButtonComponent = GetComponent<Button>();
        selfImageComponent = GetComponent<Image>();
    }

    private void Start()
    {
        if (pauseMenuPanel != null) pauseMenuPanel.SetActive(false);

        // Starts hidden because the player is in the selection menus on launch
        SetPauseButtonGameplayState(false);
    }

    /// <summary>
    /// Handles hiding/showing the pause button safely during announcements, mode selection, or player setups.
    /// </summary>
    public void SetPauseButtonGameplayState(bool activeAndPlaying)
    {
        isMatchActive = activeAndPlaying;

        // Only show visually if the match is active AND we are not currently paused
        if (isMatchActive && !isPaused)
        {
            SetSelfButtonVisibility(true);
        }
        else
        {
            SetSelfButtonVisibility(false);
        }
    }

    public void PauseGame()
    {
        if (!isMatchActive) return;

        isPaused = true;
        Time.timeScale = 0f;

        SetSelfButtonVisibility(false);
        if (pauseMenuPanel != null) pauseMenuPanel.SetActive(true);

        TogglePlayerControls(false);
    }

    public void ResumeGame()
    {
        isPaused = false;
        Time.timeScale = 1f;

        if (pauseMenuPanel != null) pauseMenuPanel.SetActive(false);

        // FIX: Will now remain visible because isMatchActive is properly tracked!
        if (isMatchActive)
        {
            SetSelfButtonVisibility(true);
        }

        TogglePlayerControls(true);
    }

    /// <summary>
    /// RESTART FIX: Calls a custom match reset inside SumoGameManager 
    /// instead of wiping out the chosen variables via scene reloading.
    /// </summary>
    public void RestartMatch()
    {
        Time.timeScale = 1f;
        isPaused = false;

        if (pauseMenuPanel != null) pauseMenuPanel.SetActive(false);

        if (gameManager != null)
        {
            gameManager.RestartCurrentMatchSetup();
        }
        else
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex);
        }
    }

    /// <summary>
    /// EXIT TO MAIN MENU: Clears active match systems and loads the selection screen.
    /// </summary>
    public void ExitToGameModePanel()
    {
        Time.timeScale = 1f;
        isPaused = false;

        if (pauseMenuPanel != null) pauseMenuPanel.SetActive(false);
        SetPauseButtonGameplayState(false);

        if (gameManager != null)
        {
            gameManager.ReturnToHomeScreenHub();
        }
        else
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex);
        }
    }

    private void SetSelfButtonVisibility(bool visible)
    {
        if (selfButtonComponent != null) selfButtonComponent.enabled = visible;
        if (selfImageComponent != null) selfImageComponent.enabled = visible;
    }

    private void TogglePlayerControls(bool state)
    {
        if (gameManager != null && gameManager.spawnedControlUIs != null)
        {
            foreach (var ui in gameManager.spawnedControlUIs)
            {
                if (ui != null) ui.SetActive(state);
            }
        }
    }
}