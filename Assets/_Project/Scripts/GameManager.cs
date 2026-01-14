using UnityEngine;
using TMPro;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }
    public bool IsGameActive { get; private set; }

    [Header("Game State")]
    public int startGold = 500;
    public int gold = 10;
    public int currentRound = 1;

    [Header("UI References")]
    public TextMeshProUGUI goldText;
    public TextMeshProUGUI roundText;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject); // Prevent duplicates
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject); // Optional: persists across scenes
    }

    void Start()
    {
        if (Instance != null)
        {
            Instance.gold = startGold;
        }
        UpdateUI();
    }

    public void UpdateUI()
    {
        if (goldText != null)
            goldText.text = $"{gold}G";

        if (roundText != null)
            roundText.text = $"Round: {currentRound}";
    }

    public void StartGame()
    {
        IsGameActive = true;
    }

    public void EndGame()
    {
        IsGameActive = false;
    }
}
