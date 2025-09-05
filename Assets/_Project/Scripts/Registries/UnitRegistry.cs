using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "UnitRegistry", menuName = "Alpha Instinct/UnitRegistry")]
public class UnitRegistry : ScriptableObject
{
    public List<UnitDataSO> units;
}