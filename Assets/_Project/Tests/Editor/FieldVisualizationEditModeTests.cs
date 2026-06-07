using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using AIR.Shared.GameSession;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using System.Collections;

public class FieldVisualizationEditModeTests
{
    private const string BisonAssetPath = "Assets/_Project/Data/Units/Bison.asset";

    private readonly List<UnityEngine.Object> createdObjects = new();

    [TearDown]
    public void TearDown()
    {
        for (int index = createdObjects.Count - 1; index >= 0; index--)
        {
            if (createdObjects[index] != null)
            {
                UnityEngine.Object.DestroyImmediate(createdObjects[index]);
            }
        }

        createdObjects.Clear();
    }

    [Test]
    public void FieldManager_RendersMovingUnitBetweenTilesFromActionStateMetadata()
    {
        UnitDataSO bison = LoadUnitData();
        FieldManager fieldManager = CreateFieldManager(out HexGridGenerator gridGenerator);
        CreateGridCell(gridGenerator, "0,0", new Vector3(0f, 0f, 0f));
        CreateGridCell(gridGenerator, "0,1", new Vector3(0f, 0f, 3f));
        SetUnitLookup(fieldManager, bison);

        MatchSnapshot snapshot = new()
        {
            TickIndex = 111,
            Arena = BuildArena("0,0", "0,1"),
            Players =
            {
                new PlayerSnapshot
                {
                    PlayerId = GameManager.LocalPlayerId,
                    Field = new FieldState
                    {
                        Capacity = 2,
                        Slots = new List<FieldSlotState>
                        {
                            new()
                            {
                                TileId = "0,0",
                                Unit = new UnitRuntimeState
                                {
                                    RuntimeUnitId = "unit-a",
                                    UnitKey = "Bison",
                                    Container = UnitContainerType.Field,
                                    FieldTileId = "0,0",
                                    CurrentTargetUnitId = "legacy-target",
                                    CurrentActionType = BattleUnitActionType.Moving,
                                    CurrentActionTargetUnitId = "unit-b",
                                    CurrentActionOriginTileId = "0,0",
                                    CurrentActionDestinationTileId = "0,1",
                                    CurrentActionProgress01 = 0.5f,
                                    CurrentHealth = 120,
                                    MaxHealth = 120,
                                },
                            },
                            new() { TileId = "0,1" },
                        },
                    },
                },
            },
        };

        InvokeHandleSnapshotUpdated(fieldManager, snapshot);

        Dictionary<string, UnitInstance> activeInstances = GetPrivateField<Dictionary<string, UnitInstance>>(fieldManager, "activeInstances");
        UnitInstance instance = activeInstances.Values.Single();
        Assert.That(instance.transform.position.z, Is.GreaterThan(1.0f).And.LessThan(2.0f), "Moving unit should render between the action origin and destination tiles.");
        Assert.That(instance.CurrentTargetUnitId, Is.EqualTo("unit-b"), "Rendered instance should prefer action-state target metadata over legacy target metadata.");
        Assert.That(instance.CurrentActionType, Is.EqualTo(BattleUnitActionType.Moving), "Rendered instance should retain authoritative movement action state.");
    }

    [Test]
    public void FieldManager_RendersMovingUnitBetweenTilesFromSnapshotMetadata()
    {
        UnitDataSO bison = LoadUnitData();
        FieldManager fieldManager = CreateFieldManager(out HexGridGenerator gridGenerator);
        CreateGridCell(gridGenerator, "0,0", new Vector3(0f, 0f, 0f));
        CreateGridCell(gridGenerator, "0,1", new Vector3(0f, 0f, 3f));
        SetUnitLookup(fieldManager, bison);

        MatchSnapshot snapshot = new()
        {
            TickIndex = 101,
            Arena = BuildArena("0,0", "0,1"),
            Players =
            {
                new PlayerSnapshot
                {
                    PlayerId = GameManager.LocalPlayerId,
                    Field = new FieldState
                    {
                        Capacity = 2,
                        Slots = new List<FieldSlotState>
                        {
                            new()
                            {
                                TileId = "0,0",
                                Unit = new UnitRuntimeState
                                {
                                    RuntimeUnitId = "unit-a",
                                    UnitKey = "Bison",
                                    Container = UnitContainerType.Field,
                                    FieldTileId = "0,0",
                                    CurrentTargetUnitId = "unit-b",
                                    IsMoving = true,
                                    MovementStartTileId = "0,0",
                                    MovementTargetTileId = "0,1",
                                    MovementProgress01 = 0.5f,
                                    CurrentHealth = 120,
                                    MaxHealth = 120,
                                },
                            },
                            new() { TileId = "0,1" },
                        },
                    },
                },
            },
        };

        InvokeHandleSnapshotUpdated(fieldManager, snapshot);

        Dictionary<string, UnitInstance> activeInstances = GetPrivateField<Dictionary<string, UnitInstance>>(fieldManager, "activeInstances");
        UnitInstance instance = activeInstances.Values.Single();
        Assert.That(instance.transform.position.z, Is.GreaterThan(1.0f).And.LessThan(2.0f), "Moving unit should render between the start and target tiles.");
        Assert.That(instance.CurrentTargetUnitId, Is.EqualTo("unit-b"), "Rendered instance should preserve target metadata from the snapshot.");
    }

