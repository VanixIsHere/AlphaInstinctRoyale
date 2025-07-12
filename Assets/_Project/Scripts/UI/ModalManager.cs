using System;
using UnityEngine;
using UnityEngine.UIElements;

public class ModalManager : MonoBehaviour
{
    public VisualTreeAsset modalTemplate;
    private VisualElement root;
    private VisualElement activeModal;
    private Coroutine countdownRoutine;

    void Awake()
    {
        root = GetComponent<UIDocument>().rootVisualElement.Q<VisualElement>("ROOT");
    }

    public void ShowConfirm(string primaryLabel, string secondaryLabel, Action onConfirm, Action onCancel = null, string confirmText = "Confirm", string cancelText = "Cancel")
    {
        if (activeModal != null)
            return;

        ShowConfirmInternal(primaryLabel, secondaryLabel, onConfirm, onCancel, confirmText, cancelText);
    }

    public void ShowTimedConfirm(string primaryLabel, string secondaryLabel, Action onConfirm, Action onCancel = null, float timeout = 30f, string confirmText = "Confirm", string cancelText = "Cancel")
    {
        if (activeModal != null)
            return;

        ShowConfirmInternal(primaryLabel, secondaryLabel, onConfirm, onCancel, confirmText, cancelText);

        var timeoutLabel = activeModal.Q<Label>("TimeoutText");
        if (timeoutLabel != null)
            timeoutLabel.text = FormatCountdown(timeout);

        countdownRoutine = StartCoroutine(Countdown(timeout, timeoutLabel, onCancel));
    }

    private void ShowConfirmInternal(string primaryLabel, string secondaryLabel, Action onConfirm, Action onCancel, string confirmText, string cancelText)
    {
        activeModal = modalTemplate.CloneTree();


        activeModal.Q<Label>("PrimaryText").text = primaryLabel;
        activeModal.Q<Label>("SecondaryText").text = secondaryLabel;
        var confirmButton = activeModal.Q<Button>("Confirm");
        confirmButton.text = confirmText;
        confirmButton.clicked += () =>
        {
            if (countdownRoutine != null)
                StopCoroutine(countdownRoutine);
            root.Remove(activeModal);
            activeModal = null;
            onConfirm?.Invoke();
        };
        var cancelButton = activeModal.Q<Button>("Cancel");
        cancelButton.text = cancelText;
        cancelButton.clicked += () =>
        {
            if (countdownRoutine != null)
                StopCoroutine(countdownRoutine);
            root.Remove(activeModal);
            activeModal = null;
            onCancel?.Invoke();
        };

        root.Add(activeModal);
    }

    private System.Collections.IEnumerator Countdown(float time, Label label, Action onCancel)
    {
        var start = DateTime.UtcNow;
        int lastDisplay = Mathf.CeilToInt(time);
        while (true)
        {
            float elapsed = (float)(DateTime.UtcNow - start).TotalSeconds;
            float remaining = time - elapsed;
            if (remaining <= 0f)
                break;

            int seconds = Mathf.CeilToInt(remaining);
            if (label != null && seconds != lastDisplay)
            {
                label.text = FormatCountdown(remaining);
                lastDisplay = seconds;
            }

            yield return null;
        }

        root.Remove(activeModal);
        activeModal = null;
        countdownRoutine = null;
        onCancel?.Invoke();
    }

    private static string FormatCountdown(float seconds)
    {
        int rounded = Mathf.CeilToInt(seconds);
        return rounded == 1 ? "1 second" : $"{rounded} seconds";
    }
}
