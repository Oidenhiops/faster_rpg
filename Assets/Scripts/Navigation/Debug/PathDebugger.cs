using System.Collections.Generic;
using UnityEngine;

// Herramienta de depuración para visualizar pathfinding sin necesidad de un personaje.
// Asigna un Transform de start y otro de end (cubos vacíos en la escena), y verás el path en gizmos.
//
// Modos:
//   - autoRecompute = true: recomputa cada vez que se mueve un transform en el editor (Update).
//   - autoRecompute = false: usa el botón "Recompute Now" del context menu.
public class PathDebugger : MonoBehaviour
{
    [Header("Endpoints")]
    public Transform startPoint;
    public Transform endPoint;

    [Header("Comportamiento")]
    public bool autoRecompute = true;
    public bool applySmoothing = true;
    [Range(1, 16)] public int smoothSamplesPerUnit = 4;

    [Header("Visualización")]
    public bool showExploredNodes = true;
    public bool showPath = true;
    public float pathLineThickness = 0.05f;
    public Color pathColor      = new Color(1f, 0.9f, 0.1f, 1f);
    public Color smoothedColor  = new Color(0.2f, 1f, 0.5f, 1f);
    public Color exploredColor  = new Color(1f, 0.2f, 0.2f, 0.35f);
    public Color startColor     = new Color(0.2f, 0.8f, 1f, 1f);
    public Color endColor       = new Color(1f, 0.3f, 0.8f, 1f);

    [Header("Stats (read-only)")]
    [SerializeField] int statsNodesExplored;
    [SerializeField] int statsPathLength;
    [SerializeField] bool statsFound;
    [SerializeField] float statsLastComputeMs;

    List<Vector3> rawPath;
    List<Vector3> smoothedPath;
    readonly List<Vector3Int> explored = new List<Vector3Int>(256);
    Vector3 lastStart, lastEnd;

    void Update()
    {
        if (!autoRecompute) return;
        if (startPoint == null || endPoint == null) return;
        if (startPoint.position == lastStart && endPoint.position == lastEnd) return;
        Recompute();
    }

    [ContextMenu("Recompute Now")]
    public void Recompute()
    {
        if (startPoint == null || endPoint == null) return;
        GridMap map = GridMap.Instance;
        if (map == null) return;

        explored.Clear();
        float t0 = Time.realtimeSinceStartup;
        rawPath = Pathfinder.FindPath(startPoint.position, endPoint.position, map, showExploredNodes ? explored : null);
        statsLastComputeMs = (Time.realtimeSinceStartup - t0) * 1000f;

        smoothedPath = (applySmoothing && rawPath != null && rawPath.Count > 2)
            ? Pathfinder.SmoothPath(rawPath, map, smoothSamplesPerUnit)
            : null;

        statsNodesExplored = Pathfinder.LastStats.nodesExplored;
        statsPathLength = Pathfinder.LastStats.pathLength;
        statsFound = Pathfinder.LastStats.found;

        lastStart = startPoint.position;
        lastEnd = endPoint.position;
    }

#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        GridMap map = GridMap.Instance;
        if (map == null) return;

        if (startPoint != null)
        {
            Gizmos.color = startColor;
            Gizmos.DrawSphere(startPoint.position, 0.18f);
            Vector3Int cell = map.WorldToGrid(startPoint.position);
            Gizmos.color = new Color(startColor.r, startColor.g, startColor.b, 0.25f);
            Gizmos.DrawCube(map.GridToWorld(cell), Vector3.one * map.blockSize * 0.9f);
        }

        if (endPoint != null)
        {
            Gizmos.color = endColor;
            Gizmos.DrawSphere(endPoint.position, 0.18f);
            Vector3Int cell = map.WorldToGrid(endPoint.position);
            Gizmos.color = new Color(endColor.r, endColor.g, endColor.b, 0.25f);
            Gizmos.DrawCube(map.GridToWorld(cell), Vector3.one * map.blockSize * 0.9f);
        }

        if (showExploredNodes && explored.Count > 0)
        {
            Gizmos.color = exploredColor;
            float exploredSize = map.blockSize * 0.4f;
            for (int i = 0; i < explored.Count; i++)
            {
                Gizmos.DrawCube(map.GridToWorld(explored[i]), Vector3.one * exploredSize);
            }
        }

        if (showPath && rawPath != null && rawPath.Count > 1)
        {
            Gizmos.color = pathColor;
            for (int i = 0; i < rawPath.Count - 1; i++)
            {
                Gizmos.DrawLine(rawPath[i], rawPath[i + 1]);
                Gizmos.DrawSphere(rawPath[i], pathLineThickness);
            }
            Gizmos.DrawSphere(rawPath[rawPath.Count - 1], pathLineThickness);
        }

        if (showPath && smoothedPath != null && smoothedPath.Count > 1)
        {
            Gizmos.color = smoothedColor;
            for (int i = 0; i < smoothedPath.Count - 1; i++)
            {
                Gizmos.DrawLine(smoothedPath[i] + Vector3.up * 0.05f, smoothedPath[i + 1] + Vector3.up * 0.05f);
                Gizmos.DrawSphere(smoothedPath[i] + Vector3.up * 0.05f, pathLineThickness * 1.2f);
            }
            Gizmos.DrawSphere(smoothedPath[smoothedPath.Count - 1] + Vector3.up * 0.05f, pathLineThickness * 1.2f);
        }
    }
#endif
}
