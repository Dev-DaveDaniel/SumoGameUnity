using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

public enum GameMode { Versus, Team }

public class SumoGameManager : MonoBehaviour
{
    public static SumoGameManager Instance { get; private set; }

    [Header("UI Menus & Main Panels")]
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

    private GameMode activeGameMode = GameMode.Versus;
    private int activePlayerCount = 2;
    private int currentRound = 1;
    private float currentTimer;
    private bool isRoundActive = false;
    private bool isGamePaused = false;
    private bool isMatchOngoing = false;

    private int[] playerScores = new int[4];
    private int teamAScore = 0;
    private int teamBScore = 0;
    private const int VERSUS_WINNING_SCORE = 15;

    private int[] playerTeamAssignments = new int[4];
    private bool[] playerPassedEngagementCheck = new bool[] { true, true, true, true };

    private List<GameObject> activeWrestlersInRound = new List<GameObject>();
    private List<GameObject> eliminatedThisRound = new List<GameObject>();
    public GameObject[] spawnedControlUIs = new GameObject[4];

    private bool isSuddenDeathActive = false;
    private int suddenDeathPlayerA = -1;
    private int suddenDeathPlayerB = -1;

    private Color teamARed = new Color(1f, 0.15f, 0.2f, 1f);
    private Color teamBBlue = new Color(0f, 0.5f, 1f, 1f);

    private Color[] versusPlayerColors = new Color[] {
        new Color(1f, 0.15f, 0.35f, 1f),
        new Color(1f, 0.84f, 0f, 1f),
        new Color(0.2f, 1f, 0.2f, 1f),
        new Color(0f, 0.65f, 1f, 1f)
    };

    private string[] versusColorNames = new string[] {
        "RED",
        "GOLD",
        "GREEN",
        "BLUE"
    };

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void Start()
    {
        if (modeSelectPanel != null) modeSelectPanel.SetActive(true);
        characterSelectPanel.SetActive(false);
        gameplayUIPanel.SetActive(false);
        if (pauseMenuPanel != null) pauseMenuPanel.SetActive(false);
        if (roundWinnerAnnounceText != null) roundWinnerAnnounceText.gameObject.SetActive(false);
    }

    public void SelectVersusModeMenu()
    {
        activeGameMode = GameMode.Versus;
        if (modeSelectPanel != null) modeSelectPanel.SetActive(false);
        if (pauseButtonManager != null) pauseButtonManager.SetPauseButtonGameplayState(false);
        characterSelectPanel.SetActive(true);
    }

    public void SelectTeamModeGame()
    {
        activeGameMode = GameMode.Team;
        activePlayerCount = 4;

        if (modeSelectPanel != null) modeSelectPanel.SetActive(false);
        if (pauseButtonManager != null) pauseButtonManager.SetPauseButtonGameplayState(false);
        characterSelectPanel.SetActive(false);
        gameplayUIPanel.SetActive(true);

        ResetMatchDataFull();
        isMatchOngoing = true;
        StartNewRound();
    }

    public void SelectPlayerCount(int count)
    {
        activeGameMode = GameMode.Versus;
        activePlayerCount = Mathf.Clamp(count, 2, 4);
        characterSelectPanel.SetActive(false);
        gameplayUIPanel.SetActive(true);
        if (pauseButtonManager != null) pauseButtonManager.SetPauseButtonGameplayState(false);

        ResetMatchDataFull();
        isMatchOngoing = true;
        StartNewRound();
    }

    public void LaunchMatchLive(int totalPlayers, int[] cosmeticSkins)
    {
        activePlayerCount = totalPlayers;
        if (characterSelectPanel != null) characterSelectPanel.SetActive(false);
        if (gameplayUIPanel != null) gameplayUIPanel.SetActive(true);

        ResetMatchDataFull();
        isMatchOngoing = true;

        if (pauseButtonManager != null) pauseButtonManager.SetPauseButtonGameplayState(true);

        StartNewRound();
    }

