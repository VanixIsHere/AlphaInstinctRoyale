using TMPro;
using UnityEngine;
using UnityEngine.UI;
using CardSystem;

[DisallowMultipleComponent]
public class BenchUnitInteraction : MonoBehaviour
{
    [Header("Inspect")]
    [SerializeField] private KeyCode inspectKey = KeyCode.I;

    [Header("Debug Panel")]
    [SerializeField] private float debugPanelYOffset = 0.35f;
    [SerializeField] private Vector2 debugPanelSize = new(260f, 110f);
    [SerializeField] private Vector2 debugPanelScreenOffset = new(0f, -36f);
    [SerializeField] private Vector2 debugPanelScreenMargin = new(12f, 12f);

    public static bool IsAnyUnitDragging => activeDragCount > 0;

    private static int activeDragCount;
    private static Canvas overlayCanvas;
    private static RectTransform overlayCanvasRect;

    private Camera mainCamera;
    private HandleCursor cursor;
    private UIBlockerManager uiBlockerManager;
    private HandManager handManager;
    private BenchManager benchManager;
    private UnitInstance unitInstance;

    private bool isHovering;
    private bool isDragging;
    private bool isDebugVisible;

    private Plane dragPlane;
    private Quaternion dragWorldRotation;
    private Vector3 dragCenterToTransformOffset;
    private Vector3 originalLocalPosition;
    private Quaternion originalLocalRotation;

    private GameObject debugPanelRoot;
    private TextMeshProUGUI debugPanelText;
    private RectTransform debugPanelRect;

    private void Awake()
    {
        mainCamera = Camera.main;
        if (mainCamera != null)
        {
            cursor = mainCamera.GetComponent<HandleCursor>();
        }

        uiBlockerManager = FindFirstObjectByType<UIBlockerManager>();
        handManager = FindFirstObjectByType<HandManager>();
        unitInstance = GetComponent<UnitInstance>();
    }

    private void LateUpdate()
    {
        if (mainCamera == null)
        {
            mainCamera = Camera.main;
            if (mainCamera != null && cursor == null)
            {
                cursor = mainCamera.GetComponent<HandleCursor>();
            }
        }

        if (isDragging)
        {
            if (IsInputBlocked())
            {
                EndDrag();
            }
            else
            {
                UpdateDragPosition();
            }
        }

        if (isHovering && !isDragging && Input.GetKeyDown(inspectKey))
        {
            ToggleDebugPanel();
        }

        if (debugPanelRoot != null && isDebugVisible)
        {
            UpdateDebugPanel();
        }
    }

    private void OnDisable()
    {
        if (isDragging)
        {
            activeDragCount = Mathf.Max(0, activeDragCount - 1);
        }

        isDragging = false;
        isHovering = false;
        if (debugPanelRoot != null)
        {
            debugPanelRoot.SetActive(false);
        }
        TryUpdateCursor();
    }

    public void Initialize(BenchManager bench, UnitInstance instance)
    {
        benchManager = bench;
        unitInstance = instance;
        CacheOriginalLocalTransform();
    }

    private void OnMouseEnter()
    {
        if (!CanHover())
        {
            return;
        }

        isHovering = true;
        TryUpdateCursor();
    }

    private void OnMouseExit()
    {
        if (isDragging)
        {
            return;
        }

        isHovering = false;
        TryUpdateCursor();
    }

    private void OnMouseOver()
    {
        if (!CanHover())
        {
            return;
        }

        isHovering = true;
        TryUpdateCursor();
    }

    private void OnMouseDown()
    {
        if (!CanStartDrag())
        {
            return;
        }

        BeginDrag();
    }

    private void OnMouseUp()
    {
        if (!isDragging)
        {
            return;
        }

        EndDrag();
    }

    private bool CanHover()
    {
        if (isDragging)
        {
            return true;
        }

        if (IsInputBlocked() || IsAnyUnitDragging)
        {
            return false;
        }

        if (handManager != null && handManager.IsCardBeingDragged())
        {
            return false;
        }

        return benchManager != null && unitInstance != null && benchManager.CanInteract(unitInstance);
    }

