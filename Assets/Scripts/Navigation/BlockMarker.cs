using UnityEngine;

public class BlockMarker : MonoBehaviour
{
    public BlockFace openFaces = BlockFace.All;
    public bool isWalkable = true;
    [Min(0.01f)] public float moveCost = 1f;

    public BlockFace stairUpDirection = BlockFace.None;

    public bool dynamic = false;
    public float dynamicMoveThreshold = 0.1f;

    public bool drawGizmos = true;

    [HideInInspector] public Vector3Int cachedGridPos;
    [HideInInspector] public float cachedBlockSize = 1f;

    Vector3Int registeredCell;
    bool isRegistered;
    Vector3 lastSampledPos;

    public Vector3Int ResolveGridPos(float blockSize, Vector3 gridOrigin)
    {
        Vector3 local = (transform.position - gridOrigin) / blockSize;
        return new Vector3Int(
            Mathf.FloorToInt(local.x),
            Mathf.FloorToInt(local.y),
            Mathf.FloorToInt(local.z));
    }

    void OnEnable()
    {
        if (!Application.isPlaying) return;
        if (!dynamic) return;
        RegisterDynamicInGrid();
    }

    void OnDisable()
    {
        if (!Application.isPlaying) return;
        if (!dynamic) return;
        UnregisterDynamicFromGrid();
    }

    void RegisterDynamicInGrid()
    {
        if (isRegistered) return;
        GridMap map = GridMap.Instance;
        if (map == null) return;

        Vector3Int cell = ResolveGridPos(map.blockSize, map.gridOrigin);
        if (map.HasBlock(cell))
        {
            Debug.LogWarning($"[BlockMarker] '{name}' no se registra en celda {cell}: ya existe otro bloque ahí.", this);
            return;
        }

        Block b = new Block(cell, openFaces, isWalkable, moveCost)
        {
            sourceObject = gameObject,
            stairUpDirection = stairUpDirection
        };
        map.AddBlock(b);

        registeredCell = cell;
        cachedGridPos = cell;
        cachedBlockSize = map.blockSize;
        lastSampledPos = transform.position;
        isRegistered = true;
    }

    void UnregisterDynamicFromGrid()
    {
        if (!isRegistered) return;
        GridMap.Instance?.RemoveBlock(registeredCell);
        isRegistered = false;
    }

    void UpdateDynamicCellIfMoved()
    {
        if (!isRegistered) { RegisterDynamicInGrid(); return; }
        if ((transform.position - lastSampledPos).sqrMagnitude < dynamicMoveThreshold * dynamicMoveThreshold) return;

        GridMap map = GridMap.Instance;
        if (map == null) return;

        Vector3Int newCell = ResolveGridPos(map.blockSize, map.gridOrigin);
        if (newCell != registeredCell)
        {
            Block existing = map.GetBlock(registeredCell);
            if (existing == null)
            {
                isRegistered = false;
                RegisterDynamicInGrid();
                return;
            }

            if (map.HasBlock(newCell))
            {
                map.RemoveBlock(registeredCell);
                isRegistered = false;
                lastSampledPos = transform.position;
                return;
            }

            map.RemoveBlock(registeredCell);
            existing.gridPos = newCell;
            map.AddBlock(existing);
            registeredCell = newCell;
            cachedGridPos = newCell;
        }
        lastSampledPos = transform.position;
    }

    void Update()
    {
        if (!Application.isPlaying) return;
        if (dynamic) UpdateDynamicCellIfMoved();
    }

#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        if (!drawGizmos) return;

        float size = cachedBlockSize > 0f ? cachedBlockSize : 1f;
        if (GridMap.Instance != null) size = GridMap.Instance.blockSize;

        Vector3 center;
        if (GridMap.Instance != null)
        {
            Vector3Int gridPos = ResolveGridPos(size, GridMap.Instance.gridOrigin);
            center = GridMap.Instance.GridToWorld(gridPos);
        }
        else
        {
            center = transform.position;
        }

        float half = size * 0.5f;

        Vector3 topCenter = center + Vector3.up * (half + 0.01f);
        float arrowReach = half * 0.7f;
        float headSize   = half * 0.18f;

        for (int i = 2; i < BlockFaceExtensions.FaceOrder.Length; i++)
        {
            BlockFace face = BlockFaceExtensions.FaceOrder[i];
            Vector3 dir = BlockFaceExtensions.NeighborOffsets[i];
            bool open = openFaces.HasFace(face);

            if (open)
            {
                Vector3 tip = topCenter + dir * arrowReach;
                Gizmos.color = new Color(0.2f, 1f, 0.5f, 0.95f);
                Gizmos.DrawLine(topCenter, tip);

                Vector3 perp = Vector3.Cross(dir, Vector3.up).normalized * headSize;
                Vector3 back = tip - dir * headSize;
                Gizmos.DrawLine(tip, back + perp);
                Gizmos.DrawLine(tip, back - perp);
            }
            else
            {
                Vector3 edge = topCenter + dir * arrowReach;
                Vector3 perp = Vector3.Cross(dir, Vector3.up).normalized * (half * 0.22f);
                Gizmos.color = new Color(1f, 0.25f, 0.25f, 0.9f);
                Gizmos.DrawLine(edge - perp, edge + perp);
            }
        }

        Gizmos.color = isWalkable ? new Color(1f, 1f, 1f, 0.3f) : new Color(0.4f, 0.4f, 0.4f, 0.3f);
        Gizmos.DrawWireCube(center, Vector3.one * size);

        if (stairUpDirection != BlockFace.None)
        {
            int stairIdx = -1;
            for (int i = 0; i < BlockFaceExtensions.FaceOrder.Length; i++)
            {
                if (BlockFaceExtensions.FaceOrder[i] == stairUpDirection) { stairIdx = i; break; }
            }
            if (stairIdx >= 0)
            {
                Vector3 dir = BlockFaceExtensions.NeighborOffsets[stairIdx];

                Vector3 tiltedDir = (dir + Vector3.up).normalized;

                float reach = half * 0.85f;
                Vector3 backLow   = center - tiltedDir * reach;
                Vector3 frontHigh = center + tiltedDir * reach;

                Gizmos.color = new Color(1f, 0.85f, 0.1f, 1f);
                Gizmos.DrawLine(backLow, frontHigh);

                Vector3 perpInSlopePlane = (dir - Vector3.up).normalized;
                float headLen = size * 0.18f;
                Vector3 back = frontHigh - tiltedDir * headLen;
                Gizmos.DrawLine(frontHigh, back + perpInSlopePlane * headLen * 0.6f);
                Gizmos.DrawLine(frontHigh, back - perpInSlopePlane * headLen * 0.6f);
            }
        }
    }
#endif
}
