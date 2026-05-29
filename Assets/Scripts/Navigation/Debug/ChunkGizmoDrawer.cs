using UnityEngine;

[ExecuteAlways]
public class ChunkGizmoDrawer : MonoBehaviour
{
    public bool drawChunks = true;
    public bool showOnlyDirty = false;
    public bool fillFaces = false;
    public bool showCoordLabels = false;
    public bool onlyWhenSelected = false;

    [Range(0f, 1f)] public float fillAlpha = 0.05f;
    [Range(0f, 1f)] public float edgeAlpha = 0.6f;

    public Color cleanColor = new Color(0.3f, 0.7f, 1f, 1f);
    public Color dirtyColor = new Color(1f, 0.6f, 0.1f, 1f);

#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        if (onlyWhenSelected) return;
        Draw();
    }

    void OnDrawGizmosSelected()
    {
        if (!onlyWhenSelected) return;
        Draw();
    }

    void Draw()
    {
        if (!drawChunks) return;

        GridMap map = GridMap.Instance != null ? GridMap.Instance : GetComponent<GridMap>();
        if (map == null) return;
        if (map.ChunkCount == 0) return;

        foreach (var kv in map.Chunks)
        {
            Vector3Int coord = kv.Key;
            GridChunk chunk = kv.Value;

            bool isDirty = map.IsChunkDirty(coord);
            if (showOnlyDirty && !isDirty) continue;

            Bounds bounds = map.ChunkWorldBounds(coord);
            Color baseColor = isDirty ? dirtyColor : cleanColor;

            if (fillFaces)
            {
                Gizmos.color = new Color(baseColor.r, baseColor.g, baseColor.b, fillAlpha);
                Gizmos.DrawCube(bounds.center, bounds.size);
            }

            Gizmos.color = new Color(baseColor.r, baseColor.g, baseColor.b, edgeAlpha);
            Gizmos.DrawWireCube(bounds.center, bounds.size);

            if (showCoordLabels)
            {
                UnityEditor.Handles.color = baseColor;
                UnityEditor.Handles.Label(bounds.center, $"{coord}\n{chunk.cells.Count} cells");
            }
        }
    }
#endif
}
