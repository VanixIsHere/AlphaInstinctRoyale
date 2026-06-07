using System.Collections;
using AIR.Shared.GameSession;
using UnityEngine;
using UnityEngine.Audio;

public enum MatchMusicState
{
    Idle,
    SetupTrack,
    PreBattleLoop,
    BattleTrack,
    PostBattleLoop,
}

public enum PostBattleLoopVariant
{
    None,
    A,
    B,
}

[DisallowMultipleComponent]
public class MatchMusicDirector : MonoBehaviour
{
    private const float MusicSourceVolume = 1f;

    [Header("Authority")]
    [SerializeField] private GameManager gameManager;

    [Header("Mixer Routing")]
    [SerializeField] private AudioMixerGroup musicMixerGroup;

    [Header("Music Clips")]
    [SerializeField] private AudioClip setupTrackClip;
    [SerializeField] private AudioClip preBattleLoopClip;
    [SerializeField] private AudioClip battleTrackClip;
    [SerializeField] private AudioClip postBattleLoopClipA;
    [SerializeField] private AudioClip postBattleLoopClipB;

    [Header("Transitions")]
    [SerializeField] private float crossfadeSeconds = 0.35f;
    [SerializeField] private double seamToleranceSeconds = 0.05d;
    [SerializeField] private double postBattleScheduleLeadSeconds = 0.15d;
    [SerializeField] private bool fallbackToPostBattleOnAuthoritativeResolve = true;
    [SerializeField] private float battleTimeoutDetectionLeewaySeconds = 0.1f;

    public MatchMusicState CurrentMusicState { get; private set; } = MatchMusicState.Idle;
    public Phase? CurrentAuthorityPhase => lastObservedPhase;
    public bool LocalBattleStarted { get; private set; }
    public bool LocalBattleEnded { get; private set; }
    public bool IsPreBattleLoopQueued => preBattleLoopQueued;
    public PostBattleLoopVariant CurrentPostBattleVariant { get; private set; } = PostBattleLoopVariant.None;
    public float CurrentSetupMusicPositionSeconds { get; private set; }
    public float CurrentBattleMusicPositionSeconds { get; private set; }

    private AudioSource sourceA;
    private AudioSource sourceB;
    private AudioSource activeSource;
    private AudioSource inactiveSource;

    private Phase? lastObservedPhase;
    private Coroutine crossfadeCoroutine;
    private double activeSourceEndDsp;
    private double scheduledInactiveStartDsp;
    private bool preBattleLoopQueued;
    private bool postBattleLoopQueued;
    private bool postBattleNextScheduled;
    private PostBattleLoopVariant queuedPostBattleVariant = PostBattleLoopVariant.None;
    private float lastObservedBattlePhaseElapsedSeconds;

    private void Awake()
    {
        gameManager ??= GetComponent<GameManager>();
        CreateManagedSources();
        WarmMusicClips();
    }

    private void Update()
    {
        if (gameManager == null)
        {
            gameManager = GetComponent<GameManager>();
        }

        var runner = gameManager != null ? gameManager.MatchRunner : null;
        if (runner == null || !runner.isActive)
        {
            if (CurrentMusicState != MatchMusicState.Idle)
            {
                StopAllMusic();
                SetMusicState(MatchMusicState.Idle, "match runner inactive");
            }

            lastObservedPhase = null;
            lastObservedBattlePhaseElapsedSeconds = 0f;
            return;
        }

        if (lastObservedPhase != runner.Phase)
        {
            lastObservedPhase = runner.Phase;
            HandleAuthoritativePhaseChange(runner.Phase);
        }

        if (runner.Phase == Phase.Battle)
        {
            lastObservedBattlePhaseElapsedSeconds = runner.PhaseElapsedSeconds;
        }

        UpdateQueuedPreBattleLoop(runner.Phase);
        UpdatePostBattleAlternation();
    }

    public void NotifyLocalBattleStarted()
    {
        LocalBattleStarted = true;
        LocalBattleEnded = false;
        EnterBattleTrack("local battle started");
    }

    public void NotifyLocalBattleEnded()
    {
        LocalBattleEnded = true;
    }

