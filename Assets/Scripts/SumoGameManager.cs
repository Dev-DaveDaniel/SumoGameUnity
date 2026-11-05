using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

public enum GameMode { Versus, Team }

public class SumoGameManager : MonoBehaviour
{
    public static SumoGameManager Instance { get; private set; }

    [Header("UI Panels")]
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
    [SerializeField] private TextMeshProUGUI roundWinnerAnnounceText;

    [Header("Prefabs")]
    [SerializeField] private GameObject playerPrefab;
    [SerializeField] private GameObject mobileControlsUIPrefab;

    [Header("Match")]
    public float roundDurationSeconds = 15f;
    public int totalVersusRounds = 5;
    public int teamRoundsToWin = 3;
    [SerializeField] private float spawnRadiusFromCenter = 3.5f;
    [SerializeField] private RectTransform[] uiCornerAnchors = new RectTransform[4];
    [SerializeField] private List<SumoRingZone> arenaRings = new List<SumoRingZone>();

    [Header("Audio")]
    [SerializeField] private AudioSource globalAudioSource;
    [SerializeField] private AudioClip roundStartClip;

    public GameObject[] spawnedControlUIs = new GameObject[4];

    private const int VERSUS_WINNING_SCORE = 15;
    private static readonly Color teamARed = new Color(1f, 0.15f, 0.2f, 1f);
    private static readonly Color teamBBlue = new Color(0f, 0.5f, 1f, 1f);
    private static readonly Color[] versusPlayerColors = {
        new Color(1f, 0.15f, 0.35f, 1f), new Color(1f, 0.84f, 0f, 1f),
        new Color(0.2f, 1f, 0.2f, 1f), new Color(0f, 0.65f, 1f, 1f)
    };
    private static readonly string[] versusColorNames = { "RED", "GOLD", "GREEN", "BLUE" };

    private GameMode activeGameMode = GameMode.Versus;
    private int activePlayerCount = 2, currentRound = 1, currentActiveRingIndex;
    private float currentTimer;
    private bool isRoundActive, isGamePaused, isSuddenDeathActive;
    private int suddenDeathPlayerA = -1, suddenDeathPlayerB = -1;
    private int teamAScore, teamBScore;
    private readonly int[] playerScores = new int[4];
    private readonly int[] playerTeamAssignments = new int[4];
    private readonly bool[] playerPassedEngagementCheck = { true, true, true, true };
    private readonly List<GameObject> activeWrestlersInRound = new List<GameObject>();
    private readonly List<GameObject> eliminatedThisRound = new List<GameObject>();

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void Start()
    {
        ShowOnlyThisPanel(titleScreenPanel);
        if (pauseMenuPanel != null) pauseMenuPanel.SetActive(false);
        if (roundWinnerAnnounceText != null) roundWinnerAnnounceText.gameObject.SetActive(false);
    }

    private void ShowOnlyThisPanel(GameObject target)
    {
        foreach (var p in new[] { titleScreenPanel, howToPlayPanel, controlsImagePanel, rulesTextPanel, modeSelectPanel, characterSelectPanel, gameplayUIPanel })
            if (p != null) p.SetActive(p == target);
    }

    // --- Home screen navigation (wired to UI buttons) ---
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

    public void SelectTeamModeGame() => BeginMatch(GameMode.Team, 4);
    public void SelectPlayerCount(int count) => BeginMatch(GameMode.Versus, Mathf.Clamp(count, 2, 4));
    public void LaunchMatchLive(int totalPlayers, int[] cosmeticSkins) => BeginMatch(activeGameMode, totalPlayers);

