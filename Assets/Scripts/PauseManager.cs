using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class PauseManager : MonoBehaviour
{
    [SerializeField] private GameObject pauseMenuPanel;
    [SerializeField] private SumoGameManager gameManager;

    private Button selfButton;
    private Image selfImage;
    private bool isPaused, isMatchActive;

    private void Awake()
    {
        selfButton = GetComponent<Button>();
        selfImage = GetComponent<Image>();
    }

    private void Start()
    {
        SetMenu(false);
        SetPauseButtonGameplayState(false); // hidden while in selection menus
    }

    public void SetPauseButtonGameplayState(bool activeAndPlaying)
    {
        isMatchActive = activeAndPlaying;
        SetSelfButtonVisibility(isMatchActive && !isPaused);
    }

    public void PauseGame()
    {
        if (!isMatchActive) return;
        isPaused = true;
        Time.timeScale = 0f;
        SetSelfButtonVisibility(false);
        SetMenu(true);
        TogglePlayerControls(false);
        if (gameManager != null) gameManager.CancelInvoke();
    }

    public void ResumeGame()
    {
        isPaused = false;
        Time.timeScale = 1f;
        SetMenu(false);
        if (isMatchActive) SetSelfButtonVisibility(true);
        TogglePlayerControls(true);
        if (gameManager != null) gameManager.TogglePauseGameSystem(); // clears manager's frozen update state
    }

    // Resets via the manager so chosen mode/player count survive (no scene reload).
    public void RestartMatch()
    {
        Unpause();
        if (gameManager != null) gameManager.RestartCurrentMatchSetup();
        else ReloadScene();
    }

    public void ExitToGameModePanel()
    {
        Unpause();
        SetPauseButtonGameplayState(false);
        if (gameManager != null) gameManager.ReturnToHomeScreenHub();
        else ReloadScene();
    }

    private void Unpause()
    {
        Time.timeScale = 1f;
        isPaused = false;
        SetMenu(false);
    }

    private void SetMenu(bool on) { if (pauseMenuPanel != null) pauseMenuPanel.SetActive(on); }

    private static void ReloadScene() => SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);

    private void SetSelfButtonVisibility(bool visible)
    {
        if (selfButton != null) selfButton.enabled = visible;
        if (selfImage != null) selfImage.enabled = visible;
    }

    private void TogglePlayerControls(bool state)
    {
        if (gameManager == null || gameManager.spawnedControlUIs == null) return;
        foreach (var ui in gameManager.spawnedControlUIs) if (ui != null) ui.SetActive(state);
    }
}