    private void HandleAuthoritativePhaseChange(Phase phase)
    {
        Debug.Log($"[MatchMusicDirector] Authority phase -> {phase}");

        switch (phase)
        {
            case Phase.GameStart:
            case Phase.RoundStart:
                ResetLocalBattleFlags();
                break;

            case Phase.Setup:
                ResetLocalBattleFlags();
                AlignSetupFlowToAuthority("setup phase started");
                break;

            case Phase.SetupLock:
            case Phase.BattleIntro:
                ContinueOrAlignSetupFlowToAuthority($"authority entered {phase}");
                break;

            case Phase.Battle:
                LocalBattleStarted = true;
                LocalBattleEnded = false;
                AlignBattleFlowToAuthority("authority entered battle");
                break;

            case Phase.BattleResolve:
            case Phase.PostBattleSync:
            case Phase.RoundEnd:
                bool battleEndedByTimer = DidLastBattleEndByTimer();
                if (LocalBattleEnded)
                {
                    ContinueOrQueuePostBattleFlow($"authority entered {phase}", battleEndedByTimer);
                }
                else if (fallbackToPostBattleOnAuthoritativeResolve)
                {
                    LocalBattleEnded = true;
                    ContinueOrQueuePostBattleFlow($"authority fallback on {phase}", battleEndedByTimer);
                }
                break;

            case Phase.GameEnd:
                EnterPostBattleLoop("authority entered game end", GetInitialPostBattleVariant());
                break;
        }
    }

    private void AlignSetupFlowToAuthority(string reason)
    {
        var runner = gameManager != null ? gameManager.MatchRunner : null;
        if (runner == null)
        {
            return;
        }

        if (!TryGetAuthoritativeSetupMusicPosition(runner, out var targetPositionSeconds))
        {
            return;
        }

        CurrentSetupMusicPositionSeconds = targetPositionSeconds;

        if (targetPositionSeconds < setupTrackClip.length)
        {
            BeginSetupTrackAtPosition(targetPositionSeconds, reason);

            if (runner.Phase == Phase.SetupLock || runner.Phase == Phase.BattleIntro)
            {
                QueueLoopAtSetupTrackEnd($"continuing setup tail during {runner.Phase}");
            }

            return;
        }

        var loopOffsetSeconds = targetPositionSeconds - setupTrackClip.length;
        EnterPreBattleLoopAtOffset(loopOffsetSeconds, reason);
    }

    private void ContinueOrAlignSetupFlowToAuthority(string reason)
    {
        if (CurrentMusicState == MatchMusicState.SetupTrack || CurrentMusicState == MatchMusicState.PreBattleLoop)
        {
            if (CurrentMusicState == MatchMusicState.SetupTrack)
            {
                QueueLoopAtSetupTrackEnd(reason);
            }

            return;
        }

        AlignSetupFlowToAuthority(reason);
    }

    private void BeginSetupTrackAtPosition(float positionSeconds, string reason)
    {
        if (setupTrackClip == null)
        {
            Debug.LogWarning("[MatchMusicDirector] Setup track clip is missing.");
            return;
        }

        StopCrossfade();
        CancelQueuedPlayback();
        StopSource(sourceA);
        StopSource(sourceB);

        activeSource = sourceA;
        inactiveSource = sourceB;
        ConfigureSource(activeSource, setupTrackClip, false);
        activeSource.time = Mathf.Clamp(positionSeconds, 0f, setupTrackClip.length);
        activeSource.Play();
        activeSourceEndDsp = AudioSettings.dspTime + (setupTrackClip.length - activeSource.time);
        SetMusicState(MatchMusicState.SetupTrack, $"{reason} @ {activeSource.time:F2}s");
    }

    private void QueueOrStartPreBattleLoop(string reason)
    {
        if (preBattleLoopClip == null)
        {
            Debug.LogWarning("[MatchMusicDirector] Pre-battle loop clip is missing.");
            return;
        }

        if (CurrentMusicState == MatchMusicState.BattleTrack || CurrentMusicState == MatchMusicState.PostBattleLoop)
        {
            return;
        }

        if (CurrentMusicState == MatchMusicState.SetupTrack &&
            IsSourcePlaying(activeSource) &&
            activeSource.clip == setupTrackClip &&
            AudioSettings.dspTime < activeSourceEndDsp - seamToleranceSeconds)
        {
            if (!preBattleLoopQueued)
            {
                ConfigureSource(inactiveSource, preBattleLoopClip, true);
                inactiveSource.volume = MusicSourceVolume;
                scheduledInactiveStartDsp = activeSourceEndDsp;
                inactiveSource.PlayScheduled(scheduledInactiveStartDsp);
                preBattleLoopQueued = true;
                Debug.Log($"[MatchMusicDirector] Queued pre-battle loop at setup seam | reason: {reason}");
            }

            return;
        }

        PlayLoopNow(preBattleLoopClip);
        SetMusicState(MatchMusicState.PreBattleLoop, reason);
    }

