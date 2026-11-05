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

    [Header("Match Parameters")]
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
    private GameMode activeGameMode = GameMode.Versus;
    private int activePlayerCount = 2, currentRound = 1, currentActiveRingIndex, teamAScore, teamBScore;
    private float currentTimer;
    private bool isRoundActive, isGamePaused, isSuddenDeathActive;
    private int suddenDeathPlayerA = -1, suddenDeathPlayerB = -1;

    private readonly int[] playerScores = new int[4];
    private readonly int[] playerTeamAssignments = new int[4];
    private readonly bool[] playerPassedEngagementCheck = { true, true, true, true };
    private readonly List<GameObject> activeWrestlersInRound = new List<GameObject>();
    private readonly List<GameObject> eliminatedThisRound = new List<GameObject>();

    private static readonly Color teamARed = new Color(1f, 0.15f, 0.2f, 1f);
    private static readonly Color teamBBlue = new Color(0f, 0.5f, 1f, 1f);
    private static readonly Color[] versusPlayerColors = {
        new Color(1f, 0.15f, 0.35f, 1f), new Color(1f, 0.84f, 0f, 1f),
        new Color(0.2f, 1f, 0.2f, 1f), new Color(0f, 0.65f, 1f, 1f)
    };
    private static readonly string[] versusColorNames = { "RED", "GOLD", "GREEN", "BLUE" };

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

    // --- Home screen navigation (button hooks) ---
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
        activePlayerCount = 4;
        BeginMatch(false);
    }

    public void SelectPlayerCount(int count)
    {
        activeGameMode = GameMode.Versus;
        activePlayerCount = Mathf.Clamp(count, 2, 4);
        BeginMatch(false);
    }

    public void LaunchMatchLive(int totalPlayers, int[] cosmeticSkins)
    {
        activePlayerCount = totalPlayers;
        BeginMatch(true);
    }

    private void BeginMatch(bool pauseButtonVisible)
    {
        ShowOnlyThisPanel(gameplayUIPanel);
        SetPauseButton(pauseButtonVisible);
        ResetMatchDataFull();
        StartNewRound();
    }

    private void SetPauseButton(bool on) { if (pauseButtonManager != null) pauseButtonManager.SetPauseButtonGameplayState(on); }
    private void StopMusic() { if (globalAudioSource != null) globalAudioSource.Stop(); }

    public void ReturnToHomeScreenHub()
    {
        isGamePaused = false;
        isRoundActive = false;
        Time.timeScale = 1f;
        StopMusic();
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

    private static int IndexOf(GameObject wrestler) => wrestler.GetComponent<TopDownMovement>().playerIndex;

    private void SpawnPlayersAndUI(int count, float radius)
    {
        for (int i = 0; i < count; i++)
        {
            float angle = i * (360f / count) * Mathf.Deg2Rad;
            Vector2 pos = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;

            GameObject player = Instantiate(playerPrefab, pos, Quaternion.identity);
            player.name = $"Wrestler_{i + 1}";
            activeWrestlersInRound.Add(player);

            if (player.TryGetComponent<TopDownMovement>(out var movement))
            {
                movement.playerIndex = i;
                movement.currentKnockbackReceivedMultiplier = playerPassedEngagementCheck[i] ? 1.0f : 1.5f;
            }

            // Face the arena center.
            player.transform.rotation = Quaternion.AngleAxis(Mathf.Atan2(-pos.y, -pos.x) * Mathf.Rad2Deg - 90f, Vector3.forward);

            Color color = versusPlayerColors[i];
            string label = versusColorNames[i];
            if (activeGameMode == GameMode.Team)
            {
                playerTeamAssignments[i] = i % 2;
                color = i % 2 == 0 ? teamARed : teamBBlue;
                label = $"T{(i % 2 == 0 ? "A" : "B")}-P{i + 1}";
            }

            SpriteRenderer sr = player.GetComponentInChildren<SpriteRenderer>();
            if (sr != null) sr.color = color;

            GameObject textObj = new GameObject("PlayerNumberText");
            textObj.transform.SetParent(player.transform);
            textObj.transform.localPosition = new Vector3(0f, 1.2f, 0f);
            textObj.transform.rotation = Quaternion.identity;

            TextMeshPro tmp = textObj.AddComponent<TextMeshPro>();
            tmp.text = label;
            tmp.fontSize = 5f;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;
            tmp.fontStyle = FontStyles.Bold;
            tmp.outlineWidth = 0.2f;
            tmp.outlineColor = Color.black;
            textObj.AddComponent<LookAtCameraUpright>();

            SpawnControllerCanvas(player, i, color);
        }
    }

    private void SpawnControllerCanvas(GameObject playerObj, int idx, Color color)
    {
        if (idx >= uiCornerAnchors.Length || uiCornerAnchors[idx] == null) return;

        GameObject ui = Instantiate(mobileControlsUIPrefab, uiCornerAnchors[idx]);
        spawnedControlUIs[idx] = ui;

        // Rotate controls so they face outward toward their screen corner.
        Vector3 corner = uiCornerAnchors[idx].localPosition;
        ui.transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(corner.y, corner.x) * Mathf.Rad2Deg + 90f);

        foreach (var img in ui.GetComponentsInChildren<Image>())
            if (img.gameObject != ui) img.color = color * 0.85f;

        MobileControls input = ui.GetComponent<MobileControls>();
        TopDownMovement movement = playerObj.GetComponent<TopDownMovement>();
        if (movement != null && input != null) movement.controls = input;
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

        // KO credit: 1/2/3 pts by consecutive hits, if the last hit is recent enough.
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

        string msg;
        if (aLive == 0 && bLive == 0) msg = "MUTUAL TEAM ELIMINATION!\nROUND DRAW!";
        else
        {
            string winner = aLive == 0 ? "BLUE" : "RED";
            if (aLive == 0) teamBScore++; else teamAScore++;
            msg = $"{winner} TEAM WINS THE ROUND!\n\n<b>--- STANDINGS ---</b>\nRED TEAM: {teamAScore} WINS\nBLUE TEAM: {teamBScore} WINS";
        }

        DisplayRoundEndNotification(msg);
        if (teamAScore >= teamRoundsToWin) EndMatch("RED TEAM HAS WON THE MATCH!");
        else if (teamBScore >= teamRoundsToWin) EndMatch("BLUE TEAM HAS WON THE MATCH!");
        else NextRound();
    }

    private void EvaluateVersusRoundStatus(int victimIdx)
    {
        if (isSuddenDeathActive)
        {
            isRoundActive = false;
            FreezeAllMovement();
            int winner = victimIdx == suddenDeathPlayerA ? suddenDeathPlayerB : suddenDeathPlayerA;
            playerScores[winner] += 5;
            CheckVersusTournamentStandings($"{versusColorNames[winner]} WINS SUDDEN DEATH!\n\n" + BuildLeaderboardString());
            return;
        }

        // Placement points for the player who just fell, by how many were left before the fall.
        int remaining = activeWrestlersInRound.Count + 1;
        if (activePlayerCount == 4 && remaining == 3) playerScores[victimIdx] += 1;
        else if ((activePlayerCount == 3 || activePlayerCount == 4) && remaining == 2) playerScores[victimIdx] += 3;
        else if (activePlayerCount == 2 && remaining == 2) playerScores[victimIdx] += 1;

        if (activeWrestlersInRound.Count == 1)
        {
            isRoundActive = false;
            FreezeAllMovement();
            int survivor = IndexOf(activeWrestlersInRound[0]);
            playerScores[survivor] += 5;
            RecordEngagementStates();
            CheckVersusTournamentStandings($"{versusColorNames[survivor]} SURVIVED THE ROUND! (+5 pts)\n\n" + BuildLeaderboardString());
        }
        else if (activeWrestlersInRound.Count == 0 && eliminatedThisRound.Count >= 2)
        {
            isRoundActive = false;
            FreezeAllMovement();
            int a = IndexOf(eliminatedThisRound[eliminatedThisRound.Count - 1]);
            int b = IndexOf(eliminatedThisRound[eliminatedThisRound.Count - 2]);
            playerScores[a] += 3;
            playerScores[b] += 3;
            RecordEngagementStates();
            StartCoroutine(TriggerSuddenDeathTransitionSequence(a, b));
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

        // Players who never landed a hit lose a point and take extra knockback next round.
        for (int i = 0; i < activePlayerCount; i++)
        {
            GameObject p = activeWrestlersInRound.Find(w => w != null && IndexOf(w) == i);
            if (p == null) continue;
            bool engaged = p.GetComponent<TopDownMovement>().hasDealtDamageThisRound;
            if (!engaged) playerScores[i] = Mathf.Max(0, playerScores[i] - 1);
            playerPassedEngagementCheck[i] = engaged;
        }

        CheckVersusTournamentStandings("TIME OUT! NO ENGAGEMENT DETECTED! RING SHRUNK!\n\n" + BuildLeaderboardString());
    }

    private void CheckVersusTournamentStandings(string message)
    {
        DisplayRoundEndNotification(message);

        int best = 0, bestScore = -1;
        bool matchWon = false;
        for (int i = 0; i < activePlayerCount; i++)
        {
            if (playerScores[i] > bestScore) { bestScore = playerScores[i]; best = i; }
            if (playerScores[i] >= VERSUS_WINNING_SCORE) matchWon = true;
        }

        if (matchWon) EndMatch($"{versusColorNames[best]} IS THE FIRST TO 15 PTS!");
        else if (++currentRound <= totalVersusRounds) StartCoroutine(RoundTransitionDelayRoutine(5f));
        else EndMatch($"{versusColorNames[best]} HIGHEST FINAL SCORE WIN!");
    }

    private void NextRound()
    {
        currentRound++;
        StartCoroutine(RoundTransitionDelayRoutine(5f));
    }

    private void EndMatch(string winnerLine)
    {
        StopMusic();
        StartCoroutine(DelayedBannerOverride("🏆 MATCH OVER 🏆\n" + winnerLine, 3.5f));
        StartCoroutine(MatchEndReturnDelayRoutine(7f));
    }

    private string BuildLeaderboardString()
    {
        var order = new List<int>();
        for (int i = 0; i < activePlayerCount; i++) order.Add(i);
        order.Sort((a, b) => playerScores[b].CompareTo(playerScores[a]));

        string s = "<b>--- SCOREBOARD (RACE TO 15) ---</b>\n";
        for (int place = 0; place < order.Count; place++)
            s += $"{(place == 0 ? "👑 1st" : $"{place + 1}th")}: {versusColorNames[order[place]]} - {playerScores[order[place]]} / 15 pts\n";
        return s;
    }

    private void RecordEngagementStates()
    {
        foreach (var list in new[] { activeWrestlersInRound, eliminatedThisRound })
            foreach (var p in list)
                if (p != null)
                {
                    var m = p.GetComponent<TopDownMovement>();
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

    private void SetRing(int i, bool on)
    {
        if (arenaRings[i] == null) return;
        arenaRings[i].isCurrentOutboundLimit = on;
        arenaRings[i].gameObject.SetActive(on);
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
        foreach (var p in activeWrestlersInRound)
            if (p != null && p.TryGetComponent<Rigidbody2D>(out var rb))
            {
                rb.linearVelocity = Vector2.zero;
                rb.angularVelocity = 0f;
            }
    }

    // Empty message hides the banner and shows timer + controls; otherwise the reverse.
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

    private IEnumerator DelayedBannerOverride(string text, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (roundWinnerAnnounceText != null) roundWinnerAnnounceText.text = text;
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

    private IEnumerator RoundTransitionDelayRoutine(float delay)
    {
        yield return new WaitForSeconds(delay - 0.5f);
        HideBanner();
        yield return new WaitForSeconds(0.5f);
        StartNewRound();
    }

    private IEnumerator MatchEndReturnDelayRoutine(float delay)
    {
        yield return new WaitForSeconds(delay);
        ReturnToHomeScreenHub();
    }

    public void RestartCurrentMatchSetup()
    {
        StopAllCoroutines();
        StopMusic();
        ClearMatchData();
        for (int i = 0; i < playerScores.Length; i++) playerScores[i] = 0;
        HideBanner();
        StartNewRound();
    }

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
