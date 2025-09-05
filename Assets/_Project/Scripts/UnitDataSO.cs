using System;
using UnityEngine;
using Unity.VisualScripting;
using AIRBattleSimulation;
using AIRBattleSimulation.Models;

[CreateAssetMenu(fileName = "UnitData", menuName = "Alpha Instinct/Unit")]
public class UnitDataSO : ScriptableObject
{
    public string unitName;
    public Texture cardArtwork;
    public GameObject unitPrefab;

    [Header("Stats")]
    public int baseHealth;
    public int baseAttack;
    public float baseSpeed;
    public int baseRange;
    public int cost;
    [Range(1, 5)]
    public int rarity = 1;

    [Header("Class/Role")]
    public UnitRole role; // enum (e.g. Tank, Flanker, Healer)
    public UnitOrigin origin; // enum or country if you want synergy

    // 💡 Derived dynamically — do not try to assign during field init!
    public UnitClass UnitClass;

    public UnitDefinition ToSimulationDefinition()
    {
        return new UnitDefinition
        {
            UnitName = unitName,
            BaseHealth = baseHealth,
            BaseAttack = baseAttack,
            BaseSpeed = baseSpeed,
            BaseRange = baseRange,
            Cost = cost,
            Rarity = rarity,
            Role = role,
            Origin = origin
        };
    }




}
