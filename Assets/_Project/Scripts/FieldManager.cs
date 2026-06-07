using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using AIR.Shared.GameSession;

[DisallowMultipleComponent]
public class FieldManager : MonoBehaviour
{
    private sealed class CombatLabelState
    {
        public GameObject LabelObject;
        public TextMesh TextMesh;
        public Vector3 WorldPosition;
        public Vector3 Velocity;
        public float RemainingSeconds;
    }

    [Header("Health Bar")]
    [SerializeField] private UnitHealthBar.Settings healthBarSettings = default;

    private readonly Dictionary<string, UnitDataSO> unitLookup = new();
    private readonly Dictionary<string, UnitInstance> activeInstances = new();
    private readonly List<CombatLabelState> activeCombatLabels = new();
    private readonly HashSet<string> processedBattleEventKeys = new(StringComparer.Ordinal);

    private HexGridGenerator gridGenerator;

    private const float CombatLabelLifetimeSeconds = 0.8f;
    private const float CombatLabelRiseSpeed = 1.15f;
    private static readonly Color DamageLabelColor = new(1f, 0.4f, 0.35f, 1f);
    private static readonly Color DeathLabelColor = new(1f, 0.88f, 0.52f, 1f);

    private void Start()
    {
        if (healthBarSettings.HpPerChunk <= 0)
        {
            healthBarSettings = UnitHealthBar.Settings.Default;
        }

        gridGenerator = GetComponent<HexGridGenerator>();
        CacheUnitLookup();
        SubscribeToAuthority();
    }

    private void Update()
    {
        UpdateCombatLabels();
        CleanupRetiredInstances();
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
        if (snapshot?.Players == null || snapshot.Players.Count == 0)
        {
            ClearFieldVisuals();
            return;
        }

        RenderField(snapshot);
    }

    private void RenderField(MatchSnapshot snapshot)
    {
        HashSet<string> desiredKeys = new(StringComparer.Ordinal);
        Dictionary<string, UnitInstance> unitsByRuntimeId = new(StringComparer.Ordinal);
        Dictionary<string, UnitInstance> allInstancesByRuntimeId = activeInstances.Values
            .Where(instance => instance != null && !string.IsNullOrWhiteSpace(instance.RuntimeUnitId))
            .ToDictionary(instance => instance.RuntimeUnitId, instance => instance, StringComparer.Ordinal);
        int snapshotTick = snapshot.TickIndex;
        foreach (PlayerSnapshot player in snapshot.Players)
        {
            if (player?.Field?.Slots == null)
            {
                continue;
            }

            foreach (FieldSlotState slot in player.Field.Slots)
            {
                if (slot.Unit == null)
                {
                    continue;
                }

                if (snapshot.BattleInstance != null && !slot.Unit.IsAlive)
                {
                    continue;
                }

                string renderKey = BuildRenderKey(player.PlayerId, slot.Unit.RuntimeUnitId);
                desiredKeys.Add(renderKey);

                if (!unitLookup.TryGetValue(slot.Unit.UnitKey, out UnitDataSO unitData))
                {
                    Debug.LogWarning($"FieldManager: Could not resolve UnitDataSO for unit key '{slot.Unit.UnitKey}'.");
                    continue;
                }

                if (!TryResolveRenderAnchor(snapshot, slot, out HexCell cell, out Vector3 worldPosition))
                {
                    continue;
                }

                if (!activeInstances.TryGetValue(renderKey, out UnitInstance instance) || instance == null)
                {
                    instance = SpawnUnit(player.PlayerId, unitData, cell, slot, worldPosition, snapshotTick);
                    if (instance == null)
                    {
                        continue;
                    }

                    activeInstances[renderKey] = instance;
                }

                UpdateInstance(instance, unitData, slot, cell, worldPosition, snapshotTick);
                unitsByRuntimeId[slot.Unit.RuntimeUnitId] = instance;
                allInstancesByRuntimeId[slot.Unit.RuntimeUnitId] = instance;
            }
        }

        UpdateBattleFacing(unitsByRuntimeId);
        ProcessBattleEvents(snapshot, allInstancesByRuntimeId);
        BeginRetirementForMissingDefeatedUnits(snapshot, desiredKeys);

        foreach (string staleKey in activeInstances.Keys.Except(desiredKeys, StringComparer.Ordinal).ToArray())
        {
            if (activeInstances[staleKey] != null)
            {
                if (activeInstances[staleKey].IsRetiring)
                {
                    continue;
                }

                Destroy(activeInstances[staleKey].gameObject);
            }

            if (!activeInstances.ContainsKey(staleKey) || activeInstances[staleKey] == null || !activeInstances[staleKey].IsRetiring)
            {
                activeInstances.Remove(staleKey);
            }
        }
    }

