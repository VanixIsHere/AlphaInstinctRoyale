using UnityEngine;
using AIRBattleSimulation;

public class UnitInstance : MonoBehaviour
{
    public UnitDataSO data;
    public int level = 1;
    public int currentHealth;

    public void Init(UnitDataSO unitData, int level = 1)
    {
        data = unitData;
        this.level = level;
        currentHealth = GetMaxHealth();
        name = $"{data.unitName} (Lvl {level})";
    }

    public int GetMaxHealth()
    {
        return data.baseHealth * level;
    }

    public int GetAttack()
    {
        return data.baseAttack * level;
    }
}