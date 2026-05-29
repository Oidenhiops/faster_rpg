using UnityEngine;

[ExecuteAlways]
public class GridGizmoDrawer : MonoBehaviour
{
    public bool drawBlocks = true;
    public bool drawConnections = false;
    public bool onlyWhenSelected = false;

    [Range(0.02f, 0.5f)] public float markerRadius = 0.12f;

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

        foreach (var kv in map.Blocks)
        {
            Vector3Int pos = kv.Key;
            Block b = kv.Value;
            Vector3 center = map.GridToWorld(pos);

            Color c;
            if (!b.isWalkable) c = unwalkableColor;
            else if (b.isOccupiedOnTop || map.GetOccupancyCount(pos) > 0) c = occupiedColor;
            else c = walkableColor;

            if (drawBlocks)
            {
                Gizmos.color = c;
                Gizmos.DrawSphere(center, markerRadius);
            }

            if (drawConnections)
            {
                Gizmos.color = connectionColor;
                foreach (Block neighbor in map.GetTraversableNeighbors(pos))
                {
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
