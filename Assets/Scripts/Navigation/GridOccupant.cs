using UnityEngine;

// Componente para cualquier objeto que deba marcar su celda como NO transitable: cofres, items dropeados,
// puertas cerradas, NPCs estacionados, decoración voluminosa. Reemplaza la detección por Physics.
//
// Uso típico:
//   - Estático (un cofre que nunca se mueve): dynamic = false. Se registra una vez en OnEnable.
//   - Dinámico (un NPC que se mueve): dynamic = true. Recalcula su celda cuando se mueve > moveThreshold.
//
// La ocupación es por contador: dos GridOccupants en la misma celda no se pisan al desregistrarse.
[DefaultExecutionOrder(-50)]
public class GridOccupant : MonoBehaviour
{
    [Tooltip("Si está marcado, revisa cada frame si cambió de celda y actualiza el registro.")]
    public bool dynamic = false;

    [Tooltip("Solo aplica si dynamic = true. Distancia mínima movida antes de re-evaluar la celda.")]
    public float moveThreshold = 0.1f;

    [Tooltip("Offset que se suma a transform.position antes de resolver la celda. " +
             "Si el pivot del objeto está en la base, deja un pequeño valor negativo en Y para caer dentro del bloque-suelo.")]
    public Vector3 footOffset = new Vector3(0f, -0.05f, 0f);

    Vector3Int currentCell;
    Vector3 lastSampledPos;
    bool registered;

    void OnEnable()
    {
        Register();
    }

    void OnDisable()
    {
        Unregister();
    }

    void Update()
    {
        if (!dynamic || !registered) return;

        // Optimización: solo recomputar gridPos si nos movimos suficientemente.
        if ((transform.position - lastSampledPos).sqrMagnitude < moveThreshold * moveThreshold) return;

        Vector3Int newCell = ResolveCell();
        if (newCell != currentCell)
        {
            GridMap.Instance?.RemoveOccupancy(currentCell);
            GridMap.Instance?.AddOccupancy(newCell);
            currentCell = newCell;
        }
        lastSampledPos = transform.position;
    }

    void Register()
    {
        if (registered) return;
        if (GridMap.Instance == null) return;

        currentCell = ResolveCell();
        GridMap.Instance.AddOccupancy(currentCell);
        lastSampledPos = transform.position;
        registered = true;
    }

    void Unregister()
    {
        if (!registered) return;
        GridMap.Instance?.RemoveOccupancy(currentCell);
        registered = false;
    }

    // API pública para forzar re-registro (ej. después de teletransportar al objeto o de un Rebake).
    public void Refresh()
    {
        Unregister();
        Register();
    }

    Vector3Int ResolveCell()
    {
        GridMap map = GridMap.Instance;
        return map.WorldToGrid(transform.position + footOffset);
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        if (GridMap.Instance == null) return;
        Vector3Int cell = GridMap.Instance.WorldToGrid(transform.position + footOffset);
        Vector3 center = GridMap.Instance.GridToWorld(cell);
        float size = GridMap.Instance.blockSize;

        Gizmos.color = new Color(1f, 0.6f, 0.1f, 0.35f);
        Gizmos.DrawCube(center, Vector3.one * size);
        Gizmos.color = new Color(1f, 0.6f, 0.1f, 0.9f);
        Gizmos.DrawWireCube(center, Vector3.one * size);
    }
#endif
}
