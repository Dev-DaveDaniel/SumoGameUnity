using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class SumoGameManager : MonoBehaviour
{
    public static SumoGameManager Instance { get; private set; }

    [Header("UI Panels & Overlays")]
    [SerializeField] private GameObject characterSelectPanel;
    [SerializeField] private GameObject gameplayUIPanel;
    [SerializeField] private GameObject pauseMenuPanel;
    [SerializeField] private TextMeshProUGUI timerText;

    [Header("Round Announcement Settings")]
    [SerializeField] private TextMeshProUGUI roundWinnerAnnounceText;

    [Header("Prefabs to Spawn")]
    [SerializeField] private GameObject playerPrefab;
    [SerializeField] private GameObject mobileControlsUIPrefab;

    [Header("Match Parameters")]
    public float roundDurationSeconds = 15f;
    [Tooltip("Set this to 5 for the primary game mode.")]
    public int totalRounds = 5;
    [SerializeField] private float spawnRadiusFromCenter = 3.5f;

    [Header("UI Spawn Anchors")]
    [Tooltip("0=BL, 1=TR, 2=TL, 3=BR")]
    [SerializeField] private RectTransform[] uiCornerAnchors = new RectTransform[4];

    [Header("Sumo Arena Rings (Ordered from Largest to Smallest)")]
    [SerializeField] private List<SumoRingZone> arenaRings = new List<SumoRingZone>();
    private int currentActiveRingIndex = 0;

    private int activePlayerCount = 2;
    private int currentRound = 1;
    private float currentTimer;
    private bool isRoundActive = false;
    private bool isGamePaused = false;

    // OVERHAULED: Leaderboard Points System
    private int[] playerScores = new int[4];
    private const int WINNING_SCORE_THRESHOLD = 15; // First to 15 Wins!

    // Track engagement penalties across round transitions
    private bool[] playerPassedEngagementCheck = new bool[] { true, true, true, true };

    // Real-Time Round Tracking Lists
    private List<GameObject> activeWrestlersInRound = new List<GameObject>();
    private List<GameObject> eliminatedThisRound = new List<GameObject>();
    private GameObject[] spawnedControlUIs = new GameObject[4];

    // Sudden Death Variables
    private bool isSuddenDeathActive = false;
    private int suddenDeathPlayerA = -1;
    private int suddenDeathPlayerB = -1;

    private Color[] playerColors = new Color[] {
        new Color(1f, 0.15f, 0.35f, 1f),   // Player 1: Vivid Red
        new Color(1f, 0.84f, 0f, 1f),      // Player 2: Cyber Gold
        new Color(0.2f, 1f, 0.2f, 1f),     // Player 3: Neon Green
        new Color(0f, 0.65f, 1f, 1f)       // Player 4: Electric Blue
    };

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void Start()
    {
        characterSelectPanel.SetActive(true);
        gameplayUIPanel.SetActive(false);
        if (pauseMenuPanel != null) pauseMenuPanel.SetActive(false);
        if (roundWinnerAnnounceText != null) roundWinnerAnnounceText.gameObject.SetActive(false);
    }

    public void SelectPlayerCount(int count)
    {
        activePlayerCount = Mathf.Clamp(count, 2, 4);
        characterSelectPanel.SetActive(false);
        gameplayUIPanel.SetActive(true);
        if (pauseMenuPanel != null) pauseMenuPanel.SetActive(false);

        currentRound = 1;
        isSuddenDeathActive = false;
        isGamePaused = false;
        Time.timeScale = 1f;

        // Reset scores and engagement arrays
        for (int i = 0; i < 4; i++)
        {
            playerScores[i] = 0;
            playerPassedEngagementCheck[i] = true;
        }

        ResetRingsToDefault();
        StartNewRound();
    }

    private void StartNewRound()
    {
        ClearMatchData();

        // FORCE HIDE AND WIPE THE ANNOUNCEMENT TEXT IMMEDIATELY
        if (roundWinnerAnnounceText != null)
        {
            roundWinnerAnnounceText.text = "";
            roundWinnerAnnounceText.gameObject.SetActive(false);
        }

        isSuddenDeathActive = false;
        if (currentRound == 1) ResetRingsToDefault();

        SpawnPlayersAndUI(activePlayerCount, spawnRadiusFromCenter);

        currentTimer = roundDurationSeconds;
        isRoundActive = true;
        isGamePaused = false;
        Time.timeScale = 1f;
    }

    private void StartSuddenDeathRound(int playerA_Idx, int playerB_Idx)
    {
        ClearMatchData();
        isSuddenDeathActive = true;

        for (int i = 0; i < arenaRings.Count; i++)
        {
            if (arenaRings[i] != null)
            {
                bool isCenter = (i == arenaRings.Count - 1);
                arenaRings[i].gameObject.SetActive(isCenter);
                arenaRings[i].isCurrentOutboundLimit = isCenter;
            }
        }
        currentActiveRingIndex = arenaRings.Count - 1;

        suddenDeathPlayerA = playerA_Idx;
        suddenDeathPlayerB = playerB_Idx;

        SpawnSuddenDeathContestant(playerA_Idx, 0, -1.0f);
        SpawnSuddenDeathContestant(playerB_Idx, 1, 1.0f);

        currentTimer = roundDurationSeconds;
        isRoundActive = true;
        Time.timeScale = 1f;
    }

    private void Update()
    {
        if (!isRoundActive || isGamePaused) return;

        currentTimer -= Time.deltaTime;
        if (timerText != null) timerText.text = Mathf.CeilToInt(currentTimer).ToString();

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

                // If they hid or didn't land a push last round, scale up incoming knockback danger by 50%!
                if (!playerPassedEngagementCheck[i])
                {
                    movement.currentKnockbackReceivedMultiplier = 1.5f;
                    Debug.Log($"<color=red>[DANGER MODIFIER]</color> Player {i + 1} receives 1.5x Knockback this round for being passive!");
                }
                else
                {
                    movement.currentKnockbackReceivedMultiplier = 1.0f;
                }
            }

            Vector3 directionToCenter = Vector3.zero - newPlayer.transform.position;
            float angleToCenter = Mathf.Atan2(directionToCenter.y, directionToCenter.x) * Mathf.Rad2Deg;
            newPlayer.transform.rotation = Quaternion.AngleAxis(angleToCenter - 90f, Vector3.forward);

            SpriteRenderer sr = newPlayer.GetComponentInChildren<SpriteRenderer>();
            if (sr != null) sr.color = playerColors[i];

            // Overhead UI Text Configuration
            GameObject textObj = new GameObject("PlayerNumberText");
            textObj.transform.SetParent(newPlayer.transform);
            textObj.transform.localPosition = new Vector3(0f, 1.2f, 0f);
            textObj.transform.rotation = Quaternion.identity;

            TextMeshPro tmproText = textObj.AddComponent<TextMeshPro>();
            tmproText.text = $"P{i + 1}";
            tmproText.fontSize = 5f;
            tmproText.alignment = TextAlignmentOptions.Center;
            tmproText.color = Color.white;
            tmproText.fontStyle = FontStyles.Bold;
            tmproText.outlineWidth = 0.2f;
            tmproText.outlineColor = Color.black;

            // Fix text rotation flipping out
            textObj.AddComponent<LookAtCameraUpright>();

            SpawnControllerCanvas(newPlayer, i);
        }
    }

    private void SpawnSuddenDeathContestant(int originalPlayerIndex, int layoutSlot, float offsetPosX)
    {
        Vector2 spawnPosition = new Vector2(offsetPosX, 0f);
        GameObject newPlayer = Instantiate(playerPrefab, spawnPosition, Quaternion.identity);
        newPlayer.name = $"Wrestler_{originalPlayerIndex + 1}";
        activeWrestlersInRound.Add(newPlayer);

        TopDownMovement movement = newPlayer.GetComponent<TopDownMovement>();
        if (movement != null) movement.playerIndex = originalPlayerIndex;

        newPlayer.transform.rotation = Quaternion.Euler(0f, 0f, layoutSlot == 0 ? -90f : 90f);

        SpriteRenderer sr = newPlayer.GetComponentInChildren<SpriteRenderer>();
        if (sr != null) sr.color = playerColors[originalPlayerIndex];

        SpawnControllerCanvas(newPlayer, originalPlayerIndex);
    }

    private void SpawnControllerCanvas(GameObject playerObj, int playerIdx)
    {
        if (playerIdx < uiCornerAnchors.Length && uiCornerAnchors[playerIdx] != null)
        {
            GameObject uiControl = Instantiate(mobileControlsUIPrefab, uiCornerAnchors[playerIdx]);

            // Store it securely by its explicit player index slot
            spawnedControlUIs[playerIdx] = uiControl;

            Image[] buttonBackgrounds = uiControl.GetComponentsInChildren<Image>();
            foreach (var img in buttonBackgrounds)
            {
                if (img.gameObject != uiControl) img.color = playerColors[playerIdx] * 0.85f;
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

        // INSTANTLY DESTROY THE ELIMINATED PLAYER'S UI CONTROLS
        if (spawnedControlUIs[victimPlayerIndex] != null)
        {
            Destroy(spawnedControlUIs[victimPlayerIndex]);
            spawnedControlUIs[victimPlayerIndex] = null;
            Debug.Log($"<color=red>[UI CLEANUP]</color> Disabled screen controls for Player {victimPlayerIndex + 1}");
        }

        if (JuiceManager.Instance != null)
        {
            JuiceManager.Instance.TriggerImpactJuice(0.12f, 0.25f, 0.45f, 0.4f);
        }

        activeWrestlersInRound.Remove(victimObj);
        eliminatedThisRound.Add(victimObj);

        // --- ENFORCING ACTIVE POWER BONUS CREDIT SYSTEM ---
        if (victimMovement != null && victimMovement.lastAttackerIndex != -1)
        {
            int killerIdx = victimMovement.lastAttackerIndex;

            // Standard KO = +1 Point
            int koPayout = 1;

            // Combo KO Scaling calculations: 2 consecutive hits = +2, 3+ hits = +3 points!
            if (victimMovement.consecutiveHitCount == 2) koPayout = 2;
            else if (victimMovement.consecutiveHitCount >= 3) koPayout = 3;

            playerScores[killerIdx] += koPayout;
            Debug.Log($"<color=green>[KO CREDIT]</color> Player {killerIdx + 1} scored a KO bonus (+{koPayout} pts) on Player {victimPlayerIndex + 1}!");
        }

        // Handle Sudden Death Outbound calculations
        if (isSuddenDeathActive)
        {
            isRoundActive = false;
            FreezeAllMovement();

            int winnerIdx = (victimPlayerIndex == suddenDeathPlayerA) ? suddenDeathPlayerB : suddenDeathPlayerA;
            playerScores[winnerIdx] += 5; // Survival payout

            string summaryText = $"PLAYER {winnerIdx + 1} WINS SUDDEN DEATH!\n\n" + BuildLeaderboardString();
            CheckTournamentStandings(summaryText);
            return;
        }

        // --- ROUND PLACEMENT POINT DISTRIBUTION ENGINE ---
        // 4th out = 0 pts, 3rd out = 1 pt, 2nd out = 3 pts, 1st remaining survivor = 5 pts
        int remainingCount = activeWrestlersInRound.Count + 1; // Placement position tracking calculation

        int placementPayout = 0;
        if (activePlayerCount == 4)
        {
            if (remainingCount == 4) placementPayout = 0; // 4th place
            else if (remainingCount == 3) placementPayout = 1; // 3rd place
            else if (remainingCount == 2) placementPayout = 3; // 2nd place
        }
        else if (activePlayerCount == 3)
        {
            if (remainingCount == 3) placementPayout = 1; // 3rd place
            else if (remainingCount == 2) placementPayout = 3; // 2nd place
        }
        else if (activePlayerCount == 2)
        {
            if (remainingCount == 2) placementPayout = 1; // 2nd place
        }

        playerScores[victimPlayerIndex] += placementPayout;

        // Process the final survivor standing inside the arena boundary bounds
        if (activeWrestlersInRound.Count == 1)
        {
            isRoundActive = false;
            FreezeAllMovement();

            GameObject survivorObj = activeWrestlersInRound[0];
            TopDownMovement survivorMovement = survivorObj.GetComponent<TopDownMovement>();
            int survivorIdx = survivorMovement != null ? survivorMovement.playerIndex : 0;

            playerScores[survivorIdx] += 5; // 1st Place = +5 Points

            // Cache engagement logs before closing down the scene references
            RecordEngagementStates();

            string summaryText = $"PLAYER {survivorIdx + 1} SURVIVED THE ROUND! (+5 pts)\n\n" + BuildLeaderboardString();
            CheckTournamentStandings(summaryText);
        }
        else if (activeWrestlersInRound.Count == 0)
        {
            isRoundActive = false;
            FreezeAllMovement();

            if (eliminatedThisRound.Count >= 2)
            {
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
    }

    private void EndRoundDueToTimeout()
    {
        if (!isRoundActive) return;
        isRoundActive = false;
        FreezeAllMovement();
        HandleRingDrop();

        // --- OVERHAULED TIMEOUT ENGAGEMENT PENALTY MATRIX ---
        for (int i = 0; i < activePlayerCount; i++)
        {
            bool activeObjFound = false;
            GameObject foundPlayer = null;

            foreach (var p in activeWrestlersInRound)
            {
                if (p != null && p.GetComponent<TopDownMovement>().playerIndex == i)
                {
                    activeObjFound = true;
                    foundPlayer = p;
                    break;
                }
            }

            if (activeObjFound && foundPlayer != null)
            {
                TopDownMovement tdm = foundPlayer.GetComponent<TopDownMovement>();
                if (tdm != null && !tdm.hasDealtDamageThisRound)
                {
                    playerScores[i] = Mathf.Max(0, playerScores[i] - 1); // Apply -1 Penalty
                    playerPassedEngagementCheck[i] = false; // Flag for danger scaling next round
                    Debug.Log($"<color=red>[TIMEOUT PENALTY]</color> Player {i + 1} penalized -1 pt for camping/stalling!");
                }
                else
                {
                    playerPassedEngagementCheck[i] = true;
                }
            }
        }

        string summaryText = "TIME OUT! NO ENGAGEMENT DETECTED! RING SHRUNK!\n\n" + BuildLeaderboardString();
        CheckTournamentStandings(summaryText);
    }

    private void RecordEngagementStates()
    {
        foreach (var player in activeWrestlersInRound)
        {
            if (player != null)
            {
                TopDownMovement tdm = player.GetComponent<TopDownMovement>();
                if (tdm != null)
                {
                    playerPassedEngagementCheck[tdm.playerIndex] = tdm.hasDealtDamageThisRound;
                }
            }
        }
        foreach (var player in eliminatedThisRound)
        {
            if (player != null)
            {
                TopDownMovement tdm = player.GetComponent<TopDownMovement>();
                if (tdm != null)
                {
                    playerPassedEngagementCheck[tdm.playerIndex] = tdm.hasDealtDamageThisRound;
                }
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
            string placementMedal = (placement == 0) ? "👑 1st" : $"{placement + 1}th";
            header += $"{placementMedal}: Player {originalPlayerIdx + 1} - {playerScores[originalPlayerIdx]} / 15 pts\n";
        }
        return header;
    }

    private void CheckTournamentStandings(string completionBannerMessage)
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

            if (playerScores[i] >= WINNING_SCORE_THRESHOLD)
            {
                matchWon = true;
            }
        }

        if (matchWon)
        {
            StartCoroutine(DelayedBannerOverride($"🏆 MATCH OVER 🏆\nPLAYER {highestScoreIdx + 1} IS THE FIRST TO 15 PTS!", 3.5f));
            StartCoroutine(MatchEndReturnDelayRoutine(7f));
        }
        else
        {
            currentRound++;
            if (currentRound <= totalRounds)
            {
                StartCoroutine(RoundTransitionDelayRoutine(5f));
            }
            else
            {
                StartCoroutine(DelayedBannerOverride($"🏆 MATCH OVER 🏆\nPLAYER {highestScoreIdx + 1} HIGHEST FINAL SCORE WIN!", 3.5f));
                StartCoroutine(MatchEndReturnDelayRoutine(7f));
            }
        }
    }

    public void TogglePauseGameSystem()
    {
        if (!gameplayUIPanel.activeSelf) return;
        isGamePaused = !isGamePaused;
        if (pauseMenuPanel != null) pauseMenuPanel.SetActive(isGamePaused);
        Time.timeScale = isGamePaused ? 0f : 1f;
    }

    public void ReturnToHomeScreenHub()
    {
        isGamePaused = false;
        isRoundActive = false;
        Time.timeScale = 1f;

        if (pauseMenuPanel != null) pauseMenuPanel.SetActive(false);
        characterSelectPanel.SetActive(true);
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

    private void DisplayRoundEndNotification(string displayMessage)
    {
        if (roundWinnerAnnounceText != null)
        {
            if (string.IsNullOrEmpty(displayMessage))
            {
                roundWinnerAnnounceText.gameObject.SetActive(false);
            }
            else
            {
                roundWinnerAnnounceText.gameObject.SetActive(true);
                roundWinnerAnnounceText.text = displayMessage;
            }
        }
    }

    private IEnumerator TriggerSuddenDeathTransitionSequence(int pA, int pB)
    {
        DisplayRoundEndNotification("MUTUAL ELIMINATION DETECTED!\nPREPARING SUDDEN DEATH TIEBREAKER...");
        yield return new WaitForSeconds(3.5f);
        StartSuddenDeathRound(pA, pB);
    }

    private IEnumerator DelayedBannerOverride(string text, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (roundWinnerAnnounceText != null) roundWinnerAnnounceText.text = text;
    }

    private IEnumerator RoundTransitionDelayRoutine(float delayDuration)
    {
        yield return new WaitForSeconds(delayDuration - 0.5f);

        // FADES OUT TEXT JUST BEFORE THE NEXT ROUND STARTS SPAWNING
        if (roundWinnerAnnounceText != null)
        {
            roundWinnerAnnounceText.text = "";
            roundWinnerAnnounceText.gameObject.SetActive(false);
        }
        yield return new WaitForSeconds(0.5f);

        StartNewRound();
    }

    private IEnumerator MatchEndReturnDelayRoutine(float delayDuration)
    {
        yield return new WaitForSeconds(delayDuration);
        ReturnToHomeScreenHub();
    }

    private void ClearMatchData()
    {
        foreach (var player in activeWrestlersInRound) if (player != null) Destroy(player);
        foreach (var player in eliminatedThisRound) if (player != null) Destroy(player);

        // FIXED ARRAY CLEAR LOOP
        for (int i = 0; i < spawnedControlUIs.Length; i++)
        {
            if (spawnedControlUIs[i] != null) Destroy(spawnedControlUIs[i]);
            spawnedControlUIs[i] = null;
        }

        activeWrestlersInRound.Clear();
        eliminatedThisRound.Clear();
    }
}