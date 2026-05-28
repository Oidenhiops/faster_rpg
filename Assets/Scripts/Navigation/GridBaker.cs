using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(-100)]
public class GridBaker : MonoBehaviour
{
    public GridMap gridMap;
    public bool bakeOnAwake = true;
    public bool bakeOnStart = false;
    public bool includeInactiveMarkers = false;
    public bool warnOnDuplicateCells = true;

    void Awake()
    {
        if (gridMap == null) gridMap = GridMap.Instance != null ? GridMap.Instance : GetComponent<GridMap>();
        if (bakeOnAwake) Bake();
    }

    void Start()
    {
        if (bakeOnStart) Bake();
    }

    [ContextMenu("Bake")]
    public void Bake()
    {
        if (gridMap == null)
        {
            Debug.LogError("[GridBaker] No hay referencia a GridMap.");
            return;
        }

        gridMap.Clear();
        BakeMarkers(null);
        Debug.Log($"[GridBaker] Bake completo. {gridMap.BlockCount} bloques registrados.");
    }

    public void Rebake(Bounds region)
    {
        if (gridMap == null) return;

        List<Vector3Int> toRemove = new List<Vector3Int>();
        foreach (var kv in gridMap.Blocks)
        {
            Vector3 world = gridMap.GridToWorld(kv.Key);
            if (region.Contains(world)) toRemove.Add(kv.Key);
        }
        for (int i = 0; i < toRemove.Count; i++) gridMap.RemoveBlock(toRemove[i]);

        BakeMarkers(region);
    }

    void BakeMarkers(Bounds? region)
    {
        BlockMarker[] markers = includeInactiveMarkers
            ? Resources.FindObjectsOfTypeAll<BlockMarker>()
            : FindObjectsByType<BlockMarker>(FindObjectsSortMode.None);

        for (int i = 0; i < markers.Length; i++)
        {
            BlockMarker m = markers[i];
            if (m == null) continue;
            if (!m.gameObject.scene.IsValid()) continue;

            if (region.HasValue && !region.Value.Contains(m.transform.position)) continue;

            if (m.dynamic) continue;

            Vector3Int pos = m.ResolveGridPos(gridMap.blockSize, gridMap.gridOrigin);

            if (gridMap.HasBlock(pos))
            {
                if (warnOnDuplicateCells)
                    Debug.LogWarning($"[GridBaker] Celda duplicada en {pos}. Conservo el primer marker e ignoro '{m.name}'.", m);
                continue;
            }

            Block b = new Block(pos, m.openFaces, m.isWalkable, m.moveCost)
            {
                sourceObject = m.gameObject,
                stairUpDirection = m.stairUpDirection
            };

            gridMap.AddBlock(b);

            m.cachedGridPos = pos;
            m.cachedBlockSize = gridMap.blockSize;
        }
    }
}
