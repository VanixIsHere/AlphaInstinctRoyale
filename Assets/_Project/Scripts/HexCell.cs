using UnityEngine;
using AIR.Shared.GameSession;

public class HexCell : MonoBehaviour
{
    public int q;
    public int r;
    public string tileId;

    public void Init(int q, int r)
    {
        Init(new ArenaTileState
        {
            TileId = HexArenaUtils.FormatTileId(q, r),
            Q = q,
            R = r,
        });
    }

    public void Init(ArenaTileState tile)
    {
        if (tile == null)
        {
            return;
        }

        q = tile.Q;
        r = tile.R;
        tileId = tile.TileId;

        name = $"HexCell ({q}, {r})";

        // Create a text label
        GameObject labelObj = new GameObject("Label");
        labelObj.transform.SetParent(transform);
        labelObj.transform.localPosition = new Vector3(0, 0.1f, 0);
        labelObj.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        
        var textMesh = labelObj.AddComponent<TextMesh>();
        textMesh.text = tileId;
        textMesh.characterSize = 0.2f;
        textMesh.fontSize = 32;
        textMesh.anchor = TextAnchor.MiddleCenter;
        textMesh.alignment = TextAlignment.Center;
        textMesh.color = Color.black;

        labelObj.AddComponent<WorldLabel>();
    }
}