    private void BeginRetirementForMissingDefeatedUnits(MatchSnapshot snapshot, ISet<string> desiredKeys)
    {
        if (snapshot?.BattleInstance?.Units == null)
        {
            return;
        }

        Dictionary<string, BattleUnitState> battleUnitsByRuntimeId = snapshot.BattleInstance.Units
            .Where(unit => !string.IsNullOrWhiteSpace(unit.RuntimeUnitId))
            .GroupBy(unit => unit.RuntimeUnitId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);

        foreach ((string renderKey, UnitInstance instance) in activeInstances)
        {
            if (instance == null || desiredKeys.Contains(renderKey))
            {
                continue;
            }

            if (!battleUnitsByRuntimeId.TryGetValue(instance.RuntimeUnitId, out BattleUnitState battleUnit) || !battleUnit.IsAlive)
            {
                instance.BeginRetirement();
            }
        }
    }

    private void ProcessBattleEvents(MatchSnapshot snapshot, IReadOnlyDictionary<string, UnitInstance> unitsByRuntimeId)
    {
        if (snapshot?.BattleInstance?.RecentEvents == null)
        {
            return;
        }

        foreach (BattleEventState battleEvent in snapshot.BattleInstance.RecentEvents)
        {
            string eventKey = battleEvent.EventSequenceId > 0
                ? battleEvent.EventSequenceId.ToString()
                : $"{battleEvent.TickIndex}:{battleEvent.EventType}:{battleEvent.SourceUnitId}:{battleEvent.TargetUnitId}:{battleEvent.TileId}:{battleEvent.Amount}";
            if (!processedBattleEventKeys.Add(eventKey))
            {
                continue;
            }

            switch (battleEvent.EventType)
            {
                case BattleEventType.AttackStarted:
                    BeginAttackWindupForUnit(battleEvent.SourceUnitId, unitsByRuntimeId);
                    break;
                case BattleEventType.Damage:
                    SpawnDamageLabel(battleEvent, unitsByRuntimeId);
                    break;
                case BattleEventType.Death:
                    BeginRetirementForUnit(battleEvent.TargetUnitId, unitsByRuntimeId);
                    SpawnDeathLabel(battleEvent, unitsByRuntimeId);
                    break;
            }
        }
    }

    private static void BeginAttackWindupForUnit(string runtimeUnitId, IReadOnlyDictionary<string, UnitInstance> unitsByRuntimeId)
    {
        if (string.IsNullOrWhiteSpace(runtimeUnitId))
        {
            return;
        }

        if (unitsByRuntimeId.TryGetValue(runtimeUnitId, out UnitInstance instance) && instance != null)
        {
            instance.NotifyAttackWindup();
        }
    }

    private void SpawnDamageLabel(BattleEventState battleEvent, IReadOnlyDictionary<string, UnitInstance> unitsByRuntimeId)
    {
        if (battleEvent.Amount <= 0)
        {
            return;
        }

        if (!TryResolveEventWorldPosition(battleEvent, unitsByRuntimeId, out Vector3 worldPosition))
        {
            return;
        }

        CreateCombatLabel($"-{battleEvent.Amount}", DamageLabelColor, worldPosition + (Vector3.up * 1.55f));
    }

