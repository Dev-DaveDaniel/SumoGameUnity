using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

public enum GameMode { Versus, Team }

public class SumoGameManager : MonoBehaviour
{
    public static SumoGameManager Instance { get; private set; }

    [Header("UI Menus & Main Panels")]
    [SerializeField] private GameObject titleScreenPanel;
    [SerializeField] private GameObject howToPlayPanel;
    [SerializeField] private GameObject controlsImagePanel;
    [SerializeField] private GameObject rulesTextPanel;
    [SerializeField] private GameObject modeSelectPanel;
    [SerializeField] private GameObject characterSelectPanel;
    [SerializeField] private GameObject gameplayUIPanel;
    [SerializeField] private GameObject pauseMenuPanel;
    [SerializeField] private PauseManager pauseButtonManager;
    [SerializeField] private TextMeshProUGUI timerText;

    [Header("Round Announcement Settings")]
    [SerializeField] private TextMeshProUGUI roundWinnerAnnounceText;

    [Header("Prefabs to Spawn")]
    [SerializeField] private GameObject playerPrefab;
    [SerializeField] private GameObject mobileControlsUIPrefab;

    [Header("Match Parameters")]
    public float roundDurationSeconds = 15f;
    public int totalVersusRounds = 5;
    public int teamRoundsToWin = 3;
    [SerializeField] private float spawnRadiusFromCenter = 3.5f;

    [Header("UI Spawn Anchors")]
    [SerializeField] private RectTransform[] uiCornerAnchors = new RectTransform[4];

    [Header("Sumo Arena Rings")]
    [SerializeField] private List<SumoRingZone> arenaRings = new List<SumoRingZone>();
    private int currentActiveRingIndex = 0;

    [Header("Audio Settings")]
    [SerializeField] private AudioSource globalAudioSource;
    [SerializeField] private AudioClip roundStartClip;

    private GameMode activeGameMode = GameMode.Versus;
    private int activePlayerCount = 2;
    private int currentRound = 1;
    private float currentTimer;
    private bool isRoundActive = false;
    private bool isGamePaused = false;

    private int[] playerScores = new int[4];
    private int teamAScore = 0;
    private int teamBScore = 0;
    private const int VERSUS_WINNING_SCORE = 15;

    private int[] playerTeamAssignments = new int[4];
    private bool[] playerPassedEngagementCheck = { true, true, true, true };

    private List<GameObject> activeWrestlersInRound = new List<GameObject>();
    private List<GameObject> eliminatedThisRound = new List<GameObject>();
    public GameObject[] spawnedControlUIs = new GameObject[4];

    private bool isSuddenDeathActive = false;
    private int suddenDeathPlayerA = -1;
    private int suddenDeathPlayerB = -1;

    private Color teamARed = new Color(1f, 0.15f, 0.2f, 1f);
    private Color teamBBlue = new Color(0f, 0.5f, 1f, 1f);

    private Color[] versusPlayerColors = {
        new Color(1f, 0.15f, 0.35f, 1f),
        new Color(1f, 0.84f, 0f, 1f),
        new Color(0.2f, 1f, 0.2f, 1f),
        new Color(0f, 0.65f, 1f, 1f)
    };

    private string[] versusColorNames = { "RED", "GOLD", "GREEN", "BLUE" };

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void Start()
    {
        AudioListener.volume = 0f; // sound is disabled for the whole game
        ShowOnlyThisPanel(titleScreenPanel);
        if (pauseMenuPanel != null) pauseMenuPanel.SetActive(false);
        if (roundWinnerAnnounceText != null) roundWinnerAnnounceText.gameObject.SetActive(false);
    }

    private void ShowOnlyThisPanel(GameObject targetPanel)
    {
        foreach (var p in new[] { titleScreenPanel, howToPlayPanel, controlsImagePanel, rulesTextPanel, modeSelectPanel, characterSelectPanel, gameplayUIPanel })
            if (p != null) p.SetActive(p == targetPanel);
    }

    private void SetPauseButton(bool state)
    {
        if (pauseButtonManager != null) pauseButtonManager.SetPauseButtonGameplayState(state);
    }

    private int IndexOf(GameObject player) => player.GetComponent<TopDownMovement>().playerIndex;

    // Home screen navigation (wired to UI buttons)
    public void OnClickPlayButton() => ShowOnlyThisPanel(modeSelectPanel);
    public void OnClickHowToPlayMenuButton() => ShowOnlyThisPanel(howToPlayPanel);
    public void OnClickControlSchemeSubButton() => ShowOnlyThisPanel(controlsImagePanel);
    public void OnClickRulesSubButton() => ShowOnlyThisPanel(rulesTextPanel);
    public void OnClickBackToHowToPlayPanel() => ShowOnlyThisPanel(howToPlayPanel);
    public void OnClickExitToTitleScreen() => ShowOnlyThisPanel(titleScreenPanel);

