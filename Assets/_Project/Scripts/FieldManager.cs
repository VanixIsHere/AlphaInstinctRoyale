using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using AIR.Shared.GameSession;

[DisallowMultipleComponent]
public class FieldManager : MonoBehaviour
{
    private readonly Dictionary<string, UnitDataSO> unitLookup = new();
    private readonly List<UnitInstance> activeInstances = new();

    private HexGridGenerator gridGenerator;

    private void Start()
    {
        gridGenerator = GetComponent<HexGridGenerator>();
        CacheUnitLookup();
        SubscribeToAuthority();
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
        if (playerSnapshot?.Field == null)
        {
            ClearFieldVisuals();
            return;
        }

        RenderField(playerSnapshot.Field);
    }

    private void RenderField(FieldState fieldState)
    {
        ClearFieldVisuals();

        foreach (FieldSlotState slot in fieldState.Slots)
        {
            if (slot.Unit == null)
            {
                continue;
            }

            HexCell cell = gridGenerator != null ? gridGenerator.GetCell(slot.TileId) : null;
            if (cell == null)
            {
                continue;
            }

            if (!unitLookup.TryGetValue(slot.Unit.UnitKey, out UnitDataSO unitData))
            {
                Debug.LogWarning($"FieldManager: Could not resolve UnitDataSO for unit key '{slot.Unit.UnitKey}'.");
                continue;
            }

            UnitInstance instance = SpawnUnit(unitData, cell, slot);
            if (instance != null)
            {
                activeInstances.Add(instance);
            }
        }
    }

    private UnitInstance SpawnUnit(UnitDataSO unitData, HexCell cell, FieldSlotState slotState)
    {
        GameObject unitPrefab = UnitVisualFactory.GetUnitPrefab(unitData.UnitSizeClassification);
        if (unitPrefab == null)
        {
            return null;
        }

        GameObject instanceObj = Instantiate(unitPrefab, cell.transform.position, Quaternion.identity, cell.transform);
        Utils.StandOnTop(instanceObj, cell.GetSurfaceWorldBounds());

        UnitInstance instance = instanceObj.GetComponent<UnitInstance>();
        if (instance == null)
        {
            instance = instanceObj.AddComponent<UnitInstance>();
        }

        instance.Init(unitData, slotState.Unit.Level, slotState.Unit.CurrentHealth, slotState.Unit.RuntimeUnitId);
        instance.BindToField(slotState.TileId, cell.transform);

        UnitDragInteraction interaction = instanceObj.GetComponent<UnitDragInteraction>();
        if (interaction == null)
        {
            interaction = instanceObj.AddComponent<UnitDragInteraction>();
        }

        interaction.Initialize(instance);
        return instance;
    }

    private void ClearFieldVisuals()
    {
        foreach (UnitInstance instance in activeInstances.Where(instance => instance != null))
        {
            Destroy(instance.gameObject);
        }

        activeInstances.Clear();
    }
}
