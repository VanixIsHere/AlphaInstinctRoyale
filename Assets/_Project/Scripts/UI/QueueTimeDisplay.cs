using System;
using TMPro;
using UnityEngine;

public class QueueTimeDisplay : MonoBehaviour
{
    [SerializeField] private TMP_Text targetLabel;
    [SerializeField] private MatchmakerClient matchmaker;
    [SerializeField] private string prefix = "Queue Time: ";

    private void Awake()
    {
        ResolveReferences();
        Render();
    }

    private void OnEnable()
    {
        ResolveReferences();
        Render();
    }

    private void Reset()
    {
        ResolveReferences();
    }

    private void Update()
    {
        Render();
    }

    private void ResolveReferences()
    {
        if (targetLabel == null)
            targetLabel = GetComponent<TMP_Text>();

        var activeMatchmaker = MatchmakerClient.ActiveQueueInstance;
        if (activeMatchmaker != null && activeMatchmaker != matchmaker)
        {
            matchmaker = activeMatchmaker;
            return;
        }

        if (matchmaker != null && (matchmaker.HasQueueSession || matchmaker.IsQueued))
            return;

        foreach (var candidate in FindObjectsByType<MatchmakerClient>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (candidate != null && (candidate.HasQueueSession || candidate.IsQueued))
            {
                matchmaker = candidate;
                return;
            }
        }

        if (matchmaker == null)
            matchmaker = FindFirstObjectByType<MatchmakerClient>();
    }

    private void Render()
    {
        ResolveReferences();

        if (targetLabel == null)
            return;

        if (matchmaker == null || !matchmaker.HasQueueSession)
        {
            targetLabel.text = $"{prefix}--:--";
            return;
        }

        targetLabel.text = $"{prefix}{FormatElapsed(matchmaker.QueueTimeElapsed)}";
    }

    private static string FormatElapsed(TimeSpan elapsed)
    {
        if (elapsed < TimeSpan.Zero)
            elapsed = TimeSpan.Zero;

        var minutes = Mathf.FloorToInt((float)elapsed.TotalMinutes);
        var seconds = elapsed.Seconds;
        return $"{minutes:00}:{seconds:00}";
    }
}
