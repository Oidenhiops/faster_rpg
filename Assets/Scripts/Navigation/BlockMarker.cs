using UnityEngine;

// Adjunta este componente a cualquier GameObject que represente un bloque del mapa.
// El GridBaker lo recoge en Awake y crea el Block correspondiente en el GridMap.
// En el editor dibuja gizmos: verde = cara abierta, rojo = cara cerrada (pasamanos, muro, etc.)
[ExecuteAlways]
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

    [Header("Override de posición en grilla")]
    [Tooltip("Si está marcado, usa overrideGridPos en vez de inferir desde transform.position.")]
    public bool useOverridePos = false;
    public Vector3Int overrideGridPos;

    [Header("Gizmos")]
    public bool drawGizmos = true;
    public float gizmoFaceInset = 0.05f;

    // El baker calcula esto al registrar el bloque; lo guardamos para que el gizmo lo use sin recalcular.
    [HideInInspector] public Vector3Int cachedGridPos;
    [HideInInspector] public float cachedBlockSize = 1f;

    public Vector3Int ResolveGridPos(float blockSize, Vector3 gridOrigin)
    {
        if (useOverridePos) return overrideGridPos;
        Vector3 local = (transform.position - gridOrigin) / blockSize;
        return new Vector3Int(
            Mathf.FloorToInt(local.x),
            Mathf.FloorToInt(local.y),
            Mathf.FloorToInt(local.z));
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

        for (int i = 0; i < BlockFaceExtensions.FaceOrder.Length; i++)
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
