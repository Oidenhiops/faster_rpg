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
    public float gizmoFaceInset = 0.05f;

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
        float inset = gizmoFaceInset;

        // Solo dibujamos las 4 caras horizontales (índices 2..5: N, S, E, W).
        // Up/Down quedaron fuera de la navegación, así que mostrarlos sería engañoso.
        for (int i = 2; i < BlockFaceExtensions.FaceOrder.Length; i++)
        {
            BlockFace face = BlockFaceExtensions.FaceOrder[i];
            Vector3 offset = BlockFaceExtensions.NeighborOffsets[i];
            Vector3 faceCenter = center + offset * (half - inset);

            // Tamaño del quad de cara (perpendicular al offset).
            Vector3 quadSize = new Vector3(size - inset * 2, size - inset * 2, size - inset * 2);
            if (offset.x != 0) quadSize.x = 0.02f;
            if (offset.y != 0) quadSize.y = 0.02f;
            if (offset.z != 0) quadSize.z = 0.02f;

            bool open = openFaces.HasFace(face);
            Gizmos.color = open
                ? new Color(0.2f, 1f, 0.2f, 0.35f)
                : new Color(1f, 0.2f, 0.2f, 0.55f);
            Gizmos.DrawCube(faceCenter, quadSize);
        }

        // Outline del bloque para ubicarlo visualmente.
        Gizmos.color = isWalkable ? new Color(1f, 1f, 1f, 0.3f) : new Color(0.4f, 0.4f, 0.4f, 0.3f);
        Gizmos.DrawWireCube(center, Vector3.one * size);

        // Si es escalera, dibuja la pendiente como línea amarilla diagonal y una flechita en la dirección de subida.
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
                Vector3 backLow   = center - dir * (half * 0.9f) - Vector3.up * (half * 0.9f);
                Vector3 frontHigh = center + dir * (half * 0.9f) + Vector3.up * (half * 0.9f);
                Gizmos.color = new Color(1f, 0.9f, 0.1f, 0.95f);
                Gizmos.DrawLine(backLow, frontHigh);
                // Flecha arriba del bloque indicando dirección de subida.
                Vector3 arrowBase = center + Vector3.up * (half + 0.05f);
                Vector3 arrowTip  = arrowBase + dir * (size * 0.4f);
                Gizmos.DrawLine(arrowBase, arrowTip);
                Vector3 perp = Vector3.Cross(dir, Vector3.up).normalized * (size * 0.1f);
                Gizmos.DrawLine(arrowTip, arrowTip - dir * (size * 0.15f) + perp);
                Gizmos.DrawLine(arrowTip, arrowTip - dir * (size * 0.15f) - perp);
            }
        }
    }
#endif
}