    private void BeginMatch(GameMode mode, int playerCount)
    {
        activeGameMode = mode;
        activePlayerCount = playerCount;
        ShowOnlyThisPanel(gameplayUIPanel);
        SetPauseButton(false);
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
        SetPauseButton(true);
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

    private void SpawnPlayersAndUI(int count, float radius)
    {
        for (int i = 0; i < count; i++)
        {
            float angle = i * (360f / count) * Mathf.Deg2Rad;
            Vector2 pos = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;

            GameObject player = Instantiate(playerPrefab, pos, Quaternion.identity);
            player.name = $"Wrestler_{i + 1}";
            activeWrestlersInRound.Add(player);

            TopDownMovement movement = player.GetComponent<TopDownMovement>();
            if (movement != null)
            {
                movement.playerIndex = i;
                movement.currentKnockbackReceivedMultiplier = playerPassedEngagementCheck[i] ? 1.0f : 1.5f;
            }

            // Face the arena centre
            float angleToCenter = Mathf.Atan2(-pos.y, -pos.x) * Mathf.Rad2Deg;
            player.transform.rotation = Quaternion.AngleAxis(angleToCenter - 90f, Vector3.forward);

            Color color = versusPlayerColors[i];
            if (activeGameMode == GameMode.Team)
            {
                playerTeamAssignments[i] = i % 2;
                color = playerTeamAssignments[i] == 0 ? teamARed : teamBBlue;
            }

            SpriteRenderer sr = player.GetComponentInChildren<SpriteRenderer>();
            if (sr != null) sr.color = color;

            GameObject textObj = new GameObject("PlayerNumberText");
            textObj.transform.SetParent(player.transform);
            textObj.transform.localPosition = new Vector3(0f, 1.2f, 0f);
            textObj.transform.rotation = Quaternion.identity;

            TextMeshPro label = textObj.AddComponent<TextMeshPro>();
            label.text = activeGameMode == GameMode.Team ? $"T{(playerTeamAssignments[i] == 0 ? "A" : "B")}-P{i + 1}" : versusColorNames[i];
            label.fontSize = 5f;
            label.alignment = TextAlignmentOptions.Center;
            label.color = Color.white;
            label.fontStyle = FontStyles.Bold;
            label.outlineWidth = 0.2f;
            label.outlineColor = Color.black;
            textObj.AddComponent<LookAtCameraUpright>();

            SpawnControllerCanvas(player, i, color);
        }
    }

    private void SpawnControllerCanvas(GameObject playerObj, int playerIdx, Color identityColor)
    {
        if (playerIdx >= uiCornerAnchors.Length || uiCornerAnchors[playerIdx] == null) return;

        GameObject uiControl = Instantiate(mobileControlsUIPrefab, uiCornerAnchors[playerIdx]);
        spawnedControlUIs[playerIdx] = uiControl;

        // Rotate the controls to face their screen corner
        Vector3 toCorner = uiCornerAnchors[playerIdx].localPosition;
        uiControl.transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(toCorner.y, toCorner.x) * Mathf.Rad2Deg + 90f);

        foreach (var img in uiControl.GetComponentsInChildren<Image>())
            if (img.gameObject != uiControl) img.color = identityColor * 0.85f;

        MobileControls inputBridge = uiControl.GetComponent<MobileControls>();
        TopDownMovement movement = playerObj.GetComponent<TopDownMovement>();
        if (movement != null && inputBridge != null) movement.controls = inputBridge;
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

        // Kill credit: 1-3 pts depending on the attacker's hit streak
        if (victim != null && victim.lastAttackerIndex != -1 && Time.time - victim.lastInteractionTimestamp <= victim.trackingWindowDuration)
            playerScores[victim.lastAttackerIndex] += Mathf.Clamp(victim.consecutiveHitCount, 1, 3);

        if (activeGameMode == GameMode.Team) EvaluateTeamRoundStatus();
        else EvaluateVersusRoundStatus(victimIdx);
    }

