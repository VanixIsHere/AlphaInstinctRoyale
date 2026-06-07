using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class UnitHealthBar : MonoBehaviour
{
    [Serializable]
    public struct Settings
    {
        public float Width;
        public float Height;
        public float VerticalOffset;
        public float ChunkGap;
        public int HpPerChunk;
        public float BackgroundInset;
        public Color FrameColor;
        public Color BackgroundColor;
        public Color FriendlyFillColor;
        public Color EnemyFillColor;

        public static Settings Default => new()
        {
            Width = 75f,
            Height = 10f,
            VerticalOffset = 0.35f,
            ChunkGap = 1f,
            HpPerChunk = 10,
            BackgroundInset = 1f,
            FrameColor = new Color(0.1f, 0.08f, 0.08f, 0.95f),
            BackgroundColor = new Color(0.18f, 0.08f, 0.08f, 0.9f),
            FriendlyFillColor = new Color(0.42f, 0.92f, 0.38f, 0.98f),
            EnemyFillColor = new Color(0.92f, 0.34f, 0.34f, 0.98f),
        };
    }

    private sealed class BarElement
    {
        public RectTransform RectTransform;
        public Image Image;
    }

    private readonly List<BarElement> chunkElements = new();
    private UnitInstance owner;
    private Settings settings;
    private Canvas hostCanvas;
    private RectTransform canvasRect;
    private RectTransform rootRect;
    private BarElement frameElement;
    private BarElement backgroundElement;
    private Color activeFillColor;
    private int lastCurrentHealth = int.MinValue;
    private int lastMaxHealth = int.MinValue;

    public void Initialize(UnitInstance owner, Settings settings, bool isEnemy)
    {
        this.owner = owner;
        this.settings = settings;
        activeFillColor = isEnemy ? settings.EnemyFillColor : settings.FriendlyFillColor;
        EnsureVisuals();
        Refresh(force: true);
        UpdateScreenPosition();
    }

    public void Refresh(bool force = false)
    {
        if (owner == null)
        {
            return;
        }

        int currentHealth = Mathf.Max(0, owner.currentHealth);
        int maxHealth = Mathf.Max(1, owner.GetMaxHealth());
        if (!force && currentHealth == lastCurrentHealth && maxHealth == lastMaxHealth)
        {
            return;
        }

        lastCurrentHealth = currentHealth;
        lastMaxHealth = maxHealth;
        UpdateChunkVisuals(currentHealth, maxHealth);
    }

    private void LateUpdate()
    {
        if (owner == null)
        {
            return;
        }

        Refresh();
        UpdateScreenPosition();
    }

    private void OnDestroy()
    {
        if (rootRect != null)
        {
            Destroy(rootRect.gameObject);
        }
    }

    private void EnsureVisuals()
    {
        if (rootRect != null)
        {
            return;
        }

        hostCanvas = FindHostCanvas();
        if (hostCanvas == null)
        {
            return;
        }

        canvasRect = hostCanvas.transform as RectTransform;
        if (canvasRect == null)
        {
            return;
        }

        GameObject rootObject = new("UnitHealthBarUI", typeof(RectTransform));
        rootRect = rootObject.GetComponent<RectTransform>();
        rootRect.SetParent(canvasRect, false);
        rootRect.anchorMin = new Vector2(0.5f, 0.5f);
        rootRect.anchorMax = new Vector2(0.5f, 0.5f);
        rootRect.pivot = new Vector2(0.5f, 0.5f);
        rootRect.sizeDelta = new Vector2(settings.Width, settings.Height);

        frameElement = CreateElement("Frame", settings.FrameColor, rootRect);
        backgroundElement = CreateElement("Background", settings.BackgroundColor, rootRect);
    }

    private void UpdateScreenPosition()
    {
        if (rootRect == null || canvasRect == null || owner == null)
        {
            return;
        }

        Camera cam = Camera.main;
        if (cam == null)
        {
            rootRect.gameObject.SetActive(false);
            return;
        }

        Bounds bounds = GetOwnerBounds();
        Vector3 worldPoint = new(bounds.center.x, bounds.max.y + settings.VerticalOffset, bounds.center.z);
        Vector3 screenPoint = cam.WorldToScreenPoint(worldPoint);
        if (screenPoint.z <= 0f)
        {
            rootRect.gameObject.SetActive(false);
            return;
        }

        Camera eventCamera = hostCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : cam;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPoint, eventCamera, out Vector2 localPoint))
        {
            rootRect.gameObject.SetActive(true);
            rootRect.anchoredPosition = localPoint;
        }
        else
        {
            rootRect.gameObject.SetActive(false);
        }
    }

    private void UpdateChunkVisuals(int currentHealth, int maxHealth)
    {
        if (rootRect == null)
        {
            return;
        }

        float barWidth = Mathf.Max(8f, settings.Width);
        float barHeight = Mathf.Max(2f, settings.Height);
        float inset = Mathf.Clamp(settings.BackgroundInset, 0f, barHeight * 0.45f);
        float innerWidth = Mathf.Max(1f, barWidth - (inset * 2f));
        float innerHeight = Mathf.Max(1f, barHeight - (inset * 2f));

        rootRect.sizeDelta = new Vector2(barWidth, barHeight);

        ConfigureRect(frameElement.RectTransform, Vector2.zero, new Vector2(barWidth, barHeight));
        ConfigureRect(backgroundElement.RectTransform, Vector2.zero, new Vector2(innerWidth, innerHeight));

        int hpPerChunk = Mathf.Max(1, settings.HpPerChunk);
        int totalChunkCount = Mathf.CeilToInt(maxHealth / (float)hpPerChunk);
        EnsureChunkCount(totalChunkCount);

        float leftEdge = -innerWidth * 0.5f;
        float gap = Mathf.Max(0f, settings.ChunkGap);
        float cursor = leftEdge;
        int remainingHealth = currentHealth;

        for (int index = 0; index < totalChunkCount; index++)
        {
            int chunkHealthCapacity = Mathf.Min(hpPerChunk, maxHealth - (index * hpPerChunk));
            float chunkWidth = innerWidth * (chunkHealthCapacity / (float)maxHealth);
            float visibleRatio = Mathf.Clamp01(remainingHealth / (float)chunkHealthCapacity);
            float visibleWidth = chunkWidth * visibleRatio;

            BarElement element = chunkElements[index];
            element.Image.enabled = visibleWidth > 0.0001f;

            if (element.Image.enabled)
            {
                float finalWidth = Mathf.Max(1f, visibleWidth - gap);
                ConfigureRect(
                    element.RectTransform,
                    new Vector2(cursor + (finalWidth * 0.5f), 0f),
                    new Vector2(finalWidth, innerHeight));
            }

            cursor += chunkWidth;
            remainingHealth = Mathf.Max(0, remainingHealth - chunkHealthCapacity);
        }
    }

    private void EnsureChunkCount(int chunkCount)
    {
        while (chunkElements.Count < chunkCount)
        {
            chunkElements.Add(CreateElement($"Chunk{chunkElements.Count}", activeFillColor, rootRect));
        }

        for (int index = 0; index < chunkElements.Count; index++)
        {
            chunkElements[index].Image.enabled = index < chunkCount;
            chunkElements[index].Image.color = activeFillColor;
        }
    }

    private static void ConfigureRect(RectTransform rectTransform, Vector2 anchoredPosition, Vector2 size)
    {
        rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        rectTransform.anchoredPosition = anchoredPosition;
        rectTransform.sizeDelta = size;
        rectTransform.localScale = Vector3.one;
        rectTransform.localRotation = Quaternion.identity;
    }

    private static BarElement CreateElement(string objectName, Color color, RectTransform parent)
    {
        GameObject child = new(objectName, typeof(RectTransform), typeof(Image));
        RectTransform rectTransform = child.GetComponent<RectTransform>();
        rectTransform.SetParent(parent, false);

        Image image = child.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;

        return new BarElement
        {
            RectTransform = rectTransform,
            Image = image,
        };
    }

    private static Canvas FindHostCanvas()
    {
        Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        foreach (Canvas canvas in canvases)
        {
            if (!canvas.isRootCanvas)
            {
                continue;
            }

            if (canvas.renderMode == RenderMode.WorldSpace)
            {
                continue;
            }

            return canvas;
        }

        return null;
    }

    private Bounds GetOwnerBounds()
    {
        Renderer[] renderers = owner.GetComponentsInChildren<Renderer>();
        Bounds? bounds = null;

        foreach (Renderer renderer in renderers)
        {
            if (renderer == null)
            {
                continue;
            }

            bounds = bounds.HasValue ? Encapsulate(bounds.Value, renderer.bounds) : renderer.bounds;
        }

        return bounds ?? new Bounds(owner.transform.position, Vector3.one * 0.5f);
    }

    private static Bounds Encapsulate(Bounds original, Bounds additional)
    {
        original.Encapsulate(additional);
        return original;
    }
}
