using UnityEngine;

[DisallowMultipleComponent]
public class BenchSlotDropTarget : MonoBehaviour
{
    [SerializeField] private Color playableColor = new(0.2f, 0.8f, 1f, 0.75f);
    [SerializeField] private Color validColor = new(0.2f, 1f, 0.2f, 0.9f);
    [SerializeField] private Color invalidColor = new(1f, 0.25f, 0.25f, 0.9f);

    public int SlotIndex { get; private set; } = -1;
    public PlacementHighlightState HighlightState { get; private set; }

    public void Initialize(int slotIndex)
    {
        SlotIndex = slotIndex;
        EnsureCollider();
    }

    public void SetHighlightState(PlacementHighlightState state)
    {
        HighlightState = state;
    }

    private void EnsureCollider()
    {
        if (GetComponent<Collider>() != null)
        {
            return;
        }

        BoxCollider collider = gameObject.AddComponent<BoxCollider>();
        Renderer renderer = GetComponentInChildren<Renderer>();
        if (renderer != null)
        {
            Bounds localBounds = GetLocalBoundsFromWorldBounds(renderer.bounds);
            collider.center = localBounds.center;
            collider.size = localBounds.size;
        }
        else
        {
            collider.size = new Vector3(1f, 0.2f, 1f);
            collider.center = Vector3.zero;
        }
    }

    private Bounds GetLocalBoundsFromWorldBounds(Bounds worldBounds)
    {
        Vector3 center = worldBounds.center;
        Vector3 extents = worldBounds.extents;

        Vector3[] corners =
        {
            center + new Vector3(-extents.x, -extents.y, -extents.z),
            center + new Vector3(-extents.x, -extents.y, extents.z),
            center + new Vector3(-extents.x, extents.y, -extents.z),
            center + new Vector3(-extents.x, extents.y, extents.z),
            center + new Vector3(extents.x, -extents.y, -extents.z),
            center + new Vector3(extents.x, -extents.y, extents.z),
            center + new Vector3(extents.x, extents.y, -extents.z),
            center + new Vector3(extents.x, extents.y, extents.z),
        };

        Vector3 firstLocalCorner = transform.InverseTransformPoint(corners[0]);
        Bounds localBounds = new(firstLocalCorner, Vector3.zero);
        for (int index = 1; index < corners.Length; index++)
        {
            localBounds.Encapsulate(transform.InverseTransformPoint(corners[index]));
        }

        return localBounds;
    }

    private void OnDrawGizmos()
    {
        if (HighlightState == PlacementHighlightState.None)
        {
            return;
        }

        Gizmos.color = HighlightState switch
        {
            PlacementHighlightState.Playable => playableColor,
            PlacementHighlightState.Valid => validColor,
            PlacementHighlightState.Invalid => invalidColor,
            _ => playableColor,
        };

        Bounds bounds = GetWorldBounds();
        Gizmos.DrawWireCube(bounds.center, bounds.size + new Vector3(0.05f, 0.05f, 0.05f));
    }

    private Bounds GetWorldBounds()
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
        {
            return new Bounds(transform.position, new Vector3(1f, 0.1f, 1f));
        }

        Bounds bounds = renderers[0].bounds;
        for (int index = 1; index < renderers.Length; index++)
        {
            bounds.Encapsulate(renderers[index].bounds);
        }

        return bounds;
    }
}
