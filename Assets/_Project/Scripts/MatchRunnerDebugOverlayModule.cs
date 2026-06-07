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

[DisallowMultipleComponent]
public class BattleSimulationDebugOverlayModule : DebugOverlayModule
{
    private GameManager gameManager;

    private void Awake()
    {
        gameManager = GetComponent<GameManager>();
    }

    public override void BuildContent(StringBuilder builder, DebugOverlayVerbosity verbosity)
    {
        gameManager ??= GetComponent<GameManager>();
        MatchSnapshot snapshot = gameManager?.CurrentSnapshot;
        BattleInstanceState battle = snapshot?.BattleInstance;
        if (battle == null)
        {
            builder.Append("No active battle snapshot.");
            return;
        }

        builder.Append("Battle Id: ").Append(battle.BattleInstanceId).AppendLine();
        builder.Append("Resolved: ").Append(battle.IsResolved ? "<color=#7CFC00>true</color>" : "<color=#FF7A7A>false</color>").AppendLine();
        builder.Append("Winner: ").Append(string.IsNullOrWhiteSpace(battle.WinningPlayerId) ? "-" : battle.WinningPlayerId).AppendLine();
        builder.Append("Sim Tick: ").Append(battle.LastSimulatedTick).AppendLine();
        builder.Append("Units: ").Append(battle.Units.Count).AppendLine();
        builder.Append("Recent Events: ").Append(battle.RecentEvents.Count).AppendLine();

        if (verbosity != DebugOverlayVerbosity.Verbose)
        {
            return;
        }

        foreach (BattleUnitState unit in battle.Units
                     .OrderBy(unit => unit.OwnerPlayerId, System.StringComparer.Ordinal)
                     .ThenBy(unit => unit.RuntimeUnitId, System.StringComparer.Ordinal)
                     .Take(8))
        {
            builder.Append(unit.OwnerPlayerId).Append(" | ")
                .Append(unit.UnitKey).Append(" | ")
                .Append(unit.CurrentTileId).Append(" | HP ")
                .Append(unit.CurrentHealth).Append('/').Append(unit.MaxHealth).Append(" | ")
                .Append(unit.IsAlive ? "Alive" : "Dead");

            if (!string.IsNullOrWhiteSpace(unit.CurrentTargetUnitId))
            {
                builder.Append(" | Tgt ").Append(unit.CurrentTargetUnitId);
            }

            if (!string.IsNullOrWhiteSpace(unit.MovementTargetTileId))
            {
                float progress = unit.MovementDurationSeconds > 0f
                    ? Mathf.Clamp01(unit.MovementElapsedSeconds / unit.MovementDurationSeconds)
                    : 0f;
                builder.Append(" | Move ").Append(unit.MovementStartTileId)
                    .Append("->").Append(unit.MovementTargetTileId)
                    .Append(" ").Append(progress.ToString("P0"));
            }

            if (unit.CurrentActionType != BattleUnitActionType.Idle)
            {
                float actionProgress = unit.CurrentActionDurationSeconds > 0f
                    ? Mathf.Clamp01(unit.CurrentActionElapsedSeconds / unit.CurrentActionDurationSeconds)
                    : 0f;
                builder.Append(" | Act ").Append(unit.CurrentActionType)
                    .Append(" ").Append(unit.CurrentActionOriginTileId)
                    .Append("->").Append(unit.CurrentActionDestinationTileId)
                    .Append(" ").Append(actionProgress.ToString("P0"));

                if (unit.CurrentActionType == BattleUnitActionType.Attacking)
                {
                    float executionProgress = unit.CurrentActionExecutionDelaySeconds > 0f
                        ? Mathf.Clamp01(unit.CurrentActionElapsedSeconds / unit.CurrentActionExecutionDelaySeconds)
                        : (unit.CurrentActionHasExecuted ? 1f : 0f);
                    builder.Append(" hit ").Append(executionProgress.ToString("P0"));
                    builder.Append(unit.CurrentActionHasExecuted ? " done" : " windup");
                }

                if (!string.IsNullOrWhiteSpace(unit.CurrentActionTargetUnitId))
                {
                    builder.Append(" tgt ").Append(unit.CurrentActionTargetUnitId);
                }
            }

            if (unit.LastResolvedActionType != BattleUnitActionType.Idle)
            {
                builder.Append(" | Last ").Append(unit.LastResolvedActionType)
                    .Append(" @").Append(unit.LastResolvedActionTick);
            }

            builder.Append(" | CD ").Append(unit.AttackCooldownSecondsRemaining.ToString("F2")).AppendLine();
        }

        if (battle.RecentEvents.Count > 0)
        {
            builder.AppendLine("Events:");
            foreach (BattleEventState battleEvent in battle.RecentEvents.TakeLast(6))
            {
                builder.Append("  #").Append(battleEvent.EventSequenceId)
                    .Append(" [").Append(battleEvent.TickIndex).Append("] ")
                    .Append(battleEvent.ActionType).Append('/')
                    .Append(battleEvent.EventType).Append(" ")
                    .Append(string.IsNullOrWhiteSpace(battleEvent.SourceUnitId) ? "-" : battleEvent.SourceUnitId)
                    .Append(" -> ")
                    .Append(string.IsNullOrWhiteSpace(battleEvent.TargetUnitId) ? "-" : battleEvent.TargetUnitId);

                if (battleEvent.DamageSourceType.HasValue)
                {
                    builder.Append(" ").Append(battleEvent.DamageSourceType.Value);
                }

                if (battleEvent.Amount > 0)
                {
                    builder.Append(" (").Append(battleEvent.Amount).Append(')');
                }

                if (!string.IsNullOrWhiteSpace(battleEvent.OriginTileId) || !string.IsNullOrWhiteSpace(battleEvent.DestinationTileId))
                {
                    builder.Append(" ").Append(string.IsNullOrWhiteSpace(battleEvent.OriginTileId) ? "-" : battleEvent.OriginTileId)
                        .Append("->")
                        .Append(string.IsNullOrWhiteSpace(battleEvent.DestinationTileId) ? "-" : battleEvent.DestinationTileId);
                }
                else if (!string.IsNullOrWhiteSpace(battleEvent.TileId))
                {
                    builder.Append(" @ ").Append(battleEvent.TileId);
                }

                builder.AppendLine();
            }
        }
    }
}