    private void QueueLoopAtSetupTrackEnd(string reason)
    {
        if (preBattleLoopClip == null || preBattleLoopQueued)
        {
            return;
        }

        if (!IsSourcePlaying(activeSource) || activeSource.clip != setupTrackClip)
        {
            return;
        }

        ConfigureSource(inactiveSource, preBattleLoopClip, true);
        inactiveSource.volume = MusicSourceVolume;
        scheduledInactiveStartDsp = activeSourceEndDsp;
        inactiveSource.PlayScheduled(scheduledInactiveStartDsp);
        preBattleLoopQueued = true;
        Debug.Log($"[MatchMusicDirector] Queued pre-battle loop at setup seam | reason: {reason}");
    }

    private void EnterPreBattleLoopAtOffset(float loopOffsetSeconds, string reason)
    {
        if (preBattleLoopClip == null)
        {
            Debug.LogWarning("[MatchMusicDirector] Pre-battle loop clip is missing.");
            return;
        }

        StopCrossfade();
        CancelQueuedPlayback();
        StopSource(sourceA);
        StopSource(sourceB);

        activeSource = sourceA;
        inactiveSource = sourceB;
        ConfigureSource(activeSource, preBattleLoopClip, true);

        var normalizedOffset = preBattleLoopClip.length > 0f
            ? Mathf.Repeat(loopOffsetSeconds, preBattleLoopClip.length)
            : 0f;

        activeSource.time = normalizedOffset;
        activeSource.Play();
        activeSourceEndDsp = double.PositiveInfinity;
        SetMusicState(MatchMusicState.PreBattleLoop, $"{reason} @ loop {normalizedOffset:F2}s");
    }

    private void EnterBattleTrack(string reason)
    {
        BeginBattleTrackAtPosition(0f, reason);
    }

    private void AlignBattleFlowToAuthority(string reason)
    {
        var runner = gameManager != null ? gameManager.MatchRunner : null;
        if (runner == null || battleTrackClip == null)
        {
            return;
        }

        var targetPositionSeconds = Mathf.Clamp(runner.PhaseElapsedSeconds, 0f, battleTrackClip.length);
        BeginBattleTrackAtPosition(targetPositionSeconds, $"{reason} @ {targetPositionSeconds:F2}s");
    }

    private void BeginBattleTrackAtPosition(float positionSeconds, string reason)
    {
        if (battleTrackClip == null)
        {
            Debug.LogWarning("[MatchMusicDirector] Battle track clip is missing.");
            return;
        }

        preBattleLoopQueued = false;
        CurrentBattleMusicPositionSeconds = positionSeconds;
        CrossfadeToClip(battleTrackClip, false, MatchMusicState.BattleTrack, reason, PostBattleLoopVariant.None, positionSeconds);
    }

    private void EnterPostBattleLoop(string reason, PostBattleLoopVariant initialVariant)
    {
        if (postBattleLoopClipA == null || postBattleLoopClipB == null)
        {
            Debug.LogWarning("[MatchMusicDirector] Post-battle loop clips A/B are both required.");
            return;
        }

        preBattleLoopQueued = false;
        postBattleLoopQueued = false;
        postBattleNextScheduled = false;
        queuedPostBattleVariant = PostBattleLoopVariant.None;
        CrossfadeToClip(GetPostBattleClip(initialVariant), false, MatchMusicState.PostBattleLoop, reason, initialVariant);
    }

