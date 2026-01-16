using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Manages the shop
/// </summary>
public class ShopManager : MonoBehaviour
{
    /// <summary>
    /// List of all possible units
    /// </summary>
    public List<UnitDataSO> unitPool;
    /// <summary>
    /// Parent for card visuals
    /// </summary>
    public Transform handUIParent;
    /// <summary>
    /// The card prefab to render
    /// </summary>
    public GameObject cardPrefab;
    /// <summary>
    /// The player's current gold count
    /// </summary>
    public int gold = 10;
    /// <summary>
    /// The player's current hand size
    /// </summary>
    public int handSize = 5;
    /// <summary>
    /// The current hand
    /// </summary>
    internal List<UnitDataSO> currentHand = new();

    /// <summary>
    /// Generates a new hand
    /// </summary>
    public void GenerateHand()
    {
        ClearHand();

        for (int i = 0; i < handSize; i++)
        {
            UnitDataSO unit = unitPool[Random.Range(0, unitPool.Count)];
            currentHand.Add(unit);
            InstantiateCard(unit);
        }
    }

    /// <summary>
    /// Reroles the hand
    /// </summary>
    public void Reroll()
    {
        if (gold >= 2)
        {
            gold -= 2;
            GenerateHand();
        }
    }

    /// <summary>
    /// Creates the card in the hand ui
    /// </summary>
    /// <param name="data"></param>
    void InstantiateCard(UnitDataSO data)
    {
        GameObject card = Instantiate(cardPrefab, handUIParent);
        card.GetComponent<CardUI>().Init(data);
    }

    /// <summary>
    /// Clears the hand
    /// </summary>
    void ClearHand()
    {
        foreach (Transform child in handUIParent)
            Destroy(child.gameObject);
        currentHand.Clear();
    }
}