    private void SpawnDeathLabel(BattleEventState battleEvent, IReadOnlyDictionary<string, UnitInstance> unitsByRuntimeId)
    {
        if (!TryResolveEventWorldPosition(battleEvent, unitsByRuntimeId, out Vector3 worldPosition))
        {
            return;
        }

        CreateCombatLabel("KO", DeathLabelColor, worldPosition + (Vector3.up * 1.9f));
    }

    private void BeginRetirementForUnit(string runtimeUnitId, IReadOnlyDictionary<string, UnitInstance> unitsByRuntimeId)
    {
        if (string.IsNullOrWhiteSpace(runtimeUnitId))
        {
            return;
        }

        if (unitsByRuntimeId.TryGetValue(runtimeUnitId, out UnitInstance instance) && instance != null)
        {
            instance.BeginRetirement();
        }
    }

    private bool TryResolveEventWorldPosition(BattleEventState battleEvent, IReadOnlyDictionary<string, UnitInstance> unitsByRuntimeId, out Vector3 worldPosition)
    {
        worldPosition = Vector3.zero;

        if (!string.IsNullOrWhiteSpace(battleEvent.TargetUnitId) &&
            unitsByRuntimeId.TryGetValue(battleEvent.TargetUnitId, out UnitInstance targetInstance) &&
            targetInstance != null)
        {
            worldPosition = targetInstance.transform.position;
            return true;
        }

        if (gridGenerator != null)
        {
            string preferredTileId = !string.IsNullOrWhiteSpace(battleEvent.DestinationTileId)
                ? battleEvent.DestinationTileId
                : !string.IsNullOrWhiteSpace(battleEvent.TileId)
                    ? battleEvent.TileId
                    : battleEvent.OriginTileId;

            if (!string.IsNullOrWhiteSpace(preferredTileId))
            {
                HexCell cell = gridGenerator.GetCell(preferredTileId);
                if (cell != null)
                {
                    worldPosition = cell.transform.position;
                    return true;
                }
            }
        }

        if (!string.IsNullOrWhiteSpace(battleEvent.SourceUnitId) &&
            unitsByRuntimeId.TryGetValue(battleEvent.SourceUnitId, out UnitInstance sourceInstance) &&
            sourceInstance != null)
        {
            worldPosition = sourceInstance.transform.position;
            return true;
        }

        return false;
    }

    private void CreateCombatLabel(string text, Color color, Vector3 worldPosition)
    {
        GameObject labelObject = new($"CombatLabel:{text}");
        labelObject.transform.SetParent(transform, worldPositionStays: true);
        labelObject.transform.position = worldPosition;

        TextMesh textMesh = labelObject.AddComponent<TextMesh>();
        textMesh.text = text;
        textMesh.anchor = TextAnchor.MiddleCenter;
        textMesh.alignment = TextAlignment.Center;
        textMesh.characterSize = 0.12f;
        textMesh.fontSize = 52;
        textMesh.color = color;

        activeCombatLabels.Add(new CombatLabelState
        {
            LabelObject = labelObject,
            TextMesh = textMesh,
            WorldPosition = worldPosition,
            Velocity = new Vector3(0f, CombatLabelRiseSpeed, 0f),
            RemainingSeconds = CombatLabelLifetimeSeconds,
        });
    }

