using System;
using System.Collections.Generic;
using UnityEngine;
using AIR.Shared.GameSession;

public class HexGridGenerator : MonoBehaviour
{
    public GameObject hexPrefab;
    [Header("Arena Bootstrap")]
    [SerializeField] private int bootstrapWidth = 9;
    [SerializeField] private int bootstrapHeight = 9;
    public float hexSize = 1f;

    private ArenaState generatedArena;
    private readonly Dictionary<string, HexCell> cellsByTileId = new(StringComparer.Ordinal);

    public int ArenaWidth => generatedArena?.Config?.Width ?? bootstrapWidth;
    public int ArenaHeight => generatedArena?.Config?.Height ?? bootstrapHeight;
    public MatchArenaConfig BootstrapArenaConfig => new()
    {
        Width = bootstrapWidth,
        Height = bootstrapHeight,
    };
    public IReadOnlyDictionary<string, HexCell> CellsByTileId => cellsByTileId;

    void Start()
    {
        if (GetComponent<FieldManager>() == null)
        {
            gameObject.AddComponent<FieldManager>();
        }

        SubscribeToAuthority();
        RenderArena(BuildInitialArena());
    }

    private ArenaState BuildInitialArena()
    {
        MatchSnapshot snapshot = GameManager.Instance?.CurrentSnapshot;
        if (snapshot?.Arena?.Tiles != null && snapshot.Arena.Tiles.Count > 0)
        {
            return snapshot.Arena;
        }

        return HexArenaUtils.BuildArena(BootstrapArenaConfig);
    }

    private void SubscribeToAuthority()
    {
        if (GameManager.Instance == null)
        {
            return;
        }

        GameManager.Instance.SnapshotUpdated -= HandleSnapshotUpdated;
        GameManager.Instance.SnapshotUpdated += HandleSnapshotUpdated;
    }

    private void HandleSnapshotUpdated(MatchSnapshot snapshot)
    {
        if (snapshot?.Arena?.Tiles == null || snapshot.Arena.Tiles.Count == 0)
        {
            return;
        }

        if (generatedArena != null &&
            generatedArena.Config.Width == snapshot.Arena.Config.Width &&
            generatedArena.Config.Height == snapshot.Arena.Config.Height)
        {
            return;
        }

        RenderArena(snapshot.Arena);
    }

    private void RenderArena(ArenaState arena)
    {
        if (arena == null)
        {
            return;
        }

        generatedArena = arena;
        ClearGridVisuals();
        cellsByTileId.Clear();

        float xOffset = Mathf.Sqrt(3f) * hexSize;
        float yOffset = 1.5f * hexSize;

        foreach (ArenaTileState tile in arena.Tiles)
        {
            Vector3 pos = new Vector3(
                xOffset * (tile.Q + tile.R * 0.5f),
                0,
                yOffset * tile.R
            );

            GameObject hex = Instantiate(hexPrefab, pos, Quaternion.identity, transform);
            HexCell cell = hex.GetComponent<HexCell>();
            cell.Init(tile);
            cellsByTileId[tile.TileId] = cell;
        }

        CreateGridCenterAndPositionCamera(xOffset, yOffset, arena.Config.Width, arena.Config.Height);
    }

    private void ClearGridVisuals()
    {
        for (int index = transform.childCount - 1; index >= 0; index--)
        {
            Transform child = transform.GetChild(index);
            if (Application.isPlaying)
            {
                Destroy(child.gameObject);
            }
            else
            {
                DestroyImmediate(child.gameObject);
            }
        }

        GameObject existingCenter = GameObject.Find("GridCenter");
        if (existingCenter != null)
        {
            if (Application.isPlaying)
            {
                Destroy(existingCenter);
            }
            else
            {
                DestroyImmediate(existingCenter);
            }
        }
    }

    public HexCell GetCell(string tileId)
    {
        cellsByTileId.TryGetValue(tileId, out HexCell cell);
        return cell;
    }

    void CreateGridCenterAndPositionCamera(float xOffset, float yOffset, int width, int height)
    {
        // Calculate grid center
        float centerX = xOffset * (width - 1) / 2f;
        float centerZ = yOffset * (height - 1) / 2f;

        Vector3 center = new Vector3(centerX, 0, centerZ);

        // Create the center marker
        GameObject gridCenter = new GameObject("GridCenter");
        gridCenter.transform.position = center;

        // Attach a gizmo for debug
        gridCenter.AddComponent<GridCenterGizmo>();

        // Reposition the camera
        Camera.main.GetComponent<CameraPositioner>().PositionCamera(width, height, hexSize);
        Camera.main.GetComponent<CameraPositioner>().target = gridCenter.transform;

        var cam = Camera.main;
        if (cam != null)
        {
            // Position the camera if the script is present
            var camPositioner = cam.GetComponent<CameraPositioner>();
            if (camPositioner != null)
            {
                camPositioner.target = gridCenter.transform;
                camPositioner.PositionCamera(width, height, hexSize);
            }
            else
            {
                Debug.LogWarning("Camera Positioner script missing on Main Camera.");
            }
        }
        else
        {
            Debug.LogError("Main Camera not found in scene!");
        }
    }
}
