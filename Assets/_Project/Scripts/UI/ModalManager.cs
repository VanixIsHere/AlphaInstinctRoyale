using System;
using UnityEngine;
using UnityEngine.UIElements;

public class ModalManager : MonoBehaviour
{
    public VisualTreeAsset modalTemplate;
    private VisualElement root;
    private VisualElement activeModal;

    void Awake()
    {
        root = GetComponent<UIDocument>().rootVisualElement.Q<VisualElement>("ROOT");
    }

    public void ShowConfirm(string primaryLabel, string secondaryLabel, Action onConfirm, Action onCancel = null, string confirmText = "Confirm", string cancelText = "Cancel")
    {
        if (activeModal != null)
            return;

        activeModal = modalTemplate.CloneTree();

        activeModal.Q<Label>("PrimaryText").text = primaryLabel;
        activeModal.Q<Label>("SecondaryText").text = secondaryLabel;
        var confirmButton = activeModal.Q<Button>("Confirm");
        confirmButton.text = confirmText;
        confirmButton.clicked += () =>
        {
            root.Remove(activeModal);
            activeModal = null;
            onConfirm?.Invoke();
        };
        var cancelButton = activeModal.Q<Button>("Cancel");
        cancelButton.text = cancelText;
        cancelButton.clicked += () =>
        {
            root.Remove(activeModal);
            activeModal = null;
            onCancel?.Invoke();
        };

        root.Add(activeModal);
    }
}