    public void SelectVersusModeMenu()
    {
        activeGameMode = GameMode.Versus;
        ShowOnlyThisPanel(characterSelectPanel);
        SetPauseButton(false);
    }

    public void SelectTeamModeGame()
    {
        activeGameMode = GameMode.Team;
        BeginMatch(4, false);
    }

    public void SelectPlayerCount(int count)
    {
        activeGameMode = GameMode.Versus;
        BeginMatch(Mathf.Clamp(count, 2, 4), false);
    }

    public void LaunchMatchLive(int totalPlayers, int[] cosmeticSkins) => BeginMatch(totalPlayers, true);

    private void BeginMatch(int players, bool pauseButtonState)
    {
        activePlayerCount = players;
        ShowOnlyThisPanel(gameplayUIPanel);
        SetPauseButton(pauseButtonState);
        ResetMatchDataFull();
        StartNewRound();
    }

    public void ReturnToHomeScreenHub()
    {
        isGamePaused = false;
        isRoundActive = false;
        Time.timeScale = 1f;
        if (globalAudioSource != null) globalAudioSource.Stop();
        if (pauseMenuPanel != null) pauseMenuPanel.SetActive(false);
        SetPauseButton(false);
        ShowOnlyThisPanel(titleScreenPanel);
        ClearMatchData();
    }

    private void ResetMatchDataFull()
    {
        currentRound = 1;
        isSuddenDeathActive = false;
        isGamePaused = false;
        teamAScore = teamBScore = 0;
        Time.timeScale = 1f;
        for (int i = 0; i < 4; i++)
        {
            playerScores[i] = 0;
            playerPassedEngagementCheck[i] = true;
        }
        ResetRingsToDefault();
    }

    private void StartNewRound()
    {
        isRoundActive = false;
        Time.timeScale = 1f;
        StopAllCoroutines();
        ClearMatchData();
        SetPauseButton(false);

        isSuddenDeathActive = false;
        if (currentRound == 1) ResetRingsToDefault();

        SpawnPlayersAndUI(activePlayerCount, spawnRadiusFromCenter);
        currentTimer = roundDurationSeconds;
        StartCoroutine(IntroCountdownSequence());
    }

    private IEnumerator IntroCountdownSequence()
    {
        DisplayRoundEndNotification(activeGameMode == GameMode.Versus ? $"ROUND {currentRound}" : $"TEAM BATTLE - ROUND {currentRound}");
        yield return new WaitForSeconds(1.5f);
        DisplayRoundEndNotification("READY...");
        yield return new WaitForSeconds(1.0f);
        DisplayRoundEndNotification("FIGHT!");

        if (globalAudioSource != null && roundStartClip != null && !globalAudioSource.isPlaying)
        {
            globalAudioSource.clip = roundStartClip;
            globalAudioSource.Play();
        }

        Time.timeScale = 1f;
        isRoundActive = true;
        SetPauseButton(true);

        yield return new WaitForSeconds(1.0f);
        DisplayRoundEndNotification("");
    }

    private void Update()
    {
        if (!isRoundActive || isGamePaused) return;
        currentTimer -= Time.deltaTime;
        if (timerText != null) timerText.text = Mathf.Max(0, Mathf.CeilToInt(currentTimer)).ToString();
        if (currentTimer <= 0) EndRoundDueToTimeout();
    }