    private void UpdateCombatLabels()
    {
        Camera mainCamera = Camera.main;
        for (int index = activeCombatLabels.Count - 1; index >= 0; index--)
        {
            CombatLabelState label = activeCombatLabels[index];
            if (label?.LabelObject == null)
            {
                activeCombatLabels.RemoveAt(index);
                continue;
            }

            label.RemainingSeconds -= Time.deltaTime;
            if (label.RemainingSeconds <= 0f)
            {
                Destroy(label.LabelObject);
                activeCombatLabels.RemoveAt(index);
                continue;
            }

            label.WorldPosition += label.Velocity * Time.deltaTime;
            label.LabelObject.transform.position = label.WorldPosition;
            if (mainCamera != null)
            {
                label.LabelObject.transform.rotation = Quaternion.LookRotation(mainCamera.transform.forward, Vector3.up);
            }

            Color color = label.TextMesh.color;
            color.a = Mathf.Clamp01(label.RemainingSeconds / CombatLabelLifetimeSeconds);
            label.TextMesh.color = color;
        }
    }

    private void CleanupRetiredInstances()
    {
        foreach (string key in activeInstances.Keys.ToArray())
        {
            UnitInstance instance = activeInstances[key];
            if (instance == null)
            {
                activeInstances.Remove(key);
                continue;
            }

            if (!instance.IsRetiring || !instance.IsRetirementComplete())
            {
                continue;
            }

            Destroy(instance.gameObject);
            activeInstances.Remove(key);
        }
    }

    private void UpdateBattleFacing(IReadOnlyDictionary<string, UnitInstance> unitsByRuntimeId)
    {
        foreach (UnitInstance instance in unitsByRuntimeId.Values)
        {
            if (instance == null)
            {
                instance?.ClearBattleTarget();
                continue;
            }

            if (!string.IsNullOrWhiteSpace(instance.CurrentTargetUnitId) &&
                unitsByRuntimeId.TryGetValue(instance.CurrentTargetUnitId, out UnitInstance targetInstance) &&
                targetInstance != null)
            {
                instance.SetBattleTarget(targetInstance.transform.position);
            }
            else if (instance.CurrentActionType == BattleUnitActionType.Attacking &&
                     gridGenerator != null &&
                     !string.IsNullOrWhiteSpace(instance.CurrentActionDestinationTileId))
            {
                HexCell actionDestinationCell = gridGenerator.GetCell(instance.CurrentActionDestinationTileId);
                if (actionDestinationCell != null)
                {
                    instance.SetBattleTarget(actionDestinationCell.transform.position);
                }
                else
                {
                    instance.ClearBattleTarget();
                }
            }
            else
            {
                instance.ClearBattleTarget();
            }
        }
    }

    private bool TryResolveRenderAnchor(MatchSnapshot snapshot, FieldSlotState slotState, out HexCell cell, out Vector3 worldPosition)
    {
        cell = null;
        worldPosition = Vector3.zero;

        if (gridGenerator == null || snapshot?.Arena?.Config == null || slotState == null)
        {
            return false;
        }

        cell = gridGenerator.GetCell(slotState.TileId);
        if (cell == null)
        {
            return false;
        }

        worldPosition = cell.transform.position;
        UnitRuntimeState unitState = slotState.Unit;
        if (unitState != null)
        {
            bool hasActionMovement = unitState.CurrentActionType == BattleUnitActionType.Moving &&
                !string.IsNullOrWhiteSpace(unitState.CurrentActionOriginTileId) &&
                !string.IsNullOrWhiteSpace(unitState.CurrentActionDestinationTileId);
            bool hasLegacyMovement = unitState.IsMoving &&
                !string.IsNullOrWhiteSpace(unitState.MovementTargetTileId) &&
                !string.IsNullOrWhiteSpace(unitState.MovementStartTileId);

            string startTileId = hasActionMovement ? unitState.CurrentActionOriginTileId : unitState.MovementStartTileId;
            string targetTileId = hasActionMovement ? unitState.CurrentActionDestinationTileId : unitState.MovementTargetTileId;
            float progress01 = hasActionMovement ? unitState.CurrentActionProgress01 : unitState.MovementProgress01;

            if ((hasActionMovement || hasLegacyMovement) &&
                !string.IsNullOrWhiteSpace(startTileId) &&
                !string.IsNullOrWhiteSpace(targetTileId))
            {
                HexCell startCell = gridGenerator.GetCell(startTileId);
                HexCell targetCell = gridGenerator.GetCell(targetTileId);
                if (startCell == null || targetCell == null)
                {
                    return true;
                }

                worldPosition = Vector3.Lerp(
                    startCell.transform.position,
                    targetCell.transform.position,
                    Mathf.Clamp01(progress01));
            }
        }

        return true;
    }

