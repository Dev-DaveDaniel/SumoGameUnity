using UnityEngine;
using UnityEngine.SceneManagement;
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
    private bool isMatchActive = false;

    private void Awake()
    {
        selfButtonComponent = GetComponent<Button>();
        selfImageComponent = GetComponent<Image>();
    }

    private void Start()
    {
        if (pauseMenuPanel != null) pauseMenuPanel.SetActive(false);
        SetPauseButtonGameplayState(false); // hidden while in the menus
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

    // Resets the match in place so the chosen mode/player count survive (no scene reload).
    public void RestartMatch()
    {
        SetPaused(false);
        if (gameManager != null) gameManager.RestartCurrentMatchSetup();
        else ReloadScene();
    }

    public void ExitToGameModePanel()
    {
        SetPaused(false);
        SetPauseButtonGameplayState(false);
        if (gameManager != null) gameManager.ReturnToHomeScreenHub();
        else ReloadScene();
    }

    private void SetPaused(bool paused)
    {
        isPaused = paused;
        Time.timeScale = paused ? 0f : 1f;
        if (pauseMenuPanel != null) pauseMenuPanel.SetActive(paused);
    }

    private static void ReloadScene() => SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);

    private void SetSelfButtonVisibility(bool visible)
    {
        if (selfButtonComponent != null) selfButtonComponent.enabled = visible;
        if (selfImageComponent != null) selfImageComponent.enabled = visible;
    }

    private void TogglePlayerControls(bool state)
    {
        if (gameManager == null || gameManager.spawnedControlUIs == null) return;
        foreach (var ui in gameManager.spawnedControlUIs) if (ui != null) ui.SetActive(state);
    }
}
