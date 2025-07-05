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

        var overlay = activeModal.Q<VisualElement>("WholeScreen");
        if (overlay != null)
        {
            overlay.style.position = Position.Absolute;
            overlay.style.left = 0;
            overlay.style.top = 0;
            overlay.style.right = 0;
            overlay.style.bottom = 0;
            overlay.style.zIndex = 1000;
        }

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
