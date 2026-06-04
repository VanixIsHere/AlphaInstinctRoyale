using System;
using System.Linq;
using AIR.Shared.GameSession;
using UnityEngine;

[DisallowMultipleComponent]
public class UnitPlacementCoordinator : MonoBehaviour
{
    public bool IsAnyUnitDragging => activeInteraction != null;

    private UnitDragInteraction activeInteraction;
    private UnitInstance activeUnit;
    private HexGridGenerator gridGenerator;
    private BenchManager benchManager;
    private HexCell hoveredCell;
    private BenchSlotDropTarget hoveredBenchSlot;
    private bool hoveredFieldValid;
    private bool hoveredBenchValid;

    private void Awake()
    {
        CacheReferences();
    }

    private void Update()
    {
        if (!IsAnyUnitDragging)
        {
            return;
        }

        CacheReferences();
        UpdateHoveredTargets();
        UpdateHighlights();
    }

    public bool CanStartDrag(UnitInstance unit)
    {
        if (unit == null || IsAnyUnitDragging)
        {
            return false;
        }

        PlayerSnapshot player = GameManager.Instance?.GetLocalPlayerSnapshot();
        if (player == null || GameManager.Instance?.MatchRunner?.Phase != Phase.Setup)
        {
            return false;
        }

        return unit.ContainerType switch
        {
            UnitContainerType.Bench => player.Bench.Slots.Any(slot => slot.Unit?.RuntimeUnitId == unit.RuntimeUnitId),
            UnitContainerType.Field => player.Field.Slots.Any(slot => slot.Unit?.RuntimeUnitId == unit.RuntimeUnitId),
            _ => false,
        };
    }

    public void BeginDrag(UnitDragInteraction interaction, UnitInstance unit)
    {
        activeInteraction = interaction;
        activeUnit = unit;
        UpdateHoveredTargets();
        UpdateHighlights();
    }

    public void CompleteDrag(UnitDragInteraction interaction)
    {
        if (interaction != activeInteraction || activeUnit == null)
        {
            return;
        }

        MatchCommand command = BuildCommand();
        ClearDragState();

        if (command == null || GameManager.Instance?.AuthorityClient == null)
        {
            return;
        }

        CommandResult result = GameManager.Instance.AuthorityClient.SendCommand(command);
        if (!result.Accepted)
        {
            Debug.Log($"UnitPlacementCoordinator: move rejected: {result.RejectionReason} - {result.RejectionMessage}");
        }
    }

    public void CancelDrag(UnitDragInteraction interaction)
    {
        if (interaction != activeInteraction)
        {
            return;
        }

        ClearDragState();
    }

    private void CacheReferences()
    {
        if (gridGenerator == null)
        {
            gridGenerator = FindFirstObjectByType<HexGridGenerator>();
        }

        if (benchManager == null)
        {
            benchManager = FindFirstObjectByType<BenchManager>();
        }
    }

    private void UpdateHoveredTargets()
    {
        hoveredCell = null;
        hoveredBenchSlot = null;
        hoveredFieldValid = false;
        hoveredBenchValid = false;

        Camera camera = Camera.main;
        if (camera == null)
        {
            return;
        }

        Ray ray = camera.ScreenPointToRay(Input.mousePosition);
        RaycastHit[] hits = Physics.RaycastAll(ray, Mathf.Infinity)
            .OrderBy(hit => hit.distance)
            .ToArray();

        if (hits.Length == 0)
        {
            return;
        }

        foreach (RaycastHit hit in hits)
        {
            if (activeUnit != null && hit.transform.IsChildOf(activeUnit.transform))
            {
                continue;
            }

            if (hoveredCell == null)
            {
                hoveredCell = hit.transform.GetComponentInParent<HexCell>();
            }

            if (hoveredBenchSlot == null)
            {
                hoveredBenchSlot = hit.transform.GetComponentInParent<BenchSlotDropTarget>();
            }

            if (hoveredCell != null || hoveredBenchSlot != null)
            {
                break;
            }
        }

        if (hoveredCell != null)
        {
            hoveredFieldValid = ValidateFieldTarget(hoveredCell.tileId);
        }

        if (hoveredBenchSlot != null)
        {
            hoveredBenchValid = ValidateBenchTarget(hoveredBenchSlot.SlotIndex);
        }
    }

