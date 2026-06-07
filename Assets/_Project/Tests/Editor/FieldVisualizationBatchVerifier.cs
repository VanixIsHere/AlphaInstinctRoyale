using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using AIR.Shared.GameSession;
using UnityEditor;
using UnityEngine;

public static class FieldVisualizationBatchVerifier
{
    private const string BisonAssetPath = "Assets/_Project/Data/Units/Bison.asset";
    private static bool autoRunScheduled;

    [InitializeOnLoadMethod]
    private static void MaybeRunFromBatchMode()
    {
        if (autoRunScheduled || !Application.isBatchMode)
        {
            return;
        }

        string[] args = Environment.GetCommandLineArgs();
        bool shouldRun = args.Any(arg => string.Equals(arg, "-fieldVisualizationOutput", StringComparison.OrdinalIgnoreCase));
        if (!shouldRun)
        {
            return;
        }

        autoRunScheduled = true;
        EditorApplication.delayCall += Run;
    }

    public static void Run()
    {
        string outputPath = GetOutputPath();
        try
        {
            VerifyMovingUnitInterpolationFromActionState();
            VerifyMovingUnitInterpolation();
            VerifyAttackStartedEventTriggersWindupPresentation();
            VerifyDeathEventRetirementAndLabel();
            VerifyMissingDefeatedUnitBeginsRetirementFromBattleSnapshot();
            VerifyDamageEventDestinationTilePlacement();
            VerifyInitialPostBattleLoopVariantIsA();
            File.WriteAllText(outputPath, "PASS");
            Debug.Log($"[FieldVisualizationBatchVerifier] PASS -> {outputPath}");
        }
        catch (Exception ex)
        {
            File.WriteAllText(outputPath, $"FAIL{Environment.NewLine}{ex}");
            Debug.LogError($"[FieldVisualizationBatchVerifier] FAIL -> {outputPath}{Environment.NewLine}{ex}");
            EditorApplication.Exit(1);
            return;
        }

        EditorApplication.Exit(0);
    }

