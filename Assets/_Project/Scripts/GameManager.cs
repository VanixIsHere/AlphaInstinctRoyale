using UnityEngine;
using TMPro;
using System;
using AIR.Shared.GameSession;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }
    
    private MatchRunner Runner;
    private MatchMusicDirector musicDirector;
    private Phase? lastLoggedPhase;
    private int lastLoggedRemainingSeconds = int.MinValue;
    private int lastKnownRound = -1;

    public int testTick = 0;

    [Header("Game State")]
    public int startGold = 500;
    public int gold = 10;
    public int currentRound = 1;

    [Header("UI References")]
    public TextMeshProUGUI goldText;
    public TextMeshProUGUI roundText;

    public MatchRunner MatchRunner => Runner;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject); // Prevent duplicates
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject); // Optional: persists across scenes
        Runner = new MatchRunner();
        EnsureDebugOverlay();
        EnsureMusicDirectorReference();
    }

    void Start()
    {
        if (Instance != null)
        {
            Instance.gold = startGold;
        }
        UpdateUI();

        Debug.Log("Start in Game Manager");

        StartCoroutine(Utils.Delay(3.0f, () =>
        {
            Runner.StartMatch();
            currentRound = Runner.RoundIndex;
            UpdateUI();
            ResetPhaseDebugLogging();
            Debug.Log("Delayed runner start");
        }));
    }

    public void UpdateUI()
    {
        if (goldText != null)
            goldText.text = $"{gold}G";

        var displayedRound = Runner != null && Runner.isActive
            ? Runner.RoundIndex
            : currentRound;

        if (roundText != null)
            roundText.text = $"Round {displayedRound}";
    }

    void Update()
    {
        if (Runner.isActive)
        {
            Runner.Advance(Time.deltaTime);
            testTick = Runner.TickIndex;
            currentRound = Runner.RoundIndex;
            if (currentRound != lastKnownRound)
            {
                lastKnownRound = currentRound;
                UpdateUI();
            }

            LogMatchPhaseDebug();
        }
    }

    public void NotifyLocalBattleStarted()
    {
        if (musicDirector != null)
        {
            musicDirector.NotifyLocalBattleStarted();
        }
    }

    public void NotifyLocalBattleEnded()
    {
        if (musicDirector != null)
        {
            musicDirector.NotifyLocalBattleEnded();
        }
    }

    private void ResetPhaseDebugLogging()
    {
        lastLoggedPhase = null;
        lastLoggedRemainingSeconds = int.MinValue;
        lastKnownRound = -1;
    }

    private void LogMatchPhaseDebug()
    {
        if (lastLoggedPhase != Runner.Phase)
        {
            var remainingSeconds = Runner.CurrentPhaseRemainingSeconds;
            var remainingTicks = Runner.CurrentPhaseRemainingTicks;
            var remainingText = remainingSeconds.HasValue && remainingTicks.HasValue
                ? $"{remainingSeconds.Value:F2}s / {remainingTicks.Value} ticks"
                : "no fixed duration";

            Debug.Log(
                $"[MatchRunner] Phase transition -> {Runner.Phase} | Round {Runner.RoundIndex} | " +
                $"Tick {Runner.TickIndex} | Next: {Runner.NextPhase} in {remainingText}");

            lastLoggedPhase = Runner.Phase;
            lastLoggedRemainingSeconds = int.MinValue;
        }

        var secondsRemaining = Runner.CurrentPhaseRemainingSeconds;
        var ticksRemaining = Runner.CurrentPhaseRemainingTicks;
        if (!secondsRemaining.HasValue || !ticksRemaining.HasValue)
        {
            return;
        }

        var remainingBucket = Mathf.Max(0, Mathf.CeilToInt(secondsRemaining.Value));
        if (remainingBucket == lastLoggedRemainingSeconds)
        {
            return;
        }

        lastLoggedRemainingSeconds = remainingBucket;
        Debug.Log(
            $"[MatchRunner] {Runner.Phase} countdown | Round {Runner.RoundIndex} | " +
            $"Remaining: {secondsRemaining.Value:F2}s / {ticksRemaining.Value} ticks");
    }

    private void EnsureDebugOverlay()
    {
        if (GetComponent<DebugOverlayController>() == null)
        {
            gameObject.AddComponent<DebugOverlayController>();
        }

        if (GetComponent<MatchRunnerDebugOverlayModule>() == null)
        {
            gameObject.AddComponent<MatchRunnerDebugOverlayModule>();
        }

        if (GetComponent<MatchMusicDebugOverlayModule>() == null)
        {
            gameObject.AddComponent<MatchMusicDebugOverlayModule>();
        }
    }

    private void EnsureMusicDirectorReference()
    {
        musicDirector = GetComponent<MatchMusicDirector>();
    }
}