    private void UpdateHighlights()
    {
        if (gridGenerator != null)
        {
            foreach (HexCell cell in gridGenerator.CellsByTileId.Values)
            {
                PlacementHighlightState state = PlacementHighlightState.None;
                if (IsAnyUnitDragging)
                {
                    state = IsPlayableFieldTile(cell.tileId)
                        ? PlacementHighlightState.Playable
                        : PlacementHighlightState.None;
                }

                if (IsAnyUnitDragging && cell == hoveredCell)
                {
                    state = hoveredFieldValid ? PlacementHighlightState.Valid : PlacementHighlightState.Invalid;
                }

                cell.SetHighlightState(state);
            }
        }

        if (benchManager != null)
        {
            foreach (BenchSlotDropTarget target in benchManager.GetDropTargets())
            {
                PlacementHighlightState state = PlacementHighlightState.None;
                if (IsAnyUnitDragging && target == hoveredBenchSlot)
                {
                    state = hoveredBenchValid ? PlacementHighlightState.Valid : PlacementHighlightState.Invalid;
                }

                target.SetHighlightState(state);
            }
        }
    }

    private void ClearDragState()
    {
        activeInteraction = null;
        activeUnit = null;
        hoveredCell = null;
        hoveredBenchSlot = null;
        hoveredFieldValid = false;
        hoveredBenchValid = false;
        UpdateHighlights();
    }

    private MatchCommand BuildCommand()
    {
        if (activeUnit == null || GameManager.Instance == null)
        {
            return null;
        }

        if (hoveredCell != null && hoveredFieldValid)
        {
            return activeUnit.ContainerType switch
            {
                UnitContainerType.Bench => new COM_MoveBenchToField
                {
                    PlayerId = GameManager.LocalPlayerId,
                    RuntimeUnitId = activeUnit.RuntimeUnitId,
                    TargetTileId = hoveredCell.tileId,
                },
                UnitContainerType.Field => new COM_MoveFieldUnit
                {
                    PlayerId = GameManager.LocalPlayerId,
                    RuntimeUnitId = activeUnit.RuntimeUnitId,
                    TargetTileId = hoveredCell.tileId,
                },
                _ => null,
            };
        }

        if (hoveredBenchSlot != null && hoveredBenchValid)
        {
            return activeUnit.ContainerType switch
            {
                UnitContainerType.Bench => new COM_MoveBenchUnit
                {
                    PlayerId = GameManager.LocalPlayerId,
                    RuntimeUnitId = activeUnit.RuntimeUnitId,
                    TargetBenchSlotIndex = hoveredBenchSlot.SlotIndex,
                },
                UnitContainerType.Field => new COM_MoveFieldToBench
                {
                    PlayerId = GameManager.LocalPlayerId,
                    RuntimeUnitId = activeUnit.RuntimeUnitId,
                    TargetBenchSlotIndex = hoveredBenchSlot.SlotIndex,
                },
                _ => null,
            };
        }

        return null;
    }

    private bool ValidateFieldTarget(string tileId)
    {
        if (activeUnit == null)
        {
            return false;
        }

        PlayerSnapshot player = GameManager.Instance?.GetLocalPlayerSnapshot();
        MatchSnapshot snapshot = GameManager.Instance?.CurrentSnapshot;
        if (player == null || snapshot?.Arena == null || string.IsNullOrWhiteSpace(tileId))
        {
            return false;
        }

        if (snapshot.Arena.GetTile(tileId) == null || !IsPlayableFieldTile(tileId))
        {
            return false;
        }

        int currentFieldCount = player.Field.Slots.Count(slot => slot.Unit != null);
        if (activeUnit.ContainerType == UnitContainerType.Bench && currentFieldCount >= player.MaxFieldedUnits)
        {
            return false;
        }

        FieldSlotState targetSlot = player.Field.Slots.FirstOrDefault(slot => slot.TileId == tileId);
        if (targetSlot == null || targetSlot.Unit != null)
        {
            return false;
        }

        return activeUnit.ContainerType switch
        {
            UnitContainerType.Bench => true,
            UnitContainerType.Field => !string.Equals(activeUnit.FieldTileId, tileId, StringComparison.Ordinal),
            _ => false,
        };
    }

    private bool ValidateBenchTarget(int slotIndex)
    {
        if (activeUnit == null)
        {
            return false;
        }

        PlayerSnapshot player = GameManager.Instance?.GetLocalPlayerSnapshot();
        if (player == null)
        {
            return false;
        }

        BenchSlotState targetSlot = player.Bench.Slots.FirstOrDefault(slot => slot.SlotIndex == slotIndex);
        if (targetSlot == null || targetSlot.Unit != null)
        {
            return false;
        }

        return activeUnit.ContainerType switch
        {
            UnitContainerType.Bench => activeUnit.BenchSlotIndex != slotIndex,
            UnitContainerType.Field => true,
            _ => false,
        };
    }

    private bool IsPlayableFieldTile(string tileId)
    {
        MatchSnapshot snapshot = GameManager.Instance?.CurrentSnapshot;
        return snapshot?.Arena != null && HexArenaUtils.IsTileDeployable(tileId, snapshot.Arena.Config);
    }
}
