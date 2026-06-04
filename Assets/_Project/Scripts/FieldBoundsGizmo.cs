using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Draws a rectangle around the combined hex grid and bench area.
/// </summary>
[ExecuteAlways]
public class FieldBoundsGizmo : MonoBehaviour
{
    public HexGridGenerator gridGenerator;
    public BenchManager benchManager;
    public CameraPositioner cameraPositioner;
    public Color fillColor = new Color(0f, 1f, 0f, 0.1f);
    public Color outlineColor = Color.green;

    Vector3[] corners = new Vector3[4];

    void OnDrawGizmos()
    {
        if (gridGenerator == null)
            gridGenerator = FindFirstObjectByType<HexGridGenerator>();
        if (benchManager == null)
            benchManager = FindFirstObjectByType<BenchManager>();
        if (gridGenerator == null || benchManager == null)
            return;

        GameObject centerObj = GameObject.Find("GridCenter");
        if (centerObj == null)
            return;
        Vector3 center = centerObj.transform.position;

        float width = (gridGenerator.ArenaWidth + 0.5f) * Mathf.Sqrt(3f) * gridGenerator.hexSize;
        float halfWidth = width * 0.5f;
        float halfHeight = (gridGenerator.ArenaHeight - 1) * 1.5f * gridGenerator.hexSize * 0.5f;

        float topZ = halfHeight;
        float bottomZ = benchManager.CurrentYOffset;

        corners[0] = center + new Vector3(-halfWidth, 0f, bottomZ);
        corners[1] = center + new Vector3(halfWidth, 0f, bottomZ);
        corners[2] = center + new Vector3(halfWidth, 0f, topZ);
        corners[3] = center + new Vector3(-halfWidth, 0f, topZ);

#if UNITY_EDITOR
        Handles.DrawSolidRectangleWithOutline(corners, fillColor, outlineColor);

        if (cameraPositioner == null)
            cameraPositioner = FindFirstObjectByType<CameraPositioner>();
        Camera cam = Camera.main;
        if (cameraPositioner != null && cam != null)
        {
            float topV = 1f - Mathf.Clamp01(cameraPositioner.topOfGridPercent);
            float bottomV = 1f - Mathf.Clamp01(cameraPositioner.benchBottomPercent);

            Vector3 centerCam = cam.transform.InverseTransformPoint(center);
            float zDist = Mathf.Abs(centerCam.z);

            Vector3 lt = cam.ViewportToWorldPoint(new Vector3(0f, topV, zDist));
            Vector3 rt = cam.ViewportToWorldPoint(new Vector3(1f, topV, zDist));
            Vector3 lb = cam.ViewportToWorldPoint(new Vector3(0f, bottomV, zDist));
            Vector3 rb = cam.ViewportToWorldPoint(new Vector3(1f, bottomV, zDist));

            Handles.color = Color.yellow;
            Handles.DrawLine(lt, rt);
            Handles.color = Color.red;
            Handles.DrawLine(lb, rb);
        }
#endif
    }
}
