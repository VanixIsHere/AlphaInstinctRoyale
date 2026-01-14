using System;
using Unity.VisualScripting.FullSerializer;
using UnityEditor.Localization.Plugins.XLIFF.V12;
using UnityEngine;

public class BenchManager : MonoBehaviour
{
    const String benchSlotGameObjectName = "BenchSlot";

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
    public GameObject unitInstancePrefab; // Placeholder prefab for unit

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

    UnitInstance SpawnUnit(UnitDataSO unit, Transform slot)
    {
        GameObject instanceObj = Instantiate(unit.unitPrefab, slot.position, Quaternion.identity, slot);
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
}
