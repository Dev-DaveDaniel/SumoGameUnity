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
        if (pauseMenuPanel != null) pauseMenuPanel.SetActive(false);
        SetPauseButtonGameplayState(false); // hidden while in the selection menus
    }

    public void SetPauseButtonGameplayState(bool activeAndPlaying)
    {
        isMatchActive = activeAndPlaying;
        SetSelfButtonVisibility(isMatchActive && !isPaused);
    }

    public void PauseGame()
    {
        if (!isMatchActive) return;
        SetPaused(true);
        SetSelfButtonVisibility(false);
        TogglePlayerControls(false);
        if (gameManager != null) gameManager.CancelInvoke();
    }

    public void ResumeGame()
    {
        SetPaused(false);
        if (isMatchActive) SetSelfButtonVisibility(true);
        TogglePlayerControls(true);
        if (gameManager != null) gameManager.TogglePauseGameSystem();
    }

    // Resets through the manager instead of reloading the scene, so the chosen setup survives.
    public void RestartMatch()
    {
        SetPaused(false);
        if (gameManager != null) gameManager.RestartCurrentMatchSetup();
        else SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void ExitToGameModePanel()
    {
        SetPaused(false);
        SetPauseButtonGameplayState(false);
        if (gameManager != null) gameManager.ReturnToHomeScreenHub();
        else SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    private void SetPaused(bool paused)
    {
        isPaused = paused;
        Time.timeScale = paused ? 0f : 1f;
        if (pauseMenuPanel != null) pauseMenuPanel.SetActive(paused);
    }

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
