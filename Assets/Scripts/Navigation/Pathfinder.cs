using System.Collections.Generic;
using UnityEngine;

public static class Pathfinder
{
    static readonly Dictionary<Vector3Int, float>      gScore   = new Dictionary<Vector3Int, float>(256);
    static readonly Dictionary<Vector3Int, Vector3Int> cameFrom = new Dictionary<Vector3Int, Vector3Int>(256);
    static readonly HashSet<Vector3Int>                closed   = new HashSet<Vector3Int>();
    static readonly BinaryHeap<Vector3Int>             open     = new BinaryHeap<Vector3Int>(256);

    static readonly List<Vector3> _pathScratch = new List<Vector3>(64);

    public struct Stats
    {
        public int nodesExplored;
        public int pathLength;
        public bool found;
    }
    public static Stats LastStats { get; private set; }

    public static List<Vector3> FindPath(Vector3 startWorld, Vector3 endWorld, int jumpDistance = 0, GridMap map = null, List<Vector3Int> exploredOut = null)
    {
        if (map == null) map = GridMap.Instance;
        if (map == null)
        {
            Debug.LogWarning("[Pathfinder] No hay GridMap activo.");
            return null;
        }

        Vector3Int startGrid = map.WorldToGrid(startWorld);
        Vector3Int endGrid   = map.WorldToGrid(endWorld);

        return FindPathGrid(startGrid, endGrid, jumpDistance, map, exploredOut);
    }

    public static List<Vector3> FindPathGrid(Vector3Int start, Vector3Int end, int jumpDistance = 0, GridMap map = null, List<Vector3Int> exploredOut = null)
    {
        if (map == null) map = GridMap.Instance;
        if (map == null) return null;

        LastStats = default;

        if (!map.IsTraversable(start))
        {
            if (!TryFindNearestTraversable(start, map, out start)) return null;
        }
        if (!map.IsTraversable(end))
        {
            if (!TryFindNearestTraversable(end, map, out end)) return null;
        }

        if (start == end)
        {
            List<Vector3> single = new List<Vector3>(1) { map.GridToWorld(end) };
            LastStats = new Stats { nodesExplored = 0, pathLength = 1, found = true };
            return single;
        }

        gScore.Clear();
        cameFrom.Clear();
        closed.Clear();
        open.Clear();

        gScore[start] = 0f;
        open.Enqueue(start, Heuristic(start, end));

        int explored = 0;

        while (open.Count > 0)
        {
            Vector3Int current = open.Dequeue();

            if (current == end)
            {
                List<Vector3> path = ReconstructPath(current, map);
                LastStats = new Stats { nodesExplored = explored, pathLength = path.Count, found = true };
                return path;
            }

            if (!closed.Add(current)) continue;
            explored++;
            exploredOut?.Add(current);

            float currentG = gScore[current];

            List<GridMap.NeighborEdge> edges = map.GetNeighborEdges(current, jumpDistance);
            for (int i = 0; i < edges.Count; i++)
            {
                Vector3Int neighborPos = edges[i].neighbor.gridPos;
                if (closed.Contains(neighborPos)) continue;

                float tentativeG = currentG + edges[i].cost;

                if (!gScore.TryGetValue(neighborPos, out float knownG) || tentativeG < knownG)
                {
                    gScore[neighborPos] = tentativeG;
                    cameFrom[neighborPos] = current;
                    float f = tentativeG + Heuristic(neighborPos, end);
                    open.Enqueue(neighborPos, f);
                }
            }
        }

        LastStats = new Stats { nodesExplored = explored, pathLength = 0, found = false };
        return null;
    }

    static float Heuristic(Vector3Int a, Vector3Int b)
    {
        int dx = Mathf.Abs(a.x - b.x);
        int dy = Mathf.Abs(a.y - b.y);
        int dz = Mathf.Abs(a.z - b.z);
        return dx + dy + dz;
    }

