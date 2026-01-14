using System;
using UnityEngine;


/// <summary>
/// Single-source-of-truth for the Setup phase music timeline, anchored to the audio DSP clock.
///
/// Responsibilities
/// - Schedule the 48s Setup track to start at a precise DSP timestamp.
/// - Compute the current music timeline time (MusicTime) from the scheduled start.
/// - Emit one-shot events when crossing 7s (interactive open), 37s (interactive close), and 48s (end of base track).
/// - Seamlessly hand off to / start a loopable "tail" source exactly at 48s (server slack window).
///
/// Notes
/// - This file is intentionally focused on the Setup phase. Crossfades to Battle/PostBattle will be layered on later.
/// - Drive UI countdowns by reading CountdownSeconds (derived), do NOT run your own timers.
/// - Use BeginSetupRound() to start a new round; it schedules both the main setup track and the tail loop.
///
/// Inspector setup
/// - setupTrackSource: AudioSource with the full 48s setup AudioClip (do not loop).
/// - setupTailLoopSource: AudioSource with the loopable tail clip (set Loop = true), volume matched.
/// - Ensure both sources share the same AudioMixer route if you plan global fades.
/// </summary>

public enum SetupPhase { Idle, WaitingToStart, LeadIn, Interactive, Tail, Completed }

[DisallowMultipleComponent]
public class SetupMusicConductor : MonoBehaviour
{
    public SetupPhase Phase { get; private set; } = SetupPhase.Idle;

    [Header("Audio Sources (same output group)")]
    [Tooltip("Full Setup track (Lead-In + Interactive window + Tail Section). Do NOT enable Loop.")]
    public AudioSource setupTrackSource; // 48s clip, Loop = false

    [Tooltip("Loopable tail clip that matches the 37-48s section musically. Enable Loop = true.")]
    public AudioSource setupTailLoopSource; // loopable neutral tail

    [Header("Timeline (seconds)")]
    [Tooltip("Interactive opens at t= 7s.")]
    public double interactiveOpenS = 7.0;

    [Header("Interactive closes at t = 37s (exact 30s window).")]
    public double interactiveCloseS = 37.0;

    [Tooltip("End of the base setup track timeline (tail begins looping here).")]
    public double trackEndS = 48.0;

    [Header("Scheduling")]
    [Tooltip("How far in the future (seconds) to schedule the track start. 0.05-0.5s is typical to avoid immediate-start jitter.")]
    public double scheduleLeadS = 0.25;

    /// <summary>
    /// DSP timestamp when t=0 of the setup track will occur. Valid after BeginSetupRound().
    /// </summary>
    public double RoundStartDsp { get; private set; } = double.NaN;

    /// <summary>
    /// Music timeline time in seconds since RoundStartDsp. Can be negative before the scheduled start.
    /// </summary>
    public double MusicTime => double.IsNaN(RoundStartDsp) ? 0.0 : (AudioSettings.dspTime - RoundStartDsp);

    /// <summary>
    /// Returns remaining seconds in the 30s interactive window, clamped [0, 30].
    /// </summary>
    public float CountdownSeconds
    {
        get
        {
            var remain = interactiveCloseS - MusicTime;
            // Clamp to [0, window]
            var window = Math.Max(0.0, interactiveCloseS - interactiveOpenS);
            return (float)Mathf.Clamp((float)remain, 0f, (float)window);
        }
    }

    // One-shot event latches
    private bool _openedLatch, _closedLatch, _endedLatch;

    // Events
    public event Action OnInteractiveOpen; // fires when crossing >= 7.0s
    public event Action OnInteractiveClose; // fires when crossing >= 37.0s
    public event Action OnTrackEnd; // fires when crossing >= 48.0s