    private void SpawnPlayersAndUI(int countToSpawn, float radius)
    {
        for (int i = 0; i < countToSpawn; i++)
        {
            float angle = i * (360f / countToSpawn) * Mathf.Deg2Rad;
            Vector2 spawnPosition = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;

            GameObject newPlayer = Instantiate(playerPrefab, spawnPosition, Quaternion.identity);
            newPlayer.name = "Sumo Main";
            activeWrestlersInRound.Add(newPlayer);

            TopDownMovement movement = newPlayer.GetComponent<TopDownMovement>();
            if (movement != null)
            {
                movement.playerIndex = i;
                movement.currentKnockbackReceivedMultiplier = playerPassedEngagementCheck[i] ? 1.0f : 1.5f;
            }

            // face the arena centre
            newPlayer.transform.rotation = Quaternion.AngleAxis(Mathf.Atan2(-spawnPosition.y, -spawnPosition.x) * Mathf.Rad2Deg - 90f, Vector3.forward);

            Color renderingColor = versusPlayerColors[i];
            string label = versusColorNames[i];
            if (activeGameMode == GameMode.Team)
            {
                playerTeamAssignments[i] = i % 2;
                renderingColor = i % 2 == 0 ? teamARed : teamBBlue;
                label = $"T{(i % 2 == 0 ? "A" : "B")}-P{i + 1}";
            }

            SpriteRenderer sr = newPlayer.GetComponentInChildren<SpriteRenderer>();
            if (sr != null) sr.color = renderingColor;

            GameObject textObj = new GameObject("PlayerNumberText");
            textObj.transform.SetParent(newPlayer.transform);
            textObj.transform.localPosition = new Vector3(0f, 1.2f, 0f);
            textObj.transform.rotation = Quaternion.identity;

            TextMeshPro tmproText = textObj.AddComponent<TextMeshPro>();
            tmproText.text = label;
            tmproText.fontSize = 5f;
            tmproText.alignment = TextAlignmentOptions.Center;
            tmproText.color = Color.white;
            tmproText.fontStyle = FontStyles.Bold;
            tmproText.outlineWidth = 0.2f;
            tmproText.outlineColor = Color.black;
            textObj.AddComponent<LookAtCameraUpright>();

            SpawnControllerCanvas(newPlayer, i, renderingColor);
        }
    }

    private void SpawnControllerCanvas(GameObject playerObj, int playerIdx, Color identityColor)
    {
        if (playerIdx >= uiCornerAnchors.Length || uiCornerAnchors[playerIdx] == null) return;

        GameObject uiControl = Instantiate(mobileControlsUIPrefab, uiCornerAnchors[playerIdx]);
        spawnedControlUIs[playerIdx] = uiControl;

        Vector3 corner = uiCornerAnchors[playerIdx].localPosition;
        uiControl.transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(corner.y, corner.x) * Mathf.Rad2Deg + 90f);

        foreach (var img in uiControl.GetComponentsInChildren<Image>())
            if (img.gameObject != uiControl) img.color = identityColor * 0.85f;