    [Test]
    public void FieldManager_DeathEventStartsRetirementAndCreatesCombatLabel()
    {
        UnitDataSO bison = LoadUnitData();
        FieldManager fieldManager = CreateFieldManager(out HexGridGenerator gridGenerator);
        CreateGridCell(gridGenerator, "0,0", new Vector3(0f, 0f, 0f));
        SetUnitLookup(fieldManager, bison);

        MatchSnapshot aliveSnapshot = new()
        {
            TickIndex = 200,
            Arena = BuildArena("0,0"),
            Players =
            {
                new PlayerSnapshot
                {
                    PlayerId = GameManager.LocalPlayerId,
                    Field = new FieldState
                    {
                        Capacity = 1,
                        Slots = new List<FieldSlotState>
                        {
                            new()
                            {
                                TileId = "0,0",
                                Unit = new UnitRuntimeState
                                {
                                    RuntimeUnitId = "unit-a",
                                    UnitKey = "Bison",
                                    Container = UnitContainerType.Field,
                                    FieldTileId = "0,0",
                                    CurrentHealth = 120,
                                    MaxHealth = 120,
                                },
                            },
                        },
                    },
                },
            },
        };

        InvokeHandleSnapshotUpdated(fieldManager, aliveSnapshot);

        MatchSnapshot deathSnapshot = new()
        {
            TickIndex = 201,
            Arena = BuildArena("0,0"),
            BattleInstance = new BattleInstanceState
            {
                RecentEvents = new List<BattleEventState>
                {
                    new()
                    {
                        TickIndex = 201,
                        EventType = BattleEventType.Death,
                        SourceUnitId = "enemy",
                        TargetUnitId = "unit-a",
                        TileId = "0,0",
                    },
                },
            },
            Players =
            {
                new PlayerSnapshot
                {
                    PlayerId = GameManager.LocalPlayerId,
                    Field = new FieldState
                    {
                        Capacity = 1,
                        Slots = new List<FieldSlotState>
                        {
                            new() { TileId = "0,0" },
                        },
                    },
                },
            },
        };

        InvokeHandleSnapshotUpdated(fieldManager, deathSnapshot);

        Dictionary<string, UnitInstance> activeInstances = GetPrivateField<Dictionary<string, UnitInstance>>(fieldManager, "activeInstances");
        UnitInstance retiringUnit = activeInstances.Values.Single();
        Assert.That(retiringUnit.IsRetiring, Is.True, "Death events should begin unit retirement instead of removing the unit immediately.");

        var activeLabels = GetPrivateField<System.Collections.IList>(fieldManager, "activeCombatLabels");
        Assert.That(activeLabels.Count, Is.GreaterThan(0), "Death events should create at least one combat label for visualization.");
    }

