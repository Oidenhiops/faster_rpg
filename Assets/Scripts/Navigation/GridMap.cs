using System.Collections.Generic;
using UnityEngine;

public class GridMap : MonoBehaviour
{
    public static GridMap Instance { get; private set; }

    // Tamaño en mundo de un bloque (lado del cubo). 1f = un cubo unitario por nodo.
    public float blockSize = 1f;

    // Offset del origen de la grilla en coordenadas de mundo. La esquina inferior-suroeste-baja del bloque (0,0,0).
    public Vector3 gridOrigin = Vector3.zero;

    // Almacenamiento principal. Lookup O(1) por Vector3Int.
    Dictionary<Vector3Int, Block> blocks = new Dictionary<Vector3Int, Block>();

    // Contador de ocupantes por celda. Permite que múltiples objetos (cofre + item dropeado) cohabiten temporalmente
    // sin que al quitar uno se "limpie" la ocupación del otro. La celda es transitable solo cuando el contador es 0.
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

    // ---------- API de consulta ----------

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

    // Caminable AHORA: existe, es walkable, no tiene ningún ocupante registrado y su flag manual está limpio.
    public bool IsTraversable(Vector3Int pos)
    {
        if (!blocks.TryGetValue(pos, out Block b)) return false;
        if (!b.IsTraversable) return false;
        return GetOccupancyCount(pos) == 0;
    }

    public int GetOccupancyCount(Vector3Int pos)
    {
        return occupancyCount.TryGetValue(pos, out int n) ? n : 0;
    }

    // ---------- API de mutación ----------

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

    // Flag manual de ocupación (uso desde inspector o lógica de gameplay puntual).
    // Para objetos dinámicos prefiere AddOccupancy/RemoveOccupancy del componente GridOccupant.
    public void MarkOccupiedOnTop(Vector3Int pos, bool value)
    {
        if (blocks.TryGetValue(pos, out Block b))
        {
            b.isOccupiedOnTop = value;
        }
    }

    // ---------- API de ocupación por referencia ----------

    // Suma 1 al contador. Si el bloque pasaba de 0 a 1 cae a no transitable.
    public void AddOccupancy(Vector3Int pos)
    {
        occupancyCount.TryGetValue(pos, out int n);
        occupancyCount[pos] = n + 1;
    }

    // Resta 1 al contador. No baja de cero por seguridad.
    public void RemoveOccupancy(Vector3Int pos)
    {
        if (!occupancyCount.TryGetValue(pos, out int n)) return;
        if (n <= 1) occupancyCount.Remove(pos);
        else occupancyCount[pos] = n - 1;
    }

    // ---------- Adyacencia ----------

    // Devuelve los vecinos transitables desde `pos`, respetando la regla bidireccional:
    // A->B es válido sólo si A.openFaces permite salir hacia B Y B.openFaces permite recibir desde A.
    public IEnumerable<Block> GetTraversableNeighbors(Vector3Int pos)
    {
        if (!blocks.ContainsKey(pos)) yield break;
        if (!IsTraversable(pos)) yield break;

        // Reutiliza la lógica completa de aristas (cardinales + diagonales de escalera).
        // Hacemos una copia local porque el buffer es estático y podría reusarse durante la iteración.
        var edges = GetNeighborEdges(pos);
        int count = edges.Count;
        Block[] copy = new Block[count];
        for (int e = 0; e < count; e++) copy[e] = edges[e].neighbor;
        for (int e = 0; e < count; e++) yield return copy[e];
    }

    // Variante que devuelve también el costo de la arista (útil para A*).
    // Sin allocations: usa un buffer interno reusable.
    static readonly List<NeighborEdge> _neighborBuffer = new List<NeighborEdge>(10);

    // 4 caras horizontales para iterar aristas diagonales de escalera.
    static readonly BlockFace[] _horizontalFaces = new BlockFace[] {
        BlockFace.North, BlockFace.South, BlockFace.East, BlockFace.West
    };

