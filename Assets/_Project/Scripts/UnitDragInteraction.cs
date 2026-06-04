using UnityEngine;
using CardSystem;

[DisallowMultipleComponent]
public class UnitDragInteraction : MonoBehaviour
{
    private Camera mainCamera;
    private HandleCursor cursor;
    private UIBlockerManager uiBlockerManager;
    private HandManager handManager;
    private UnitInstance unitInstance;
    private UnitPlacementCoordinator placementCoordinator;

    private bool isHovering;
    private bool isDragging;
    private Plane dragPlane;
    private Quaternion dragWorldRotation;
    private Vector3 dragCenterToTransformOffset;
    private Vector3 originalLocalPosition;
    private Quaternion originalLocalRotation;

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
        placementCoordinator = FindFirstObjectByType<UnitPlacementCoordinator>();
    }

    public void Initialize(UnitInstance instance)
    {
        unitInstance = instance;
        CacheOriginalLocalTransform();
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

        if (placementCoordinator == null)
        {
            placementCoordinator = FindFirstObjectByType<UnitPlacementCoordinator>();
        }

        if (isDragging)
        {
            if (IsInputBlocked())
            {
                CancelDrag();
            }
            else
            {
                UpdateDragPosition();
            }
        }
    }

    private void OnDisable()
    {
        if (isDragging && placementCoordinator != null)
        {
            placementCoordinator.CancelDrag(this);
        }

        isDragging = false;
        isHovering = false;
        TryUpdateCursor();
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

        if (IsInputBlocked() || placementCoordinator == null)
        {
            return false;
        }

        if (handManager != null && handManager.IsCardBeingDragged())
        {
            return false;
        }

        return placementCoordinator.CanStartDrag(unitInstance);
    }

    private bool CanStartDrag()
    {
        return !IsInputBlocked()
            && placementCoordinator != null
            && (handManager == null || !handManager.IsCardBeingDragged())
            && placementCoordinator.CanStartDrag(unitInstance);
    }

    private bool IsInputBlocked()
    {
        return uiBlockerManager != null && uiBlockerManager.IsBlockingInput;
    }

    private void BeginDrag()
    {
        CacheOriginalLocalTransform();

        dragWorldRotation = transform.rotation;
        Vector3 visualCenter = GetVisualBounds().center;
        // Keep dragged units on a plane parallel to the board (the board is laid out on XZ).
        dragPlane = new Plane(Vector3.up, visualCenter);
        dragCenterToTransformOffset = transform.position - GetVisualBounds().center;

        isDragging = true;
        isHovering = true;
        placementCoordinator.BeginDrag(this, unitInstance);
        TryUpdateCursor();
    }

    private void EndDrag()
    {
        RestoreOriginalLocalTransform();
        placementCoordinator?.CompleteDrag(this);
        isDragging = false;
        isHovering = IsPointerOverThisUnit();
        TryUpdateCursor();
    }

    private void CancelDrag()
    {
        placementCoordinator?.CancelDrag(this);
        RestoreOriginalLocalTransform();
        isDragging = false;
        isHovering = false;
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

    private Bounds GetVisualBounds()
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
        {
            return new Bounds(transform.position, Vector3.zero);
        }

        Bounds bounds = renderers[0].bounds;
        for (int index = 1; index < renderers.Length; index++)
        {
            bounds.Encapsulate(renderers[index].bounds);
        }

        return bounds;
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
        else if (placementCoordinator == null || !placementCoordinator.IsAnyUnitDragging)
        {
            cursor.SetState(CursorState.Normal);
        }
    }
}
