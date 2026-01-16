using UnityEngine;
using TMPro;
using System;
using AIR.Shared.GameSession;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }
    
    private MatchRunner Runner;

    public int testTick = 0;

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
        Runner = new MatchRunner();
    }

    void Start()
    {
        if (Instance != null)
        {
            Instance.gold = startGold;
        }
        UpdateUI();

        Debug.Log("Start in Game Manager");

        StartCoroutine(Utils.Delay(3.0f, () =>
        {
            Runner.StartMatch();
            Debug.Log("Delayed runner start");
        }));
    }

    public void UpdateUI()
    {
        if (goldText != null)
            goldText.text = $"{gold}G";

        if (roundText != null)
            roundText.text = $"Round: {currentRound}";
    }

    void Update()
    {
        if (Runner.isActive)
        {
            Debug.Log("Runner active");
            Runner.Advance(Time.deltaTime);
            testTick = Runner.TickIndex;
        }
    }
}