    /// <summary>
    /// and pre-schedules the loopable tail to begin exactly at trackEndS.
    /// </summary>
    public void BeginSetupRound()
    {
        if (setupTrackSource == null)
        {
            Debug.LogError("SetupMusicConductor: Missing setupTrackSource.");
            return;
        }
        if (setupTrackSource.clip == null)
        {
            Debug.LogError("SetupMusicConductor: setupTrackSource has no clip.");
            return;
        }
        if (setupTailLoopSource == null || setupTailLoopSource.clip == null)
        {
            Debug.LogWarning("SetupMusicConductor: Tail loop source/clip not set. Tail loop handoff will be skipped.");
        }


        // Validate window length
        var window = interactiveCloseS - interactiveOpenS;
        if (Math.Abs(window - 30.0) > 0.001)
        {
            Debug.LogWarning($"SetupMusicConductor: Interactive window is {window:F3}s (expected 30.0s). Adjust interactiveOpenS/CloseS.");
        }
        if (trackEndS <= interactiveCloseS)
        {
            Debug.LogWarning("SetupMusicConductor: trackEndS should be after interactiveCloseS.");
        }

        // Compute DSP start
        var now = AudioSettings.dspTime;
        RoundStartDsp = now + Math.Max(0.0, scheduleLeadS);

        // Reset and schedule the main track
        setupTrackSource.Stop();
        setupTrackSource.time = 0f;
        setupTrackSource.loop = false;
        setupTrackSource.PlayScheduled(RoundStartDsp);

        // Pre-schedule the tail loop to start exactly at trackEndS from round start
        if (setupTailLoopSource != null && setupTailLoopSource.clip != null)
        {
            var tailStart = RoundStartDsp + trackEndS;
            setupTailLoopSource.Stop();
            setupTailLoopSource.loop = true; // ensure loop
            setupTailLoopSource.PlayScheduled(tailStart);
        }

        // Init state
        Phase = SetupPhase.WaitingToStart;
        _openedLatch = _closedLatch = _endedLatch = false;

    }

    private void Update()
    {
        if (double.IsNaN(RoundStartDsp)) return; // not started
        var t = MusicTime; // can be < 0 before scheduled start

        // Phase bookkeeping
        if (t < 0.0)
        {
            Phase = SetupPhase.WaitingToStart;
            return;
        }
        else if (t < interactiveOpenS)
        {
            Phase = SetupPhase.LeadIn;
        }
        else if (t < interactiveCloseS)
        {
            Phase = SetupPhase.Interactive;
        }
        else if (t < trackEndS)
        {
            Phase = SetupPhase.Tail;
        }
        else
        {
            // Past the base track end; tail (if any) should now be audible/looping
            Phase = SetupPhase.Completed; // from the perspective of the base 48s asset
        }


        // One-shot threshold events (idempotent)
        if (!_openedLatch && t >= interactiveOpenS)
        {
            _openedLatch = true;
            SafeInvoke(OnInteractiveOpen, nameof(OnInteractiveOpen));
        }
        if (!_closedLatch && t >= interactiveCloseS)
        {
            _closedLatch = true;
            SafeInvoke(OnInteractiveClose, nameof(OnInteractiveClose));
        }
        if (!_endedLatch && t >= trackEndS)
        {
            _endedLatch = true;
            SafeInvoke(OnTrackEnd, nameof(OnTrackEnd));
        }
    }

    private void SafeInvoke(Action evt, string name)
    {
        try
        {
            evt?.Invoke();
        }
        catch (Exception ex)
        {
            Debug.LogException(new Exception($"SetupMusicConductor event '{name}' threw:", ex));
        }
    }

    #region Debug Helpers
    /// <summary>
    /// For development: instantly jump playback to a specific timeline time.
    /// Note: this interrupts scheduled playback precision. For testing only.
    /// </summary>
    public void DebugJumpTo(double seconds)
    {
        if (setupTrackSource == null || setupTrackSource.clip == null) return;

        // Update round start to align DSP-derived MusicTime with the new position
        RoundStartDsp = AudioSettings.dspTime - seconds;

        // Retarget main source
        setupTrackSource.Stop();
        setupTrackSource.time = Mathf.Clamp((float)seconds, 0f, setupTrackSource.clip.length);
        setupTrackSource.Play();

        // Handle tail: if jumping beyond trackEndS, start the loop; else stop it (will resume via schedule or not at all)
        if (setupTailLoopSource != null && setupTailLoopSource.clip != null)
            {
            if (seconds >= trackEndS)
            {
                if (!setupTailLoopSource.isPlaying)
                {
                    setupTailLoopSource.loop = true;
                    setupTailLoopSource.Play();
                }
            }
            else
            {
                if (setupTailLoopSource.isPlaying) setupTailLoopSource.Stop();
            }
        }

        // Reset latches based on position
        _openedLatch = seconds >= interactiveOpenS;
        _closedLatch = seconds >= interactiveCloseS;
        _endedLatch = seconds >= trackEndS;
    }
    #endregion
}