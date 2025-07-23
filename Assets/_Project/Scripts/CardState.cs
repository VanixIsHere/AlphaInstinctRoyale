using System;
using AudioSystem;
using UnityEngine;

public class CardState : MonoBehaviour
{
    [Header("Runtime State")]
    [SerializeField] private bool isDragging = false;
    [SerializeField] private bool isHovering = false;
    [SerializeField] private bool isLowered = false;

    [Tooltip("The default world position based on hand layout.")]
    [SerializeField] private Vector3 handAnchorPosition = Vector3.zero;
    [Tooltip("The default world rotation based on hand layout.")]
    [SerializeField] private Quaternion handAnchorRotation = Quaternion.identity;

    [SerializeField] private int originalLayer;
    [SerializeField] private string focusedLayerName = "CardFocused";

    [SerializeField] private SoundData hoverUpSoundEffect;
    [SerializeField] private SoundData hoverDownSoundEffect;

    public bool IsDragging
    {
        get => isDragging;
        set => isDragging = value;
    }

    public bool IsHovering
    {
        get => isHovering;
        set
        {
            if (isHovering == value) return;
            isHovering = value;

            if (isHovering)
            {
                // AudioManager.Instance.CreateSound().WithSoundData(hoverUpSoundEffect).Play();
            }
            else
            {
                // AudioManager.Instance.CreateSound().WithSoundData(hoverDownSoundEffect).Play();
            }
        }
    }

    public bool IsLowered
    {
        get => isLowered;
        set => isLowered = value;
    }

    public Vector3 HandAnchorPosition
    {
        get => handAnchorPosition;
        set => handAnchorPosition = value;
    }

    public Quaternion HandAnchorRotation
    {
        get => handAnchorRotation;
        set => handAnchorRotation = value;
    }

    void Awake()
    {
        originalLayer = gameObject.layer;
    }

    void Update()
    {
        if (IsDragging || IsHovering)
        {
            if (gameObject.layer != LayerMask.NameToLayer(focusedLayerName))
                LayerUtils.SetLayerRecursive(gameObject, LayerMask.NameToLayer(focusedLayerName));
        }
        else
        {
            LayerUtils.SetLayerRecursive(gameObject, originalLayer);
        }
    }

    public void SetBaseRenderLayer(string layerName)
    {
        int layer = LayerMask.NameToLayer(layerName);
        if (layer == -1)
        {
            Debug.LogError($"Layer '{layerName}' does not exist. Check your Unity layer settings.");
            return;
        }

        originalLayer = layer;

        // If not currently being hovered or dragged, apply immediately
        if (!IsDragging && !IsHovering)
        {
            gameObject.layer = originalLayer;
        }
    }
}