        MobileControls inputBridge = uiControl.GetComponent<MobileControls>();
        TopDownMovement movementController = playerObj.GetComponent<TopDownMovement>();
        if (movementController != null && inputBridge != null) movementController.controls = inputBridge;
    }

    public void WrestlerFellOut(GameObject victimObj)
    {
        if (!isRoundActive || isGamePaused || !activeWrestlersInRound.Contains(victimObj)) return;

        TopDownMovement victim = victimObj.GetComponent<TopDownMovement>();
        int victimIdx = victim != null ? victim.playerIndex : 0;

        if (spawnedControlUIs[victimIdx] != null)
        {
            Destroy(spawnedControlUIs[victimIdx]);
            spawnedControlUIs[victimIdx] = null;
        }

        if (JuiceManager.Instance != null) JuiceManager.Instance.TriggerImpactJuice(0.12f, 0.25f, 0.45f, 0.4f);

        activeWrestlersInRound.Remove(victimObj);
        eliminatedThisRound.Add(victimObj);

        // kill credit: 1 / 2 / 3 pts for a 1 / 2 / 3+ hit combo inside the tracking window
        if (victim != null && victim.lastAttackerIndex != -1 && Time.time - victim.lastInteractionTimestamp <= victim.trackingWindowDuration)
            playerScores[victim.lastAttackerIndex] += Mathf.Clamp(victim.consecutiveHitCount, 1, 3);

        if (activeGameMode == GameMode.Team) EvaluateTeamRoundStatus();
        else EvaluateVersusRoundStatus(victimIdx);
    }

    private void StopRound()
    {
        isRoundActive = false;
        FreezeAllMovement();
        SetPauseButton(false);
    }

    private void EvaluateTeamRoundStatus()
    {
        int teamALive = activeWrestlersInRound.Count(w => w != null && playerTeamAssignments[IndexOf(w)] == 0);
        int teamBLive = activeWrestlersInRound.Count(w => w != null && playerTeamAssignments[IndexOf(w)] != 0);

        if (teamALive == 1 && teamBLive == 1) HandleRingDrop();
        if (teamALive != 0 && teamBLive != 0) return;

        StopRound();

        string text;
        if (teamALive == 0 && teamBLive == 0) text = "MUTUAL TEAM ELIMINATION!\nROUND DRAW!";
        else
        {
            if (teamALive == 0) teamBScore++; else teamAScore++;
            text = $"{(teamALive == 0 ? "BLUE" : "RED")} TEAM WINS THE ROUND!\n\n<b>--- STANDINGS ---</b>\nRED TEAM: {teamAScore} WINS\nBLUE TEAM: {teamBScore} WINS";
        }

        DisplayRoundEndNotification(text);
        if (teamAScore >= teamRoundsToWin) EndMatch("RED TEAM HAS WON THE MATCH!");
        else if (teamBScore >= teamRoundsToWin) EndMatch("BLUE TEAM HAS WON THE MATCH!");
        else NextRound();
    }

    private void EvaluateVersusRoundStatus(int victimIdx)
    {
        if (isSuddenDeathActive)
        {
            StopRound();
            int winnerIdx = victimIdx == suddenDeathPlayerA ? suddenDeathPlayerB : suddenDeathPlayerA;
            playerScores[winnerIdx] += 5;
            CheckVersusTournamentStandings($"{versusColorNames[winnerIdx]} WINS SUDDEN DEATH!\n\n" + BuildLeaderboardString());
            return;
        }

        // placement points for the player who just fell
        int remaining = activeWrestlersInRound.Count + 1;
        if (remaining == 2) playerScores[victimIdx] += activePlayerCount == 2 ? 1 : 3;
        else if (remaining == 3 && activePlayerCount == 4) playerScores[victimIdx] += 1;

        if (activeWrestlersInRound.Count == 1)
        {
            StopRound();
            int survivorIdx = IndexOf(activeWrestlersInRound[0]);
            playerScores[survivorIdx] += 5;
            RecordEngagementStates();
            CheckVersusTournamentStandings($"{versusColorNames[survivorIdx]} SURVIVED THE ROUND! (+5 pts)\n\n" + BuildLeaderboardString());
        }
        else if (activeWrestlersInRound.Count == 0 && eliminatedThisRound.Count >= 2)
        {
            StopRound();
            int pIdxA = IndexOf(eliminatedThisRound[eliminatedThisRound.Count - 1]);
            int pIdxB = IndexOf(eliminatedThisRound[eliminatedThisRound.Count - 2]);
            playerScores[pIdxA] += 3;
            playerScores[pIdxB] += 3;
            RecordEngagementStates();
            StartCoroutine(TriggerSuddenDeathTransitionSequence(pIdxA, pIdxB));
        }
    }

    private void EndRoundDueToTimeout()
    {
        if (!isRoundActive) return;

        if (activeGameMode == GameMode.Team)
        {
            if (activeWrestlersInRound.Count == 4) HandleRingDrop();
            currentTimer = roundDurationSeconds;
            return;
        }

        StopRound();
        HandleRingDrop();

        foreach (var p in activeWrestlersInRound)
        {
            TopDownMovement tdm = p != null ? p.GetComponent<TopDownMovement>() : null;
            if (tdm == null || tdm.playerIndex >= activePlayerCount) continue;
            int i = tdm.playerIndex;
            playerPassedEngagementCheck[i] = tdm.hasDealtDamageThisRound;
            if (!tdm.hasDealtDamageThisRound) playerScores[i] = Mathf.Max(0, playerScores[i] - 1);
        }

        CheckVersusTournamentStandings("TIME OUT! NO ENGAGEMENT DETECTED! RING SHRUNK!\n\n" + BuildLeaderboardString());
    }

    private void CheckVersusTournamentStandings(string completionBannerMessage)
    {
        DisplayRoundEndNotification(completionBannerMessage);

        int highestScoreIdx = 0;
        for (int i = 1; i < activePlayerCount; i++)
            if (playerScores[i] > playerScores[highestScoreIdx]) highestScoreIdx = i;

        string leader = versusColorNames[highestScoreIdx];
        if (playerScores[highestScoreIdx] >= VERSUS_WINNING_SCORE) EndMatch($"{leader} IS THE FIRST TO 15 PTS!");
        else if (currentRound >= totalVersusRounds) { currentRound++; EndMatch($"{leader} HIGHEST FINAL SCORE WIN!"); }
        else NextRound();
    }

    private void NextRound()
    {
        currentRound++;
        StartCoroutine(RoundTransitionDelayRoutine(5f));
    }

    private void EndMatch(string winnerLine)
    {
        if (pauseMenuPanel != null) pauseMenuPanel.SetActive(false);
        if (globalAudioSource != null) globalAudioSource.Stop();
        StartCoroutine(DelayedBannerOverride($"🏆 MATCH OVER 🏆\n{winnerLine}", 3.5f));
        StartCoroutine(MatchEndReturnDelayRoutine(7f));
    }

    private string BuildLeaderboardString()
    {
        string board = "<b>--- SCOREBOARD (RACE TO 15) ---</b>\n";
        var sorted = Enumerable.Range(0, activePlayerCount).ToList();
        sorted.Sort((a, b) => playerScores[b].CompareTo(playerScores[a]));
        for (int place = 0; place < sorted.Count; place++)
            board += $"{(place == 0 ? "👑 1st" : $"{place + 1}th")}: {versusColorNames[sorted[place]]} - {playerScores[sorted[place]]} / 15 pts\n";
        return board;
    }

    private void RecordEngagementStates()
    {
        foreach (var player in activeWrestlersInRound.Concat(eliminatedThisRound))
            if (player != null) playerPassedEngagementCheck[IndexOf(player)] = player.GetComponent<TopDownMovement>().hasDealtDamageThisRound;
    }

    public void TogglePauseGameSystem()
    {
        if (!gameplayUIPanel.activeSelf) return;
        isGamePaused = !isGamePaused;
        if (pauseMenuPanel != null) pauseMenuPanel.SetActive(isGamePaused);
        Time.timeScale = isGamePaused ? 0f : 1f;
    }

    private void SetRing(int i, bool active)
    {
        if (arenaRings[i] == null) return;
        arenaRings[i].isCurrentOutboundLimit = active;
        arenaRings[i].gameObject.SetActive(active);
    }

    private void HandleRingDrop()
    {
        if (currentActiveRingIndex >= arenaRings.Count - 1) return;
        SetRing(currentActiveRingIndex, false);
        SetRing(++currentActiveRingIndex, true);
    }

    private void ResetRingsToDefault()
    {
        currentActiveRingIndex = 0;
        for (int i = 0; i < arenaRings.Count; i++) SetRing(i, i == 0);
    }

    private void FreezeAllMovement()
    {
        foreach (var player in activeWrestlersInRound)
        {
            if (player != null && player.TryGetComponent<Rigidbody2D>(out var rb))
            {
                rb.linearVelocity = Vector2.zero;
                rb.angularVelocity = 0f;
            }
        }
    }

    public void DisplayRoundEndNotification(string displayMessage)
    {
        if (roundWinnerAnnounceText == null) return;
        bool show = !string.IsNullOrEmpty(displayMessage);
        roundWinnerAnnounceText.gameObject.SetActive(show);
        if (show) roundWinnerAnnounceText.text = displayMessage;
        if (timerText != null) timerText.gameObject.SetActive(!show);
        foreach (var ui in spawnedControlUIs) if (ui != null) ui.SetActive(!show);
    }

    private void HideAnnouncement()
    {
        if (roundWinnerAnnounceText != null)
        {
            roundWinnerAnnounceText.text = "";
            roundWinnerAnnounceText.gameObject.SetActive(false);
        }
        if (timerText != null) timerText.gameObject.SetActive(true);
    }

    private IEnumerator TriggerSuddenDeathTransitionSequence(int pA, int pB)
    {
        DisplayRoundEndNotification("MUTUAL ELIMINATION DETECTED!\nPREPARING SUDDEN DEATH TIEBREAKER...");
        yield return new WaitForSeconds(3.5f);

        ClearMatchData();
        isSuddenDeathActive = true;
        suddenDeathPlayerA = pA;
        suddenDeathPlayerB = pB;
        currentTimer = roundDurationSeconds;
        isRoundActive = true;
        SetPauseButton(true);
    }

    private IEnumerator DelayedBannerOverride(string text, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (roundWinnerAnnounceText != null) roundWinnerAnnounceText.text = text;
    }

    private IEnumerator RoundTransitionDelayRoutine(float delayDuration)
    {
        yield return new WaitForSeconds(delayDuration - 0.5f);
        HideAnnouncement();
        yield return new WaitForSeconds(0.5f);
        StartNewRound();
    }

    private IEnumerator MatchEndReturnDelayRoutine(float delayDuration)
    {
        yield return new WaitForSeconds(delayDuration);
        ReturnToHomeScreenHub();
    }

    public void RestartCurrentMatchSetup()
    {
        isGamePaused = false;
        StopAllCoroutines();
        if (globalAudioSource != null) globalAudioSource.Stop();
        ClearMatchData();
        for (int i = 0; i < playerScores.Length; i++) playerScores[i] = 0;
        HideAnnouncement();
        StartNewRound();
    }

    private void ClearMatchData()
    {
        foreach (var player in activeWrestlersInRound.Concat(eliminatedThisRound)) if (player != null) Destroy(player);
        for (int i = 0; i < spawnedControlUIs.Length; i++)
        {
            if (spawnedControlUIs[i] != null) Destroy(spawnedControlUIs[i]);
            spawnedControlUIs[i] = null;
        }
        activeWrestlersInRound.Clear();
        eliminatedThisRound.Clear();
    }
}
