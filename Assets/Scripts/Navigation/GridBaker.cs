using System.Collections.Generic;
using UnityEngine;

public class GridBaker : MonoBehaviour
{
    public GridMap gridMap;
    public bool bakeOnAwake = true;
    public bool bakeOnStart = false;
    public bool includeInactiveMarkers = false;
    public bool warnOnDuplicateCells = true;

    public bool autoRegenerateDirtyChunks = false;

    void Awake()
    {
        if (gridMap == null) gridMap = GridMap.Instance != null ? GridMap.Instance : GetComponent<GridMap>();
        if (bakeOnAwake) Bake();
    }

    void Start()
    {
        if (bakeOnStart) Bake();
    }

    void LateUpdate()
    {
        if (!autoRegenerateDirtyChunks) return;
        if (gridMap == null || gridMap.DirtyChunkCount == 0) return;
        RegenerateAllDirtyChunks();
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

        foreach (var coord in new List<Vector3Int>(gridMap.DirtyChunks))
        {
            gridMap.MarkChunkClean(coord);
        }

        Debug.Log($"[GridBaker] Bake completo. {gridMap.BlockCount} bloques en {gridMap.ChunkCount} chunks.");
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

    [ContextMenu("Regenerate All Dirty Chunks")]
    public void RegenerateAllDirtyChunks()
    {
        if (gridMap == null) return;
        if (gridMap.DirtyChunkCount == 0) return;

        var snapshot = new List<Vector3Int>(gridMap.DirtyChunks);
        for (int i = 0; i < snapshot.Count; i++)
        {
            RegenerateChunk(snapshot[i]);
        }
    }

    public void RegenerateChunk(Vector3Int chunkCoord)
    {
        if (gridMap == null) return;

        Bounds bounds = gridMap.ChunkWorldBounds(chunkCoord);

        List<Vector3Int> toRemove = new List<Vector3Int>();
        foreach (var kv in gridMap.Blocks)
        {
            if (gridMap.CellToChunk(kv.Key) == chunkCoord) toRemove.Add(kv.Key);
        }
        for (int i = 0; i < toRemove.Count; i++) gridMap.RemoveBlock(toRemove[i]);

        BakeMarkers(bounds);

        gridMap.MarkChunkClean(chunkCoord);
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
                sourceObject = m.gameObject
            };

            gridMap.AddBlock(b);

            m.cachedGridPos = pos;
            m.cachedBlockSize = gridMap.blockSize;
            m.TrackBlock(b);
        }
    }
}