    private bool CanStartDrag()
    {
        return !IsInputBlocked()
            && !IsAnyUnitDragging
            && (handManager == null || !handManager.IsCardBeingDragged())
            && benchManager != null
            && unitInstance != null
            && benchManager.CanInteract(unitInstance);
    }

    private bool IsInputBlocked()
    {
        return uiBlockerManager != null && uiBlockerManager.IsBlockingInput;
    }

    private void BeginDrag()
    {
        CacheOriginalLocalTransform();

        dragWorldRotation = transform.rotation;
        dragPlane = new Plane(-mainCamera.transform.forward, GetVisualBounds().center);
        dragCenterToTransformOffset = transform.position - GetVisualBounds().center;

        isDragging = true;
        isHovering = true;
        activeDragCount++;
        TryUpdateCursor();
    }

    private void EndDrag()
    {
        if (isDragging)
        {
            activeDragCount = Mathf.Max(0, activeDragCount - 1);
        }

        isDragging = false;
        RestoreOriginalLocalTransform();
        isHovering = IsPointerOverThisUnit();
        TryUpdateCursor();
    }

    private void CacheOriginalLocalTransform()
    {
        originalLocalPosition = transform.localPosition;
        originalLocalRotation = transform.localRotation;
    }

    private void RestoreOriginalLocalTransform()
    {
        transform.localPosition = originalLocalPosition;
        transform.localRotation = originalLocalRotation;
    }

    private void UpdateDragPosition()
    {
        Ray mouseRay = mainCamera.ScreenPointToRay(Input.mousePosition);
        if (!dragPlane.Raycast(mouseRay, out float enter))
        {
            return;
        }

        Vector3 targetCenter = mouseRay.GetPoint(enter);
        transform.position = targetCenter + dragCenterToTransformOffset;
        transform.rotation = dragWorldRotation;
    }

    private bool IsPointerOverThisUnit()
    {
        if (mainCamera == null)
        {
            return false;
        }

        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
        return Physics.Raycast(ray, out RaycastHit hit) && hit.transform.IsChildOf(transform);
    }

    private void ToggleDebugPanel()
    {
        if (debugPanelRoot == null)
        {
            CreateDebugPanel();
        }

        isDebugVisible = !isDebugVisible;
        debugPanelRoot.SetActive(isDebugVisible);

        if (isDebugVisible)
        {
            UpdateDebugPanel();
        }
    }

    private void CreateDebugPanel()
    {
        debugPanelRoot = new GameObject("BenchUnitDebugPanel", typeof(RectTransform));
        debugPanelRect = debugPanelRoot.GetComponent<RectTransform>();
        EnsureOverlayCanvas();
        debugPanelRoot.transform.SetParent(overlayCanvasRect, false);
        debugPanelRoot.layer = overlayCanvas.gameObject.layer;
        debugPanelRect.sizeDelta = debugPanelSize;
        debugPanelRect.anchorMin = new Vector2(0.5f, 0.5f);
        debugPanelRect.anchorMax = new Vector2(0.5f, 0.5f);
        debugPanelRect.pivot = new Vector2(0.5f, 0.5f);

        GameObject backgroundObject = new("Background");
        backgroundObject.transform.SetParent(debugPanelRoot.transform, false);
        backgroundObject.layer = debugPanelRoot.layer;

        RectTransform backgroundRect = backgroundObject.AddComponent<RectTransform>();
        backgroundRect.anchorMin = Vector2.zero;
        backgroundRect.anchorMax = Vector2.one;
        backgroundRect.offsetMin = Vector2.zero;
        backgroundRect.offsetMax = Vector2.zero;

        Image background = backgroundObject.AddComponent<Image>();
        background.color = new Color(0.08f, 0.08f, 0.08f, 0.92f);

        GameObject textObject = new("Text");
        textObject.transform.SetParent(backgroundObject.transform, false);
        textObject.layer = debugPanelRoot.layer;

        RectTransform textRect = textObject.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(12f, 10f);
        textRect.offsetMax = new Vector2(-12f, -10f);

        debugPanelText = textObject.AddComponent<TextMeshProUGUI>();
        debugPanelText.alignment = TextAlignmentOptions.TopLeft;
        debugPanelText.fontSize = 22f;
        debugPanelText.textWrappingMode = TextWrappingModes.NoWrap;
        debugPanelText.color = Color.white;

        debugPanelRoot.SetActive(false);
    }

