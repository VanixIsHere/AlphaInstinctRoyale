using UnityEngine;
using AIRBattleSimulation;

public class UnitInstance : MonoBehaviour
{
    public string RuntimeUnitId { get; private set; } = string.Empty;
    public UnitDataSO unit;
    public int level = 1;
    public int currentHealth;

    public BenchManager OwningBench { get; private set; }
    public int BenchSlotIndex { get; private set; } = -1;
    public Transform BenchSlotTransform { get; private set; }

    public void Init(UnitDataSO data, int level = 1, int? currentHealthOverride = null, string runtimeUnitId = "")
    {
        RuntimeUnitId = runtimeUnitId;
        this.unit = data;
        this.level = level;
        currentHealth = currentHealthOverride ?? GetMaxHealth();
        name = $"{unit.Data.UnitName} (Lvl {level})";
    }

    public int GetMaxHealth()
    {
        return unit.Data.BaseHealth * level;
    }

    public int GetAttack()
    {
        return unit.Data.BaseAttack * level;
    }

    public void BindToBench(BenchManager bench, int slotIndex, Transform slotTransform)
    {
        OwningBench = bench;
        BenchSlotIndex = slotIndex;
        BenchSlotTransform = slotTransform;
    }
}