    private void ContinueOrQueuePostBattleFlow(string reason, bool battleEndedByTimer)
    {
        if (CurrentMusicState == MatchMusicState.PostBattleLoop)
        {
            return;
        }

        PostBattleLoopVariant initialVariant = battleEndedByTimer
            ? PostBattleLoopVariant.B
            : GetInitialPostBattleVariant();

        if (!battleEndedByTimer)
        {
            EnterPostBattleLoop(reason, initialVariant);
            return;
        }

        if (CurrentMusicState == MatchMusicState.BattleTrack &&
            IsSourcePlaying(activeSource) &&
            activeSource.clip == battleTrackClip &&
            AudioSettings.dspTime < activeSourceEndDsp - seamToleranceSeconds)
        {
            QueuePostBattleLoopAtBattleEnd(reason, initialVariant);
            return;
        }

        EnterPostBattleLoop(reason, initialVariant);
    }

    private void CrossfadeToClip(
        AudioClip targetClip,
        bool loop,
        MatchMusicState targetState,
        string reason,
        PostBattleLoopVariant postBattleVariant = PostBattleLoopVariant.None,
        float startTimeSeconds = 0f)
    {
        if (targetClip == null)
        {
            return;
        }

        StopCrossfade();
        CancelQueuedPlayback();
        crossfadeCoroutine = StartCoroutine(CrossfadeRoutine(targetClip, loop, targetState, reason, postBattleVariant, startTimeSeconds));
    }

    private IEnumerator CrossfadeRoutine(
        AudioClip targetClip,
        bool loop,
        MatchMusicState targetState,
        string reason,
        PostBattleLoopVariant postBattleVariant,
        float startTimeSeconds)
    {
        var outgoing = activeSource;
        var incoming = inactiveSource;

        ConfigureSource(incoming, targetClip, loop);
        incoming.time = Mathf.Clamp(startTimeSeconds, 0f, targetClip.length);
        incoming.volume = 0f;
        incoming.Play();

        var incomingEndDsp = loop
            ? double.PositiveInfinity
            : AudioSettings.dspTime + (targetClip.length - incoming.time);
        var outgoingStartVolume = outgoing != null ? outgoing.volume : 0f;
        var elapsed = 0f;
        var duration = Mathf.Max(0.01f, crossfadeSeconds);

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            var t = Mathf.Clamp01(elapsed / duration);

            if (outgoing != null)
            {
                outgoing.volume = Mathf.Lerp(outgoingStartVolume, 0f, t);
            }

            incoming.volume = Mathf.Lerp(0f, MusicSourceVolume, t);
            yield return null;
        }

        if (outgoing != null)
        {
            StopSource(outgoing);
        }

