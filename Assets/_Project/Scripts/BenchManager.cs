using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using AIR.Shared.GameSession;

/// <summary>
/// Manages the bench
/// </summary>
public class BenchManager : MonoBehaviour
{
    const string benchSlotGameObjectName = "BenchSlot";

    [Header("Bench Layout")]
    public int benchSlotCount = 9;
    public float slotSpacing = 1.5f;
    // How many hexes below the bottom row the bench should appear
    public float distanceBelowGridInHexes = 2.5f;
    float yOffsetFromGrid = -5.5f; // will be recalculated at runtime

    /// <summary>
    /// Current offset of the bench relative to the grid center in world units.
    /// </summary>
    public float CurrentYOffset => yOffsetFromGrid;
    [Header("Prefabs")]
    public GameObject slotVisualPrefab;

    [Header("Interaction")]
    [SerializeField] private bool allowBenchUnitInteraction = true;

    private Transform[] benchSlots;
    private BenchSlotDropTarget[] dropTargets;
    private UnitInstance[] occupiedInstances;
    private readonly Dictionary<string, UnitDataSO> unitLookup = new();

    [Header("Runtime")]
    public Transform gridCenter;

    void Start()
    {
        GameObject centerObj = GameObject.Find("GridCenter");
        if (centerObj == null)
        {
            Debug.LogError("GridCenter not found! Bench will not be positioned correctly");
            return;
        }

        gridCenter = centerObj.transform;

        // Adjust bench offset based on current grid dimensions if generator is available
        HexGridGenerator gridGen = FindFirstObjectByType<HexGridGenerator>();
        if (gridGen != null)
        {
            float halfGridHeight = (gridGen.ArenaHeight - 1) * 1.5f * gridGen.hexSize * 0.5f;
            float extraOffset = distanceBelowGridInHexes * gridGen.hexSize;
            yOffsetFromGrid = -(halfGridHeight + extraOffset);
        }
        else
        {
            Debug.LogWarning("HexGridGenerator not found, using default bench offset");
        }

        GenerateBenchSlots();
        CacheUnitLookup();
        SubscribeToAuthority();
    }


    /// <summary>
    /// Generates the bench slots
    /// </summary>
    void GenerateBenchSlots()
    {
        benchSlots = new Transform[benchSlotCount];
        dropTargets = new BenchSlotDropTarget[benchSlotCount];
        occupiedInstances = new UnitInstance[benchSlotCount];

        float totalWidth = (benchSlotCount - 1) * slotSpacing;
        float startX = -totalWidth / 2f;

        for (int i = 0; i < benchSlotCount; i++)
        {
            Vector3 localOffset = new Vector3(startX + i * slotSpacing, 0f, yOffsetFromGrid);
            Vector3 worldPos = gridCenter.position + localOffset;

            GameObject slot = Instantiate(slotVisualPrefab, worldPos, Quaternion.identity, transform);
            slot.name = $"BenchSlot{i}";
            benchSlots[i] = slot.transform;
            BenchSlotDropTarget dropTarget = slot.GetComponent<BenchSlotDropTarget>();
            if (dropTarget == null)
            {
                dropTarget = slot.AddComponent<BenchSlotDropTarget>();
            }

            dropTarget.Initialize(i);
            dropTargets[i] = dropTarget;
        }
    }

    public bool CanInteract(UnitInstance unitInstance)
    {
        return allowBenchUnitInteraction
            && unitInstance != null
            && unitInstance.OwningBench == this
            && unitInstance.BenchSlotIndex >= 0
            && unitInstance.BenchSlotIndex < benchSlots.Length
            && occupiedInstances[unitInstance.BenchSlotIndex] == unitInstance;
    }

    public Transform GetBenchSlotTransform(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= benchSlots.Length)
        {
            return null;
        }

