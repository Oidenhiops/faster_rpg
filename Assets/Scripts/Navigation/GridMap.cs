using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(-200)]
public class GridMap : MonoBehaviour
{
    public static GridMap Instance { get; private set; }

    public float blockSize = 1f;
    public Vector3 gridOrigin = Vector3.zero;
    public bool requireHeadroom = true;

    Dictionary<Vector3Int, Block> blocks = new Dictionary<Vector3Int, Block>();
    Dictionary<Vector3Int, int> occupancyCount = new Dictionary<Vector3Int, int>();

    public int BlockCount => blocks.Count;
    public IReadOnlyDictionary<Vector3Int, Block> Blocks => blocks;

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
    }

    public void RemoveBlock(Vector3Int pos)
    {
        blocks.Remove(pos);
    }

    public void Clear()
    {
        blocks.Clear();
        occupancyCount.Clear();
    }

    public void MarkOccupiedOnTop(Vector3Int pos, bool value)
    {
        if (blocks.TryGetValue(pos, out Block b))
        {
            b.isOccupiedOnTop = value;
        }
    }

    public void AddOccupancy(Vector3Int pos)
    {
        occupancyCount.TryGetValue(pos, out int n);
        occupancyCount[pos] = n + 1;
    }

    public void RemoveOccupancy(Vector3Int pos)
    {
        if (!occupancyCount.TryGetValue(pos, out int n)) return;
        if (n <= 1) occupancyCount.Remove(pos);
        else occupancyCount[pos] = n - 1;
    }

    public IEnumerable<Block> GetTraversableNeighbors(Vector3Int pos)
    {
        if (!blocks.ContainsKey(pos)) yield break;
        if (!IsTraversable(pos)) yield break;

        var edges = GetNeighborEdges(pos);
        int count = edges.Count;
        Block[] copy = new Block[count];
        for (int e = 0; e < count; e++) copy[e] = edges[e].neighbor;
        for (int e = 0; e < count; e++) yield return copy[e];
    }

    static readonly List<NeighborEdge> _neighborBuffer = new List<NeighborEdge>(10);

    static readonly BlockFace[] _horizontalFaces = new BlockFace[] {
        BlockFace.North, BlockFace.South, BlockFace.East, BlockFace.West
    };

    public List<NeighborEdge> GetNeighborEdges(Vector3Int pos)
    {
        _neighborBuffer.Clear();
        if (!blocks.TryGetValue(pos, out Block current)) return _neighborBuffer;
        if (!IsTraversable(pos)) return _neighborBuffer;

        for (int i = 2; i < BlockFaceExtensions.FaceOrder.Length; i++)
        {
            BlockFace face = BlockFaceExtensions.FaceOrder[i];
            if (!current.openFaces.HasFace(face)) continue;

            Vector3Int neighborPos = pos + BlockFaceExtensions.NeighborOffsets[i];
            if (!blocks.TryGetValue(neighborPos, out Block neighbor)) continue;
            if (!IsTraversable(neighborPos)) continue;
            if (!neighbor.openFaces.HasFace(face.Opposite())) continue;

            _neighborBuffer.Add(new NeighborEdge { neighbor = neighbor, cost = neighbor.moveCost });
        }

        for (int i = 0; i < _horizontalFaces.Length; i++)
        {
            BlockFace dir = _horizontalFaces[i];
            int faceIdx = (int)i + 2;
            Vector3Int dOffset = BlockFaceExtensions.NeighborOffsets[faceIdx];

            TryAddStairEdge(pos, dOffset + Vector3Int.up, dir, dir, current);
            TryAddStairEdge(pos, dOffset + Vector3Int.down, dir, dir.Opposite(), current);
        }

        return _neighborBuffer;
    }

    void TryAddStairEdge(Vector3Int pos, Vector3Int totalOffset, BlockFace cardinalDir, BlockFace requiredStairDir, Block current)
    {
        Vector3Int target = pos + totalOffset;
        if (!blocks.TryGetValue(target, out Block neighbor)) return;
        if (!IsTraversable(target)) return;

        if (!current.openFaces.HasFace(cardinalDir)) return;
        if (!neighbor.openFaces.HasFace(cardinalDir.Opposite())) return;

        bool valid = current.stairUpDirection == requiredStairDir
                  || neighbor.stairUpDirection == requiredStairDir;
        if (!valid) return;

        _neighborBuffer.Add(new NeighborEdge { neighbor = neighbor, cost = 2f * neighbor.moveCost });
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
            Mathf.FloorToInt(local.x),
            Mathf.FloorToInt(local.y),
            Mathf.FloorToInt(local.z));
    }

    public Vector3 GridToWorld(Vector3Int grid)
    {
        return gridOrigin + new Vector3(
            (grid.x + 0.5f) * blockSize,
            (grid.y + 0.5f) * blockSize,
            (grid.z + 0.5f) * blockSize);
    }
}