        incoming.volume = MusicSourceVolume;
        activeSource = incoming;
        inactiveSource = outgoing ?? GetAlternateSource(incoming);
        activeSourceEndDsp = incomingEndDsp;
        postBattleNextScheduled = false;
        queuedPostBattleVariant = PostBattleLoopVariant.None;
        CurrentPostBattleVariant = postBattleVariant;
        CurrentBattleMusicPositionSeconds = targetState == MatchMusicState.BattleTrack ? incoming.time : 0f;
        SetMusicState(targetState, reason);
        crossfadeCoroutine = null;
    }

    private void UpdateQueuedPreBattleLoop(Phase currentPhase)
    {
        if (!preBattleLoopQueued || CurrentMusicState != MatchMusicState.SetupTrack)
        {
            return;
        }

        if (AudioSettings.dspTime >= scheduledInactiveStartDsp - seamToleranceSeconds)
        {
            activeSource = inactiveSource;
            inactiveSource = GetAlternateSource(activeSource);
            activeSourceEndDsp = double.PositiveInfinity;
            preBattleLoopQueued = false;
            SetMusicState(MatchMusicState.PreBattleLoop, "setup track reached pre-battle seam");
        }
        else if (!(currentPhase == Phase.SetupLock || currentPhase == Phase.BattleIntro))
        {
            inactiveSource.Stop();
            preBattleLoopQueued = false;
        }
    }

    private void QueuePostBattleLoopAtBattleEnd(string reason, PostBattleLoopVariant initialVariant)
    {
        AudioClip initialClip = GetPostBattleClip(initialVariant);
        if (initialClip == null || postBattleLoopQueued)
        {
            return;
        }

        if (!IsSourcePlaying(activeSource) || activeSource.clip != battleTrackClip)
        {
            return;
        }

        ConfigureSource(inactiveSource, initialClip, false);
        scheduledInactiveStartDsp = activeSourceEndDsp;
        inactiveSource.PlayScheduled(scheduledInactiveStartDsp);
        postBattleLoopQueued = true;
        queuedPostBattleVariant = initialVariant;
        Debug.Log($"[MatchMusicDirector] Queued post-battle loop at battle seam | reason: {reason}");
    }

    private void UpdatePostBattleAlternation()
    {
        if (CurrentMusicState == MatchMusicState.BattleTrack && IsSourcePlaying(activeSource))
        {
            CurrentBattleMusicPositionSeconds = activeSource.time;
        }

        if (postBattleLoopQueued && AudioSettings.dspTime >= scheduledInactiveStartDsp - seamToleranceSeconds)
        {
            StopSource(activeSource);
            activeSource = inactiveSource;
            inactiveSource = GetAlternateSource(activeSource);
            activeSourceEndDsp = AudioSettings.dspTime + activeSource.clip.length;
            CurrentPostBattleVariant = queuedPostBattleVariant;
            queuedPostBattleVariant = PostBattleLoopVariant.None;
            postBattleLoopQueued = false;
            postBattleNextScheduled = false;
            SetMusicState(MatchMusicState.PostBattleLoop, "battle track reached post-battle seam");
        }

        if (CurrentMusicState != MatchMusicState.PostBattleLoop || !IsSourcePlaying(activeSource) || activeSource.clip == null)
        {
            return;
        }

        if (!postBattleNextScheduled && AudioSettings.dspTime >= activeSourceEndDsp - postBattleScheduleLeadSeconds)
        {
            var nextVariant = GetNextPostBattleVariant(CurrentPostBattleVariant);
            var nextClip = GetPostBattleClip(nextVariant);
            if (nextClip == null)
            {
                return;
            }

            ConfigureSource(inactiveSource, nextClip, false);
            inactiveSource.PlayScheduled(activeSourceEndDsp);
            postBattleNextScheduled = true;
            queuedPostBattleVariant = nextVariant;
            Debug.Log($"[MatchMusicDirector] Queued post-battle variant {nextVariant} at seam");
        }

        if (postBattleNextScheduled && AudioSettings.dspTime >= activeSourceEndDsp - seamToleranceSeconds)
        {
            StopSource(activeSource);
            activeSource = inactiveSource;
            inactiveSource = GetAlternateSource(activeSource);
            activeSourceEndDsp = AudioSettings.dspTime + activeSource.clip.length;
            CurrentPostBattleVariant = queuedPostBattleVariant;
            queuedPostBattleVariant = PostBattleLoopVariant.None;
            postBattleNextScheduled = false;
            Debug.Log($"[MatchMusicDirector] Post-battle variant -> {CurrentPostBattleVariant}");
        }
    }

    private void PlayLoopNow(AudioClip clip)
    {
        if (clip == null)
        {
            return;
        }

        StopCrossfade();
        CancelQueuedPlayback();
        StopSource(sourceA);
        StopSource(sourceB);

        activeSource = sourceA;
        inactiveSource = sourceB;
        ConfigureSource(activeSource, clip, true);
        activeSource.Play();
        activeSourceEndDsp = double.PositiveInfinity;
    }

    private void ConfigureSource(AudioSource source, AudioClip clip, bool loop)
    {
        source.clip = clip;
        source.loop = loop;
        source.volume = MusicSourceVolume;
        source.playOnAwake = false;
        source.outputAudioMixerGroup = musicMixerGroup;
        source.time = 0f;
    }

    private void CancelQueuedPlayback()
    {
        preBattleLoopQueued = false;
        postBattleLoopQueued = false;
        postBattleNextScheduled = false;
        queuedPostBattleVariant = PostBattleLoopVariant.None;
        CurrentPostBattleVariant = PostBattleLoopVariant.None;
        CurrentSetupMusicPositionSeconds = 0f;
        CurrentBattleMusicPositionSeconds = 0f;
        scheduledInactiveStartDsp = 0d;

        if (inactiveSource != null)
        {
            inactiveSource.Stop();
        }
    }

    private void StopAllMusic()
    {
        StopCrossfade();
        CancelQueuedPlayback();
        StopSource(sourceA);
        StopSource(sourceB);
        activeSourceEndDsp = 0d;
        CurrentPostBattleVariant = PostBattleLoopVariant.None;
    }

    private void StopCrossfade()
    {
        if (crossfadeCoroutine != null)
        {
            StopCoroutine(crossfadeCoroutine);
            crossfadeCoroutine = null;
        }
    }

    private void StopSource(AudioSource source)
    {
        if (source == null)
        {
            return;
        }

        source.Stop();
    }

    private bool IsSourcePlaying(AudioSource source)
    {
        return source != null && source.isPlaying;
    }

    private void SetMusicState(MatchMusicState nextState, string reason)
    {
        if (CurrentMusicState == nextState)
        {
            return;
        }

        CurrentMusicState = nextState;
        Debug.Log($"[MatchMusicDirector] Music state -> {CurrentMusicState} | reason: {reason}");
    }

    private void ResetLocalBattleFlags()
    {
        LocalBattleStarted = false;
        LocalBattleEnded = false;
        lastObservedBattlePhaseElapsedSeconds = 0f;
    }

    private bool TryGetAuthoritativeSetupMusicPosition(MatchRunner runner, out float targetPositionSeconds)
    {
        targetPositionSeconds = 0f;
        if (setupTrackClip == null)
        {
            return false;
        }

        var setupLoopStartSeconds = GetSetupLoopStartSeconds();
        switch (runner.Phase)
        {
            case Phase.Setup:
                targetPositionSeconds = runner.PhaseElapsedSeconds;
                return true;

            case Phase.SetupLock:
                targetPositionSeconds = setupLoopStartSeconds + runner.PhaseElapsedSeconds;
                return true;

            case Phase.BattleIntro:
                var setupLockDuration = runner.GetFixedPhaseDurationSeconds(Phase.SetupLock) ?? 0f;
                targetPositionSeconds = setupLoopStartSeconds + setupLockDuration + runner.PhaseElapsedSeconds;
                return true;

            default:
                return false;
        }
    }

    private float GetSetupLoopStartSeconds()
    {
        if (setupTrackClip == null)
        {
            return 0f;
        }

        var preBattleLoopLength = preBattleLoopClip != null ? preBattleLoopClip.length : 0f;
        return Mathf.Max(0f, setupTrackClip.length - preBattleLoopLength);
    }

    private void CreateManagedSources()
    {
        sourceA = CreateSource("MusicSourceA");
        sourceB = CreateSource("MusicSourceB");
        activeSource = sourceA;
        inactiveSource = sourceB;
    }

    private AudioSource CreateSource(string childName)
    {
        var child = new GameObject(childName);
        child.transform.SetParent(transform, false);

        var source = child.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = false;
        source.spatialBlend = 0f;
        source.outputAudioMixerGroup = musicMixerGroup;
        source.volume = MusicSourceVolume;
        return source;
    }

    private void WarmMusicClips()
    {
        WarmMusicClip(setupTrackClip);
        WarmMusicClip(preBattleLoopClip);
        WarmMusicClip(battleTrackClip);
        WarmMusicClip(postBattleLoopClipA);
        WarmMusicClip(postBattleLoopClipB);
    }

    private static void WarmMusicClip(AudioClip clip)
    {
        if (clip == null)
        {
            return;
        }

        clip.LoadAudioData();
    }

    private AudioSource GetAlternateSource(AudioSource source)
    {
        return source == sourceA ? sourceB : sourceA;
    }

    private AudioClip GetPostBattleClip(PostBattleLoopVariant variant)
    {
        return variant switch
        {
            PostBattleLoopVariant.A => postBattleLoopClipA,
            PostBattleLoopVariant.B => postBattleLoopClipB,
            _ => null,
        };
    }

    private static PostBattleLoopVariant GetInitialPostBattleVariant()
    {
        return PostBattleLoopVariant.A;
    }

    private bool DidLastBattleEndByTimer()
    {
        var runner = gameManager != null ? gameManager.MatchRunner : null;
        if (runner == null)
        {
            return false;
        }

        float? battleDurationSeconds = runner.GetFixedPhaseDurationSeconds(Phase.Battle);
        if (!battleDurationSeconds.HasValue)
        {
            return false;
        }

        return lastObservedBattlePhaseElapsedSeconds >= battleDurationSeconds.Value - Mathf.Max(0.01f, battleTimeoutDetectionLeewaySeconds);
    }

    private PostBattleLoopVariant GetNextPostBattleVariant(PostBattleLoopVariant current)
    {
        return current == PostBattleLoopVariant.A ? PostBattleLoopVariant.B : PostBattleLoopVariant.A;
    }
}
