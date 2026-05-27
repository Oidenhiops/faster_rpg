using System.Collections.Generic;
using UnityEngine;

// A* sobre GridMap. Estático y sin estado externo: todos los buffers internos se reusan entre llamadas
// para no presionar el GC cuando hay muchas peticiones por segundo (varios NPCs pidiendo path).
//
// Uso:
//   List<Vector3> path = Pathfinder.FindPath(player.position, target.position);
//   if (path != null) seguir waypoints;
//
// Devuelve null si no hay ruta posible. Devuelve una lista con un solo punto si start y end están en el mismo bloque.
public static class Pathfinder
{
    // Buffers reusables. Se limpian al inicio de cada FindPath en vez de reasignarse.
    static readonly Dictionary<Vector3Int, float>      gScore   = new Dictionary<Vector3Int, float>(256);
    static readonly Dictionary<Vector3Int, Vector3Int> cameFrom = new Dictionary<Vector3Int, Vector3Int>(256);
    static readonly HashSet<Vector3Int>                closed   = new HashSet<Vector3Int>();
    static readonly BinaryHeap<Vector3Int>             open     = new BinaryHeap<Vector3Int>(256);

    // Para que Pathfinder no instancie List<Vector3> nuevas si el caller le pasa una.
    static readonly List<Vector3> _pathScratch = new List<Vector3>(64);

    public struct Stats
    {
        public int nodesExplored;
        public int pathLength;
        public bool found;
    }
    public static Stats LastStats { get; private set; }

    // ---------- API pública ----------

    public static List<Vector3> FindPath(Vector3 startWorld, Vector3 endWorld, GridMap map = null, List<Vector3Int> exploredOut = null)
    {
        if (map == null) map = GridMap.Instance;
        if (map == null)
        {
            Debug.LogWarning("[Pathfinder] No hay GridMap activo.");
            return null;
        }

        Vector3Int startGrid = map.WorldToGrid(startWorld);
        Vector3Int endGrid   = map.WorldToGrid(endWorld);

        return FindPathGrid(startGrid, endGrid, map, exploredOut);
    }

    public static List<Vector3> FindPathGrid(Vector3Int start, Vector3Int end, GridMap map = null, List<Vector3Int> exploredOut = null)
    {
        if (map == null) map = GridMap.Instance;
        if (map == null) return null;

        LastStats = default;

        // Si el start o end no son transitables, intentamos snap al vecino transitable más cercano.
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

            if (!closed.Add(current)) continue; // si ya estaba cerrado (entrada duplicada en heap), saltamos
            explored++;
            exploredOut?.Add(current);

            float currentG = gScore[current];

            List<GridMap.NeighborEdge> edges = map.GetNeighborEdges(current);
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

    // ---------- Internas ----------

    // Manhattan 3D. Admisible y consistente para movimiento en 6 direcciones cardinales con costos >= 1.
    // Si en el futuro permitimos diagonales, cambiar a octile 3D.
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
        // Copia a una lista nueva para devolverla al caller — el scratch se reusa en la próxima llamada.
        return new List<Vector3>(_pathScratch);
    }

    // BFS muy corto alrededor del punto buscando un bloque transitable. Útil cuando el caller pasa una posición
    // de mundo que cayó en un bloque ocupado (ej. el destino está dentro de un cofre).
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

    // ---------- Path smoothing por line-of-sight ----------

    // Reduce el path eliminando waypoints intermedios cuando hay línea recta libre entre i e i+2 según el grid.
    // No usa Physics: muestrea el segmento contra el GridMap. Devuelve un nuevo List<Vector3> con menos puntos.
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
            if (!HasGridLineOfSight(path[anchor], path[i], map, samplesPerUnit))
            {
                // Cierra el segmento en el waypoint anterior.
                result.Add(path[i - 1]);
                anchor = i - 1;
            }
        }
        result.Add(path[path.Count - 1]);
        return result;
    }

    static bool HasGridLineOfSight(Vector3 a, Vector3 b, GridMap map, int samplesPerUnit)
    {
        float dist = Vector3.Distance(a, b);
        int samples = Mathf.Max(2, Mathf.CeilToInt(dist * samplesPerUnit));
        for (int s = 1; s < samples; s++)
        {
            float t = s / (float)samples;
            Vector3 p = Vector3.Lerp(a, b, t);
            Vector3Int g = map.WorldToGrid(p);
            if (!map.IsTraversable(g)) return false;
        }
        return true;
    }
}
