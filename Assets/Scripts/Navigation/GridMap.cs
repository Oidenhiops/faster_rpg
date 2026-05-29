using System;
using System.Collections.Generic;
using UnityEngine;

public class GridMap : MonoBehaviour
{
    public static GridMap Instance { get; private set; }

    public float blockSize = 1f;
    public Vector3 gridOrigin = Vector3.zero;
    public bool requireHeadroom = true;
    [Min(1)] public int chunkSize = 16;

    Dictionary<Vector3Int, Block> blocks = new Dictionary<Vector3Int, Block>();
    Dictionary<Vector3Int, int> occupancyCount = new Dictionary<Vector3Int, int>();
    Dictionary<Vector3Int, GridChunk> chunks = new Dictionary<Vector3Int, GridChunk>();
    HashSet<Vector3Int> dirtyChunks = new HashSet<Vector3Int>();

    public int BlockCount => blocks.Count;
    public int ChunkCount => chunks.Count;
    public int DirtyChunkCount => dirtyChunks.Count;
    public IReadOnlyDictionary<Vector3Int, Block> Blocks => blocks;
    public IReadOnlyDictionary<Vector3Int, GridChunk> Chunks => chunks;
    public IEnumerable<Vector3Int> DirtyChunks => dirtyChunks;

    public event Action<Vector3Int> OnChunkDirty;
    public event Action<Vector3Int> OnChunkRegenerated;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public Block GetBlock(Vector3Int pos)
    {
        blocks.TryGetValue(pos, out Block b);
        return b;
    }

    public bool TryGetBlock(Vector3Int pos, out Block block)
    {
        return blocks.TryGetValue(pos, out block);
    }

    public bool HasBlock(Vector3Int pos)
    {
        return blocks.ContainsKey(pos);
    }

    public bool IsTraversable(Vector3Int pos)
    {
        if (!blocks.TryGetValue(pos, out Block b)) return false;
        if (!b.IsTraversable) return false;
        if (GetOccupancyCount(pos) > 0) return false;
        if (requireHeadroom && blocks.ContainsKey(pos + Vector3Int.up)) return false;
        return true;
    }

    public int GetOccupancyCount(Vector3Int pos)
    {
        return occupancyCount.TryGetValue(pos, out int n) ? n : 0;
    }

    public void AddBlock(Block block)
    {
        if (block == null) return;
        blocks[block.gridPos] = block;
        GetOrCreateChunk(CellToChunk(block.gridPos)).cells.Add(block.gridPos);
        MarkChunkDirty(CellToChunk(block.gridPos));
    }

    public void RemoveBlock(Vector3Int pos)
    {
        if (!blocks.Remove(pos)) return;
        Vector3Int chunkCoord = CellToChunk(pos);
        if (chunks.TryGetValue(chunkCoord, out GridChunk chunk))
        {
            chunk.cells.Remove(pos);
            MarkChunkDirty(chunkCoord);
            if (chunk.cells.Count == 0) chunks.Remove(chunkCoord);
        }
    }

    public void Clear()
    {
        blocks.Clear();
        occupancyCount.Clear();
        foreach (var c in chunks.Values) c.cells.Clear();
        chunks.Clear();
        dirtyChunks.Clear();
    }

    public void MarkOccupiedOnTop(Vector3Int pos, bool value)
    {
        if (blocks.TryGetValue(pos, out Block b))
        {
            b.isOccupiedOnTop = value;
            MarkChunkDirty(CellToChunk(pos));
        }
    }

    public void AddOccupancy(Vector3Int pos)
    {
        occupancyCount.TryGetValue(pos, out int n);
        occupancyCount[pos] = n + 1;
        MarkChunkDirty(CellToChunk(pos));
    }

    public void RemoveOccupancy(Vector3Int pos)
    {
        if (!occupancyCount.TryGetValue(pos, out int n)) return;
        if (n <= 1) occupancyCount.Remove(pos);
        else occupancyCount[pos] = n - 1;
        MarkChunkDirty(CellToChunk(pos));
    }

    public Vector3Int CellToChunk(Vector3Int cell)
    {
        return new Vector3Int(
            FloorDiv(cell.x, chunkSize),
            FloorDiv(cell.y, chunkSize),
            FloorDiv(cell.z, chunkSize));
    }

    public Bounds ChunkWorldBounds(Vector3Int chunkCoord)
    {
        float chunkWorldSize = chunkSize * blockSize;
        float halfBlock = blockSize * 0.5f;
        Vector3 center = gridOrigin + new Vector3(
            chunkCoord.x * chunkWorldSize + chunkWorldSize * 0.5f - halfBlock,
            chunkCoord.y * chunkWorldSize + chunkWorldSize * 0.5f - halfBlock,
            chunkCoord.z * chunkWorldSize + chunkWorldSize * 0.5f - halfBlock);
        return new Bounds(center, Vector3.one * chunkWorldSize);
    }

    GridChunk GetOrCreateChunk(Vector3Int coord)
    {
        if (!chunks.TryGetValue(coord, out GridChunk chunk))
        {
            chunk = new GridChunk { coord = coord };
            chunks[coord] = chunk;
        }
        return chunk;
    }

    public bool IsChunkDirty(Vector3Int chunkCoord) => dirtyChunks.Contains(chunkCoord);