    private void EvaluateTeamRoundStatus()
    {
        int aLive = 0, bLive = 0;
        foreach (GameObject w in activeWrestlersInRound)
        {
            if (w == null) continue;
            if (playerTeamAssignments[IndexOf(w)] == 0) aLive++;
            else bLive++;
        }

        if (aLive == 1 && bLive == 1) HandleRingDrop();
        if (aLive != 0 && bLive != 0) return;

        isRoundActive = false;
        FreezeAllMovement();

        string message;
        if (aLive == 0 && bLive == 0) message = "MUTUAL TEAM ELIMINATION!\nROUND DRAW!";
        else
        {
            if (aLive == 0) teamBScore++;
            else teamAScore++;
            message = $"{(aLive == 0 ? "BLUE" : "RED")} TEAM WINS THE ROUND!\n\n<b>--- STANDINGS ---</b>\nRED TEAM: {teamAScore} WINS\nBLUE TEAM: {teamBScore} WINS";
        }

        DisplayRoundEndNotification(message);
        if (teamAScore >= teamRoundsToWin) EndMatch("RED TEAM HAS WON THE MATCH!");
        else if (teamBScore >= teamRoundsToWin) EndMatch("BLUE TEAM HAS WON THE MATCH!");
        else
        {
            currentRound++;
            StartCoroutine(RoundTransitionDelayRoutine(5f));
        }
    }

