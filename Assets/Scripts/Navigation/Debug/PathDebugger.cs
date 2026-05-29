using System.Collections.Generic;
using UnityEngine;

public class PathDebugger : MonoBehaviour
{
    public CharacterBase startCharacter;
    public CharacterBase endCharacter;

    public bool autoRecompute = true;
    public bool applySmoothing = true;
    [Range(1, 16)] public int smoothSamplesPerUnit = 4;

    public bool showExploredNodes = true;
    public bool showPath = true;
    public float pathLineThickness = 0.05f;
    public Color pathColor      = new Color(1f, 0.9f, 0.1f, 1f);
    public Color smoothedColor  = new Color(0.2f, 1f, 0.5f, 1f);
    public Color exploredColor  = new Color(1f, 0.2f, 0.2f, 0.35f);
    public Color startColor     = new Color(0.2f, 0.8f, 1f, 1f);
    public Color endColor       = new Color(1f, 0.3f, 0.8f, 1f);

    [SerializeField] int statsNodesExplored;
    [SerializeField] int statsPathLength;
    [SerializeField] bool statsFound;
    [SerializeField] float statsLastComputeMs;
    [SerializeField] int statsJumpDistance;

    List<Vector3> rawPath;
    List<Vector3> smoothedPath;
    readonly List<Vector3Int> explored = new List<Vector3Int>(256);
    Vector3 lastStart, lastEnd;
    int lastJumpDistance;

    void Update()
    {
        if (!autoRecompute) return;
        if (startCharacter == null || endCharacter == null) return;

        Vector3 startPos = startCharacter.transform.position;
        Vector3 endPos = endCharacter.transform.position;
        int jd = ResolveJumpDistance(startCharacter);

        if (startPos == lastStart && endPos == lastEnd && jd == lastJumpDistance) return;
        Recompute();
    }

    [ContextMenu("Recompute Now")]
    public void Recompute()
    {
        if (startCharacter == null || endCharacter == null) return;
        GridMap map = GridMap.Instance;
        if (map == null) return;

        Vector3 startPos = startCharacter.transform.position;
        Vector3 endPos = endCharacter.transform.position;
        int jumpDistance = ResolveJumpDistance(startCharacter);

        explored.Clear();
        float t0 = Time.realtimeSinceStartup;
        rawPath = Pathfinder.FindPath(startPos, endPos, jumpDistance, map, showExploredNodes ? explored : null);
        statsLastComputeMs = (Time.realtimeSinceStartup - t0) * 1000f;

        smoothedPath = (applySmoothing && rawPath != null && rawPath.Count > 2)
            ? Pathfinder.SmoothPath(rawPath, map, smoothSamplesPerUnit)
            : null;

        statsNodesExplored = Pathfinder.LastStats.nodesExplored;
        statsPathLength = Pathfinder.LastStats.pathLength;
        statsFound = Pathfinder.LastStats.found;
        statsJumpDistance = jumpDistance;

        lastStart = startPos;
        lastEnd = endPos;
        lastJumpDistance = jumpDistance;
    }

    static int ResolveJumpDistance(CharacterBase c)
    {
        if (c == null) return 0;
        if (c.charactersData == null || c.charactersData.Length == 0) return 0;
        var data = c.charactersData[c.characterIndex];
        if (data == null || data.statistics == null) return 0;
        if (!data.statistics.TryGetValue(CharacterData.TypeStatistic.JumpDistance, out var stat)) return 0;
        return stat.currentValue;
    }

#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        GridMap map = GridMap.Instance;
        if (map == null) return;

        if (startCharacter != null)
        {
            Vector3 startPos = startCharacter.transform.position;
            Gizmos.color = startColor;
            Gizmos.DrawSphere(startPos, 0.18f);
            Vector3Int cell = map.WorldToGrid(startPos);
            Gizmos.color = new Color(startColor.r, startColor.g, startColor.b, 0.25f);
            Gizmos.DrawCube(map.GridToWorld(cell), Vector3.one * map.blockSize * 0.9f);
        }

        if (endCharacter != null)
        {
            Vector3 endPos = endCharacter.transform.position;
            Gizmos.color = endColor;
            Gizmos.DrawSphere(endPos, 0.18f);
            Vector3Int cell = map.WorldToGrid(endPos);
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
