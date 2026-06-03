using System.Text;
using UnityEngine;

[DisallowMultipleComponent]
public class MatchMusicDebugOverlayModule : DebugOverlayModule
{
    private MatchMusicDirector musicDirector;

    private void Awake()
    {
        musicDirector = GetComponent<MatchMusicDirector>();
    }

    public override void BuildContent(StringBuilder builder, DebugOverlayVerbosity verbosity)
    {
        musicDirector ??= GetComponent<MatchMusicDirector>();
        if (musicDirector == null)
        {
            builder.Append("MatchMusicDirector not found.");
            return;
        }

        builder.Append("Authority Phase: ").Append(musicDirector.CurrentAuthorityPhase?.ToString() ?? "None").AppendLine();
        builder.Append("Music State: ").Append(GetMusicStateColorTag(musicDirector.CurrentMusicState)).Append(musicDirector.CurrentMusicState).Append("</color>").AppendLine();
        builder.Append("Pre-Battle Queued: ").Append(musicDirector.IsPreBattleLoopQueued ? "true" : "false").AppendLine();
        builder.Append("Local Battle Started: ").Append(musicDirector.LocalBattleStarted ? "true" : "false").AppendLine();
        builder.Append("Local Battle Ended: ").Append(musicDirector.LocalBattleEnded ? "true" : "false").AppendLine();
        builder.Append("Post-Battle Variant: ").Append(musicDirector.CurrentPostBattleVariant).AppendLine();
        builder.Append("Setup Position: ").Append(musicDirector.CurrentSetupMusicPositionSeconds.ToString("F2")).Append("s").AppendLine();
        builder.Append("Battle Position: ").Append(musicDirector.CurrentBattleMusicPositionSeconds.ToString("F2")).Append("s").AppendLine();

        if (verbosity == DebugOverlayVerbosity.Verbose)
        {
            builder.Append("Director Enabled: ").Append(musicDirector.isActiveAndEnabled ? "true" : "false").AppendLine();
        }
    }

    private static string GetMusicStateColorTag(MatchMusicState state)
    {
        return state switch
        {
            MatchMusicState.SetupTrack => "<color=#7FDBFF>",
            MatchMusicState.PreBattleLoop => "<color=#FFD166>",
            MatchMusicState.BattleTrack => "<color=#FF6B6B>",
            MatchMusicState.PostBattleLoop => "<color=#B8F2E6>",
            _ => "<color=#FFFFFF>",
        };
    }
}