    private static void VerifyMovingUnitInterpolation()
    {
        using VerificationContext context = new();
        UnitDataSO bison = LoadUnitData();
        FieldManager fieldManager = context.CreateFieldManager(out HexGridGenerator gridGenerator);
        context.CreateGridCell(gridGenerator, "0,0", new Vector3(0f, 0f, 0f));
        context.CreateGridCell(gridGenerator, "0,1", new Vector3(0f, 0f, 3f));
        context.SetUnitLookup(fieldManager, bison);

        MatchSnapshot snapshot = new()
        {
            TickIndex = 101,
            Arena = VerificationContext.BuildArena("0,0", "0,1"),
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

        VerificationContext.InvokeHandleSnapshotUpdated(fieldManager, snapshot);
        Dictionary<string, UnitInstance> activeInstances = VerificationContext.GetPrivateField<Dictionary<string, UnitInstance>>(fieldManager, "activeInstances");
        UnitInstance instance = activeInstances.Values.Single();
        Assert(instance.transform.position.z > 1.0f && instance.transform.position.z < 2.0f, "Moving unit should render between the start and target tiles.");
        Assert(instance.CurrentTargetUnitId == "unit-b", "Rendered instance should preserve target metadata from the snapshot.");
    }

    private static void VerifyMovingUnitInterpolationFromActionState()
    {
        using VerificationContext context = new();
        UnitDataSO bison = LoadUnitData();
        FieldManager fieldManager = context.CreateFieldManager(out HexGridGenerator gridGenerator);
        context.CreateGridCell(gridGenerator, "0,0", new Vector3(0f, 0f, 0f));
        context.CreateGridCell(gridGenerator, "0,1", new Vector3(0f, 0f, 3f));
        context.SetUnitLookup(fieldManager, bison);

        MatchSnapshot snapshot = new()
        {
            TickIndex = 111,
            Arena = VerificationContext.BuildArena("0,0", "0,1"),
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

        VerificationContext.InvokeHandleSnapshotUpdated(fieldManager, snapshot);
        Dictionary<string, UnitInstance> activeInstances = VerificationContext.GetPrivateField<Dictionary<string, UnitInstance>>(fieldManager, "activeInstances");
        UnitInstance instance = activeInstances.Values.Single();
        Assert(instance.transform.position.z > 1.0f && instance.transform.position.z < 2.0f, "Moving unit should render between the action origin and destination tiles.");
        Assert(instance.CurrentTargetUnitId == "unit-b", "Rendered instance should prefer action-state target metadata over legacy target metadata.");
        Assert(instance.CurrentActionType == BattleUnitActionType.Moving, "Rendered instance should preserve authoritative movement action state.");
    }

    private static void VerifyDeathEventRetirementAndLabel()
    {
        using VerificationContext context = new();
        UnitDataSO bison = LoadUnitData();
        FieldManager fieldManager = context.CreateFieldManager(out HexGridGenerator gridGenerator);
        context.CreateGridCell(gridGenerator, "0,0", new Vector3(0f, 0f, 0f));
        context.SetUnitLookup(fieldManager, bison);

        MatchSnapshot aliveSnapshot = new()
        {
            TickIndex = 200,
            Arena = VerificationContext.BuildArena("0,0"),
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

        VerificationContext.InvokeHandleSnapshotUpdated(fieldManager, aliveSnapshot);

        MatchSnapshot deathSnapshot = new()
        {
            TickIndex = 201,
            Arena = VerificationContext.BuildArena("0,0"),
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

        VerificationContext.InvokeHandleSnapshotUpdated(fieldManager, deathSnapshot);
        Dictionary<string, UnitInstance> activeInstances = VerificationContext.GetPrivateField<Dictionary<string, UnitInstance>>(fieldManager, "activeInstances");
        UnitInstance retiringUnit = activeInstances.Values.Single();
        Assert(retiringUnit.IsRetiring, "Death events should begin unit retirement instead of removing the unit immediately.");

        var activeLabels = VerificationContext.GetPrivateField<System.Collections.IList>(fieldManager, "activeCombatLabels");
        Assert(activeLabels.Count > 0, "Death events should create at least one combat label for visualization.");
    }

    private static void VerifyAttackStartedEventTriggersWindupPresentation()
    {
        using VerificationContext context = new();
        UnitDataSO bison = LoadUnitData();
        FieldManager fieldManager = context.CreateFieldManager(out HexGridGenerator gridGenerator);
        context.CreateGridCell(gridGenerator, "0,0", new Vector3(0f, 0f, 0f));
        context.CreateGridCell(gridGenerator, "0,1", new Vector3(0f, 0f, 3f));
        context.SetUnitLookup(fieldManager, bison);

        MatchSnapshot snapshot = new()
        {
            TickIndex = 400,
            Arena = VerificationContext.BuildArena("0,0", "0,1"),
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

        VerificationContext.InvokeHandleSnapshotUpdated(fieldManager, snapshot);
        Dictionary<string, UnitInstance> activeInstances = VerificationContext.GetPrivateField<Dictionary<string, UnitInstance>>(fieldManager, "activeInstances");
        UnitInstance instance = activeInstances.Values.First(unit => unit.RuntimeUnitId == "unit-a");
        FieldInfo windupTimerField = typeof(UnitInstance).GetField("attackWindupPresentationTimer", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert(windupTimerField != null, "Expected UnitInstance attack windup presentation timer.");
        float windupTimer = (float)windupTimerField.GetValue(instance);
        Assert(windupTimer > 0f, "AttackStarted events should trigger transient attack windup presentation.");
    }

    private static void VerifyMissingDefeatedUnitBeginsRetirementFromBattleSnapshot()
    {
        using VerificationContext context = new();
        UnitDataSO bison = LoadUnitData();
        FieldManager fieldManager = context.CreateFieldManager(out HexGridGenerator gridGenerator);
        context.CreateGridCell(gridGenerator, "0,0", new Vector3(0f, 0f, 0f));
        context.SetUnitLookup(fieldManager, bison);

        MatchSnapshot aliveSnapshot = new()
        {
            TickIndex = 500,
            Arena = VerificationContext.BuildArena("0,0"),
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

        VerificationContext.InvokeHandleSnapshotUpdated(fieldManager, aliveSnapshot);

        MatchSnapshot missingDefeatedSnapshot = new()
        {
            TickIndex = 501,
            Arena = VerificationContext.BuildArena("0,0"),
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

        VerificationContext.InvokeHandleSnapshotUpdated(fieldManager, missingDefeatedSnapshot);
        Dictionary<string, UnitInstance> activeInstances = VerificationContext.GetPrivateField<Dictionary<string, UnitInstance>>(fieldManager, "activeInstances");
        UnitInstance retiringUnit = activeInstances.Values.Single();
        Assert(retiringUnit.IsRetiring, "Units missing from the rendered field should still retire when the battle snapshot marks them dead.");
    }

    private static void VerifyInitialPostBattleLoopVariantIsA()
    {
        MethodInfo method = typeof(MatchMusicDirector).GetMethod("GetInitialPostBattleVariant", BindingFlags.Static | BindingFlags.NonPublic);
        Assert(method != null, "Expected MatchMusicDirector initial post-battle variant helper.");
        PostBattleLoopVariant variant = (PostBattleLoopVariant)method.Invoke(null, Array.Empty<object>());
        Assert(variant == PostBattleLoopVariant.A, "Post-battle music should begin from loop A before alternating.");
    }

    private static void VerifyDamageEventDestinationTilePlacement()
    {
        using VerificationContext context = new();
        UnitDataSO bison = LoadUnitData();
        FieldManager fieldManager = context.CreateFieldManager(out HexGridGenerator gridGenerator);
        context.CreateGridCell(gridGenerator, "0,0", new Vector3(0f, 0f, 0f));
        context.CreateGridCell(gridGenerator, "0,1", new Vector3(0f, 0f, 4f));
        context.SetUnitLookup(fieldManager, bison);

        MatchSnapshot snapshot = new()
        {
            TickIndex = 300,
            Arena = VerificationContext.BuildArena("0,0", "0,1"),
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

        VerificationContext.InvokeHandleSnapshotUpdated(fieldManager, snapshot);
        var activeLabels = VerificationContext.GetPrivateField<System.Collections.IList>(fieldManager, "activeCombatLabels");
        Assert(activeLabels.Count == 1, "Damage events should create a combat label.");

        object labelState = activeLabels[0];
        FieldInfo worldPositionField = labelState.GetType().GetField("WorldPosition", BindingFlags.Instance | BindingFlags.Public);
        Assert(worldPositionField != null, "Expected combat label world position field.");
        Vector3 worldPosition = (Vector3)worldPositionField.GetValue(labelState);
        Assert(worldPosition.z > 3.5f, "Damage label should use destination tile metadata when no target instance is available.");
    }

    private static UnitDataSO LoadUnitData()
    {
        UnitDataSO unitData = AssetDatabase.LoadAssetAtPath<UnitDataSO>(BisonAssetPath);
        Assert(unitData != null, "Expected the Bison unit asset to exist for visualization verification.");
        return unitData;
    }

    private static string GetOutputPath()
    {
        string[] args = Environment.GetCommandLineArgs();
        for (int index = 0; index < args.Length - 1; index++)
        {
            if (string.Equals(args[index], "-fieldVisualizationOutput", StringComparison.OrdinalIgnoreCase))
            {
                return args[index + 1];
            }
        }

        return Path.Combine(Environment.CurrentDirectory, "FieldVisualizationVerification.txt");
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private sealed class VerificationContext : IDisposable
    {
        private readonly List<UnityEngine.Object> createdObjects = new();

        public FieldManager CreateFieldManager(out HexGridGenerator gridGenerator)
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

        public HexCell CreateGridCell(HexGridGenerator gridGenerator, string tileId, Vector3 position)
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

        public void SetUnitLookup(FieldManager fieldManager, UnitDataSO unitData)
        {
            Dictionary<string, UnitDataSO> unitLookup = GetPrivateField<Dictionary<string, UnitDataSO>>(fieldManager, "unitLookup");
            unitLookup[unitData.unitKey] = unitData;
        }

        public void Dispose()
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

        public static ArenaState BuildArena(params string[] tileIds)
        {
            ArenaState arena = new() { Config = new MatchArenaConfig { Width = 2, Height = 2 } };
            foreach (string tileId in tileIds)
            {
                ParseTile(tileId, out int q, out int r);
                arena.Tiles.Add(new ArenaTileState { TileId = tileId, Q = q, R = r });
            }

            return arena;
        }

        public static void InvokeHandleSnapshotUpdated(FieldManager fieldManager, MatchSnapshot snapshot)
        {
            MethodInfo method = typeof(FieldManager).GetMethod("HandleSnapshotUpdated", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert(method != null, "Expected FieldManager.HandleSnapshotUpdated to exist.");
            method.Invoke(fieldManager, new object[] { snapshot });
        }

        public static T GetPrivateField<T>(object target, string fieldName)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert(field != null, $"Expected private field '{fieldName}' on {target.GetType().Name}.");
            return (T)field.GetValue(target);
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert(field != null, $"Expected private field '{fieldName}' on {target.GetType().Name}.");
            field.SetValue(target, value);
        }

        private static void ParseTile(string tileId, out int q, out int r)
        {
            if (!HexArenaUtils.TryParseTileId(tileId, out q, out r))
            {
                throw new InvalidOperationException($"Could not parse tile id '{tileId}'.");
            }
        }
    }
}
