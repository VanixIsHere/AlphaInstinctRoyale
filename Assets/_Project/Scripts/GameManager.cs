using UnityEngine;
using TMPro;
using System;
using AIR.Shared.GameSession;
using CardSystem;

public class GameManager : MonoBehaviour
{
    public const string LocalPlayerId = "local-player";

    public static GameManager Instance { get; private set; }
    
    private MatchRunner Runner;
    private MatchMusicDirector musicDirector;
    private IMatchAuthorityClient authorityClient;
    private Phase? lastLoggedPhase;
    private Phase? lastKnownSnapshotPhase;
    private int lastLoggedRemainingSeconds = int.MinValue;
    private int lastKnownRound = -1;

    public int testTick = 0;

    [Header("Game State")]
    public int startGold = 500;
    public int currentRound = 1;

    [Header("UI References")]
    public TextMeshProUGUI goldText;
    public TextMeshProUGUI roundText;

    public MatchRunner MatchRunner => Runner;
    public IMatchAuthorityClient AuthorityClient => authorityClient;
    public MatchSnapshot CurrentSnapshot => authorityClient?.CurrentSnapshot;
    public event Action<MatchSnapshot> SnapshotUpdated;
    public int gold => GetLocalPlayerSnapshot()?.Economy?.Gold ?? startGold;

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
        authorityClient = new LocalMatchAuthorityClient(Runner);
        authorityClient.SnapshotUpdated += HandleAuthoritySnapshotUpdated;
        EnsureDebugOverlay();
        EnsureMusicDirectorReference();
    }

    void Start()
    {
        if (Instance != null)
        {
            currentRound = 1;
        }
        UpdateUI();

        Debug.Log("Start in Game Manager");

        StartCoroutine(Utils.Delay(3.0f, () =>
        {
            Runner.StartMatch(BuildStartConfig());
            currentRound = Runner.RoundIndex;
            authorityClient.RefreshSnapshot();
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
            if (Runner.Phase != lastKnownSnapshotPhase)
            {
                lastKnownSnapshotPhase = Runner.Phase;
                authorityClient?.RefreshSnapshot();
            }

            if (currentRound != lastKnownRound)
            {
                lastKnownRound = currentRound;
                authorityClient?.RefreshSnapshot();
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
        lastKnownSnapshotPhase = null;
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

    public PlayerSnapshot GetLocalPlayerSnapshot()
    {
        return CurrentSnapshot?.GetPlayer(LocalPlayerId);
    }

    private MatchStartConfig BuildStartConfig()
    {
        MatchStartConfig config = new()
        {
            StartingGold = startGold,
            PlayerIds = { LocalPlayerId },
        };

        HexGridGenerator gridGenerator = FindFirstObjectByType<HexGridGenerator>();
        if (gridGenerator != null)
        {
            config.Arena = gridGenerator.BootstrapArenaConfig;
        }

        HandManager handManager = FindFirstObjectByType<HandManager>();
        if (handManager != null)
        {
            config.ShopOfferCount = handManager.GetLayoutHandSize();
        }

        BenchManager benchManager = FindFirstObjectByType<BenchManager>();
        if (benchManager != null)
        {
            config.BenchSlotCount = benchManager.benchSlotCount;
        }

        UnitPoolManager poolManager = FindFirstObjectByType<UnitPoolManager>();
        if (poolManager?.unitRegistry?.units != null)
        {
            foreach (UnitDataSO unit in poolManager.unitRegistry.units)
            {
                if (unit != null && !string.IsNullOrWhiteSpace(unit.unitKey))
                {
                    config.AvailableUnitKeys.Add(unit.unitKey);
                }
            }
        }

        return config;
    }

    private void HandleAuthoritySnapshotUpdated(MatchSnapshot snapshot)
    {
        SnapshotUpdated?.Invoke(snapshot);
        UpdateUI();
    }
}

public interface IMatchAuthorityClient
{
    MatchSnapshot CurrentSnapshot { get; }
    event Action<MatchSnapshot> SnapshotUpdated;
    CommandResult SendCommand(MatchCommand command);
    void RefreshSnapshot();
}

public sealed class LocalMatchAuthorityClient : IMatchAuthorityClient
{
    private readonly MatchRunner runner;

    public LocalMatchAuthorityClient(MatchRunner runner)
    {
        this.runner = runner;
    }

    public MatchSnapshot CurrentSnapshot { get; private set; } = new();
    public event Action<MatchSnapshot> SnapshotUpdated;

    public CommandResult SendCommand(MatchCommand command)
    {
        CommandResult result = runner.ProcessCommand(command);
        CurrentSnapshot = result.Snapshot;
        SnapshotUpdated?.Invoke(CurrentSnapshot);
        return result;
    }

    public void RefreshSnapshot()
    {
        CurrentSnapshot = runner.CurrentSnapshot;
        SnapshotUpdated?.Invoke(CurrentSnapshot);
    }
}