        return benchSlots[slotIndex];
    }

    public IReadOnlyList<BenchSlotDropTarget> GetDropTargets()
    {
        return dropTargets ?? Array.Empty<BenchSlotDropTarget>();
    }

    /// <summary>
    /// Spawns the played unit to the bench
    /// </summary>
    /// <param name="unit"></param>
    /// <param name="slot"></param>
    /// <returns></returns>
    UnitInstance SpawnUnit(UnitDataSO unit, Transform slot, int slotIndex)
    {
        if (UnitVisualFactory.GetUnitPrefab(unit.UnitSizeClassification) is not GameObject unitPrefab)
        {
            return null;
        }

        GameObject instanceObj = Instantiate(unitPrefab, slot.position, Quaternion.identity, slot);
        foreach (Transform child in instanceObj.transform.parent)
        {
            if (child == transform) continue;
            if (child.name == benchSlotGameObjectName)
            {
                Utils.StandOnTop(instanceObj, child.gameObject);
            }
        }
        
        
        UnitInstance inst = instanceObj.GetComponent<UnitInstance>();
        if (inst == null)
        {
            inst = instanceObj.AddComponent<UnitInstance>();
        }

        inst.Init(unit);
        inst.BindToBench(this, slotIndex, slot);

        UnitDragInteraction interaction = instanceObj.GetComponent<UnitDragInteraction>();
        if (interaction == null)
        {
            interaction = instanceObj.AddComponent<UnitDragInteraction>();
        }

        interaction.Initialize(inst);

        return inst;
    }

    private void SubscribeToAuthority()
    {
        if (GameManager.Instance == null)
        {
            return;
        }

        GameManager.Instance.SnapshotUpdated -= HandleSnapshotUpdated;
        GameManager.Instance.SnapshotUpdated += HandleSnapshotUpdated;

        if (GameManager.Instance.CurrentSnapshot != null)
        {
            HandleSnapshotUpdated(GameManager.Instance.CurrentSnapshot);
        }
    }

    private void CacheUnitLookup()
    {
        unitLookup.Clear();
        UnitPoolManager poolManager = FindFirstObjectByType<UnitPoolManager>();
        if (poolManager?.unitRegistry?.units == null)
        {
            return;
        }

        foreach (UnitDataSO unitData in poolManager.unitRegistry.units)
        {
            if (unitData != null && !string.IsNullOrWhiteSpace(unitData.unitKey))
            {
                unitLookup[unitData.unitKey] = unitData;
            }
        }
    }

    private void HandleSnapshotUpdated(MatchSnapshot snapshot)
    {
        PlayerSnapshot playerSnapshot = snapshot?.GetPlayer(GameManager.LocalPlayerId);
        if (playerSnapshot == null || playerSnapshot.Bench == null)
        {
            ClearBenchVisuals();
            return;
        }

        RenderBench(playerSnapshot.Bench);
    }

    private void RenderBench(BenchState benchState)
    {
        ClearBenchVisuals();

        int renderCount = Math.Min(benchState.Slots.Count, benchSlots.Length);
        for (int slotIndex = 0; slotIndex < renderCount; slotIndex++)
        {
            BenchSlotState slotState = benchState.Slots[slotIndex];
            if (slotState.Unit == null)
            {
                continue;
            }

            if (!unitLookup.TryGetValue(slotState.Unit.UnitKey, out UnitDataSO unitData))
            {
                Debug.LogWarning($"BenchManager: Could not resolve UnitDataSO for unit key '{slotState.Unit.UnitKey}'.");
                continue;
            }

            UnitInstance instance = SpawnUnit(unitData, benchSlots[slotIndex], slotIndex);
            if (instance != null)
            {
                instance.Init(unitData, slotState.Unit.Level, slotState.Unit.CurrentHealth, slotState.Unit.RuntimeUnitId);
                occupiedInstances[slotIndex] = instance;
            }
        }
    }

    private void ClearBenchVisuals()
    {
        if (occupiedInstances == null)
        {
            return;
        }

        for (int index = 0; index < occupiedInstances.Length; index++)
        {
            if (occupiedInstances[index] != null)
            {
                Destroy(occupiedInstances[index].gameObject);
                occupiedInstances[index] = null;
            }
        }
    }

    /// <summary>
    /// Loads the unit's <see cref="GameObject"/> based off <param name="unitSizeClassification"/>
    /// </summary>
    /// <param name="unitSizeClassification"></param>
    /// <returns></returns>
    /// <exception cref="NotImplementedException"></exception>
}
