using System;
using TMPro;
using UnityEngine;

public class QueueTimeDisplay : MonoBehaviour
{
    [SerializeField] private TMP_Text targetLabel;
    [SerializeField] private MatchmakerClient matchmaker;
    [SerializeField] private string prefix = "Queue Time: ";

    private void Reset()
    {
        targetLabel = GetComponent<TMP_Text>();
        matchmaker = FindObjectOfType<MatchmakerClient>();
    }

    private void Update()
    {
        if (targetLabel == null || matchmaker == null)
            return;

        if (!matchmaker.IsQueued)
        {
            // Debug.Log($"HELLO {targetLabel} {matchmaker.IsQueued}");
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
