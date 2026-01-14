using System;
using UnityEngine;
using AIRBattleSimulation;
using AIRBattleSimulation.Data;

/// <summary>
/// The unit data
/// </summary>
[CreateAssetMenu(fileName = "UnitData", menuName = "Alpha Instinct/Unit")]
public class UnitDataSO : ScriptableObject
{
    /// <summary>
    /// Unique identifier for the unit
    /// </summary>
    [Tooltip("Must match a unit key in the simulation database exactly")]
    public string unitKey;
    /// <summary>
    /// The <see cref="Texture"/> for the card
    /// </summary>
    [Header("Unity-Specific Data")]
    public Texture cardArtwork;
    /// <summary>
    /// The <see cref="UnitSizeClassificationEnum"/> of the unit
    /// </summary>
    public UnitSizeClassificationEnum UnitSizeClassification = UnitSizeClassificationEnum.MEDIUM;
    /// <summary>
    /// The unit data
    /// </summary>
    public UnitDefinition Data => UnitDatabase.Get(unitKey);
}
