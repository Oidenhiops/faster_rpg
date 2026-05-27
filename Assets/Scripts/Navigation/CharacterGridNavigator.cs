using System;
using System.Collections.Generic;
using UnityEngine;

// Componente paralelo a CharacterBase / CharacterMovementBase. NO modifica ninguna clase de movimiento.
// Solo expone datos: waypoints actuales, índice, dirección deseada. Cualquier clase de movimiento
// (CharacterPlayerMovement, una AI, lo que sea) lee desde aquí y aplica la locomoción que corresponda.
//
// Flujo típico desde el movimiento:
//   if (navigator.HasPath && !navigator.IsAtDestination)
//   {
//       Vector3 dir = navigator.GetDesiredDirection(transform.position);
//       // ... mover el rigidbody/transform en `dir` * speed ...
//       navigator.AdvanceIfReached(transform.position);
//   }
[DefaultExecutionOrder(-40)]
public class CharacterGridNavigator : MonoBehaviour
{
    [Header("Path smoothing")]
    [Tooltip("Si está marcado, suaviza el path después de calcularlo eliminando waypoints redundantes.")]
    public bool smoothPath = true;
    [Tooltip("Densidad de muestreo del smoother (samples por unidad de mundo).")]
    [Range(1, 16)] public int smoothSamplesPerUnit = 4;

    [Header("Avance de waypoints")]
    [Tooltip("Distancia (en mundo) al waypoint actual para considerarlo alcanzado y pasar al siguiente.")]
    public float waypointThreshold = 0.15f;
    [Tooltip("Si está marcado, ignora la componente Y al medir distancia al waypoint. Útil cuando el personaje no comparte altura exacta con el centro del bloque.")]
    public bool ignoreYWhenAdvancing = true;

    [Header("Recálculo automático")]
    [Tooltip("Si está marcado, recomputa el path automáticamente cuando un waypoint queda obstruido.")]
    public bool recomputeOnBlocked = true;
    [Tooltip("Intervalo mínimo entre recomputos automáticos (segundos).")]
    public float recomputeMinInterval = 0.25f;

    // ---------- Estado expuesto (read-only) ----------

    public List<Vector3> CurrentPath { get; private set; } = new List<Vector3>();
    public int CurrentWaypointIndex { get; private set; }
    public Vector3 FinalDestination { get; private set; }
    public bool HasPath => CurrentPath != null && CurrentPath.Count > 0;
    public bool IsAtDestination => HasPath && CurrentWaypointIndex >= CurrentPath.Count;
    public Vector3 CurrentWaypoint => HasPath && CurrentWaypointIndex < CurrentPath.Count
        ? CurrentPath[CurrentWaypointIndex]
        : transform.position;

    // ---------- Eventos ----------

    public event Action<List<Vector3>> OnPathReady;
    public event Action OnDestinationReached;
    public event Action OnPathFailed;

    float lastRecomputeTime;

    // ---------- API pública ----------

    // Solicita un nuevo path desde la posición actual hasta `destination`. Devuelve true si encontró ruta.
    public bool MoveTo(Vector3 destination)
    {
        FinalDestination = destination;

        GridMap map = GridMap.Instance;
        if (map == null)
        {
            ClearPath();
            OnPathFailed?.Invoke();
            return false;
        }

        List<Vector3> path = Pathfinder.FindPath(transform.position, destination, map);
        if (path == null)
        {
            ClearPath();
            OnPathFailed?.Invoke();
            return false;
        }

        if (smoothPath && path.Count > 2)
        {
            path = Pathfinder.SmoothPath(path, map, smoothSamplesPerUnit);
        }

        CurrentPath = path;
        CurrentWaypointIndex = 0;
        lastRecomputeTime = Time.time;
        OnPathReady?.Invoke(path);
        return true;
    }

    // Cancela el path actual.
    public void Stop()
    {
        ClearPath();
    }

    // Llamar cada frame desde el movimiento. Si llegamos al waypoint actual, avanza al siguiente.
    // Devuelve true en el frame en que se alcanza el destino final.
    public bool AdvanceIfReached(Vector3 currentPosition)
    {
        if (!HasPath || IsAtDestination) return false;

        Vector3 wp = CurrentPath[CurrentWaypointIndex];
        float sqrDist = ignoreYWhenAdvancing
            ? SqrDistXZ(currentPosition, wp)
            : (currentPosition - wp).sqrMagnitude;

        float threshSqr = waypointThreshold * waypointThreshold;
        if (sqrDist > threshSqr)
        {
            if (recomputeOnBlocked) MaybeRecomputeIfBlocked();
            return false;
        }

        CurrentWaypointIndex++;

        if (CurrentWaypointIndex >= CurrentPath.Count)
        {
            OnDestinationReached?.Invoke();
            return true;
        }
        return false;
    }

    // Dirección normalizada desde `currentPosition` hacia el waypoint actual. Vector3.zero si no hay path.
    public Vector3 GetDesiredDirection(Vector3 currentPosition)
    {
        if (!HasPath || IsAtDestination) return Vector3.zero;
        Vector3 wp = CurrentPath[CurrentWaypointIndex];
        Vector3 diff = wp - currentPosition;
        if (ignoreYWhenAdvancing) diff.y = 0f;
        float mag = diff.magnitude;
        return mag > 0.0001f ? diff / mag : Vector3.zero;
    }

    // ---------- Internas ----------

    void ClearPath()
    {
        CurrentPath.Clear();
        CurrentWaypointIndex = 0;
    }

    void MaybeRecomputeIfBlocked()
    {
        if (Time.time - lastRecomputeTime < recomputeMinInterval) return;
        GridMap map = GridMap.Instance;
        if (map == null) return;

        // Chequea solo los próximos N waypoints para no recorrer toda la lista.
        int lookahead = Mathf.Min(4, CurrentPath.Count - CurrentWaypointIndex);
        for (int i = 0; i < lookahead; i++)
        {
            Vector3 wp = CurrentPath[CurrentWaypointIndex + i];
            Vector3Int cell = map.WorldToGrid(wp);
            if (!map.IsTraversable(cell))
            {
                // Path inválido — recomputa desde la posición actual al destino final.
                MoveTo(FinalDestination);
                return;
            }
        }
    }

    static float SqrDistXZ(Vector3 a, Vector3 b)
    {
        float dx = a.x - b.x;
        float dz = a.z - b.z;
        return dx * dx + dz * dz;
    }

#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        if (!HasPath) return;

        Gizmos.color = new Color(0.2f, 1f, 0.4f, 0.9f);
        for (int i = 0; i < CurrentPath.Count - 1; i++)
        {
            Gizmos.DrawLine(CurrentPath[i], CurrentPath[i + 1]);
        }
        for (int i = 0; i < CurrentPath.Count; i++)
        {
            Gizmos.color = i < CurrentWaypointIndex
                ? new Color(0.4f, 0.4f, 0.4f, 0.7f)
                : (i == CurrentWaypointIndex ? Color.yellow : new Color(0.2f, 1f, 0.4f, 0.9f));
            Gizmos.DrawSphere(CurrentPath[i], 0.08f);
        }
    }
#endif
}
