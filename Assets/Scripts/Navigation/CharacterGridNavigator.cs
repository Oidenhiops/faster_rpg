using System;
using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(-40)]
public class CharacterGridNavigator : MonoBehaviour
{
    public CharacterBase character;

    public bool smoothPath = true;
    [Range(1, 16)] public int smoothSamplesPerUnit = 4;

    public float waypointThreshold = 0.15f;
    public bool ignoreYWhenAdvancing = true;

    public bool recomputeOnBlocked = true;
    public float recomputeMinInterval = 0.25f;

    public List<Vector3> CurrentPath { get; private set; } = new List<Vector3>();
    public int CurrentWaypointIndex { get; private set; }
    public Vector3 FinalDestination { get; private set; }
    public bool HasPath => CurrentPath != null && CurrentPath.Count > 0;
    public bool IsAtDestination => HasPath && CurrentWaypointIndex >= CurrentPath.Count;
    public Vector3 CurrentWaypoint => HasPath && CurrentWaypointIndex < CurrentPath.Count
        ? CurrentPath[CurrentWaypointIndex]
        : transform.position;

    public event Action<List<Vector3>> OnPathReady;
    public event Action OnDestinationReached;
    public event Action OnPathFailed;

    float lastRecomputeTime;

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

        int jumpDistance = ResolveJumpDistance();
        List<Vector3> path = Pathfinder.FindPath(transform.position, destination, jumpDistance, map);
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

    public void Stop()
    {
        ClearPath();
    }

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

    public Vector3 GetDesiredDirection(Vector3 currentPosition)
    {
        if (!HasPath || IsAtDestination) return Vector3.zero;
        Vector3 wp = CurrentPath[CurrentWaypointIndex];
        Vector3 diff = wp - currentPosition;
        if (ignoreYWhenAdvancing) diff.y = 0f;
        float mag = diff.magnitude;
        return mag > 0.0001f ? diff / mag : Vector3.zero;
    }

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

        int lookahead = Mathf.Min(4, CurrentPath.Count - CurrentWaypointIndex);
        for (int i = 0; i < lookahead; i++)
        {
            Vector3 wp = CurrentPath[CurrentWaypointIndex + i];
            Vector3Int cell = map.WorldToGrid(wp);
            if (!map.IsTraversable(cell))
            {
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

    int ResolveJumpDistance()
    {
        if (character == null) return 0;
        if (character.charactersData == null || character.charactersData.Length == 0) return 0;
        var data = character.charactersData[character.characterIndex];
        if (data == null || data.statistics == null) return 0;
        if (!data.statistics.TryGetValue(CharacterData.TypeStatistic.JumpDistance, out var stat)) return 0;
        return stat.currentValue;
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
