using System;
using UnityEngine;
using AIRBattleSimulation;
using AIRBattleSimulation.Data;

[CreateAssetMenu(fileName = "UnitData", menuName = "Alpha Instinct/Unit")]
public class UnitDataSO : ScriptableObject
{
    [Tooltip("Must match a unit key in the simulation database exactly")]
    public string unitKey;

    [Header("Unity-Specific Data")]
    public Texture cardArtwork;
    public GameObject unitPrefab;

    public UnitDefinition Data => UnitDatabase.Get(unitKey);
}
