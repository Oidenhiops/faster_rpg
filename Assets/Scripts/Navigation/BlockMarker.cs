using UnityEngine;

// Adjunta este componente a cualquier GameObject que represente un bloque del mapa.
// El GridBaker lo recoge en Awake y crea el Block correspondiente en el GridMap.
// En el editor dibuja gizmos: verde = cara abierta, rojo = cara cerrada (pasamanos, muro, etc.)
public class BlockMarker : MonoBehaviour
{
    [Header("Configuración del bloque")]
    public BlockFace openFaces = BlockFace.All;
    public bool isWalkable = true;
    [Min(0.01f)] public float moveCost = 1f;

    [Header("Escalera / rampa")]
    [Tooltip("Si este bloque es una escalera diagonal, marca hacia qué dirección horizontal SUBE. " +
             "Dejar en None para bloques planos. Un solo cardinal: North, South, East u West.")]
    public BlockFace stairUpDirection = BlockFace.None;

    [Header("Bloque dinámico (runtime)")]
    [Tooltip("Si está marcado, este bloque se registra solo en OnEnable y actualiza su celda cuando se mueve. " +
             "Útil para plataformas móviles, puertas que se desplazan, bloques empujables por el jugador. " +
             "Los markers con dynamic = false los registra el GridBaker al iniciar la escena.")]
    public bool dynamic = false;

    [Tooltip("Solo si dynamic = true. Distancia mínima que tiene que moverse el transform antes de re-evaluar la celda.")]
    public float dynamicMoveThreshold = 0.1f;

    [Header("Gizmos")]
    public bool drawGizmos = true;

    // El baker calcula esto al registrar el bloque; lo guardamos para que el gizmo lo use sin recalcular.
    [HideInInspector] public Vector3Int cachedGridPos;
    [HideInInspector] public float cachedBlockSize = 1f;

    // Estado dinámico en runtime.
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

    // ---------- Ciclo de vida en runtime para bloques dinámicos ----------

    void OnEnable()
    {
        if (!Application.isPlaying) return;
        if (!dynamic) return;          // estáticos los maneja el GridBaker
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
            // Reutilizamos el Block existente (no perdemos su sourceObject ni isOccupiedOnTop)
            Block existing = map.GetBlock(registeredCell);
            if (existing == null)
            {
                isRegistered = false;
                RegisterDynamicInGrid();
                return;
            }

            // Si la nueva celda ya tiene otro bloque, no movemos lógicamente — quedamos "fuera del grid"
            // hasta que se libere. La pathfinding no contará con este bloque mientras tanto.
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

    // ---------- Update: solo tracking dinámico en runtime ----------

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

        // Centro del bloque en mundo. Si el GridMap existe, lo usamos; si no, asumimos que el transform ya está centrado.
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

        // Dibujamos las direcciones transitables como flechas SOBRE la cara superior del bloque.
        // Quedarse a 70% del borde evita solape con los gizmos del bloque vecino.
        // Solo se dibujan las caras abiertas (verde) — la ausencia indica que está cerrada.
        // Se muestra también una pequeña marca roja en el borde para caras cerradas, así no es ambiguo.
        Vector3 topCenter = center + Vector3.up * (half + 0.01f); // +0.01 evita z-fighting con el mesh
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

                // Cabeza de flecha: dos pequeñas líneas perpendiculares.
                Vector3 perp = Vector3.Cross(dir, Vector3.up).normalized * headSize;
                Vector3 back = tip - dir * headSize;
                Gizmos.DrawLine(tip, back + perp);
                Gizmos.DrawLine(tip, back - perp);
            }
            else
            {
                // Marca roja corta perpendicular a la dirección, en el borde, indicando "bloqueado".
                Vector3 edge = topCenter + dir * arrowReach;
                Vector3 perp = Vector3.Cross(dir, Vector3.up).normalized * (half * 0.22f);
                Gizmos.color = new Color(1f, 0.25f, 0.25f, 0.9f);
                Gizmos.DrawLine(edge - perp, edge + perp);
            }
        }

        // Outline del bloque para ubicarlo visualmente.
        Gizmos.color = isWalkable ? new Color(1f, 1f, 1f, 0.3f) : new Color(0.4f, 0.4f, 0.4f, 0.3f);
        Gizmos.DrawWireCube(center, Vector3.one * size);

        // Si es escalera, dibuja UNA sola flecha amarilla a 45° saliendo del centro del bloque
        // hacia arriba en la dirección de subida. Incluye cabeza de flecha en el plano del slope.
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

                // tiltedDir = vector unitario a 45° (horizontal + vertical) en la dirección de subida.
                Vector3 tiltedDir = (dir + Vector3.up).normalized;

                // Base hacia atrás-abajo, tip hacia adelante-arriba, ambos sobre la diagonal del bloque.
                float reach = half * 0.85f;
                Vector3 backLow   = center - tiltedDir * reach;
                Vector3 frontHigh = center + tiltedDir * reach;

                Gizmos.color = new Color(1f, 0.85f, 0.1f, 1f);
                Gizmos.DrawLine(backLow, frontHigh);

                // Cabeza de flecha en el plano vertical del slope.
                // perpInSlopePlane es perpendicular a tiltedDir y vive en el mismo plano vertical (contiene dir y Up).
                // Identidad: (dir + Up) · (dir - Up) = |dir|² - |Up|² = 0, así que (dir - Up) es perpendicular.
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