    private void UpdateDebugPanel()
    {
        if (debugPanelText == null || unitInstance == null || mainCamera == null)
        {
            return;
        }

        Bounds bounds = GetVisualBounds();
        Vector3 panelWorldAnchor = new(
            bounds.center.x,
            bounds.min.y - debugPanelYOffset,
            bounds.center.z);

        Vector3 screenPoint = mainCamera.WorldToScreenPoint(panelWorldAnchor);
        bool behindCamera = screenPoint.z <= 0f;
        debugPanelRoot.SetActive(!behindCamera && isDebugVisible);
        if (behindCamera)
        {
            return;
        }

        Vector2 clampedScreenPoint = ClampToScreenBounds((Vector2)screenPoint + debugPanelScreenOffset);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            overlayCanvasRect,
            clampedScreenPoint,
            overlayCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : overlayCanvas.worldCamera,
            out Vector2 anchoredPosition);
        debugPanelRect.anchoredPosition = anchoredPosition;

        var unitData = unitInstance.unit?.Data;
        string unitName = unitData?.UnitName ?? unitInstance.name;

        debugPanelText.text =
            $"{unitName}\n" +
            $"Lvl {unitInstance.level}  Slot {unitInstance.BenchSlotIndex}\n" +
            $"HP {unitInstance.currentHealth}/{unitInstance.GetMaxHealth()}\n" +
            $"ATK {unitInstance.GetAttack()}";
    }

    private Bounds GetVisualBounds()
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
        {
            return new Bounds(transform.position, Vector3.zero);
        }

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            bounds.Encapsulate(renderers[i].bounds);
        }

        return bounds;
    }

    private static void EnsureOverlayCanvas()
    {
        if (overlayCanvas != null && overlayCanvasRect != null)
        {
            return;
        }

        Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsSortMode.None);
        for (int i = 0; i < canvases.Length; i++)
        {
            if (canvases[i].renderMode == RenderMode.ScreenSpaceOverlay)
            {
                overlayCanvas = canvases[i];
                overlayCanvasRect = canvases[i].GetComponent<RectTransform>();
                return;
            }
        }

        GameObject canvasObject = new("BenchUnitDebugCanvas");
        overlayCanvas = canvasObject.AddComponent<Canvas>();
        overlayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        overlayCanvas.sortingOrder = 500;
        canvasObject.AddComponent<CanvasScaler>();
        canvasObject.AddComponent<GraphicRaycaster>();
        overlayCanvasRect = canvasObject.GetComponent<RectTransform>();
    }

    private Vector2 ClampToScreenBounds(Vector2 screenPoint)
    {
        float halfWidth = debugPanelSize.x * 0.5f;
        float halfHeight = debugPanelSize.y * 0.5f;

        float minX = debugPanelScreenMargin.x + halfWidth;
        float maxX = Screen.width - debugPanelScreenMargin.x - halfWidth;
        float minY = debugPanelScreenMargin.y + halfHeight;
        float maxY = Screen.height - debugPanelScreenMargin.y - halfHeight;

        return new Vector2(
            Mathf.Clamp(screenPoint.x, minX, maxX),
            Mathf.Clamp(screenPoint.y, minY, maxY));
    }

    private void TryUpdateCursor()
    {
        if (cursor == null)
        {
            return;
        }

        if (isDragging)
        {
            cursor.SetState(CursorState.Grab);
        }
        else if (isHovering && CanHover())
        {
            cursor.SetState(CursorState.HoverGrab);
        }
        else if (!IsAnyUnitDragging && (handManager == null || !handManager.IsCardBeingDragged()))
        {
            cursor.SetState(CursorState.Normal);
        }
    }
}
