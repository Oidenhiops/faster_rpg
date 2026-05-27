using UnityEngine;

// Dibuja el GridMap completo con gizmos en el editor. Color por estado del bloque.
// Ponlo en el mismo GameObject que el GridMap (o en cualquier objeto de la escena).
[ExecuteAlways]
public class GridGizmoDrawer : MonoBehaviour
{
    [Header("Qué mostrar")]
    public bool drawBlocks = true;
    public bool drawConnections = false;
    public bool onlyWhenSelected = false;

    [Header("Estilo")]
    [Range(0.05f, 1f)] public float blockScale = 0.85f;
    [Range(0f, 1f)]    public float alpha = 0.25f;
    public Color walkableColor   = new Color(0.3f, 1f, 0.5f, 1f);
    public Color unwalkableColor = new Color(0.6f, 0.6f, 0.6f, 1f);
    public Color occupiedColor   = new Color(1f, 0.4f, 0.2f, 1f);
    public Color connectionColor = new Color(0.4f, 0.8f, 1f, 0.6f);

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
        GridMap map = GridMap.Instance != null ? GridMap.Instance : GetComponent<GridMap>();
        if (map == null) return;
        if (map.BlockCount == 0) return;

        float size = map.blockSize * blockScale;

        foreach (var kv in map.Blocks)
        {
            Vector3Int pos = kv.Key;
            Block b = kv.Value;
            Vector3 center = map.GridToWorld(pos);

            Color c;
            if (!b.isWalkable) c = unwalkableColor;
            else if (b.isOccupiedOnTop || map.GetOccupancyCount(pos) > 0) c = occupiedColor;
            else c = walkableColor;

            c.a = alpha;

            if (drawBlocks)
            {
                Gizmos.color = c;
                Gizmos.DrawCube(center, Vector3.one * size);
                Gizmos.color = new Color(c.r, c.g, c.b, Mathf.Min(1f, alpha * 2.5f));
                Gizmos.DrawWireCube(center, Vector3.one * size);
            }

            if (drawConnections)
            {
                Gizmos.color = connectionColor;
                foreach (Block neighbor in map.GetTraversableNeighbors(pos))
                {
                    // Dibujamos solo una vez por par (cuando el hash del actual es menor).
                    if (PosHash(pos) < PosHash(neighbor.gridPos))
                    {
                        Gizmos.DrawLine(center, map.GridToWorld(neighbor.gridPos));
                    }
                }
            }
        }
    }

    static int PosHash(Vector3Int p)
    {
        unchecked { return p.x * 73856093 ^ p.y * 19349663 ^ p.z * 83492791; }
    }
#endif
}
