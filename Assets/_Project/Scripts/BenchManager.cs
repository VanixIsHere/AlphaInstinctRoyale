using System;
using UnityEditor;
using UnityEngine;

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

    private Transform[] benchSlots;
    private UnitDataSO[] occupiedSlots;
    private UnitInstance[] occupiedInstances;

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
            float halfGridHeight = (gridGen.height - 1) * 1.5f * gridGen.hexSize * 0.5f;
            float extraOffset = distanceBelowGridInHexes * gridGen.hexSize;
            yOffsetFromGrid = -(halfGridHeight + extraOffset);
        }
        else
        {
            Debug.LogWarning("HexGridGenerator not found, using default bench offset");
        }

        GenerateBenchSlots();
    } 


    /// <summary>
    /// Generates the bench slots
    /// </summary>
    void GenerateBenchSlots()
    {
        benchSlots = new Transform[benchSlotCount];
        occupiedSlots = new UnitDataSO[benchSlotCount];
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
        }
    }

    /// <summary>
    /// Tries to add the unit to the bench
    /// </summary>
    /// <param name="unit"></param>
    /// <returns></returns>
    public bool TryAddToBench(UnitDataSO unit)
    {
        for (int i = 0; i < benchSlots.Length; i++)
        {
            if (occupiedSlots[i] == null)
            {
                occupiedSlots[i] = unit;
                occupiedInstances[i] = SpawnUnit(unit, benchSlots[i]);
                return true;
            }
        }

        Debug.Log("Bench is full!");
        return false;
    }

    /// <summary>
    /// Checks if the unit can be added to the bench
    /// </summary>
    /// <param name="unit"></param>
    /// <returns></returns>
    public bool CanAdd(UnitDataSO unit)
    {
        // Check for empty slot
        for (int i = 0; i < occupiedSlots.Length; i++)
        {
            if (occupiedSlots[i] == null)
            {
                return true;
            }
        }

        // TODO - Merge check should probably run first and do the merge without needing to add the unit to the board
        // Bench full - check for potential merge (two level 1 units of same type)
        int count = 0;
        for (int i = 0; i < occupiedSlots.Length; i++)
        {
            if (occupiedSlots[i] == unit && occupiedInstances[i] != null && occupiedInstances[i].level == 1)
            {
                count++;
                if (count >= 2)
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Spawns the played unit to the bench
    /// </summary>
    /// <param name="unit"></param>
    /// <param name="slot"></param>
    /// <returns></returns>
    UnitInstance SpawnUnit(UnitDataSO unit, Transform slot)
    {
        if (GetUnitPrefab(unit.UnitSizeClassification) is not GameObject unitPrefab)
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
        
        
        if (instanceObj.TryGetComponent<UnitInstance>(out var inst))
        {
            inst.Init(unit);
        }
        
        return inst;
    }

    /// <summary>
    /// Loads the unit's <see cref="GameObject"/> based off <param name="unitSizeClassification"/>
    /// </summary>
    /// <param name="unitSizeClassification"></param>
    /// <returns></returns>
    /// <exception cref="NotImplementedException"></exception>
    internal GameObject GetUnitPrefab(UnitSizeClassificationEnum unitSizeClassification)
    {
        return unitSizeClassification switch
        {
            UnitSizeClassificationEnum.TINY => AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Units/TinyUnit.prefab"),
            UnitSizeClassificationEnum.SMALL => AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Units/SmallUnit.prefab"),
            UnitSizeClassificationEnum.MEDIUM => AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Units/MediumUnit.prefab"),
            UnitSizeClassificationEnum.LARGE => AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Units/LargeUnit.prefab"),
            UnitSizeClassificationEnum.EXTRALARGE => AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Units/ExtraLargeUnit.prefab"),
            _ => throw new NotImplementedException()
        };
    }
}
