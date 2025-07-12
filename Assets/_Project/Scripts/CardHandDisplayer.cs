using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class CardHandDisplayer : MonoBehaviour
{
    [Header("Camera References")]
    public Camera playerCamera;
    [SerializeField] private Camera cardCameraPrefab;

    private List<GameObject> cardsInHand = new();
    private readonly Dictionary<int, string> layerNameCache = new();
    private static readonly Vector3[] cornerBuffer = new Vector3[8];

    [Header("Layout Settings")]
    [SerializeField] public float distanceFromCamera = 2.5f;
    [SerializeField] public float spawnVerticalOffsetFromCamera = 12f;
    [SerializeField] private float verticalOffsetFromCamera = -0.5f; // negative = down
    [SerializeField] private float spacing = 1.8f; // Space between cards
    [SerializeField] private int layoutHandSize = 5;

    [Header("Arc Settings")]
    [SerializeField] private float arcRadius = 1.5f;           // how curved the hand is (0 = flat line)
    [SerializeField] private float arcVerticalCurve = 0.5f;    // how high the curve lifts outer cards
    [SerializeField] private float arcVerticalDrop = 0.5f;     // how much more vertical space cards get
    [SerializeField] private float arcTiltDegrees = 15f;       // how much cards tilt forward/back
    [SerializeField] private float arcTwistDegrees = 10f;      // how much each card rotates around its thin edge (Z)
    [SerializeField] private float arcRollDegrees = 10f;       // How much each card rotates around its front vertical edge (Y)

    [System.Serializable]
    public struct BoundaryPadding
    {
        public float top;
        public float bottom;
        public float left;
        public float right;
    }

    [Header("Standup/Sitdown")]
    [SerializeField] private BoundaryPadding padding;
    [SerializeField] private bool showBoundaryGizmo = false;

    private bool handLowered = false;
    public bool HandIsLowered => handLowered;

    private Coroutine recentlyGeneratedStandupCoroutine;
    private bool recentlyGenerated = false;

    public void SetLayoutHandSize(int size)
    {
        layoutHandSize = Mathf.Max(1, size);
    }

    void Update()
    {
        LayoutCards();
        UpdateHandLowerState();
        UpdateCardLowering();
    }

    public void SetCards(List<GameObject> cards)
    {
        cardsInHand = cards;
        LayoutCards();
    }

    private string GetLayerName(int index)
    {
        if (!layerNameCache.TryGetValue(index, out var name))
        {
            name = "Card" + index.ToString();
            layerNameCache[index] = name;
        }
        return name;
    }

    float GetHandSizeScale()
    {
        if (layoutHandSize <= 1)
            return 1f;
        return (cardsInHand.Count - 1f) / (layoutHandSize - 1f);
    }

    public void LayoutCards()
    {
        if (playerCamera == null || cardsInHand.Count == 0) return;

        float totalWidth = (cardsInHand.Count - 1) * spacing;

        // Ray direction and base point
        Vector3 rayOrigin = playerCamera.transform.position;
        Vector3 rayDirection = playerCamera.transform.forward;
        Vector3 cameraDown = -playerCamera.transform.up; // This is "screen-space down"

        // Distance outward from camera
        Vector3 centerPoint = rayOrigin + rayDirection * distanceFromCamera + cameraDown * verticalOffsetFromCamera;

        float sizeScale = GetHandSizeScale();

        for (int i = 0; i < cardsInHand.Count; i++)
        {
            var card = cardsInHand[i];
            var state = card.GetComponent<CardState>();
            state.SetBaseRenderLayer(GetLayerName(i));

            if (state != null && (state.IsDragging || state.IsHovering))
            {
                // Debug.Log($"Card {i + 1} skipping");
                continue;
            }

            LayoutSingleCard(state, i, totalWidth, centerPoint, sizeScale);
        }
    }

    void LayoutSingleCard(CardState state, int index, float totalWidth, Vector3 centerPoint, float sizeScale)
    {
        float middleIndex = (cardsInHand.Count - 1) / 2f;
        float normalizedIndex = (cardsInHand.Count == 1) ? 0f : (index - middleIndex) / middleIndex;

        float offsetX = (index * spacing) - (totalWidth * 0.5f);
        Vector3 rightOffset = playerCamera.transform.right * offsetX;
        Vector3 targetPos = centerPoint + rightOffset;

        Quaternion targetRot = GetBaseRotation();
        ApplyArcModifiers(ref targetPos, ref targetRot, normalizedIndex, sizeScale);

        if (state != null)
        {
            state.HandAnchorPosition = targetPos;
            state.HandAnchorRotation = targetRot;
        }
    }

    Quaternion GetBaseRotation()
    {
        Quaternion targetRot = Quaternion.LookRotation(-playerCamera.transform.forward, Vector3.up);
        targetRot *= Quaternion.Euler(90f, 0f, 0f);
        targetRot *= Quaternion.Euler(0f, 180f, 0f);
        return targetRot;
    }

    void ApplyArcModifiers(ref Vector3 position, ref Quaternion rotation, float normalizedIndex, float sizeScale)
    {
        float tilt = (1f - Mathf.Abs(normalizedIndex)) * arcTiltDegrees * sizeScale;
        rotation *= Quaternion.Euler(tilt, 0f, 0f);

        float twist = normalizedIndex * arcTwistDegrees * sizeScale;
        rotation *= Quaternion.Euler(0f, twist, 0f);

        float roll = normalizedIndex * arcRollDegrees * sizeScale;
        rotation *= Quaternion.Euler(0f, 0f, roll);

        float verticalArcOffset = (1f - Mathf.Abs(normalizedIndex)) * arcVerticalCurve * sizeScale;
        position += playerCamera.transform.up * verticalArcOffset;

        float forwardArcOffset = (1f - Mathf.Abs(normalizedIndex)) * arcRadius * sizeScale;
        position += playerCamera.transform.forward * forwardArcOffset;

        float dropOffset = Mathf.Pow(normalizedIndex, 2) * arcVerticalDrop * Mathf.Pow(sizeScale, 2);
        position += -playerCamera.transform.up * dropOffset;
    }

    Rect CalculateHandScreenRect()
    {
        if (playerCamera == null || cardsInHand.Count == 0)
            return new Rect();

        bool first = true;
        float minX = 0f, minY = 0f, maxX = 0f, maxY = 0f;

        foreach (var card in cardsInHand)
        {
            Renderer rend = card.GetComponentInChildren<Renderer>();
            if (rend == null) continue;

            Bounds b = rend.bounds;
            Vector3 c = b.center;
            Vector3 e = b.extents;

            cornerBuffer[0] = c + new Vector3(-e.x, -e.y, -e.z);
            cornerBuffer[1] = c + new Vector3(-e.x, -e.y,  e.z);
            cornerBuffer[2] = c + new Vector3(-e.x,  e.y, -e.z);
            cornerBuffer[3] = c + new Vector3(-e.x,  e.y,  e.z);
            cornerBuffer[4] = c + new Vector3( e.x, -e.y, -e.z);
            cornerBuffer[5] = c + new Vector3( e.x, -e.y,  e.z);
            cornerBuffer[6] = c + new Vector3( e.x,  e.y, -e.z);
            cornerBuffer[7] = c + new Vector3( e.x,  e.y,  e.z);

            for (int j = 0; j < cornerBuffer.Length; j++)
            {
                Vector3 sp = playerCamera.WorldToScreenPoint(cornerBuffer[j]);
                if (sp.z < 0f) continue;
                if (first)
                {
                    minX = maxX = sp.x;
                    minY = maxY = sp.y;
                    first = false;
                }
                else
                {
                    minX = Mathf.Min(minX, sp.x);
                    minY = Mathf.Min(minY, sp.y);
                    maxX = Mathf.Max(maxX, sp.x);
                    maxY = Mathf.Max(maxY, sp.y);
                }
            }
        }

        if (first)
            return new Rect();

        return Rect.MinMaxRect(minX, minY, maxX, maxY);
    }

    void UpdateHandLowerState()
    {
        bool isAnyDragging = false;
        bool isAnyHovering = false;
        foreach (var card in cardsInHand)
        {
            var state = card.GetComponent<CardState>();
            if (state == null) continue;
            if (state.IsDragging) isAnyDragging = true;
            if (state.IsHovering) isAnyHovering = true;
            if (isAnyDragging && isAnyHovering) break;
        }

        if (isAnyDragging || isAnyHovering || recentlyGenerated)
        {
            handLowered = false;
        }

        Rect r = CalculateHandScreenRect();
        r.xMin -= padding.left;
        r.xMax += padding.right;
        r.yMin -= padding.bottom;
        r.yMax += padding.top;

        Vector2 mouse = Input.mousePosition;

        if (r.width <= 0f || r.height <= 0f)
            return;

        bool mouseInRect = r.Contains(mouse);

        if (recentlyGenerated && mouseInRect && recentlyGeneratedStandupCoroutine != null)
        {
            recentlyGenerated = false;
            StopCoroutine(recentlyGeneratedStandupCoroutine);
        }
        else if (recentlyGenerated && !mouseInRect)
        {
            return; // Wait until coroutine timer updates 'recentlyGenerated', or the user moves their mouse into the rect
        }

        handLowered = !mouseInRect;
    }

#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        if (!showBoundaryGizmo || playerCamera == null)
            return;

        Rect r = CalculateHandScreenRect();
        r.xMin -= padding.left;
        r.xMax += padding.right;
        r.yMin -= padding.bottom;
        r.yMax += padding.top;

        if (r.width <= 0f || r.height <= 0f)
            return;

        float z = distanceFromCamera;
        Vector3 lt = playerCamera.ScreenToWorldPoint(new Vector3(r.xMin, r.yMax, z));
        Vector3 rt = playerCamera.ScreenToWorldPoint(new Vector3(r.xMax, r.yMax, z));
        Vector3 lb = playerCamera.ScreenToWorldPoint(new Vector3(r.xMin, r.yMin, z));
        Vector3 rb = playerCamera.ScreenToWorldPoint(new Vector3(r.xMax, r.yMin, z));

        Handles.color = Color.cyan;
        Handles.DrawLine(lt, rt);
        Handles.DrawLine(rt, rb);
        Handles.DrawLine(rb, lb);
        Handles.DrawLine(lb, lt);
    }
#endif

    private void UpdateCardLowering()
    {
        bool isAnyCardDragging = false;
        foreach (var card in cardsInHand)
        {
            var s = card.GetComponent<CardState>();
            if (s != null && s.IsDragging)
            {
                isAnyCardDragging = true;
                break;
            }
        }

        foreach (var card in cardsInHand)
        {
            var state = card.GetComponent<CardState>();
            if (state == null) continue;

            state.IsLowered = (isAnyCardDragging && !state.IsDragging) || handLowered;
        }
    }

    public void HandleRecentGenerationStandup()
    {
        // This function manages the private boolean 'recentlyGenerated', which keeps the hand from sitting down for a few seconds after a new hand is set.
        recentlyGenerated = true;

        if (recentlyGeneratedStandupCoroutine != null)
        {
            // Delete the old running coroutine if a hand is generated before the existing one ends
            StopCoroutine(recentlyGeneratedStandupCoroutine);
        }

        recentlyGeneratedStandupCoroutine = StartCoroutine(Utils.Delay(3f, () =>
        {
            recentlyGenerated = false;
        }));
    }
}