    public void MarkChunkDirty(Vector3Int chunkCoord)
    {
        if (dirtyChunks.Add(chunkCoord))
        {
            if (chunks.TryGetValue(chunkCoord, out GridChunk chunk)) chunk.isDirty = true;
            OnChunkDirty?.Invoke(chunkCoord);
        }
    }

    public void MarkChunkClean(Vector3Int chunkCoord)
    {
        if (dirtyChunks.Remove(chunkCoord))
        {
            if (chunks.TryGetValue(chunkCoord, out GridChunk chunk)) chunk.isDirty = false;
            OnChunkRegenerated?.Invoke(chunkCoord);
        }
    }

    public IEnumerable<Block> GetChunkBlocks(Vector3Int chunkCoord)
    {
        if (!chunks.TryGetValue(chunkCoord, out GridChunk chunk)) yield break;
        foreach (Vector3Int cell in chunk.cells)
        {
            if (blocks.TryGetValue(cell, out Block b)) yield return b;
        }
    }

    static int FloorDiv(int a, int b)
    {
        int q = a / b;
        int r = a % b;
        if ((r != 0) && ((r < 0) != (b < 0))) q--;
        return q;
    }

    public IEnumerable<Block> GetTraversableNeighbors(Vector3Int pos, int jumpDistance = 0, int dropDistance = 0)
    {
        if (!blocks.ContainsKey(pos)) yield break;
        if (!IsTraversable(pos)) yield break;

        var edges = GetNeighborEdges(pos, jumpDistance, dropDistance);
        int count = edges.Count;
        Block[] copy = new Block[count];
        for (int e = 0; e < count; e++) copy[e] = edges[e].neighbor;
        for (int e = 0; e < count; e++) yield return copy[e];
    }

    static readonly List<NeighborEdge> _neighborBuffer = new List<NeighborEdge>(16);

    public List<NeighborEdge> GetNeighborEdges(Vector3Int pos, int jumpDistance = 0, int dropDistance = 0)
    {
        _neighborBuffer.Clear();
        if (!blocks.TryGetValue(pos, out Block current)) return _neighborBuffer;
        if (!IsTraversable(pos)) return _neighborBuffer;

        if (jumpDistance < 0) jumpDistance = 0;
        if (dropDistance < 0) dropDistance = 0;

        for (int i = 2; i < BlockFaceExtensions.FaceOrder.Length; i++)
        {
            BlockFace face = BlockFaceExtensions.FaceOrder[i];
            if (!current.openFaces.HasFace(face)) continue;

            Vector3Int dOffset = BlockFaceExtensions.NeighborOffsets[i];
            BlockFace opposite = face.Opposite();

            for (int dy = -dropDistance; dy <= jumpDistance; dy++)
            {
                Vector3Int neighborPos = pos + dOffset + Vector3Int.up * dy;
                if (!blocks.TryGetValue(neighborPos, out Block neighbor)) continue;
                if (!IsTraversable(neighborPos)) continue;
                if (!neighbor.openFaces.HasFace(opposite)) continue;

                if (dy > 0)
                {
                    bool blockedAbove = false;
                    for (int up = 1; up <= dy + 1; up++)
                    {
                        if (blocks.ContainsKey(pos + Vector3Int.up * up)) { blockedAbove = true; break; }
                    }
                    if (blockedAbove) continue;

                    bool wallInPath = false;
                    for (int below = 1; below < dy; below++)
                    {
                        if (blocks.ContainsKey(pos + dOffset + Vector3Int.up * below)) { wallInPath = true; break; }
                    }
                    if (wallInPath) continue;

                    if (blocks.ContainsKey(pos + dOffset + Vector3Int.up * (dy + 1))) continue;
                }
                else if (dy < 0)
                {
                    bool wallInFallPath = false;

                    for (int up = 0; up > dy; up--)
                    {
                        if (blocks.ContainsKey(pos + dOffset + Vector3Int.up * up)) { wallInFallPath = true; break; }
                    }
                    if (wallInFallPath) continue;

                    for (int up = -1; up > dy; up--)
                    {
                        if (blocks.ContainsKey(pos + Vector3Int.up * up)) { wallInFallPath = true; break; }
                    }
                    if (wallInFallPath) continue;

                    if (blocks.ContainsKey(pos + dOffset + Vector3Int.up)) continue;
                }

                float cost = neighbor.moveCost + Mathf.Abs(dy);
                _neighborBuffer.Add(new NeighborEdge { neighbor = neighbor, cost = cost });
            }
        }

        return _neighborBuffer;
    }

    public struct NeighborEdge
    {
        public Block neighbor;
        public float cost;
    }

    public Vector3Int WorldToGrid(Vector3 world)
    {
        Vector3 local = (world - gridOrigin) / blockSize;
        return new Vector3Int(
            Mathf.RoundToInt(local.x),
            Mathf.RoundToInt(local.y),
            Mathf.RoundToInt(local.z));
    }

    public Vector3 GridToWorld(Vector3Int grid)
    {
        return gridOrigin + new Vector3(
            grid.x * blockSize,
            grid.y * blockSize,
            grid.z * blockSize);
    }
}

public class GridChunk
{
    public Vector3Int coord;
    public HashSet<Vector3Int> cells = new HashSet<Vector3Int>();
    public bool isDirty;
}
