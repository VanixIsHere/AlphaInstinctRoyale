using System.Collections.Generic;
using NUnit.Framework.Interfaces;
using UnityEngine;

[System.Serializable]
public class RarityTier
{
    public int rarity;
    public float weight;
}
public class UnitPoolManager : MonoBehaviour
{
    public UnitRegistry unitRegistry;
    public List<UnitDataSO> allUnits;
    public List<RarityTier> rarityChances;
    public int currentTurn = 1;

    [SerializeField]
    public List<RarityTier> earlyGameRarities = new()
    {
        new RarityTier { rarity = 1, weight = 60 },
        new RarityTier { rarity = 2, weight = 30 },
        new RarityTier { rarity = 3, weight = 10 },
        new RarityTier { rarity = 4, weight = 0 },
        new RarityTier { rarity = 5, weight = 0 },
    };

    [SerializeField]
    public List<RarityTier> midGameRarities = new()
    {
        new RarityTier { rarity = 1, weight = 40 },
        new RarityTier { rarity = 2, weight = 30 },
        new RarityTier { rarity = 3, weight = 20 },
        new RarityTier { rarity = 4, weight = 10 },
        new RarityTier { rarity = 5, weight = 0 },
    };

    [SerializeField]
    public List<RarityTier> lateGameRarities = new()
    {
        new RarityTier { rarity = 1, weight = 20 },
        new RarityTier { rarity = 2, weight = 25 },
        new RarityTier { rarity = 3, weight = 25 },
        new RarityTier { rarity = 4, weight = 20 },
        new RarityTier { rarity = 5, weight = 10 },
    };

    void Awake()
    {
        allUnits = new List<UnitDataSO>(unitRegistry.units);
    }

    public List<UnitDataSO> GetRandomUnits(int count, int currentTurn)
    {
        List<RarityTier> table = GetCurrentRarityTable();
        List<UnitDataSO> drawn = new();
        for (int i = 0; i < count; i++)
        {
            int rarity = RollRarity(table);
            var eligible = allUnits.FindAll(u => u.Data.Rarity == rarity);

            if (eligible.Count > 0)
            {
                drawn.Add(eligible[Random.Range(0, eligible.Count)]);
            }
            else {
                i--;
            }
        }
        return drawn;
    }

    List<RarityTier> GetCurrentRarityTable()
    {
        if (currentTurn <= 5) return earlyGameRarities;
        if (currentTurn <= 10) return midGameRarities;
        return lateGameRarities;
    }

    int RollRarity(List<RarityTier> table)
    {
        float totalWeight = 0;
        foreach (var tier in table)
        {
            totalWeight += tier.weight;
        }

        float roll = Random.Range(0, totalWeight);
        float cumulative = 0;

        foreach (var tier in table)
        {
            cumulative += tier.weight;
            if (roll <= cumulative)
            {
                return tier.rarity;
            }
        }

        return 1; // Fallback to common
    }
}