    public List<NeighborEdge> GetNeighborEdges(Vector3Int pos)
    {
        _neighborBuffer.Clear();
        if (!blocks.TryGetValue(pos, out Block current)) return _neighborBuffer;
        if (!IsTraversable(pos)) return _neighborBuffer;

        // ---------- Aristas cardinales (6 direcciones) ----------
        for (int i = 0; i < BlockFaceExtensions.FaceOrder.Length; i++)
        {
            BlockFace face = BlockFaceExtensions.FaceOrder[i];
            if (!current.openFaces.HasFace(face)) continue;

            Vector3Int neighborPos = pos + BlockFaceExtensions.NeighborOffsets[i];
            if (!blocks.TryGetValue(neighborPos, out Block neighbor)) continue;
            if (!IsTraversable(neighborPos)) continue;
            if (!neighbor.openFaces.HasFace(face.Opposite())) continue;

            _neighborBuffer.Add(new NeighborEdge { neighbor = neighbor, cost = neighbor.moveCost });
        }

        // ---------- Aristas diagonales de escalera (horizontal + vertical) ----------
        // Para cada dirección horizontal D, se considera un diagonal-arriba (pos + D + Up) y un diagonal-abajo (pos + D + Down).
        // La conexión es válida si:
        //   - el bloque actual es una escalera apuntando en la dirección correcta, O
        //   - el bloque destino es una escalera apuntando hacia el actual.
        // Cuesta 2 * moveCost para mantener la heurística Manhattan admisible (un paso diagonal cubre 2 unidades de Manhattan).
        for (int i = 0; i < _horizontalFaces.Length; i++)
        {
            BlockFace dir = _horizontalFaces[i];
            int faceIdx = (int)i + 2; // los horizontales en FaceOrder son índices 2..5: North,South,East,West
            Vector3Int dOffset = BlockFaceExtensions.NeighborOffsets[faceIdx];

            // Diagonal arriba: pos + D + Up. Movimiento cardinal = D. Stair debe apuntar hacia D.
            TryAddStairEdge(pos, dOffset + Vector3Int.up, dir, dir, current);

            // Diagonal abajo: pos + D + Down. Movimiento cardinal = D. Stair debe apuntar hacia opposite(D).
            TryAddStairEdge(pos, dOffset + Vector3Int.down, dir, dir.Opposite(), current);
        }

        return _neighborBuffer;
    }

    // cardinalDir   = dirección horizontal del movimiento (lo que la openFaces necesita permitir)
    // requiredStairDir = dirección hacia donde debe apuntar la escalera para que la conexión exista
    void TryAddStairEdge(Vector3Int pos, Vector3Int totalOffset, BlockFace cardinalDir, BlockFace requiredStairDir, Block current)
    {
        Vector3Int target = pos + totalOffset;
        if (!blocks.TryGetValue(target, out Block neighbor)) return;
        if (!IsTraversable(target)) return;

        // openFaces bidireccional, igual que las aristas cardinales:
        //   - salgo de current por la cara cardinalDir
        //   - entro a neighbor por la cara opuesta
        // Esto deja cerrar pasamanos/muros en escaleras vía openFaces, sin lógica extra.
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

    // ---------- Conversiones mundo <-> grilla ----------

    public Vector3Int WorldToGrid(Vector3 world)
    {
        Vector3 local = (world - gridOrigin) / blockSize;
        return new Vector3Int(
            Mathf.FloorToInt(local.x),
            Mathf.FloorToInt(local.y),
            Mathf.FloorToInt(local.z));
    }

    // Devuelve el centro del bloque en mundo (donde se ubica el "pie" del personaje).
    public Vector3 GridToWorld(Vector3Int grid)
    {
        return gridOrigin + new Vector3(
            (grid.x + 0.5f) * blockSize,
            (grid.y + 0.5f) * blockSize,
            (grid.z + 0.5f) * blockSize);
    }
}