    private UnitInstance SpawnUnit(string playerId, UnitDataSO unitData, HexCell cell, FieldSlotState slotState, Vector3 worldPosition, int snapshotTick)
    {
        GameObject unitPrefab = UnitVisualFactory.GetUnitPrefab(unitData.UnitSizeClassification);
        if (unitPrefab == null)
        {
            return null;
        }

        GameObject instanceObj = Instantiate(unitPrefab, worldPosition, Quaternion.identity, transform);
        instanceObj.transform.position = worldPosition;
        Utils.StandOnTop(instanceObj, GetSurfaceBoundsAt(cell, worldPosition));

        UnitInstance instance = instanceObj.GetComponent<UnitInstance>();
        if (instance == null)
        {
            instance = instanceObj.AddComponent<UnitInstance>();
        }

        instance.Init(unitData, slotState.Unit.Level, slotState.Unit.CurrentHealth, slotState.Unit.RuntimeUnitId);
        instance.UpdateRuntimeState(slotState.Unit, snapshotTick);
        instance.BindToField(slotState.TileId, cell.transform);

        UnitDragInteraction interaction = instanceObj.GetComponent<UnitDragInteraction>();
        if (interaction == null)
        {
            interaction = instanceObj.AddComponent<UnitDragInteraction>();
        }

        interaction.Initialize(instance);

        UnitHealthBar healthBar = instanceObj.GetComponent<UnitHealthBar>();
        if (healthBar == null)
        {
            healthBar = instanceObj.AddComponent<UnitHealthBar>();
        }

        bool isEnemy = !string.Equals(playerId, GameManager.LocalPlayerId, StringComparison.Ordinal);
        healthBar.Initialize(instance, healthBarSettings, isEnemy);
        return instance;
    }

    private void UpdateInstance(UnitInstance instance, UnitDataSO unitData, FieldSlotState slotState, HexCell cell, Vector3 worldPosition, int snapshotTick)
    {
        if (instance == null || slotState?.Unit == null)
        {
            return;
        }

        if (instance.unit != unitData)
        {
            instance.Init(unitData, slotState.Unit.Level, slotState.Unit.CurrentHealth, slotState.Unit.RuntimeUnitId);
        }

        instance.UpdateRuntimeState(slotState.Unit, snapshotTick);
        instance.BindToField(slotState.TileId, cell.transform);
        instance.transform.position = worldPosition;
        Utils.StandOnTop(instance.gameObject, GetSurfaceBoundsAt(cell, worldPosition));

        UnitHealthBar healthBar = instance.GetComponent<UnitHealthBar>();
        if (healthBar != null)
        {
            healthBar.Refresh(force: true);
        }
    }

    private Bounds GetSurfaceBoundsAt(HexCell cell, Vector3 worldPosition)
    {
        Bounds bounds = cell.GetSurfaceWorldBounds();
        Vector3 offset = worldPosition - cell.transform.position;
        bounds.center += offset;
        return bounds;
    }

    private void ClearFieldVisuals()
    {
        foreach (UnitInstance instance in activeInstances.Values.Where(instance => instance != null))
        {
            Destroy(instance.gameObject);
        }

        activeInstances.Clear();
        foreach (CombatLabelState combatLabel in activeCombatLabels.Where(label => label?.LabelObject != null))
        {
            Destroy(combatLabel.LabelObject);
        }

        activeCombatLabels.Clear();
        processedBattleEventKeys.Clear();
    }

    private static string BuildRenderKey(string playerId, string runtimeUnitId)
    {
        return $"{playerId}:{runtimeUnitId}";
    }
}
