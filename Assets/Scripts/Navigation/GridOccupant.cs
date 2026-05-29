using UnityEngine;

[DefaultExecutionOrder(-50)]
public class GridOccupant : MonoBehaviour
{
    public bool dynamic = false;
    public float moveThreshold = 0.1f;
    public Vector3 footOffset = new Vector3(0f, -0.05f, 0f);

    Vector3Int currentCell;
    Vector3 lastSampledPos;
    bool registered;

    BlockMarker _siblingMarker;
    bool _siblingMarkerCached;

    void OnEnable()
    {
        Register();
    }

    void Start()
    {
        if (!registered) Register();
    }

    void OnDisable()
    {
        Unregister();
    }

    void Update()
    {
        if (!dynamic || !registered) return;
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

    public void Refresh()
    {
        Unregister();
        Register();
    }

    Vector3Int ResolveCell()
    {
        GridMap map = GridMap.Instance;

        if (!_siblingMarkerCached)
        {
            _siblingMarker = GetComponent<BlockMarker>();
            _siblingMarkerCached = true;
        }
        if (_siblingMarker != null)
        {
            return _siblingMarker.ResolveGridPos(map.blockSize, map.gridOrigin);
        }

        return map.WorldToGrid(transform.position + footOffset);
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        float size = GridMap.Instance != null ? GridMap.Instance.blockSize : 1f;
        Vector3 origin = GridMap.Instance != null ? GridMap.Instance.gridOrigin : Vector3.zero;

        BlockMarker marker = GetComponent<BlockMarker>();
        Vector3Int cell;
        if (marker != null)
        {
            cell = marker.ResolveGridPos(size, origin);
        }
        else
        {
            Vector3 local = (transform.position + footOffset - origin) / size;
            cell = new Vector3Int(
                Mathf.RoundToInt(local.x),
                Mathf.RoundToInt(local.y),
                Mathf.RoundToInt(local.z));
        }

        Vector3 center = origin + new Vector3(
            cell.x * size,
            cell.y * size,
            cell.z * size);

        Gizmos.color = new Color(0.95f, 0.35f, 0.05f, 0.35f);
        Gizmos.DrawCube(center, Vector3.one * size * 0.95f);
        Gizmos.color = new Color(1f, 0.5f, 0.1f, 1f);
        Gizmos.DrawWireSphere(center, size * 0.45f);
    }
#endif
}
