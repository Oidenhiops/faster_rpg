using System;
using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(-40)]
public class CharacterGridNavigator : CharacterMovementBase
{
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

    readonly HashSet<Vector3Int> _pathChunks = new HashSet<Vector3Int>();
    bool _pendingRecompute;
    bool _subscribed;

    public override void HandleInitialize() { }

    public override void HandleMovement() { }

    void OnEnable()
    {
        TrySubscribe();
    }

    void Start()
    {
        if (!_subscribed) TrySubscribe();
    }

    void OnDisable()
    {
        TryUnsubscribe();
    }

    void Update()
    {
        if (_pendingRecompute && Time.time - lastRecomputeTime >= recomputeMinInterval)
        {
            _pendingRecompute = false;
            MoveTo(FinalDestination);
        }
    }

    void TrySubscribe()
    {
        if (_subscribed) return;
        if (GridMap.Instance == null) return;
        GridMap.Instance.OnChunkDirty += HandleChunkDirty;
        _subscribed = true;
    }

    void TryUnsubscribe()
    {
        if (!_subscribed) return;
        if (GridMap.Instance != null) GridMap.Instance.OnChunkDirty -= HandleChunkDirty;
        _subscribed = false;
    }

    void HandleChunkDirty(Vector3Int chunkCoord)
    {
        if (!recomputeOnBlocked) return;
        if (!HasPath || IsAtDestination) return;
        if (_pathChunks.Contains(chunkCoord)) _pendingRecompute = true;
    }

    void RebuildPathChunks()
    {
        _pathChunks.Clear();
        GridMap map = GridMap.Instance;
        if (map == null) return;
        for (int i = 0; i < CurrentPath.Count; i++)
        {
            Vector3Int cell = map.WorldToGrid(CurrentPath[i]);
            _pathChunks.Add(map.CellToChunk(cell));
        }
    }

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
        int dropDistance = ResolveDropDistance();
        List<Vector3> path = Pathfinder.FindPath(transform.position, destination, jumpDistance, dropDistance, map);
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

        if (path.Count > 0) path[0] = transform.position;

        CurrentPath = path;
        CurrentWaypointIndex = 0;
        lastRecomputeTime = Time.time;
        RebuildPathChunks();
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
        _pathChunks.Clear();
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
        return (int)characterBase.charactersData[characterBase.characterIndex]
                            .statistics[CharacterData.TypeStatistic.JumpDistance]
                            .currentValue;
    }

    int ResolveDropDistance()
    {
        return (int)characterBase.charactersData[characterBase.characterIndex]
                            .statistics[CharacterData.TypeStatistic.DropDistance]
                            .currentValue;
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
