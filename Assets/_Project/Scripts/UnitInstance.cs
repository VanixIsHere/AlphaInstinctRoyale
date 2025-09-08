using UnityEngine;
using AIRBattleSimulation;
using UnityEditor.Localization.Plugins.XLIFF.V20;

public class UnitInstance : MonoBehaviour
{
    public UnitDataSO unit;
    public int level = 1;
    public int currentHealth;

    public void Init(UnitDataSO data, int level = 1)
    {
        this.unit = data;
        this.level = level;
        currentHealth = GetMaxHealth();
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
}