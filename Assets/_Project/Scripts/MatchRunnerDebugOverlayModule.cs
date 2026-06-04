using System.Text;
using System.Linq;
using AIR.Shared.GameSession;
using UnityEngine;

[DisallowMultipleComponent]
public class MatchRunnerDebugOverlayModule : DebugOverlayModule
{
    private GameManager gameManager;

    private void Awake()
    {
        gameManager = GetComponent<GameManager>();
    }

    public override void BuildContent(StringBuilder builder, DebugOverlayVerbosity verbosity)
    {
        gameManager ??= GetComponent<GameManager>();
        if (gameManager == null)
        {
            builder.Append("GameManager not found.");
            return;
        }

        var runner = gameManager.MatchRunner;
        if (runner == null)
        {
            builder.Append("MatchRunner unavailable.");
            return;
        }

        builder.Append("Runner Active: ").Append(runner.isActive ? "<color=#7CFC00>true</color>" : "<color=#FF7A7A>false</color>").AppendLine();
        builder.Append("Phase: ").Append(GetPhaseColorTag(runner.Phase)).Append(runner.Phase).Append("</color>").AppendLine();
        builder.Append("Next Phase: ").Append(runner.NextPhase).AppendLine();
        builder.Append("Round: ").Append(runner.RoundIndex).AppendLine();
        builder.Append("Tick: ").Append(runner.TickIndex).AppendLine();
        builder.Append("Match Time: ").Append(runner.MatchTimeSeconds.ToString("F2")).Append("s").AppendLine();
        builder.Append("Phase Elapsed: ").Append(runner.PhaseElapsedSeconds.ToString("F2")).Append("s").AppendLine();

        var remainingSeconds = runner.CurrentPhaseRemainingSeconds;
        var remainingTicks = runner.CurrentPhaseRemainingTicks;
        builder.Append("Phase Remaining: ");
        if (remainingSeconds.HasValue && remainingTicks.HasValue)
        {
            builder.Append(remainingSeconds.Value.ToString("F2")).Append("s / ").Append(remainingTicks.Value).Append(" ticks");
        }
        else
        {
            builder.Append("No fixed duration");
        }
        builder.AppendLine();

        if (verbosity == DebugOverlayVerbosity.Verbose)
        {
            PlayerSnapshot localPlayer = gameManager.GetLocalPlayerSnapshot();
            int currentFieldCount = localPlayer?.Field?.Slots.Count(slot => slot.Unit != null) ?? 0;
            int maxFieldCount = localPlayer?.MaxFieldedUnits ?? 3;
            builder.Append("Fixed Tick: ").Append(runner.FixedTickSeconds.ToString("F3")).Append("s").AppendLine();
            builder.Append("Current Round UI: ").Append(gameManager.currentRound).AppendLine();
            builder.Append("Gold: ").Append(localPlayer?.Economy?.Gold ?? gameManager.startGold).AppendLine();
            builder.Append("Fielded Units: ").Append(currentFieldCount).Append('/').Append(maxFieldCount).AppendLine();
            builder.Append("Frame dt: ").Append(Time.deltaTime.ToString("F4")).Append("s").AppendLine();
        }
    }

    private static string GetPhaseColorTag(Phase phase)
    {
        return phase switch
        {
            Phase.Setup => "<color=#7FDBFF>",
            Phase.SetupLock => "<color=#FFD166>",
            Phase.BattleIntro => "<color=#F4A261>",
            Phase.Battle => "<color=#FF6B6B>",
            Phase.BattleResolve => "<color=#FFD166>",
            Phase.PostBattleSync => "<color=#B8F2E6>",
            Phase.RoundEnd => "<color=#CDB4DB>",
            _ => "<color=#FFFFFF>",
        };
    }
}