    [Test]
    public void FieldManager_DamageEventUsesDestinationTileMetadataForLabelPlacement()
    {
        UnitDataSO bison = LoadUnitData();
        FieldManager fieldManager = CreateFieldManager(out HexGridGenerator gridGenerator);
        CreateGridCell(gridGenerator, "0,0", new Vector3(0f, 0f, 0f));
        CreateGridCell(gridGenerator, "0,1", new Vector3(0f, 0f, 4f));
        SetUnitLookup(fieldManager, bison);

        MatchSnapshot snapshot = new()
        {
            TickIndex = 300,
            Arena = BuildArena("0,0", "0,1"),
            BattleInstance = new BattleInstanceState
            {
                RecentEvents = new List<BattleEventState>
                {
                    new()
                    {
                        EventSequenceId = 42,
                        TickIndex = 300,
                        ActionType = BattleActionType.ResolveBasicAttackHit,
                        DamageSourceType = BattleDamageSourceType.BasicAttack,
                        EventType = BattleEventType.Damage,
                        SourceUnitId = "unit-a",
                        TargetUnitId = "missing-target",
                        OriginTileId = "0,0",
                        DestinationTileId = "0,1",
                        Amount = 7,
                    },
                },
            },
            Players =
            {
                new PlayerSnapshot
                {
                    PlayerId = GameManager.LocalPlayerId,
                    Field = new FieldState
                    {
                        Capacity = 2,
                        Slots = new List<FieldSlotState>
                        {
                            new()
                            {
                                TileId = "0,0",
                                Unit = new UnitRuntimeState
                                {
                                    RuntimeUnitId = "unit-a",
                                    UnitKey = "Bison",
                                    Container = UnitContainerType.Field,
                                    FieldTileId = "0,0",
                                    CurrentHealth = 120,
                                    MaxHealth = 120,
                                },
                            },
                            new() { TileId = "0,1" },
                        },
                    },
                },
            },
        };

        InvokeHandleSnapshotUpdated(fieldManager, snapshot);

        var activeLabels = GetPrivateField<System.Collections.IList>(fieldManager, "activeCombatLabels");
        Assert.That(activeLabels.Count, Is.EqualTo(1), "Damage events should create a combat label.");

        object labelState = activeLabels[0];
        FieldInfo worldPositionField = labelState.GetType().GetField("WorldPosition", BindingFlags.Instance | BindingFlags.Public);
        Assert.That(worldPositionField, Is.Not.Null, "Expected combat label world position field.");
        Vector3 worldPosition = (Vector3)worldPositionField.GetValue(labelState);
        Assert.That(worldPosition.z, Is.GreaterThan(3.5f), "Damage label should use destination tile metadata when no target instance is available.");
    }