    static List<Vector3> ReconstructPath(Vector3Int end, GridMap map)
    {
        _pathScratch.Clear();
        Vector3Int current = end;
        _pathScratch.Add(map.GridToWorld(current));

        while (cameFrom.TryGetValue(current, out Vector3Int prev))
        {
            current = prev;
            _pathScratch.Add(map.GridToWorld(current));
        }

        _pathScratch.Reverse();
        return new List<Vector3>(_pathScratch);
    }

    static readonly Queue<Vector3Int> _snapQueue = new Queue<Vector3Int>();
    static readonly HashSet<Vector3Int> _snapVisited = new HashSet<Vector3Int>();
    const int SnapMaxNodes = 32;

    static bool TryFindNearestTraversable(Vector3Int origin, GridMap map, out Vector3Int result)
    {
        _snapQueue.Clear();
        _snapVisited.Clear();
        _snapQueue.Enqueue(origin);
        _snapVisited.Add(origin);

        int explored = 0;
        while (_snapQueue.Count > 0 && explored < SnapMaxNodes)
        {
            Vector3Int p = _snapQueue.Dequeue();
            explored++;

            if (map.IsTraversable(p))
            {
                result = p;
                return true;
            }

            for (int i = 0; i < BlockFaceExtensions.NeighborOffsets.Length; i++)
            {
                Vector3Int next = p + BlockFaceExtensions.NeighborOffsets[i];
                if (_snapVisited.Add(next)) _snapQueue.Enqueue(next);
            }
        }

        result = origin;
        return false;
    }

    public static List<Vector3> SmoothPath(List<Vector3> path, GridMap map = null, int samplesPerUnit = 4)
    {
        if (path == null || path.Count <= 2) return path;
        if (map == null) map = GridMap.Instance;
        if (map == null) return path;

        List<Vector3> result = new List<Vector3>(path.Count);
        result.Add(path[0]);

        int anchor = 0;
        for (int i = 2; i < path.Count; i++)
        {
            if (!CanDirectMove(path[anchor], path[i], map, samplesPerUnit))
            {
                result.Add(path[i - 1]);
                anchor = i - 1;
            }
        }
        result.Add(path[path.Count - 1]);
        return result;
    }

    static bool CanDirectMove(Vector3 a, Vector3 b, GridMap map, int samplesPerUnit)
    {
        Vector3Int aCell = map.WorldToGrid(a);
        Vector3Int bCell = map.WorldToGrid(b);
        Vector3Int diff = bCell - aCell;

        if (diff.y != 0) return false;

        float dist = Vector3.Distance(a, b);
        int samples = Mathf.Max(2, Mathf.CeilToInt(dist * samplesPerUnit));
        for (int s = 1; s < samples; s++)
        {
            float t = s / (float)samples;
            Vector3 p = Vector3.Lerp(a, b, t);
            Vector3Int g = map.WorldToGrid(p);
            if (!map.IsTraversable(g)) return false;
        }

        if (diff.x != 0 && diff.z != 0)
        {
            int sx = diff.x > 0 ? 1 : -1;
            int sz = diff.z > 0 ? 1 : -1;

            Vector3Int xCorner = aCell + new Vector3Int(sx, 0, 0);
            Vector3Int zCorner = aCell + new Vector3Int(0, 0, sz);
            if (!map.IsTraversable(xCorner)) return false;
            if (!map.IsTraversable(zCorner)) return false;

            Block aBlock = map.GetBlock(aCell);
            Block bBlock = map.GetBlock(bCell);
            if (aBlock == null || bBlock == null) return false;

            BlockFace xDir = sx > 0 ? BlockFace.East : BlockFace.West;
            BlockFace zDir = sz > 0 ? BlockFace.North : BlockFace.South;

            if (!aBlock.openFaces.HasFace(xDir)) return false;
            if (!aBlock.openFaces.HasFace(zDir)) return false;
            if (!bBlock.openFaces.HasFace(xDir.Opposite())) return false;
            if (!bBlock.openFaces.HasFace(zDir.Opposite())) return false;
        }

        return true;
    }
}