    private void ResetMatchDataFull()
    {
        currentRound = 1;
        isSuddenDeathActive = false;
        isGamePaused = false;
        teamAScore = 0;
        teamBScore = 0;
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
        Time.timeScale = 1f; // RESET CLOCK SPEED TO NORMAL
        StopAllCoroutines();
        ClearMatchData();

        if (pauseButtonManager != null) pauseButtonManager.SetPauseButtonGameplayState(false);

        isSuddenDeathActive = false;
        if (currentRound == 1) ResetRingsToDefault();

        SpawnPlayersAndUI(activePlayerCount, spawnRadiusFromCenter);

        currentTimer = roundDurationSeconds;

        // Run the custom countdown intro sequentially using your original announcement framework
        StartCoroutine(IntroCountdownSequence());
    }

    private IEnumerator IntroCountdownSequence()
    {
        string headerText = activeGameMode == GameMode.Versus ? $"ROUND {currentRound}" : $"TEAM BATTLE - ROUND {currentRound}";
        DisplayRoundEndNotification(headerText);
        yield return new WaitForSeconds(1.5f);

        DisplayRoundEndNotification("READY...");
        yield return new WaitForSeconds(1.0f);

        DisplayRoundEndNotification("FIGHT!");

        // --- GAME ACTIVATION POINT ---
        Time.timeScale = 1f; // FORCE TIME TO RUN UNPAUSED
        isRoundActive = true;

        yield return new WaitForSeconds(1.0f);
        DisplayRoundEndNotification(""); // Clears banner overlay completely and activates standard control layers via your logic
    }

    private void Update()
    {
        if (!isRoundActive || isGamePaused) return;

        currentTimer -= Time.deltaTime;
        if (timerText != null) timerText.text = Mathf.Max(0, Mathf.CeilToInt(currentTimer)).ToString();

        if (currentTimer <= 0)
        {
            EndRoundDueToTimeout();
        }
    }