    private void EvaluateVersusRoundStatus(int victimIdx)
    {
        if (isSuddenDeathActive)
        {
            isRoundActive = false;
            FreezeAllMovement();
            int winnerIdx = victimIdx == suddenDeathPlayerA ? suddenDeathPlayerB : suddenDeathPlayerA;
            playerScores[winnerIdx] += 5;
            CheckVersusTournamentStandings($"{versusColorNames[winnerIdx]} WINS SUDDEN DEATH!\n\n" + BuildLeaderboardString());
            return;
        }

        // Placement points for the player who just fell
        int remaining = activeWrestlersInRound.Count + 1;
        playerScores[victimIdx] += remaining == 2 ? (activePlayerCount == 2 ? 1 : 3)
                                 : (activePlayerCount == 4 && remaining == 3 ? 1 : 0);

        if (activeWrestlersInRound.Count == 1)
        {
            isRoundActive = false;
            FreezeAllMovement();
            int survivorIdx = IndexOf(activeWrestlersInRound[0]);
            playerScores[survivorIdx] += 5;
            RecordEngagementStates();
            CheckVersusTournamentStandings($"{versusColorNames[survivorIdx]} SURVIVED THE ROUND! (+5 pts)\n\n" + BuildLeaderboardString());
        }
        else if (activeWrestlersInRound.Count == 0 && eliminatedThisRound.Count >= 2)
        {
            isRoundActive = false;
            FreezeAllMovement();
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

        isRoundActive = false;
        FreezeAllMovement();
        HandleRingDrop();

        // Players who never landed a hit lose a point and take extra knockback next round
        foreach (var p in activeWrestlersInRound)
        {
            if (p == null) continue;
            TopDownMovement m = p.GetComponent<TopDownMovement>();
            playerPassedEngagementCheck[m.playerIndex] = m.hasDealtDamageThisRound;
            if (!m.hasDealtDamageThisRound) playerScores[m.playerIndex] = Mathf.Max(0, playerScores[m.playerIndex] - 1);
        }

        CheckVersusTournamentStandings("TIME OUT! NO ENGAGEMENT DETECTED! RING SHRUNK!\n\n" + BuildLeaderboardString());
    }

    private void CheckVersusTournamentStandings(string message)
    {
        DisplayRoundEndNotification(message);

        int leader = 0, highest = -1;
        bool matchWon = false;
        for (int i = 0; i < activePlayerCount; i++)
        {
            if (playerScores[i] > highest) { highest = playerScores[i]; leader = i; }
            if (playerScores[i] >= VERSUS_WINNING_SCORE) matchWon = true;
        }

        if (matchWon) EndMatch($"{versusColorNames[leader]} IS THE FIRST TO 15 PTS!");
        else if (++currentRound <= totalVersusRounds) StartCoroutine(RoundTransitionDelayRoutine(5f));
        else EndMatch($"{versusColorNames[leader]} HIGHEST FINAL SCORE WIN!");
    }

    private void EndMatch(string winnerLine)
    {
        if (globalAudioSource != null) globalAudioSource.Stop();
        StartCoroutine(MatchEndRoutine("🏆 MATCH OVER 🏆\n" + winnerLine));
    }

    private string BuildLeaderboardString()
    {
        string board = "<b>--- SCOREBOARD (RACE TO 15) ---</b>\n";
        List<int> order = new List<int>();
        for (int i = 0; i < activePlayerCount; i++) order.Add(i);
        order.Sort((a, b) => playerScores[b].CompareTo(playerScores[a]));

        for (int place = 0; place < order.Count; place++)
            board += $"{(place == 0 ? "👑 1st" : $"{place + 1}th")}: {versusColorNames[order[place]]} - {playerScores[order[place]]} / 15 pts\n";
        return board;
    }

    private void RecordEngagementStates()
    {
        foreach (var list in new[] { activeWrestlersInRound, eliminatedThisRound })
            foreach (var p in list)
                if (p != null)
                {
                    TopDownMovement m = p.GetComponent<TopDownMovement>();
                    playerPassedEngagementCheck[m.playerIndex] = m.hasDealtDamageThisRound;
                }
    }

    public void TogglePauseGameSystem()
    {
        if (!gameplayUIPanel.activeSelf) return;
        isGamePaused = !isGamePaused;
        if (pauseMenuPanel != null) pauseMenuPanel.SetActive(isGamePaused);
        Time.timeScale = isGamePaused ? 0f : 1f;
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

    // Clear the limit flag before toggling: disabling a ring fires OnTriggerExit2D, which would eliminate everyone inside.
    private void SetRing(int i, bool on)
    {
        SumoRingZone ring = arenaRings[i];
        if (ring == null) return;
        ring.isCurrentOutboundLimit = false;
        ring.gameObject.SetActive(on);
        ring.isCurrentOutboundLimit = on;
    }

    private void FreezeAllMovement()
    {
        foreach (var p in activeWrestlersInRound)
        {
            if (p == null || !p.TryGetComponent<Rigidbody2D>(out var rb)) continue;
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
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

    private IEnumerator MatchEndRoutine(string banner)
    {
        yield return new WaitForSeconds(3.5f);
        if (roundWinnerAnnounceText != null) roundWinnerAnnounceText.text = banner;
        yield return new WaitForSeconds(3.5f);
        ReturnToHomeScreenHub();
    }

    private IEnumerator RoundTransitionDelayRoutine(float delay)
    {
        yield return new WaitForSeconds(delay - 0.5f);
        HideBanner();
        yield return new WaitForSeconds(0.5f);
        StartNewRound();
    }

    public void RestartCurrentMatchSetup()
    {
        StopAllCoroutines();
        if (globalAudioSource != null) globalAudioSource.Stop();
        ClearMatchData();
        for (int i = 0; i < playerScores.Length; i++) playerScores[i] = 0;
        HideBanner();
        StartNewRound();
    }

    private void HideBanner()
    {
        if (roundWinnerAnnounceText != null)
        {
            roundWinnerAnnounceText.text = "";
            roundWinnerAnnounceText.gameObject.SetActive(false);
        }
        if (timerText != null) timerText.gameObject.SetActive(true);
    }

    private void SetPauseButton(bool on)
    {
        if (pauseButtonManager != null) pauseButtonManager.SetPauseButtonGameplayState(on);
    }

    private static int IndexOf(GameObject wrestler) => wrestler.GetComponent<TopDownMovement>().playerIndex;

    private void ClearMatchData()
    {
        foreach (var p in activeWrestlersInRound) if (p != null) Destroy(p);
        foreach (var p in eliminatedThisRound) if (p != null) Destroy(p);
        for (int i = 0; i < spawnedControlUIs.Length; i++)
        {
            if (spawnedControlUIs[i] != null) Destroy(spawnedControlUIs[i]);
            spawnedControlUIs[i] = null;
        }
        activeWrestlersInRound.Clear();
        eliminatedThisRound.Clear();
    }
}
