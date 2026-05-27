using System.Collections.Generic;
using UnityEngine;

// Construye el GridMap recorriendo los BlockMarker presentes en la escena.
// No usa Physics: openFaces y demás se configuran manualmente en cada BlockMarker (o en sus prefabs).
// La ocupación dinámica (cofres, items, NPCs) se maneja vía GridOccupant, no por overlap.
[DefaultExecutionOrder(-100)]
public class GridBaker : MonoBehaviour
{
    [Header("Referencia al GridMap")]
    public GridMap gridMap;

    [Header("Cuándo bakear")]
    public bool bakeOnAwake = true;
    public bool bakeOnStart = false;

    [Header("Opciones de scan")]
    [Tooltip("Si está marcado, también incluye markers en GameObjects desactivados.")]
    public bool includeInactiveMarkers = false;
    [Tooltip("Si está marcado, dos markers que caen en la misma celda solo conservan el primero (resto se ignora).")]
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

    // Rebake parcial: borra los bloques cuyos centros caen dentro de `region` y vuelve a registrar los markers de esa zona.
    // Útil cuando se destruye un bloque o se abre una puerta sin tener que recorrer el mapa entero.
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

    // ---------- Internas ----------

    void BakeMarkers(Bounds? region)
    {
        BlockMarker[] markers = includeInactiveMarkers
            ? Resources.FindObjectsOfTypeAll<BlockMarker>()
            : FindObjectsByType<BlockMarker>(FindObjectsSortMode.None);

        for (int i = 0; i < markers.Length; i++)
        {
            BlockMarker m = markers[i];
            if (m == null) continue;
            // Cuando se usa FindObjectsOfTypeAll, también aparecen prefabs en disco. Filtramos por escena válida.
            if (!m.gameObject.scene.IsValid()) continue;

            if (region.HasValue && !region.Value.Contains(m.transform.position)) continue;

            // Los bloques dinámicos se registran solos en OnEnable. El baker los ignora para evitar doble registro.
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