    [Test]
    public void FieldManager_AttackStartedEventTriggersWindupPresentation()
    {
        UnitDataSO bison = LoadUnitData();
        FieldManager fieldManager = CreateFieldManager(out HexGridGenerator gridGenerator);
        CreateGridCell(gridGenerator, "0,0", new Vector3(0f, 0f, 0f));
        CreateGridCell(gridGenerator, "0,1", new Vector3(0f, 0f, 3f));
        SetUnitLookup(fieldManager, bison);

        MatchSnapshot snapshot = new()
        {
            TickIndex = 400,
            Arena = BuildArena("0,0", "0,1"),
            BattleInstance = new BattleInstanceState
            {
                RecentEvents = new List<BattleEventState>
                {
                    new()
                    {
                        EventSequenceId = 88,
                        TickIndex = 400,
                        ActionType = BattleActionType.StartBasicAttack,
                        EventType = BattleEventType.AttackStarted,
                        SourceUnitId = "unit-a",
                        TargetUnitId = "unit-b",
                        OriginTileId = "0,0",
                        DestinationTileId = "0,1",
                    },
                },
            },
            Players =
            {
                new PlayerSnapshot
                {
                    PlayerId = GameManager.LocalPlayerId,
                    Field = new FieldState
                    {
                        Capacity = 2,
                        Slots = new List<FieldSlotState>
                        {
                            new()
                            {
                                TileId = "0,0",
                                Unit = new UnitRuntimeState
                                {
                                    RuntimeUnitId = "unit-a",
                                    UnitKey = "Bison",
                                    Container = UnitContainerType.Field,
                                    FieldTileId = "0,0",
                                    CurrentHealth = 120,
                                    MaxHealth = 120,
                                },
                            },
                            new()
                            {
                                TileId = "0,1",
                                Unit = new UnitRuntimeState
                                {
                                    RuntimeUnitId = "unit-b",
                                    UnitKey = "Bison",
                                    Container = UnitContainerType.Field,
                                    FieldTileId = "0,1",
                                    CurrentHealth = 120,
                                    MaxHealth = 120,
                                },
                            },
                        },
                    },
                },
            },
        };

        InvokeHandleSnapshotUpdated(fieldManager, snapshot);

        Dictionary<string, UnitInstance> activeInstances = GetPrivateField<Dictionary<string, UnitInstance>>(fieldManager, "activeInstances");
        UnitInstance instance = activeInstances.Values.First(unit => unit.RuntimeUnitId == "unit-a");
        FieldInfo windupTimerField = typeof(UnitInstance).GetField("attackWindupPresentationTimer", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(windupTimerField, Is.Not.Null, "Expected UnitInstance attack windup presentation timer.");
        float windupTimer = (float)windupTimerField.GetValue(instance);
        Assert.That(windupTimer, Is.GreaterThan(0f), "AttackStarted events should trigger transient attack windup presentation even without live action-state progress.");
    }

    [Test]
    public void FieldManager_MissingDefeatedUnitBeginsRetirementFromBattleSnapshot()
    {
        UnitDataSO bison = LoadUnitData();
        FieldManager fieldManager = CreateFieldManager(out HexGridGenerator gridGenerator);
        CreateGridCell(gridGenerator, "0,0", new Vector3(0f, 0f, 0f));
        SetUnitLookup(fieldManager, bison);

        MatchSnapshot aliveSnapshot = new()
        {
            TickIndex = 500,
            Arena = BuildArena("0,0"),
            Players =
            {
                new PlayerSnapshot
                {
                    PlayerId = "enemy",
                    Field = new FieldState
                    {
                        Capacity = 1,
                        Slots = new List<FieldSlotState>
                        {
                            new()
                            {
                                TileId = "0,0",
                                Unit = new UnitRuntimeState
                                {
                                    RuntimeUnitId = "enemy-unit",
                                    UnitKey = "Bison",
                                    Container = UnitContainerType.Field,
                                    FieldTileId = "0,0",
                                    CurrentHealth = 120,
                                    MaxHealth = 120,
                                },
                            },
                        },
                    },
                },
            },
        };

        InvokeHandleSnapshotUpdated(fieldManager, aliveSnapshot);

        MatchSnapshot missingDefeatedSnapshot = new()
        {
            TickIndex = 501,
            Arena = BuildArena("0,0"),
            BattleInstance = new BattleInstanceState
            {
                Units = new List<BattleUnitState>
                {
                    new()
                    {
                        RuntimeUnitId = "enemy-unit",
                        OwnerPlayerId = "enemy",
                        UnitKey = "Bison",
                        CurrentTileId = "0,0",
                        IsAlive = false,
                    },
                },
            },
            Players =
            {
                new PlayerSnapshot
                {
                    PlayerId = "enemy",
                    Field = new FieldState
                    {
                        Capacity = 1,
                        Slots = new List<FieldSlotState>
                        {
                            new() { TileId = "0,0" },
                        },
                    },
                },
            },
        };

        InvokeHandleSnapshotUpdated(fieldManager, missingDefeatedSnapshot);

        Dictionary<string, UnitInstance> activeInstances = GetPrivateField<Dictionary<string, UnitInstance>>(fieldManager, "activeInstances");
        UnitInstance retiringUnit = activeInstances.Values.Single();
        Assert.That(retiringUnit.IsRetiring, Is.True, "Units missing from the rendered field should still begin retirement when the authoritative battle snapshot marks them dead.");
    }

    [Test]
    public void UnitInstance_RetirementDoesNotRestartAfterCompletion()
    {
        UnitDataSO bison = LoadUnitData();
        GameObject unitObject = new("RetiringUnit");
        createdObjects.Add(unitObject);

        UnitInstance instance = unitObject.AddComponent<UnitInstance>();
        instance.Init(bison, runtimeUnitId: "unit-a");
        instance.BeginRetirement();

        FieldInfo retirementTimerField = typeof(UnitInstance).GetField("retirementTimer", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(retirementTimerField, Is.Not.Null, "Expected UnitInstance retirement timer field.");

        retirementTimerField.SetValue(instance, 0f);
        instance.BeginRetirement();

        Assert.That(instance.IsRetiring, Is.True, "Completed retirements should remain pending cleanup instead of restarting.");
        Assert.That(instance.IsRetirementComplete(), Is.True, "Completed retirements should stay eligible for cleanup.");
        Assert.That((float)retirementTimerField.GetValue(instance), Is.EqualTo(0f), "Retirement completion should not be re-armed by later snapshots.");
    }

    [Test]
    public void MatchMusicDirector_InitialPostBattleLoopVariantIsA()
    {
        var method = typeof(MatchMusicDirector).GetMethod("GetInitialPostBattleVariant", BindingFlags.Static | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null, "Expected MatchMusicDirector initial post-battle variant helper.");
        PostBattleLoopVariant variant = (PostBattleLoopVariant)method.Invoke(null, Array.Empty<object>());
        Assert.That(variant, Is.EqualTo(PostBattleLoopVariant.A), "Post-battle music should begin from loop A before alternating.");
    }

    private FieldManager CreateFieldManager(out HexGridGenerator gridGenerator)
    {
        Camera camera = new GameObject("Main Camera").AddComponent<Camera>();
        camera.tag = "MainCamera";
        createdObjects.Add(camera.gameObject);

        Canvas canvas = new GameObject("Canvas").AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        createdObjects.Add(canvas.gameObject);

        GameObject gridObject = new("GridRoot");
        createdObjects.Add(gridObject);

        gridGenerator = gridObject.AddComponent<HexGridGenerator>();
        FieldManager fieldManager = gridObject.AddComponent<FieldManager>();
        SetPrivateField(fieldManager, "gridGenerator", gridGenerator);
        return fieldManager;
    }

    private HexCell CreateGridCell(HexGridGenerator gridGenerator, string tileId, Vector3 position)
    {
        GameObject cellObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cellObject.name = $"Cell:{tileId}";
        cellObject.transform.SetParent(gridGenerator.transform, false);
        cellObject.transform.position = position;
        createdObjects.Add(cellObject);

        HexCell cell = cellObject.AddComponent<HexCell>();
        ParseTile(tileId, out int q, out int r);
        cell.Init(new ArenaTileState { TileId = tileId, Q = q, R = r });

        Dictionary<string, HexCell> cellsByTileId = GetPrivateField<Dictionary<string, HexCell>>(gridGenerator, "cellsByTileId");
        cellsByTileId[tileId] = cell;
        return cell;
    }

    private static ArenaState BuildArena(params string[] tileIds)
    {
        ArenaState arena = new() { Config = new MatchArenaConfig { Width = 2, Height = 2 } };
        foreach (string tileId in tileIds)
        {
            ParseTile(tileId, out int q, out int r);
            arena.Tiles.Add(new ArenaTileState { TileId = tileId, Q = q, R = r });
        }

        return arena;
    }

    private static void ParseTile(string tileId, out int q, out int r)
    {
        if (!HexArenaUtils.TryParseTileId(tileId, out q, out r))
        {
            throw new InvalidOperationException($"Could not parse tile id '{tileId}'.");
        }
    }

    private static UnitDataSO LoadUnitData()
    {
        UnitDataSO unitData = AssetDatabase.LoadAssetAtPath<UnitDataSO>(BisonAssetPath);
        Assert.That(unitData, Is.Not.Null, "Expected the Bison unit asset to exist for visualization tests.");
        return unitData;
    }

    private static void SetUnitLookup(FieldManager fieldManager, UnitDataSO unitData)
    {
        Dictionary<string, UnitDataSO> unitLookup = GetPrivateField<Dictionary<string, UnitDataSO>>(fieldManager, "unitLookup");
        unitLookup[unitData.unitKey] = unitData;
    }

    private static void InvokeHandleSnapshotUpdated(FieldManager fieldManager, MatchSnapshot snapshot)
    {
        MethodInfo method = typeof(FieldManager).GetMethod("HandleSnapshotUpdated", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null, "Expected FieldManager.HandleSnapshotUpdated to exist.");
        method.Invoke(fieldManager, new object[] { snapshot });
    }

    private static T GetPrivateField<T>(object target, string fieldName)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, $"Expected private field '{fieldName}' on {target.GetType().Name}.");
        return (T)field.GetValue(target);
    }

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, $"Expected private field '{fieldName}' on {target.GetType().Name}.");
        field.SetValue(target, value);
    }
}