    private void SpawnPlayersAndUI(int CountToSpawn, float radius)
    {
        for (int i = 0; i < CountToSpawn; i++)
        {
            float angle = i * (360f / CountToSpawn) * Mathf.Deg2Rad;
            Vector2 spawnPosition = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;

            GameObject newPlayer = Instantiate(playerPrefab, spawnPosition, Quaternion.identity);
            newPlayer.name = $"Wrestler_{i + 1}";
            activeWrestlersInRound.Add(newPlayer);

            TopDownMovement movement = newPlayer.GetComponent<TopDownMovement>();
            if (movement != null)
            {
                movement.playerIndex = i;
                movement.currentKnockbackReceivedMultiplier = playerPassedEngagementCheck[i] ? 1.0f : 1.5f;
            }

            Vector3 directionToCenter = Vector3.zero - newPlayer.transform.position;
            float angleToCenter = Mathf.Atan2(directionToCenter.y, directionToCenter.x) * Mathf.Rad2Deg;
            newPlayer.transform.rotation = Quaternion.AngleAxis(angleToCenter - 90f, Vector3.forward);

            Color renderingColor = versusPlayerColors[i];
            if (activeGameMode == GameMode.Team)
            {
                int teamAssignment = (i % 2 == 0) ? 0 : 1;
                playerTeamAssignments[i] = teamAssignment;
                renderingColor = (teamAssignment == 0) ? teamARed : teamBBlue;
            }

            SpriteRenderer sr = newPlayer.GetComponentInChildren<SpriteRenderer>();
            if (sr != null) sr.color = renderingColor;

            GameObject textObj = new GameObject("PlayerNumberText");
            textObj.transform.SetParent(newPlayer.transform);
            textObj.transform.localPosition = new Vector3(0f, 1.2f, 0f);
            textObj.transform.rotation = Quaternion.identity;

            TextMeshPro tmproText = textObj.AddComponent<TextMeshPro>();

            if (activeGameMode == GameMode.Team)
            {
                tmproText.text = $"T{(playerTeamAssignments[i] == 0 ? "A" : "B")}-P{i + 1}";
            }
            else
            {
                tmproText.text = versusColorNames[i];
            }

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
        if (playerIdx < uiCornerAnchors.Length && uiCornerAnchors[playerIdx] != null)
        {
            GameObject uiControl = Instantiate(mobileControlsUIPrefab, uiCornerAnchors[playerIdx]);
            spawnedControlUIs[playerIdx] = uiControl;

            Image[] buttonBackgrounds = uiControl.GetComponentsInChildren<Image>();
            foreach (var img in buttonBackgrounds)
            {
                if (img.gameObject != uiControl) img.color = identityColor * 0.85f;
            }

            MobileControls inputBridge = uiControl.GetComponent<MobileControls>();
            TopDownMovement movementController = playerObj.GetComponent<TopDownMovement>();

            if (movementController != null && inputBridge != null)
            {
                movementController.controls = inputBridge;
            }
        }
    }

    public void WrestlerFellOut(GameObject victimObj)
    {
        if (!isRoundActive || isGamePaused || !activeWrestlersInRound.Contains(victimObj)) return;

        TopDownMovement victimMovement = victimObj.GetComponent<TopDownMovement>();
        int victimPlayerIndex = victimMovement != null ? victimMovement.playerIndex : 0;

        if (spawnedControlUIs[victimPlayerIndex] != null)
        {
            Destroy(spawnedControlUIs[victimPlayerIndex]);
            spawnedControlUIs[victimPlayerIndex] = null;
        }

        if (JuiceManager.Instance != null) JuiceManager.Instance.TriggerImpactJuice(0.12f, 0.25f, 0.45f, 0.4f);

        activeWrestlersInRound.Remove(victimObj);
        eliminatedThisRound.Add(victimObj);

        if (victimMovement != null && victimMovement.lastAttackerIndex != -1)
        {
            float elapsedSinceLastHit = Time.time - victimMovement.lastInteractionTimestamp;

            if (elapsedSinceLastHit <= victimMovement.trackingWindowDuration)
            {
                int killerIdx = victimMovement.lastAttackerIndex;
                int koPayout = victimMovement.consecutiveHitCount >= 3 ? 3 : (victimMovement.consecutiveHitCount == 2 ? 2 : 1);
                playerScores[killerIdx] += koPayout;
                Debug.Log($"Elimination Verified! Player {killerIdx + 1} gets credit for pushing Player {victimPlayerIndex + 1} out.");
            }
            else
            {
                Debug.Log($"Player {victimPlayerIndex + 1} fell out, but the last hit was {elapsedSinceLastHit}s ago. Counted as self-elimination.");
            }
        }

        if (activeGameMode == GameMode.Team)
        {
            EvaluateTeamRoundStatus();
        }
        else
        {
            EvaluateVersusRoundStatus(victimPlayerIndex);
        }
    }

    private void EvaluateTeamRoundStatus()
    {
        int teamALiveCount = 0;
        int teamBLiveCount = 0;

        foreach (GameObject wrestler in activeWrestlersInRound)
        {
            if (wrestler != null)
            {
                int pIdx = wrestler.GetComponent<TopDownMovement>().playerIndex;
                if (playerTeamAssignments[pIdx] == 0) teamALiveCount++;
                else teamBLiveCount++;
            }
        }

        if (teamALiveCount == 1 && teamBLiveCount == 1)
        {
            Debug.Log("Clutch 1v1 matchup! Shrinking arena rings dynamically.");
            HandleRingDrop();
        }

        if (teamALiveCount == 0 || teamBLiveCount == 0)
        {
            isRoundActive = false;
            FreezeAllMovement();

            string visualAnnouncementText = "";

            if (teamALiveCount == 0 && teamBLiveCount == 0)
            {
                visualAnnouncementText = "MUTUAL TEAM ELIMINATION!\nROUND DRAW!";
            }
            else if (teamALiveCount == 0)
            {
                teamBScore++;
                visualAnnouncementText = $"BLUE TEAM WINS THE ROUND!\n\n<b>--- STANDINGS ---</b>\nRED TEAM: {teamAScore} WINS\nBLUE TEAM: {teamBScore} WINS";
            }
            else
            {
                teamAScore++;
                visualAnnouncementText = $"RED TEAM WINS THE ROUND!\n\n<b>--- STANDINGS ---</b>\nRED TEAM: {teamAScore} WINS\nBLUE TEAM: {teamBScore} WINS";
            }

            CheckTeamTournamentStandings(visualAnnouncementText);
        }
    }

    private void CheckTeamTournamentStandings(string baseBannerMessage)
    {
        DisplayRoundEndNotification(baseBannerMessage);

        if (teamAScore >= teamRoundsToWin)
        {
            StartCoroutine(DelayedBannerOverride("🏆 MATCH OVER 🏆\nRED TEAM HAS WON THE MATCH!", 3.5f));
            StartCoroutine(MatchEndReturnDelayRoutine(7f));
        }
        else if (teamBScore >= teamRoundsToWin)
        {
            StartCoroutine(DelayedBannerOverride("🏆 MATCH OVER 🏆\nBLUE TEAM HAS WON THE MATCH!", 3.5f));
            StartCoroutine(MatchEndReturnDelayRoutine(7f));
        }
        else
        {
            currentRound++;
            StartCoroutine(RoundTransitionDelayRoutine(5f));
        }
    }

    private void EvaluateVersusRoundStatus(int victimPlayerIndex)
    {
        if (isSuddenDeathActive)
        {
            isRoundActive = false;
            FreezeAllMovement();
            int winnerIdx = (victimPlayerIndex == suddenDeathPlayerA) ? suddenDeathPlayerB : suddenDeathPlayerA;
            playerScores[winnerIdx] += 5;

            string summaryText = $"{versusColorNames[winnerIdx]} WINS SUDDEN DEATH!\n\n" + BuildLeaderboardString();
            CheckVersusTournamentStandings(summaryText);
            return;
        }

        int remainingCount = activeWrestlersInRound.Count + 1;
        int placementPayout = 0;

        if (activePlayerCount == 4)
        {
            if (remainingCount == 3) placementPayout = 1;
            else if (remainingCount == 2) placementPayout = 3;
        }
        else if (activePlayerCount == 3 && remainingCount == 2) placementPayout = 3;
        else if (activePlayerCount == 2 && remainingCount == 2) placementPayout = 1;

        playerScores[victimPlayerIndex] += placementPayout;

        if (activeWrestlersInRound.Count == 1)
        {
            isRoundActive = false;
            FreezeAllMovement();

            GameObject survivorObj = activeWrestlersInRound[0];
            int survivorIdx = survivorObj.GetComponent<TopDownMovement>().playerIndex;
            playerScores[survivorIdx] += 5;

            RecordEngagementStates();

            string summaryText = $"{versusColorNames[survivorIdx]} SURVIVED THE ROUND! (+5 pts)\n\n" + BuildLeaderboardString();
            CheckVersusTournamentStandings(summaryText);
        }
        else if (activeWrestlersInRound.Count == 0 && eliminatedThisRound.Count >= 2)
        {
            isRoundActive = false;
            FreezeAllMovement();

            GameObject lastFell = eliminatedThisRound[eliminatedThisRound.Count - 1];
            GameObject secondLastFell = eliminatedThisRound[eliminatedThisRound.Count - 2];

            int pIdxA = lastFell.GetComponent<TopDownMovement>().playerIndex;
            int pIdxB = secondLastFell.GetComponent<TopDownMovement>().playerIndex;

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
            int liveCount = activeWrestlersInRound.Count;
            if (liveCount == 4)
            {
                Debug.Log("Time limit reached while all 4 players are alive! Shrinking ring.");
                HandleRingDrop();
            }

            currentTimer = roundDurationSeconds;
            return;
        }

        isRoundActive = false;
        FreezeAllMovement();
        HandleRingDrop();

        for (int i = 0; i < activePlayerCount; i++)
        {
            GameObject foundPlayer = activeWrestlersInRound.Find(p => p != null && p.GetComponent<TopDownMovement>().playerIndex == i);
            if (foundPlayer != null)
            {
                TopDownMovement tdm = foundPlayer.GetComponent<TopDownMovement>();
                if (tdm != null && !tdm.hasDealtDamageThisRound)
                {
                    playerScores[i] = Mathf.Max(0, playerScores[i] - 1);
                    playerPassedEngagementCheck[i] = false;
                }
                else playerPassedEngagementCheck[i] = true;
            }
        }

        string summaryText = "TIME OUT! NO ENGAGEMENT DETECTED! RING SHRUNK!\n\n" + BuildLeaderboardString();
        CheckVersusTournamentStandings(summaryText);
    }

    private void CheckVersusTournamentStandings(string completionBannerMessage)
    {
        DisplayRoundEndNotification(completionBannerMessage);

        int highestScoreIdx = 0;
        int highestScore = -1;
        bool matchWon = false;

        for (int i = 0; i < activePlayerCount; i++)
        {
            if (playerScores[i] > highestScore)
            {
                highestScore = playerScores[i];
                highestScoreIdx = i;
            }
            if (playerScores[i] >= VERSUS_WINNING_SCORE) matchWon = true;
        }

        if (matchWon)
        {
            StartCoroutine(DelayedBannerOverride($"🏆 MATCH OVER 🏆\n{versusColorNames[highestScoreIdx]} IS THE FIRST TO 15 PTS!", 3.5f));
            StartCoroutine(MatchEndReturnDelayRoutine(7f));
        }
        else
        {
            currentRound++;
            if (currentRound <= totalVersusRounds) StartCoroutine(RoundTransitionDelayRoutine(5f));
            else
            {
                StartCoroutine(DelayedBannerOverride($"🏆 MATCH OVER 🏆\n{versusColorNames[highestScoreIdx]} HIGHEST FINAL SCORE WIN!", 3.5f));
                StartCoroutine(MatchEndReturnDelayRoutine(7f));
            }
        }
    }

    private string BuildLeaderboardString()
    {
        string header = "<b>--- SCOREBOARD (RACE TO 15) ---</b>\n";
        List<int> sortedIndices = new List<int>();
        for (int i = 0; i < activePlayerCount; i++) sortedIndices.Add(i);
        sortedIndices.Sort((a, b) => playerScores[b].CompareTo(playerScores[a]));

        for (int placement = 0; placement < sortedIndices.Count; placement++)
        {
            int originalPlayerIdx = sortedIndices[placement];
            header += $"{(placement == 0 ? "👑 1st" : $"{placement + 1}th")}: {versusColorNames[originalPlayerIdx]} - {playerScores[originalPlayerIdx]} / 15 pts\n";
        }
        return header;
    }

    private void RecordEngagementStates()
    {
        foreach (var player in activeWrestlersInRound)
            if (player != null) playerPassedEngagementCheck[player.GetComponent<TopDownMovement>().playerIndex] = player.GetComponent<TopDownMovement>().hasDealtDamageThisRound;
        foreach (var player in eliminatedThisRound)
            if (player != null) playerPassedEngagementCheck[player.GetComponent<TopDownMovement>().playerIndex] = player.GetComponent<TopDownMovement>().hasDealtDamageThisRound;
    }

    public void TogglePauseGameSystem()
    {
        if (!gameplayUIPanel.activeSelf) return;
        isGamePaused = !isGamePaused;
        if (pauseMenuPanel != null) pauseMenuPanel.SetActive(isGamePaused);
        if (pauseButtonManager != null) pauseButtonManager.SetPauseButtonGameplayState(false);
        Time.timeScale = isGamePaused ? 0f : 1f;
    }

    public void ReturnToHomeScreenHub()
    {
        isGamePaused = false;
        isRoundActive = false;
        Time.timeScale = 1f;

        if (pauseMenuPanel != null) pauseMenuPanel.SetActive(false);
        if (modeSelectPanel != null) modeSelectPanel.SetActive(true);
        if (pauseButtonManager != null) pauseButtonManager.SetPauseButtonGameplayState(true);
        characterSelectPanel.SetActive(false);
        gameplayUIPanel.SetActive(false);
        ClearMatchData();
    }

    private void HandleRingDrop()
    {
        if (currentActiveRingIndex < arenaRings.Count - 1)
        {
            if (arenaRings[currentActiveRingIndex] != null)
            {
                arenaRings[currentActiveRingIndex].isCurrentOutboundLimit = false;
                arenaRings[currentActiveRingIndex].gameObject.SetActive(false);
            }
            currentActiveRingIndex++;
            if (arenaRings[currentActiveRingIndex] != null)
            {
                arenaRings[currentActiveRingIndex].gameObject.SetActive(true);
                arenaRings[currentActiveRingIndex].isCurrentOutboundLimit = true;
            }
        }
    }

    private void ResetRingsToDefault()
    {
        currentActiveRingIndex = 0;
        for (int i = 0; i < arenaRings.Count; i++)
        {
            if (arenaRings[i] != null)
            {
                arenaRings[i].gameObject.SetActive(i == 0);
                arenaRings[i].isCurrentOutboundLimit = (i == 0);
            }
        }
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
        if (roundWinnerAnnounceText != null)
        {
            if (string.IsNullOrEmpty(displayMessage))
            {
                roundWinnerAnnounceText.gameObject.SetActive(false);
                if (timerText != null) timerText.gameObject.SetActive(true);

                // --- LIVE RUNNING GAMEPLAY: Enable Pause Button ---
                if (pauseButtonManager != null) pauseButtonManager.SetPauseButtonGameplayState(true);

                for (int i = 0; i < spawnedControlUIs.Length; i++)
                {
                    if (spawnedControlUIs[i] != null) spawnedControlUIs[i].SetActive(true);
                }
            }
            else
            {
                roundWinnerAnnounceText.gameObject.SetActive(true);
                roundWinnerAnnounceText.text = displayMessage;
                if (timerText != null) timerText.gameObject.SetActive(false);

                // --- IN MENUS / ANNOUNCEMENT INTERMISSIONS: Disable Pause Button ---
                if (pauseButtonManager != null) pauseButtonManager.SetPauseButtonGameplayState(false);

                for (int i = 0; i < spawnedControlUIs.Length; i++)
                {
                    if (spawnedControlUIs[i] != null) spawnedControlUIs[i].SetActive(false);
                }
            }
        }
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
    }

    private IEnumerator DelayedBannerOverride(string text, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (roundWinnerAnnounceText != null) roundWinnerAnnounceText.text = text;
    }

    private IEnumerator RoundTransitionDelayRoutine(float delayDuration)
    {
        yield return new WaitForSeconds(delayDuration - 0.5f);
        if (roundWinnerAnnounceText != null)
        {
            roundWinnerAnnounceText.text = "";
            roundWinnerAnnounceText.gameObject.SetActive(false);
        }
        if (timerText != null) timerText.gameObject.SetActive(true);
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
        StopAllCoroutines();

        // 1. Clear characters and instantiated inputs from the previous loop iteration
        ClearMatchData();

        // 2. Wipe active player round scoreboard values back to zero
        for (int i = 0; i < playerScores.Length; i++)
        {
            playerScores[i] = 0;
        }

        // 3. Clear banner announcements and show the timer layer
        if (roundWinnerAnnounceText != null)
        {
            roundWinnerAnnounceText.text = "";
            roundWinnerAnnounceText.gameObject.SetActive(false);
        }
        if (timerText != null) timerText.gameObject.SetActive(true);

        // 4. Force state tracking flags back into operational status
        isMatchOngoing = true;

        // 5. Notify the Pause Button that gameplay is actively running so it stays visible
        if (pauseButtonManager != null) pauseButtonManager.SetPauseButtonGameplayState(true);

        StartNewRound();
    }

    private void ClearMatchData()
    {
        foreach (var player in activeWrestlersInRound) if (player != null) Destroy(player);
        foreach (var player in eliminatedThisRound) if (player != null) Destroy(player);
        for (int i = 0; i < spawnedControlUIs.Length; i++)
        {
            if (spawnedControlUIs[i] != null) Destroy(spawnedControlUIs[i]);
            spawnedControlUIs[i] = null;
        }
        activeWrestlersInRound.Clear();
        eliminatedThisRound.Clear();
    }
}